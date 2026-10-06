using Hormiguero.Reinicio;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Reinicio.Tests;

public sealed class ReinicioServicioTests : IDisposable
{
    private readonly string raiz = Path.Combine(
        Path.GetTempPath(),
        "HormigueroReinicioTests",
        Guid.NewGuid().ToString("N")
    );

    private string Carpeta => Path.Combine(raiz, "Hormiguero");
    private static readonly DateTime Ahora = new(2026, 10, 6, 12, 34, 56);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(raiz))
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    [Fact]
    public void Conserva_clientes_y_respalda_todos_los_datos()
    {
        CrearDatos();
        string otroArchivo = Path.Combine(Carpeta, "configuracion-extra.txt");
        File.WriteAllText(otroArchivo, "conservar íntegro");

        ResultadoReinicio resultado = new ReinicioServicio().Ejecutar(Carpeta, Ahora);

        Assert.True(resultado.Exitoso, resultado.Mensaje);
        Assert.Equal(2, resultado.ClientesConservados);
        Assert.Equal(Carpeta + "-respaldo-reinicio-20261006-123456", resultado.RutaRespaldo);
        Assert.Equal(
            "conservar íntegro",
            File.ReadAllText(Path.Combine(resultado.RutaRespaldo, "configuracion-extra.txt"))
        );
        Assert.Equal(
            new[]
            {
                "11111111-1|Compañía Ñandú|uno@ejemplo.cl; dos@ejemplo.cl|1|2020-01-02 03:04:05|2024-05-06 07:08:09",
                "22222222-2|Cliente inactivo|inactivo@ejemplo.cl|0|2019-10-11 12:13:14|2025-09-08 07:06:05",
            },
            LeerClientes(Path.Combine(Carpeta, "mensajero.db"))
        );
        Assert.True(File.Exists(Path.Combine(Carpeta, "reiniciado.txt")));
    }

    [Fact]
    public void Si_no_puede_renombrar_no_modifica_la_carpeta()
    {
        CrearDatos();
        string rutaBloqueada = Path.Combine(Carpeta, "bloqueado.txt");
        File.WriteAllText(rutaBloqueada, "no mover");
        byte[] contenido = File.ReadAllBytes(rutaBloqueada);
        using var bloqueador = new FileStream(
            rutaBloqueada,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
        ResultadoReinicio resultado = new ReinicioServicio().Ejecutar(Carpeta, Ahora);

        Assert.False(resultado.Exitoso);
        Assert.True(Directory.Exists(Carpeta));
        Assert.False(Directory.Exists(resultado.RutaRespaldo));
        Assert.False(File.Exists(Path.Combine(Carpeta, "reiniciado.txt")));
        var contenidoFinal = new byte[checked((int)bloqueador.Length)];
        bloqueador.Position = 0;
        bloqueador.ReadExactly(contenidoFinal);
        Assert.Equal(contenido, contenidoFinal);
        Assert.Equal(2, LeerClientes(Path.Combine(Carpeta, "mensajero.db")).Length);
    }

    [Fact]
    public void Si_falla_la_importacion_devuelve_la_carpeta_original()
    {
        CrearDatos();
        string archivo = Path.Combine(Carpeta, "anterior.txt");
        File.WriteAllText(archivo, "anterior");
        var servicio = new ReinicioServicio(
            (_, _) => throw new InvalidOperationException("fallo inducido")
        );

        ResultadoReinicio resultado = servicio.Ejecutar(Carpeta, Ahora);

        Assert.False(resultado.Exitoso);
        Assert.Contains("fallo inducido", resultado.Mensaje);
        Assert.Equal("anterior", File.ReadAllText(archivo));
        Assert.Equal(2, LeerClientes(Path.Combine(Carpeta, "mensajero.db")).Length);
        Assert.False(File.Exists(Path.Combine(Carpeta, "reiniciado.txt")));
        Assert.True(Directory.Exists(resultado.RutaRespaldo + "-fallido"));
    }

    private void CrearDatos()
    {
        Directory.CreateDirectory(Carpeta);
        using var conexion = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(Carpeta, "mensajero.db"),
                Pooling = false,
            }.ToString()
        );
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "CREATE TABLE clientes_factura(rut TEXT PRIMARY KEY, razon_social TEXT NOT NULL, correo TEXT NOT NULL, "
            + "activo INTEGER NOT NULL, creado TEXT NOT NULL, actualizado TEXT NOT NULL); "
            + "INSERT INTO clientes_factura VALUES "
            + "('11111111-1', 'Compañía Ñandú', 'uno@ejemplo.cl; dos@ejemplo.cl', 1, '2020-01-02 03:04:05', '2024-05-06 07:08:09'), "
            + "('22222222-2', 'Cliente inactivo', 'inactivo@ejemplo.cl', 0, '2019-10-11 12:13:14', '2025-09-08 07:06:05');";
        comando.ExecuteNonQuery();
    }

    private static string[] LeerClientes(string ruta)
    {
        using var conexion = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = ruta, Pooling = false }.ToString()
        );
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT rut || '|' || razon_social || '|' || correo || '|' || activo || '|' || creado || '|' || actualizado "
            + "FROM clientes_factura ORDER BY rut;";
        using var lector = comando.ExecuteReader();
        var clientes = new List<string>();
        while (lector.Read())
        {
            clientes.Add(lector.GetString(0));
        }
        return clientes.ToArray();
    }
}
