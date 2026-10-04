using Archivero.Datos;

namespace Archivero.Tests;

public class UbicacionSinTextoRepositoryTests : IDisposable
{
    private readonly string _rutaDbTemporal;

    public UbicacionSinTextoRepositoryTests()
    {
        _rutaDbTemporal = Path.Combine(Path.GetTempPath(), $"archivero-tests-{Guid.NewGuid():N}.db");
        BaseDeDatos.RutaArchivo = _rutaDbTemporal;
        BaseDeDatos.AsegurarEsquema();
    }

    [Fact]
    public void ObtenerOCrear_LaPrimeraVez_CreaUnaUbicacionNueva()
    {
        var repo = new UbicacionSinTextoRepository();

        var ubicacion = repo.ObtenerOCrear(@"C:\Guias", FormatoCarpeta.AnioMes, @"yyyy\MM");

        Assert.Equal(@"C:\Guias", ubicacion.CarpetaMadre);
        Assert.Equal(FormatoCarpeta.AnioMes, ubicacion.Formato);
        Assert.Equal(@"yyyy\MM", ubicacion.Patron);
        Assert.Single(repo.ObtenerTodas());
    }

    [Fact]
    public void ObtenerOCrear_ConLosMismosDatos_DevuelveLaMismaSinDuplicar()
    {
        var repo = new UbicacionSinTextoRepository();

        var primera = repo.ObtenerOCrear(@"C:\Guias", FormatoCarpeta.AnioMes, @"yyyy\MM");
        var segunda = repo.ObtenerOCrear(@"C:\Guias", FormatoCarpeta.AnioMes, @"yyyy\MM");

        Assert.Equal(primera.Id, segunda.Id);
        Assert.Single(repo.ObtenerTodas());
    }

    [Fact]
    public void ObtenerOCrear_ConPatronNuloDosVeces_NoDuplica()
    {
        // Caso limite: FormatoCarpeta.Directo siempre tiene Patron null -- confirma que la
        // comparacion por NULL funciona (SQLite no compara NULL = NULL como verdadero).
        var repo = new UbicacionSinTextoRepository();

        var primera = repo.ObtenerOCrear(@"C:\Directo", FormatoCarpeta.Directo, null);
        var segunda = repo.ObtenerOCrear(@"C:\Directo", FormatoCarpeta.Directo, null);

        Assert.Equal(primera.Id, segunda.Id);
        Assert.Single(repo.ObtenerTodas());
    }

    [Fact]
    public void ObtenerOCrear_ConCarpetaOFormatoDistinto_CreaUnaSegundaUbicacion()
    {
        var repo = new UbicacionSinTextoRepository();

        repo.ObtenerOCrear(@"C:\Guias", FormatoCarpeta.AnioMes, @"yyyy\MM");
        repo.ObtenerOCrear(@"C:\Guias", FormatoCarpeta.Anio, "yyyy");
        repo.ObtenerOCrear(@"C:\OtraCarpeta", FormatoCarpeta.AnioMes, @"yyyy\MM");

        Assert.Equal(3, repo.ObtenerTodas().Count);
    }

    [Fact]
    public void ObtenerTodas_SinNadaGuardado_DevuelveListaVacia()
    {
        var repo = new UbicacionSinTextoRepository();

        Assert.Empty(repo.ObtenerTodas());
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_rutaDbTemporal);
    }
}
