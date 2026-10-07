using Archivero.Datos;
using Archivero.Servicios;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Utilidades;
using Microsoft.Data.Sqlite;

namespace Archivero.Tests;

public sealed class ObservadorCarpetasServiceTests : IDisposable
{
    private readonly string _raiz = Path.Combine(
        Path.GetTempPath(),
        "observador-tests-" + Guid.NewGuid().ToString("N")
    );
    private readonly string? _datosPrevios = Environment.GetEnvironmentVariable("HORMIGUERO_DATOS");
    private readonly string _baseAnterior = BaseDeDatos.RutaArchivo;

    public ObservadorCarpetasServiceTests()
    {
        Directory.CreateDirectory(_raiz);
        Environment.SetEnvironmentVariable("HORMIGUERO_DATOS", Path.Combine(_raiz, "datos"));
        BaseDeDatos.RutaArchivo = Path.Combine(_raiz, "archivero.db");
        BaseDeDatos.AsegurarEsquema();
    }

    [Fact]
    public async Task RevisaSubcarpetas_PublicaCoincidencia_YConservaElPdf()
    {
        string observada = Path.Combine(_raiz, "observada");
        string subcarpeta = Path.Combine(observada, "mes");
        Directory.CreateDirectory(subcarpeta);
        string ruta = CreadorPdfDePrueba.CrearConLineas(subcarpeta, "Proveedor Uno", "Factura");
        string huella = Huella.Calcular(ruta);
        DateTime modificacion = File.GetLastWriteTimeUtc(ruta);
        var marcaEmisor = Marca(CampoMarca.Emisor, 0);
        var marcaTipo = Marca(CampoMarca.Tipo, 1);
        new ConfiguracionDocumentoRepository().GuardarNueva(
            "Proveedor Uno",
            "Factura",
            _raiz,
            FormatoCarpeta.Directo,
            null,
            false,
            [marcaEmisor, marcaTipo]
        );
        var carpetas = new CarpetasObservadasRepository();
        carpetas.Guardar([new(Guid.NewGuid(), "Facturas", observada, true, true)]);

        var encontro = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var servicio = new ObservadorCarpetasService(carpetas);
        servicio.DocumentoActualizado += documento =>
        {
            if (documento.Tipo == "Factura")
                encontro.TrySetResult();
        };
        servicio.Iniciar();
        await encontro.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);

