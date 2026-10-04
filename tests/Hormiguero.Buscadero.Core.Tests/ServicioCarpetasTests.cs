using Buscadero.Core.Carpetas;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Buscadero.Core.Tests;

public sealed class ServicioCarpetasTests : IDisposable
{
    private readonly string _rutaBaseDeDatos;
    private readonly ServicioCarpetas _servicio;

    public ServicioCarpetasTests()
    {
        _rutaBaseDeDatos = Path.Combine(
            Path.GetTempPath(),
            $"buscadero-test-{Guid.NewGuid():N}.db"
        );
        _servicio = new ServicioCarpetas(new RepositorioCarpetas(_rutaBaseDeDatos));
    }

    [Fact]
    public void Agregar_CarpetaNueva_QuedaDisponible()
    {
        var resultado = _servicio.Agregar(@"C:\Documentos");

        Assert.Equal(ResultadoAgregarCarpeta.Agregada, resultado);
        Assert.Single(_servicio.ObtenerTodas());
    }

    [Fact]
    public void Agregar_CarpetaYaConfigurada_NoDuplica()
    {
        _servicio.Agregar(@"C:\Documentos");

        var resultado = _servicio.Agregar(@"C:\Documentos\");

        Assert.Equal(ResultadoAgregarCarpeta.YaConfigurada, resultado);
        Assert.Single(_servicio.ObtenerTodas());
    }

    [Fact]
    public void Agregar_CarpetaYaConfigurada_IgnoraMayusculas()
    {
        _servicio.Agregar(@"C:\Documentos");

        var resultado = _servicio.Agregar(@"c:\documentos");

        Assert.Equal(ResultadoAgregarCarpeta.YaConfigurada, resultado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Agregar_RutaVacia_EsInvalida(string ruta)
    {
        var resultado = _servicio.Agregar(ruta);

        Assert.Equal(ResultadoAgregarCarpeta.RutaInvalida, resultado);
        Assert.Empty(_servicio.ObtenerTodas());
    }

    [Fact]
    public void Quitar_CarpetaExistente_LaElimina()
    {
        _servicio.Agregar(@"C:\Documentos");
        var carpeta = _servicio.ObtenerTodas().Single();

        _servicio.Quitar(carpeta.Id);

        Assert.Empty(_servicio.ObtenerTodas());
    }

    [Fact]
    public void EsAccesible_CarpetaInexistente_DevuelveFalso()
    {
        Assert.False(_servicio.EsAccesible(@"C:\Esta-Carpeta-No-Existe-" + Guid.NewGuid()));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_rutaBaseDeDatos))
        {
            File.Delete(_rutaBaseDeDatos);
        }
    }
}
