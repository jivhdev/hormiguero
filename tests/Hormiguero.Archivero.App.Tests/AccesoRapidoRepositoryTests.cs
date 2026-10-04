using Archivero.Datos;

namespace Archivero.Tests;

/// <summary>Caso-3, punto 3a: los accesos rápidos (tipos de organización favoritos) persisten entre sesiones.</summary>
public class AccesoRapidoRepositoryTests : IDisposable
{
    private readonly string _rutaDbTemporal;

    public AccesoRapidoRepositoryTests()
    {
        _rutaDbTemporal = Path.Combine(Path.GetTempPath(), $"archivero-tests-{Guid.NewGuid():N}.db");
        BaseDeDatos.RutaArchivo = _rutaDbTemporal;
        BaseDeDatos.AsegurarEsquema();
    }

    [Fact]
    public void Obtener_SinNadaConfigurado_DevuelveListaVacia()
    {
        // Caso-3, 3a: la primera vez no hay ningún acceso rápido configurado.
        Assert.Empty(new AccesoRapidoRepository().Obtener());
    }

    [Fact]
    public void GuardarYObtener_ConservaLosTiposEnOrden()
    {
        var repo = new AccesoRapidoRepository();

        repo.Guardar([FormatoCarpeta.Anio, FormatoCarpeta.AnioMes, FormatoCarpeta.Personalizado]);

        Assert.Equal([FormatoCarpeta.Anio, FormatoCarpeta.AnioMes, FormatoCarpeta.Personalizado], repo.Obtener());
    }

    [Fact]
    public void Guardar_DosVeces_ReemplazaLaListaAnterior()
    {
        var repo = new AccesoRapidoRepository();
        repo.Guardar([FormatoCarpeta.Anio, FormatoCarpeta.AnioMes]);

        repo.Guardar([FormatoCarpeta.AnioTrimestre]);

        Assert.Equal([FormatoCarpeta.AnioTrimestre], repo.Obtener());
    }

    [Fact]
    public void Obtener_ConValoresDesconocidosGuardados_LosOmiteSinRomper()
    {
        new ConfiguracionRepository().Guardar("AccesosRapidosOrganizacion", """["Anio","Inventado","AnioMes"]""");

        Assert.Equal([FormatoCarpeta.Anio, FormatoCarpeta.AnioMes], new AccesoRapidoRepository().Obtener());
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_rutaDbTemporal);
    }
}
