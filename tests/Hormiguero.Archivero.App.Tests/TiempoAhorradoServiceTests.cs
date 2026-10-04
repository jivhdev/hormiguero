using Archivero.Datos;
using Archivero.Servicios;

namespace Archivero.Tests;

public class TiempoAhorradoServiceTests : IDisposable
{
    private readonly string _rutaDbTemporal;

    public TiempoAhorradoServiceTests()
    {
        _rutaDbTemporal = Path.Combine(Path.GetTempPath(), $"archivero-tests-{Guid.NewGuid():N}.db");
        BaseDeDatos.RutaArchivo = _rutaDbTemporal;
        BaseDeDatos.AsegurarEsquema();
    }

    [Fact]
    public void ObtenerTotalDocumentos_SinNingunGuardado_DevuelveCero()
    {
        var configuracion = new ConfiguracionRepository();

        Assert.Equal(0, TiempoAhorradoService.ObtenerTotalDocumentos(configuracion));
    }

    [Fact]
    public void RegistrarDocumentoArchivado_AcumulaElTotalHistorico()
    {
        var configuracion = new ConfiguracionRepository();

        TiempoAhorradoService.RegistrarDocumentoArchivado(configuracion);
        TiempoAhorradoService.RegistrarDocumentoArchivado(configuracion);
        TiempoAhorradoService.RegistrarDocumentoArchivado(configuracion);

        Assert.Equal(3, TiempoAhorradoService.ObtenerTotalDocumentos(configuracion));
    }

    [Theory]
    [InlineData(0, "0 documentos archivados automáticamente — tiempo humano ahorrado: ~0 minutos")]
    [InlineData(1, "1 documento archivado automáticamente — tiempo humano ahorrado: ~0 minutos")]
    [InlineData(20, "20 documentos archivados automáticamente — tiempo humano ahorrado: ~1 minuto")]
    [InlineData(1200, "1200 documentos archivados automáticamente — tiempo humano ahorrado: ~1 hora")]
    [InlineData(1220, "1220 documentos archivados automáticamente — tiempo humano ahorrado: ~1 hora y 1 minuto")]
    [InlineData(2410, "2410 documentos archivados automáticamente — tiempo humano ahorrado: ~2 horas y 1 minuto")]
    public void FormatearResumen_ConDistintosTotales_DaElTextoEsperado(int total, string tituloEsperado)
    {
        var (titulo, _) = TiempoAhorradoService.FormatearResumen(total);

        Assert.Equal(tituloEsperado, titulo);
    }

    [Fact]
    public void FormatearResumen_MenosDeUnaHora_MuestraSoloMinutos()
    {
        // 3 segundos * 1000 documentos = 3000 segundos = 50 minutos exactos.
        var (titulo, _) = TiempoAhorradoService.FormatearResumen(1000);

        Assert.Contains("~50 minutos", titulo);
    }

    [Fact]
    public void FormatearResumen_UnaHoraOMas_MuestraHorasYMinutos()
    {
        // 3 segundos * 1300 documentos = 3900 segundos = 65 minutos = 1h 5min.
        var (titulo, _) = TiempoAhorradoService.FormatearResumen(1300);

        Assert.Contains("~1 hora y 5 minutos", titulo);
    }

    [Fact]
    public void FormatearResumen_IncluyeLaAclaracionDelCriterio()
    {
        var (_, aclaracion) = TiempoAhorradoService.FormatearResumen(10);

        Assert.Equal("(estimado a ~3 segundos de atención manual ahorrados por documento)", aclaracion);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_rutaDbTemporal);
    }
}
