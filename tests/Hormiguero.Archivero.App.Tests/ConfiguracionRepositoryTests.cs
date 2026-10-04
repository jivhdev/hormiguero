using Archivero.Datos;

namespace Archivero.Tests;

public class ConfiguracionRepositoryTests : IDisposable
{
    private readonly string _rutaDbTemporal;

    public ConfiguracionRepositoryTests()
    {
        _rutaDbTemporal = Path.Combine(Path.GetTempPath(), $"archivero-tests-{Guid.NewGuid():N}.db");
        BaseDeDatos.RutaArchivo = _rutaDbTemporal;
        BaseDeDatos.AsegurarEsquema();
    }

    [Fact]
    public void ObtenerContador_SinNadaGuardado_DevuelveCero()
    {
        var repo = new ConfiguracionRepository();

        Assert.Equal(0, repo.ObtenerContador("Cualquiera"));
    }

    [Fact]
    public void IncrementarContador_VariasVeces_Acumula()
    {
        var repo = new ConfiguracionRepository();

        repo.IncrementarContador("Total");
        repo.IncrementarContador("Total");
        repo.IncrementarContador("Total");

        Assert.Equal(3, repo.ObtenerContador("Total"));
    }

    [Fact]
    public void IncrementarContador_ConClavesDistintas_NoSeMezclan()
    {
        var repo = new ConfiguracionRepository();

        repo.IncrementarContador("A");
        repo.IncrementarContador("A");
        repo.IncrementarContador("B");

        Assert.Equal(2, repo.ObtenerContador("A"));
        Assert.Equal(1, repo.ObtenerContador("B"));
    }

    [Fact]
    public void IncrementarContador_SobreviveARecrearElRepositorio()
    {
        new ConfiguracionRepository().IncrementarContador("Total");

        var repoNuevo = new ConfiguracionRepository();
        repoNuevo.IncrementarContador("Total");

        Assert.Equal(2, repoNuevo.ObtenerContador("Total"));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_rutaDbTemporal);
    }
}
