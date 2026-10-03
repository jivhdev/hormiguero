using System.Diagnostics;
using Hormiguero.Buscadero.Logica;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Buscadero.Tests;

public class CarpetasConfiguradasTests
{
    private static string RutaTemporal() =>
        Path.Combine(
            Path.GetTempPath(),
            "HormigueroBuscaderoTests",
            Guid.NewGuid().ToString("N"),
            "hormiguero.db"
        );

    private static void Borrar(string ruta)
    {
        // El agrupamiento de conexiones de Microsoft.Data.Sqlite deja el archivo
        // abierto en Windows; hay que soltarlo antes de borrar la carpeta temporal.
        SqliteConnection.ClearAllPools();
        string? carpeta = Path.GetDirectoryName(ruta);
        if (!string.IsNullOrEmpty(carpeta) && Directory.Exists(carpeta))
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }

    [Fact]
    public void Base_nueva_sin_carpetas()
    {
        string ruta = RutaTemporal();
        try
        {
            using var conexion = BaseComun.Abrir(ruta);
            var carpetas = new CarpetasConfiguradas(conexion);

            Assert.Empty(carpetas.Listar());
            Assert.False(carpetas.HayAlguna);
        }
        finally
        {
            Borrar(ruta);
        }
    }

    [Fact]
    public void No_duplica_carpetas()
    {
        string ruta = RutaTemporal();
        try
        {
            using var conexion = BaseComun.Abrir(ruta);
            var carpetas = new CarpetasConfiguradas(conexion);

            Assert.True(carpetas.Agregar(@"C:\X\"));
            Assert.False(carpetas.Agregar(@"c:\x"));

            // Queda una sola, ya normalizada: sin la barra final.
            Assert.Equal(@"C:\X", Assert.Single(carpetas.Listar()));
            Assert.True(carpetas.HayAlguna);
        }
        finally
        {
            Borrar(ruta);
        }
    }

    [Fact]
    public void Quitar_inexistente_no_falla()
    {
        string ruta = RutaTemporal();
        try
        {
            using var conexion = BaseComun.Abrir(ruta);
            var carpetas = new CarpetasConfiguradas(conexion);
            carpetas.Agregar(@"C:\X\");

            Assert.False(carpetas.Quitar(@"D:\Ausente"));
            Assert.Equal(@"C:\X", Assert.Single(carpetas.Listar()));
        }
        finally
        {
            Borrar(ruta);
        }
    }

    [Fact]
    public void La_lista_persiste()
    {
        string ruta = RutaTemporal();
        try
        {
            using (var conexion = BaseComun.Abrir(ruta))
            {
                var carpetas = new CarpetasConfiguradas(conexion);
                Assert.True(carpetas.Agregar(Path.GetDirectoryName(ruta)!));
            }

            using (var conexion = BaseComun.Abrir(ruta))
            {
                var carpetas = new CarpetasConfiguradas(conexion);

                Assert.True(carpetas.HayAlguna);
                Assert.Equal(Path.GetDirectoryName(ruta), Assert.Single(carpetas.Listar()));
            }
        }
        finally
        {
            Borrar(ruta);
        }
    }

    [Fact]
    public async Task Carpeta_inexistente_no_esta_disponible()
    {
        var limite = TimeSpan.FromSeconds(5);
        string ruta = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var cronometro = Stopwatch.StartNew();
        bool disponible = await CarpetasConfiguradas.EstaDisponibleAsync(ruta, limite);
        cronometro.Stop();

        Assert.False(disponible);
        Assert.True(cronometro.Elapsed < limite, "La comprobación tardó más que el límite.");
    }
}
