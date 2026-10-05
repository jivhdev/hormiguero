using Hormiguero.Mensajero.Core;
using Hormiguero.Mensajero.Core.ClickFactura;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Mensajero.Core.Tests;

public sealed class AlmacenMensajeroTests : IDisposable
{
    private readonly string raiz = Path.Combine(
        Path.GetTempPath(),
        "HormigueroMensajeroTests",
        Guid.NewGuid().ToString("N")
    );

    public AlmacenMensajeroTests() => Directory.CreateDirectory(raiz);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(raiz, recursive: true);
    }

    private string RutaBase => Path.Combine(raiz, "mensajero.db");

    private string ClientesTxt(params string[] lineas)
    {
        string ruta = Path.Combine(raiz, "clientes.txt");
        File.WriteAllLines(ruta, lineas, new System.Text.UTF8Encoding(false));
        return ruta;
    }

    [Fact]
    public void Recuerda_la_carpeta_de_occ()
    {
        using (var almacen = new AlmacenMensajero(RutaBase))
        {
            Assert.Equal("", almacen.LeerCarpetaOcc());
            almacen.GuardarCarpetaOcc(@"C:\OCC");
        }

        using var otra = new AlmacenMensajero(RutaBase);
        Assert.Equal(@"C:\OCC", otra.LeerCarpetaOcc());
    }

    [Fact]
    public void Importa_los_clientes_de_ofisuiza_sin_tocar_el_original()
    {
        string txt = ClientesTxt(
            "76000000-1 EMPRESA UNO RETIRA CLIENTE",
            "",
            "  77000000-2 Ñandú Dos RETIRA CLIENTE  "
        );
        string antes = File.ReadAllText(txt);
        using var almacen = new AlmacenMensajero(RutaBase);

        int importados = almacen.ImportarClientesNvv(txt);

        Assert.Equal(2, importados);
        Assert.Equal(
            ["76000000-1 EMPRESA UNO RETIRA CLIENTE", "77000000-2 Ñandú Dos RETIRA CLIENTE"],
            almacen.LeerClientesNvv()
        );
        Assert.Equal(antes, File.ReadAllText(txt));
    }

    [Fact]
    public void Nunca_pisa_una_lista_que_ya_existe()
    {
        using var almacen = new AlmacenMensajero(RutaBase);
        almacen.GuardarClientesNvv(["78000000-K YA ESTABA RETIRA CLIENTE"]);

        int importados = almacen.ImportarClientesNvv(ClientesTxt("76000000-1 OTRO RETIRA CLIENTE"));

        Assert.Equal(0, importados);
        Assert.Equal(["78000000-K YA ESTABA RETIRA CLIENTE"], almacen.LeerClientesNvv());
    }

    [Fact]
    public void Guardar_conserva_el_orden_y_quita_vacios()
    {
        using var almacen = new AlmacenMensajero(RutaBase);

        almacen.GuardarClientesNvv(["B uno", "  ", "A dos ", "C tres"]);

        Assert.Equal(["B uno", "A dos", "C tres"], almacen.LeerClientesNvv());
    }

    [Fact]
    public void Archivo_inexistente_no_importa_nada()
    {
        using var almacen = new AlmacenMensajero(RutaBase);

        Assert.Equal(0, almacen.ImportarClientesNvv(Path.Combine(raiz, "no-existe.txt")));
        Assert.Empty(almacen.LeerClientesNvv());
    }

    // Base sintética con el esquema exacto de ClickFactura (src/database/db.py del original).
    private string BaseClickFactura(
        params (string Rut, string Razon, string Correo, int Activo)[] clientes
    )
    {
        string ruta = Path.Combine(raiz, "clickfactura.db");
        using (var conexion = new SqliteConnection($"Data Source={ruta};Pooling=False"))
        {
            conexion.Open();
            using var crear = conexion.CreateCommand();
            crear.CommandText =
                "CREATE TABLE clientes(rut TEXT PRIMARY KEY, razon_social TEXT NOT NULL, correo TEXT NOT NULL, "
                + "activo INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL DEFAULT (datetime('now','localtime')), "
                + "updated_at TEXT NOT NULL DEFAULT (datetime('now','localtime')));";
            crear.ExecuteNonQuery();
            foreach (var c in clientes)
            {
                using var insertar = conexion.CreateCommand();
                insertar.CommandText =
                    "INSERT INTO clientes VALUES ($r, $n, $c, $a, '2025-01-02 03:04:05', '2026-05-06 07:08:09');";
                insertar.Parameters.AddWithValue("$r", c.Rut);
                insertar.Parameters.AddWithValue("$n", c.Razon);
                insertar.Parameters.AddWithValue("$c", c.Correo);
                insertar.Parameters.AddWithValue("$a", c.Activo);
                insertar.ExecuteNonQuery();
            }
        }
        return ruta;
    }

    [Fact]
    public void Importa_clientes_de_clickfactura_sin_tocar_la_base_original()
    {
        string original = BaseClickFactura(
            ("77000000-K", "Ñandú SpA", "a@ejemplo.cl; b@ejemplo.cl", 1),
            ("76000000-1", "Áridos Uno", "aridos@ejemplo.cl", 1),
            ("78000000-0", "Inactivo Ltda", "inactivo@ejemplo.cl", 0)
        );
        string huella = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(original))
        );
        DateTime fecha = File.GetLastWriteTimeUtc(original);
        using var almacen = new AlmacenMensajero(RutaBase);

        Assert.Equal(3, almacen.ImportarClientesFactura(original));

        Assert.Equal(
            [
                new ClienteFactura("76000000-1", "Áridos Uno", "aridos@ejemplo.cl"),
                new ClienteFactura("77000000-K", "Ñandú SpA", "a@ejemplo.cl; b@ejemplo.cl"),
            ],
            almacen.LeerClientesFactura()
        );
        Assert.Null(almacen.BuscarClienteFactura("78000000-0"));
        Assert.Equal(
            huella,
            Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(original))
            )
        );
        Assert.Equal(fecha, File.GetLastWriteTimeUtc(original));
        Assert.False(File.Exists(original + "-wal"));
    }

    [Fact]
    public void Importar_clickfactura_nunca_pisa_clientes_existentes()
    {
        using var almacen = new AlmacenMensajero(RutaBase);
        almacen.GuardarClienteFactura("79000000-5", "Ya Estaba", "ya@ejemplo.cl", DateTime.Now);

        Assert.Equal(
            0,
            almacen.ImportarClientesFactura(
                BaseClickFactura(("76000000-1", "Otro", "o@ejemplo.cl", 1))
            )
        );
        Assert.Equal(
            [new ClienteFactura("79000000-5", "Ya Estaba", "ya@ejemplo.cl")],
            almacen.LeerClientesFactura()
        );
        Assert.Equal(0, almacen.ImportarClientesFactura(Path.Combine(raiz, "no-existe.db")));
    }

    [Fact]
    public void Guardar_cliente_factura_actualiza_sin_perder_la_fecha_de_creacion()
    {
        using var almacen = new AlmacenMensajero(RutaBase);
        almacen.GuardarClienteFactura(
            "76000000-1",
            "Nombre Viejo",
            "v@ejemplo.cl",
            new DateTime(2026, 1, 1, 8, 0, 0)
        );
        almacen.GuardarClienteFactura(
            "76000000-1",
            "Nombre Nuevo",
            "n@ejemplo.cl",
            new DateTime(2026, 2, 3, 9, 10, 11)
        );

        Assert.Equal(
            new ClienteFactura("76000000-1", "Nombre Nuevo", "n@ejemplo.cl"),
            almacen.BuscarClienteFactura("76000000-1")
        );
        using var conexion = new SqliteConnection($"Data Source={RutaBase};Pooling=False");
        conexion.Open();
        using var leer = conexion.CreateCommand();
        leer.CommandText = "SELECT creado || '|' || actualizado FROM clientes_factura;";
        Assert.Equal("2026-01-01 08:00:00|2026-02-03 09:10:11", leer.ExecuteScalar());
    }

    [Fact]
    public void Recuerda_valores_de_configuracion()
    {
        using (var almacen = new AlmacenMensajero(RutaBase))
        {
            Assert.Equal("", almacen.LeerValor("factura.carpeta_temporal"));
            almacen.GuardarValor("factura.carpeta_temporal", @"C:\Temporal");
            almacen.GuardarValor("factura.carpeta_temporal", @"D:\Otra");
        }

        using var otra = new AlmacenMensajero(RutaBase);
        Assert.Equal(@"D:\Otra", otra.LeerValor("factura.carpeta_temporal"));
        Assert.Equal("", otra.LeerCarpetaOcc());
    }

    [Fact]
    public void Normaliza_y_valida_varios_correos_al_guardar_cliente()
    {
        using var almacen = new AlmacenMensajero(RutaBase);

        almacen.GuardarClienteFactura(
            "76000000-1",
            "Empresa",
            "uno@ejemplo.cl, dos@ejemplo.cl\n tres@ejemplo.cl",
            DateTime.Now
        );

        Assert.Equal(
            "uno@ejemplo.cl; dos@ejemplo.cl; tres@ejemplo.cl",
            almacen.BuscarClienteFactura("76000000-1")!.Correo
        );
        Assert.Throws<FormatException>(() =>
            almacen.GuardarClienteFactura("76000000-2", "Empresa", "correo-invalido", DateTime.Now)
        );
    }

    [Fact]
    public void Recuerda_estados_de_envio_por_periodo_y_rut()
    {
        using (var almacen = new AlmacenMensajero(RutaBase))
        {
            almacen.PrepararEstadosEnvioFactura("semana julio 2026", ["76000000-1", "77000000-2"]);
            almacen.GuardarEstadoEnvioFactura("semana julio 2026", "77000000-2", true);
            almacen.GuardarEstadoEnvioFactura("semana julio 2026", "76000000-1", false);
        }

        using var otra = new AlmacenMensajero(RutaBase);
        Assert.Equal(["77000000-2"], otra.LeerRutsEnviadosFactura("semana julio 2026"));
        Assert.Empty(otra.LeerRutsEnviadosFactura("otra semana"));
        Assert.Equal(
            ["76000000-1"],
            EstadoEnvioFactura.Filtrar(
                new[] { "76000000-1", "77000000-2" },
                rut => rut,
                otra.LeerRutsEnviadosFactura("semana julio 2026"),
                FiltroEstadoEnvioFactura.Pendientes
            )
        );
        otra.PrepararEstadosEnvioFactura("semana julio 2026", ["76000000-1", "77000000-2"]);
        Assert.Equal(["77000000-2"], otra.LeerRutsEnviadosFactura("semana julio 2026"));
    }

    [Theory]
    [InlineData("a@ejemplo.cl", new[] { "a@ejemplo.cl" })]
    [InlineData("a@ejemplo.cl, b@ejemplo.cl", new[] { "a@ejemplo.cl", "b@ejemplo.cl" })]
    [InlineData(
        "Juan Pérez <juan@ejemplo.cl>; x@y",
        new[] { "Juan Pérez <juan@ejemplo.cl>", "x@y" }
    )]
    [InlineData("", new string[0])]
    public void Separar_correos_para_mostrar_nunca_falla(string entrada, string[] esperado)
    {
        Assert.Equal(esperado, CorreoFactura.Separar(entrada));
    }
}
