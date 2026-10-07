using Archivero.Datos;

namespace Archivero.Tests;

public sealed class ConfiguracionImpresionRepositoryTests : IDisposable
{
    private readonly string _rutaOriginal = BaseDeDatos.RutaArchivo;
    private readonly string _directorio = Path.Combine(
        Path.GetTempPath(),
        "archivero-config-impresion-" + Guid.NewGuid().ToString("N")
    );

    public ConfiguracionImpresionRepositoryTests()
    {
        Directory.CreateDirectory(_directorio);
        BaseDeDatos.RutaArchivo = Path.Combine(_directorio, "archivero.db");
        BaseDeDatos.AsegurarEsquema();
    }

    [Fact]
    public void ConfiguracionNueva_UsaNoComoPredeterminado()
    {
        int id = CrearConfiguracion();

        Assert.Equal((ModoImpresion.No, null), new ConfiguracionImpresionRepository().Leer(id));
    }

    [Fact]
    public void Guardar_ConservaModoEImpresoraPorTipo()
    {
        int id = CrearConfiguracion();
        var repo = new ConfiguracionImpresionRepository();

        repo.Guardar(id, ModoImpresion.PrimeraPagina, "Impresora local");

        Assert.Equal((ModoImpresion.PrimeraPagina, "Impresora local"), repo.Leer(id));
    }

    private static int CrearConfiguracion() =>
        new ConfiguracionDocumentoRepository().GuardarNueva(
            "Emisor",
            "Tipo",
            "C:\\Destino",
            FormatoCarpeta.Directo,
            null,
            false,
            []
        );

    public void Dispose()
    {
        BaseDeDatos.RutaArchivo = _rutaOriginal;
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directorio, true);
    }
}