        Assert.True(File.Exists(ruta));
        Assert.Equal(huella, Huella.Calcular(ruta));
        Assert.Equal(modificacion, File.GetLastWriteTimeUtc(ruta));
        Assert.Contains(
            carpetas.LeerActividad(),
            documento => documento.Ruta == ruta && documento.Emisor == "Proveedor Uno"
        );
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT origen FROM auditoria WHERE accion='publicar_documento' ORDER BY id DESC LIMIT 1;";
        Assert.Equal("observador", comando.ExecuteScalar());
    }

    [Fact]
    public async Task CedibleSinOriginal_QuedaEnBuscarOriginal_YSeSolucionaAlAparecer()
    {
        string observada = Path.Combine(_raiz, "observada");
        Directory.CreateDirectory(observada);
        string cedible = CreadorPdfDePrueba.CrearConLineas(observada, "CEDIBLE", "Factura");
        string rutaCedible = Path.Combine(observada, "factura_CEDIBLE.pdf");
        File.Move(cedible, rutaCedible);
        var carpetas = new CarpetasObservadasRepository();
        carpetas.Guardar([new(Guid.NewGuid(), "Facturas", observada, false, true)]);
        var encontre = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var servicio = new ObservadorCarpetasService(carpetas);
        servicio.DocumentoActualizado += documento =>
        {
            if (
                documento.Ruta == rutaCedible
                && documento.Motivo?.Contains("Buscar original") == true
            )
                encontre.TrySetResult();
        };
        servicio.Iniciar();
        await encontre.Task.WaitAsync(TimeSpan.FromSeconds(15));

        string original = Path.Combine(observada, "factura.pdf");
        File.Copy(rutaCedible, original);
        await servicio.RevisarAhoraAsync();

        Assert.Contains(
            carpetas.LeerActividad(),
            documento =>
                documento.Ruta == rutaCedible && documento.Motivo?.Contains("Solucionado") == true
        );
        Assert.True(File.Exists(rutaCedible));
    }

    [Fact]
    public async Task DocumentoSinCoincidencia_NoCreaPendienteNiActividad()
    {
        string observada = Path.Combine(_raiz, "observada");
        Directory.CreateDirectory(observada);
        CreadorPdfDePrueba.CrearConLineas(observada, "Emisor desconocido", "Otro documento");
        var carpetas = new CarpetasObservadasRepository();
        carpetas.Guardar([new(Guid.NewGuid(), "Facturas", observada, false, true)]);
        using var servicio = new ObservadorCarpetasService(carpetas);

        await servicio.RevisarAhoraAsync();

        Assert.Empty(carpetas.LeerActividad());
        Assert.Empty(new PendienteRepository().ObtenerTodos());
    }

    [Fact]
    public void Iniciar_AvisaSiUnaCarpetaConfiguradaNoExiste()
    {
        var carpetas = new CarpetasObservadasRepository();
        carpetas.Guardar([
            new(Guid.NewGuid(), "Desconectada", Path.Combine(_raiz, "ausente"), false, true),
        ]);
        using var servicio = new ObservadorCarpetasService(carpetas);
        string? aviso = null;
        servicio.ErrorVisible += mensaje => aviso = mensaje;

        servicio.Iniciar();

        Assert.Contains("no existe", aviso, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Imprime_solo_lo_que_llega_despues_de_agregar_la_carpeta()
    {
        string observada = Path.Combine(_raiz, "observada");
        Directory.CreateDirectory(observada);
        string antiguo = CreadorPdfDePrueba.CrearConLineas(observada, "Proveedor Uno", "Factura");
        File.SetLastWriteTime(antiguo, DateTime.Now.AddHours(-2));
        string nuevo = CreadorPdfDePrueba.CrearConLineas(
            observada,
            "Proveedor Uno",
            "Factura",
            "otra"
        );
        int id = new ConfiguracionDocumentoRepository().GuardarNueva(
            "Proveedor Uno",
            "Factura",
            _raiz,
            FormatoCarpeta.Directo,
            null,
            false,
            [Marca(CampoMarca.Emisor, 0), Marca(CampoMarca.Tipo, 1)]
        );
        new ConfiguracionImpresionRepository().Guardar(id, ModoImpresion.PrimeraPagina, null);
        var carpetas = new CarpetasObservadasRepository();
        carpetas.Guardar([
            new(Guid.NewGuid(), "Facturas", observada, false, true, DateTime.Now.AddHours(-1)),
        ]);
        var accion = new AccionQueCuenta();
        using var servicio = new ObservadorCarpetasService(
            carpetas,
            new ImpresionAlArchivarService(accion)
        );

        await servicio.RevisarAhoraAsync();
        await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);

        Assert.Equal(2, carpetas.LeerActividad().Count(a => a.Tipo == "Factura"));
        Assert.Equal(File.ReadAllBytes(nuevo), Assert.Single(accion.Impresos));
    }

    [Fact]
    public async Task Un_aviso_repetido_se_muestra_una_sola_vez()
    {
        var carpetas = new CarpetasObservadasRepository();
        carpetas.Guardar([
            new(Guid.NewGuid(), "Desconectada", Path.Combine(_raiz, "ausente"), false, true),
        ]);
        using var servicio = new ObservadorCarpetasService(carpetas);
        int avisos = 0;
        servicio.ErrorVisible += _ => avisos++;

        servicio.Iniciar();
        await Task.Delay(500);
        servicio.ActualizarCarpetas();
        await Task.Delay(500);

        Assert.Equal(1, avisos);
    }

    private sealed class AccionQueCuenta : IAccionImpresion
    {
        public List<byte[]> Impresos { get; } = [];
        public string ImpresoraPredeterminada => "Predeterminada";
        public IReadOnlyList<string> ImpresorasInstaladas => [];

        public void Imprimir(byte[] pdf, bool soloPrimeraPagina, string? impresora) =>
            Impresos.Add(pdf);

        public void AbrirVisor(string rutaPdf) { }
    }

    private static Marca Marca(CampoMarca campo, int indice)
    {
        var banda = CreadorPdfDePrueba.ObtenerBandaDeLinea(indice);
        return new(campo, 0, banda.X, banda.Y, banda.Ancho, banda.Alto);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HORMIGUERO_DATOS", _datosPrevios);
        BaseDeDatos.RutaArchivo = _baseAnterior;
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_raiz))
            Directory.Delete(_raiz, true);
    }
}
