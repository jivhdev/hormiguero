using Hormiguero.Mensajero.Core;
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
}
