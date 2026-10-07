using Archivero.Datos;
using Archivero.Servicios;

namespace Archivero.Tests;

public sealed class ImpresionAlArchivarServiceTests : IDisposable
{
    private readonly string _raiz = Path.Combine(
        Path.GetTempPath(),
        "archivero-impresion-" + Guid.NewGuid().ToString("N")
    );
    private readonly string _rutaLogOriginal = AuditoriaService.RutaLog;

    public ImpresionAlArchivarServiceTests()
    {
        Directory.CreateDirectory(_raiz);
        AuditoriaService.RutaLog = Path.Combine(_raiz, "auditoria.log");
    }

    [Fact]
    public void PrimeraPagina_SolicitaSoloLaPrimeraPagina()
    {
        var accion = new AccionImpresionFalsa();
        var resultado = Servicio(accion)
            .Procesar(CrearPdf(), Configuracion(ModoImpresion.PrimeraPagina));

        Assert.Null(resultado);
        Assert.True(accion.SoloPrimeraPagina);
        Assert.Equal("Predeterminada", accion.ImpresoraUsada);
    }

    [Fact]
    public void TodoElDocumento_SolicitaTodasLasPaginas()
    {
        var accion = new AccionImpresionFalsa();
        Servicio(accion).Procesar(CrearPdf(), Configuracion(ModoImpresion.TodoElDocumento));

        Assert.False(accion.SoloPrimeraPagina);
    }

    [Fact]
    public void No_NoEnviaNiAbreElDocumento()
    {
        var accion = new AccionImpresionFalsa();
        Servicio(accion).Procesar(CrearPdf(), Configuracion(ModoImpresion.No));

        Assert.Equal(0, accion.Impresiones);
        Assert.Equal(0, accion.Aperturas);
    }

    [Fact]
    public void PreguntarCadaVez_AbreElVisorPredeterminado()
    {
        var accion = new AccionImpresionFalsa();
        string ruta = CrearPdf();
        Servicio(accion).Procesar(ruta, Configuracion(ModoImpresion.PreguntarCadaVez));

        Assert.Equal(ruta, accion.RutaAbierta);
        Assert.Equal(0, accion.Impresiones);
    }

    [Fact]
    public void ImpresoraInexistente_UsaLaPredeterminadaYDevuelveAviso()
    {
        var accion = new AccionImpresionFalsa();
        string? aviso = Servicio(accion)
            .Procesar(CrearPdf(), Configuracion(ModoImpresion.PrimeraPagina, "Desaparecida"));

        Assert.Contains("predeterminada de Windows", aviso);
        Assert.Equal("Predeterminada", accion.ImpresoraUsada);
    }

    [Fact]
    public void FallaDeImpresion_SeInformaSinDeshacerElGuardado()
    {
        string origen = Path.Combine(_raiz, "origen");
        string destino = Path.Combine(_raiz, "destino");
        Directory.CreateDirectory(origen);
        Directory.CreateDirectory(destino);
        string ruta = CreadorPdfDePrueba.CrearConLineas(
            origen,
            "Banco de Prueba SA",
            "Resumen de cuenta"
        );
        var configuracion = new ConfiguracionDocumento
        {
            Id = 1,
            Emisor = "Banco de Prueba SA",
            Tipo = "Resumen de cuenta",
            CarpetaDestino = destino,
            FormatoCarpeta = FormatoCarpeta.Directo,
            Renombrar = false,
            ModoImpresion = ModoImpresion.TodoElDocumento,
            Patrones = [new(1, [Marca(CampoMarca.Emisor, 0), Marca(CampoMarca.Tipo, 1)])],
        };

        var resultado = GuardadoAutomaticoService.Procesar(
            ruta,
            configuracion,
            Servicio(new AccionImpresionFalsa { FallarImpresion = true })
        );

        Assert.Equal(ResultadoGuardadoAutomatico.Guardado, resultado.Resultado);
        Assert.Contains("no se pudo imprimir", resultado.Detalle);
        Assert.True(File.Exists(resultado.RutaFinal));
        Assert.False(File.Exists(ruta));
    }

    private string CrearPdf()
    {
        string ruta = Path.Combine(_raiz, "prueba.pdf");
        File.WriteAllBytes(ruta, [1, 2, 3]);
        return ruta;
    }

    private static ImpresionAlArchivarService Servicio(AccionImpresionFalsa accion) => new(accion);

    private static ConfiguracionDocumento Configuracion(
        ModoImpresion modo,
        string? impresora = null
    ) =>
        new()
        {
            Id = 1,
            Emisor = "Emisor",
            Tipo = "Tipo",
            CarpetaDestino = "C:\\",
            FormatoCarpeta = FormatoCarpeta.Directo,
            Renombrar = false,
            Patrones = [],
            ModoImpresion = modo,
            Impresora = impresora,
        };

    private static Marca Marca(CampoMarca campo, int indice)
    {
        var banda = CreadorPdfDePrueba.ObtenerBandaDeLinea(indice);
        return new(campo, 0, banda.X, banda.Y, banda.Ancho, banda.Alto);
    }

    public void Dispose()
    {
        AuditoriaService.RutaLog = _rutaLogOriginal;
        Directory.Delete(_raiz, true);
    }

    private sealed class AccionImpresionFalsa : IAccionImpresion
    {
        public string ImpresoraPredeterminada => "Predeterminada";
        public IReadOnlyList<string> ImpresorasInstaladas => ["Impresora disponible"];
        public int Impresiones { get; private set; }
        public int Aperturas { get; private set; }
        public bool SoloPrimeraPagina { get; private set; }
        public string? ImpresoraUsada { get; private set; }
        public string? RutaAbierta { get; private set; }
        public bool FallarImpresion { get; init; }

        public void Imprimir(byte[] pdf, bool soloPrimeraPagina, string? impresora)
        {
            Impresiones++;
            SoloPrimeraPagina = soloPrimeraPagina;
            ImpresoraUsada = impresora ?? ImpresoraPredeterminada;
            if (FallarImpresion)
                throw new InvalidOperationException("Fallo simulado");
        }

        public void AbrirVisor(string rutaPdf)
        {
            Aperturas++;
            RutaAbierta = rutaPdf;
        }
    }
}
