using Archivero.Servicios;

namespace Archivero.Tests;

/// <summary>
/// Caso-9, mejora 2. Estos tests describen el comportamiento pedido y tienen que fallar contra
/// el esqueleto sin implementar (AuditoriaService no escribe nada todavía) -- recién pasan
/// después de escribir la lógica real.
/// </summary>
public class AuditoriaServiceTests : IDisposable
{
    private readonly string _rutaLogTemporal;
    private readonly string _rutaLogOriginal;

    public AuditoriaServiceTests()
    {
        _rutaLogOriginal = AuditoriaService.RutaLog;
        _rutaLogTemporal = Path.Combine(Path.GetTempPath(), $"archivero-tests-{Guid.NewGuid():N}", "auditoria.log");
        AuditoriaService.RutaLog = _rutaLogTemporal;
    }

    [Fact]
    public void Registrar_EscribeUnaLineaConFechaTipoYDetalle()
    {
        AuditoriaService.Registrar("DOCUMENTO_DETECTADO", @"C:\Observada\factura.pdf");

        Assert.True(File.Exists(_rutaLogTemporal));
        var linea = File.ReadAllLines(_rutaLogTemporal).Single();

        Assert.Matches(@"^\[\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\] DOCUMENTO_DETECTADO — C:\\Observada\\factura\.pdf$", linea);
    }

    [Fact]
    public void Registrar_VariasVeces_AgregaLineasSinBorrarLasAnteriores()
    {
        AuditoriaService.Registrar("DOCUMENTO_DETECTADO", "uno.pdf");
        AuditoriaService.Registrar("DOCUMENTO_GUARDADO", "dos.pdf");
        AuditoriaService.Registrar("DOCUMENTO_PENDIENTE", "tres.pdf");

        var lineas = File.ReadAllLines(_rutaLogTemporal);

        Assert.Equal(3, lineas.Length);
        Assert.Contains("uno.pdf", lineas[0]);
        Assert.Contains("dos.pdf", lineas[1]);
        Assert.Contains("tres.pdf", lineas[2]);
    }

    [Fact]
    public void Registrar_CreaLaCarpetaDelLogSiNoExiste()
    {
        Assert.False(Directory.Exists(Path.GetDirectoryName(_rutaLogTemporal)));

        AuditoriaService.Registrar("DOCUMENTO_DETECTADO", "factura.pdf");

        Assert.True(File.Exists(_rutaLogTemporal));
    }

    // ----- Inyección de líneas: un valor variable nunca puede partir una línea del log -----

    [Fact]
    public void Registrar_ConSaltoDeLineaEnElDetalle_NuncaAgregaUnaLineaDeMas()
    {
        // El texto sigue apareciendo (saneado), pero como parte de LA MISMA línea -- nunca como
        // una segunda entrada de log que finja ser un evento real aparte.
        AuditoriaService.Registrar("VALIDACION_RECHAZADA", "Factura\n[2026-01-01T00:00:00] DOCUMENTO_GUARDADO — falso");

        Assert.Single(File.ReadAllLines(_rutaLogTemporal));
    }

    [Fact]
    public void Registrar_ConRetornoDeCarroEnElDetalle_NuncaAgregaUnaLineaDeMas()
    {
        AuditoriaService.Registrar("VALIDACION_RECHAZADA", "Factura\r\n[fecha-inventada] EVENTO_FALSO — x");

        Assert.Single(File.ReadAllLines(_rutaLogTemporal));
    }

    [Theory]
    [InlineData("uno\ndos", "uno dos")]
    [InlineData("uno\r\ndos", "uno dos")]
    [InlineData("con\u0000nulo", "con nulo")]
    [InlineData("con\u0007control", "con control")]
    public void Sanear_ReemplazaSaltosDeLineaYControlPorEspacio(string texto, string esperado)
    {
        Assert.Equal(esperado, AuditoriaService.Sanear(texto));
    }

    [Fact]
    public void Sanear_ConTextoNormal_NoLoToca()
    {
        Assert.Equal("Banco de Prueba SA — Factura (1).pdf", AuditoriaService.Sanear("Banco de Prueba SA — Factura (1).pdf"));
    }

    // ----- Falla al escribir nunca detiene la app -----

    [Fact]
    public void Registrar_SiLaRutaDelLogEsInvalida_NoLanzaExcepcion()
    {
        // Una carpeta con el mismo nombre que el archivo de log hace que File.AppendAllText
        // falle: Registrar tiene que tragarse el error, nunca propagarlo.
        Directory.CreateDirectory(_rutaLogTemporal);

        var ex = Record.Exception(() => AuditoriaService.Registrar("DOCUMENTO_DETECTADO", "factura.pdf"));

        Assert.Null(ex);
    }

    // ----- Rotación -----

    [Fact]
    public void Registrar_ConElArchivoSuperandoElTamanioMaximo_RotaAntesDeEscribir()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_rutaLogTemporal)!);
        File.WriteAllText(_rutaLogTemporal, new string('a', (int)AuditoriaService.TamanioMaximoBytes));

        AuditoriaService.Registrar("DOCUMENTO_DETECTADO", "factura.pdf");

        Assert.True(File.Exists($"{_rutaLogTemporal}.1"));
        var lineasNuevas = File.ReadAllLines(_rutaLogTemporal);
        Assert.Single(lineasNuevas);
        Assert.Contains("factura.pdf", lineasNuevas[0]);
    }

    [Fact]
    public void Registrar_ConRotadosPrevios_LosCorreUnNumeroYDescartaElMasViejo()
    {
        var carpeta = Path.GetDirectoryName(_rutaLogTemporal)!;
        Directory.CreateDirectory(carpeta);
        File.WriteAllText(_rutaLogTemporal, new string('a', (int)AuditoriaService.TamanioMaximoBytes));
        File.WriteAllText($"{_rutaLogTemporal}.1", "contenido-1-viejo");
        File.WriteAllText($"{_rutaLogTemporal}.2", "contenido-2-viejo");
        File.WriteAllText($"{_rutaLogTemporal}.3", "contenido-3-el-mas-viejo-de-todos");

        AuditoriaService.Registrar("DOCUMENTO_DETECTADO", "factura.pdf");

        Assert.Equal("contenido-1-viejo", File.ReadAllText($"{_rutaLogTemporal}.2"));
        Assert.Equal("contenido-2-viejo", File.ReadAllText($"{_rutaLogTemporal}.3"));
        Assert.False(File.Exists($"{_rutaLogTemporal}.4"), "Nunca se conservan más de 3 archivos rotados");
    }

    public void Dispose()
    {
        AuditoriaService.RutaLog = _rutaLogOriginal;
        var carpeta = Path.GetDirectoryName(_rutaLogTemporal);
        if (carpeta is not null && Directory.Exists(carpeta))
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }
}
