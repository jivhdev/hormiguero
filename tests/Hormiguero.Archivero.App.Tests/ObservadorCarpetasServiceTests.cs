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
        CreadorPdfDePrueba.CrearConLineas(observada, "Factura", "Proveedor");
        string documentoNuevo = Directory
            .GetFiles(observada, "*.pdf")
            .Single(ruta => ruta != rutaCedible);
        File.Move(documentoNuevo, original);
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

    [Theory]
    [InlineData("FCV0000025378.pdf", "0000025378")]
    [InlineData("sin-numero.pdf", null)]
    public void Extrae_digitos_del_nombre(string nombre, string? esperado) =>
        Assert.Equal(esperado, ObservadorCarpetasService.NumeroDesdeNombre(nombre));

    [Theory]
    [InlineData("AAAA_AAAAMM", "2026\\202610", "2026\\202609")]
    [InlineData("AAAA_MM", "2026\\10", "2026\\09")]
    [InlineData("AAAA", "2026", "2025")]
    public void Calcula_periodo_actual_y_anterior(string formato, string actual, string anterior)
    {
        string basePrueba = Path.Combine(_raiz, "periodos");
        DateTime reloj = new(2026, 10, 2);
        var carpeta = new CarpetaObservadaExterna(
            Guid.NewGuid(),
            "Mes",
            basePrueba,
            false,
            true,
            SeguirPeriodo: true,
            FormatoPeriodo: formato
        );
        foreach (string ruta in new[] { actual, anterior })
            Directory.CreateDirectory(Path.Combine(basePrueba, ruta));

        Assert.Equal(
            new[] { Path.Combine(basePrueba, actual), Path.Combine(basePrueba, anterior) },
            PeriodosCarpetaObservada.Rutas(carpeta, reloj)
        );
        string nuevoPeriodo = formato switch
        {
            "AAAA" => Path.Combine(basePrueba, "2026"),
            "AAAA_MM" => Path.Combine(basePrueba, "2026", "11"),
            _ => Path.Combine(basePrueba, "2026", "202611"),
        };
        Directory.CreateDirectory(nuevoPeriodo);
        Assert.Equal(
            nuevoPeriodo,
            PeriodosCarpetaObservada.Rutas(carpeta, new DateTime(2026, 11, 2)).First()
        );
    }

    [Fact]
    public async Task ZonaDeIdentificacion_ignoraElPdfDeOtroClienteAntesDeReconocerlo()
    {
        string observada = Path.Combine(_raiz, "clientes");
        Directory.CreateDirectory(observada);
        string propio = CreadorPdfDePrueba.CrearConLineas(
            observada,
            "Empresa Uno",
            "Proveedor",
            "Factura"
        );
        string ajeno = CreadorPdfDePrueba.CrearConLineas(
            observada,
            "Empresa Dos",
            "Proveedor",
            "Factura"
        );
        File.Move(propio, Path.Combine(observada, "propio.pdf"));
        File.Move(ajeno, Path.Combine(observada, "ajeno.pdf"));
        var b0 = CreadorPdfDePrueba.ObtenerBandaDeLinea(0);
        var marcaIdentidad = new ZonaControlCarpeta(
            1,
            b0.X,
            b0.Y,
            b0.Ancho,
            b0.Alto,
            "Empresa Uno"
        );
        var idDocumento = new ConfiguracionDocumentoRepository().GuardarNueva(
            "Proveedor",
            "Factura",
            _raiz,
            FormatoCarpeta.Directo,
            null,
            false,
            [Marca(CampoMarca.Emisor, 1), Marca(CampoMarca.Tipo, 2)]
        );
        var carpetas = new CarpetasObservadasRepository();
        carpetas.Guardar([
            new(
                Guid.NewGuid(),
                "Documentos",
                observada,
                false,
                true,
                ZonaIdentificacion: marcaIdentidad,
                IdentificacionEsperada: "Empresa Uno",
                ConfiguracionesDocumentoIds: [idDocumento]
            ),
        ]);
        using var servicio = new ObservadorCarpetasService(carpetas);

        await servicio.RevisarAhoraAsync();
        await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);

        Assert.Single(carpetas.LeerActividad());
        Assert.Equal(Path.Combine(observada, "propio.pdf"), carpetas.LeerActividad()[0].Ruta);
    }

    [Fact]
    public async Task Cedible_se_ignora_si_hay_original_y_si_no_hay_se_publica_sin_imprimir()
    {
        string observada = Path.Combine(_raiz, "cedibles-por-numero");
        Directory.CreateDirectory(observada);
        int configuracionId = CrearConfiguracionConNumeroEnZona();
        var zonaPropia = Zona(0, "Empresa Uno");
        var zonaCedible = Zona(4, "CEDIBLE");
        var carpetas = new CarpetasObservadasRepository();
        var carpeta = new CarpetaObservadaExterna(
            Guid.NewGuid(),
            "Facturas",
            observada,
            false,
            true,
            DateTime.Now.AddHours(1),
            ZonaIdentificacion: zonaPropia,
            IdentificacionEsperada: "Empresa Uno",
            TieneCedibles: true,
            ZonaCedible: zonaCedible,
            CedibleEsperado: "CEDIBLE",
            ConfiguracionesDocumentoIds: [configuracionId]
        );
        carpetas.Guardar([carpeta]);
        string originalExistente = CrearFactura(observada, "12345", false);
        using (var servicio = new ObservadorCarpetasService(carpetas))
            await servicio.RevisarAhoraAsync();
        await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);

        string cedibleConOriginal = CrearFactura(observada, "12345", true);
        var configuracion = Assert.Single(
            new ConfiguracionDocumentoRepository().ObtenerTodasConPatronesParaObservador(),
            c => c.Id == configuracionId
        );
        Assert.NotNull(
            CoincidenciaAutomaticaService.BuscarConfiguracionQueCoincide(
                cedibleConOriginal,
                [configuracion]
            )
        );
        Assert.Contains(
            PublicadorDatosDocumentoService.ExtraerValoresObservados(
                cedibleConOriginal,
                configuracion
            ),
            valor =>
                valor.DatoDiccionarioId == "factura_proveedor" && valor.ValorOriginal == "12345"
        );
        using (var servicio = new ObservadorCarpetasService(carpetas))
            await servicio.RevisarAhoraAsync();
        await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);
        Assert.DoesNotContain(carpetas.LeerActividad(), d => d.Ruta == cedibleConOriginal);
        Assert.Equal(1, ContarPublicaciones());

        string cedibleSinOriginal = CrearFactura(observada, "67890", true);
        var impresion = new AccionQueCuenta();
        carpetas.Guardar([carpeta with { Agregada = DateTime.Now.AddHours(1) }]);
        using (
            var servicio = new ObservadorCarpetasService(
                carpetas,
                new ImpresionAlArchivarService(impresion)
            )
        )
            await servicio.RevisarAhoraAsync();
        await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);
        Assert.Equal("procesado", carpetas.ResultadoRevisado(cedibleSinOriginal));
        Assert.DoesNotContain(carpetas.LeerActividad(), d => d.Ruta == cedibleSinOriginal);
        Assert.Empty(impresion.Impresos);
        Assert.Equal(2, ContarPublicaciones());

        string originalPosterior = CrearFactura(observada, "67890", false);
        carpetas.Guardar([
            carpeta with
            {
                Agregada = DateTime.Now.AddMinutes(-1),
                AccionAlLlegar = "ImprimirPrimeraPagina",
            },
        ]);
        using (
            var servicio = new ObservadorCarpetasService(
                carpetas,
                new ImpresionAlArchivarService(impresion)
            )
        )
            await servicio.RevisarAhoraAsync();
        await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);
        Assert.Contains(carpetas.LeerActividad(), d => d.Ruta == originalPosterior);
        Assert.Single(impresion.Impresos);
        Assert.Equal(3, ContarPublicaciones());
        Assert.True(File.Exists(originalExistente));
    }

    [Fact]
    public async Task Solo_publica_los_documentos_de_la_empresa_y_sus_originales()
    {
        string observada = Path.Combine(_raiz, "solo-la-empresa");
        Directory.CreateDirectory(observada);
        int configuracionId = CrearConfiguracionConNumeroEnZona();
        var carpeta = new CarpetaObservadaExterna(
            Guid.NewGuid(),
            "Facturas",
            observada,
            false,
            true,
            DateTime.Now.AddHours(1),
            ZonaIdentificacion: Zona(0, "Empresa Uno"),
            IdentificacionEsperada: "Empresa Uno",
            TieneCedibles: true,
            ZonaCedible: Zona(4, "CEDIBLE"),
            CedibleEsperado: "CEDIBLE",
            ConfiguracionesDocumentoIds: [configuracionId]
        );
        var carpetas = new CarpetasObservadasRepository();
        carpetas.Guardar([carpeta]);
        string[] propios = Enumerable
            .Range(0, 3)
            .Select(i =>
                CrearFacturaConCliente(observada, "Empresa Uno", (100 + i).ToString(), false)
            )
            .ToArray();
        string[] ajenos = Enumerable
            .Range(0, 3)
            .Select(i =>
                CrearFacturaConCliente(observada, "Empresa Dos", (200 + i).ToString(), false)
            )
            .ToArray();
        string cediblePropio = CrearFacturaConCliente(observada, "Empresa Uno", "100", true);
        string cedibleAjeno = CrearFacturaConCliente(observada, "Empresa Dos", "200", true);

        using (var servicio = new ObservadorCarpetasService(carpetas))
            await servicio.RevisarAhoraAsync();
        await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);

        Assert.Equal(3, ContarPublicaciones());
        Assert.Equal(3, carpetas.LeerActividad().Count);
        Assert.All(propios, ruta => Assert.Contains(carpetas.LeerActividad(), d => d.Ruta == ruta));
        Assert.All(
            ajenos,
            ruta => Assert.DoesNotContain(carpetas.LeerActividad(), d => d.Ruta == ruta)
        );
        Assert.DoesNotContain(carpetas.LeerActividad(), d => d.Ruta == cediblePropio);
        Assert.DoesNotContain(carpetas.LeerActividad(), d => d.Ruta == cedibleAjeno);
        Assert.Equal("cedible", carpetas.ResultadoRevisado(cediblePropio));
        Assert.Equal("ajeno", carpetas.ResultadoRevisado(cedibleAjeno));
    }

    [Fact]
    public async Task Revision_de_respaldo_y_servicio_recreado_no_abren_archivos_ya_vistos()
    {
        string observada = Path.Combine(_raiz, "sin-relectura");
        Directory.CreateDirectory(observada);
        string pdf = CreadorPdfDePrueba.CrearConLineas(observada, "Proveedor", "Factura");
        new ConfiguracionDocumentoRepository().GuardarNueva(
            "Proveedor",
            "Factura",
            _raiz,
            FormatoCarpeta.Directo,
            null,
            false,
            [Marca(CampoMarca.Emisor, 0), Marca(CampoMarca.Tipo, 1)]
        );
        var carpetas = new CarpetasObservadasRepository();
        carpetas.Guardar([new(Guid.NewGuid(), "Facturas", observada, false, true)]);
        int lecturasIniciales = 0;
        using (var servicio = new ObservadorCarpetasService(carpetas))
        {
            servicio.PdfAbiertoParaProcesar += _ => lecturasIniciales++;
            await servicio.RevisarAhoraAsync();
            await servicio.RevisarAhoraAsync();
        }

        int lecturasTrasRecrear = 0;
        using (var servicio = new ObservadorCarpetasService(carpetas))
        {
            servicio.PdfAbiertoParaProcesar += _ => lecturasTrasRecrear++;
            await servicio.RevisarAhoraAsync();
        }

        Assert.Equal(1, lecturasIniciales);
        Assert.Equal(0, lecturasTrasRecrear);
        Assert.True(File.Exists(pdf));
    }

    [Fact]
    public async Task Evento_nuevo_se_atiende_antes_del_siguiente_archivo_del_barrido()
    {
        string observada = Path.Combine(_raiz, "prioridad-nuevos");
        Directory.CreateDirectory(observada);
        string[] iniciales = Enumerable
            .Range(0, 3)
            .Select(_ => CreadorPdfDePrueba.CrearConLineas(observada, "Proveedor", "Factura"))
            .ToArray();
        new ConfiguracionDocumentoRepository().GuardarNueva(
            "Proveedor",
            "Factura",
            _raiz,
            FormatoCarpeta.Directo,
            null,
            false,
            [Marca(CampoMarca.Emisor, 0), Marca(CampoMarca.Tipo, 1)]
        );
        var carpetas = new CarpetasObservadasRepository();
        carpetas.Guardar([
            new(Guid.NewGuid(), "Facturas", observada, false, true, DateTime.Now.AddHours(1)),
        ]);
        var orden = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var completos = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        int pausaInicial = 0;
        int documentosActualizados = 0;
        string? nuevo = null;

        async Task Pausa(TimeSpan _)
        {
            if (Interlocked.Exchange(ref pausaInicial, 1) == 0)
            {
                nuevo = CreadorPdfDePrueba.CrearConLineas(observada, "Proveedor", "Factura");
                await Task.Delay(300);
            }
        }

        using var servicio = new ObservadorCarpetasService(carpetas, pausa: Pausa);
        servicio.DocumentoActualizado += documento =>
        {
            if (documento.Tipo != "Factura")
                return;
            orden.Enqueue(documento.Ruta);
            if (Interlocked.Increment(ref documentosActualizados) == 4)
                completos.TrySetResult();
        };
        servicio.Iniciar();
        await completos.Task.WaitAsync(TimeSpan.FromSeconds(25));

        string[] procesados = orden.ToArray();
        Assert.NotNull(nuevo);
        int posicionNuevo = Array.IndexOf(procesados, nuevo);
        Assert.True(posicionNuevo >= 0);
        Assert.Equal(2, iniciales.Count(ruta => Array.IndexOf(procesados, ruta) > posicionNuevo));
    }

    [Fact]
    public void Periodo_no_incluye_el_mes_anterior_despues_del_dia_cinco()
    {
        string basePrueba = Path.Combine(_raiz, "periodos-dia-seis");
        Directory.CreateDirectory(Path.Combine(basePrueba, "2026", "202610"));
        Directory.CreateDirectory(Path.Combine(basePrueba, "2026", "202609"));
        var carpeta = new CarpetaObservadaExterna(
            Guid.NewGuid(),
            "Mes",
            basePrueba,
            false,
            true,
            SeguirPeriodo: true,
            FormatoPeriodo: "AAAA_AAAAMM"
        );

        Assert.Equal(
            [Path.Combine(basePrueba, "2026", "202610")],
            PeriodosCarpetaObservada.Rutas(carpeta, new DateTime(2026, 10, 6))
        );
    }

    [Theory]
    [InlineData("2026\\202610", "AAAA_AAAAMM")]
    [InlineData("2026\\10", "AAAA_MM")]
    [InlineData("202610", "AAAAMM")]
    [InlineData("2026", "AAAA")]
    public void Detecta_ruta_de_periodo(string sufijo, string formato)
    {
        string ruta = Path.Combine(_raiz, "documentos", sufijo);
        var detectada = PeriodosCarpetaObservada.Detectar(ruta);

        Assert.NotNull(detectada);
        Assert.Equal(formato, detectada.Value.Formato);
        Assert.Equal(Path.Combine(_raiz, "documentos"), detectada.Value.RutaBase);
    }

    [Fact]
    public async Task TipoPorCarpeta_publica_dato_del_nombre_y_persiste_atencion()
    {
        string observada = Path.Combine(_raiz, "fijos");
        Directory.CreateDirectory(observada);
        string pdf = CreadorPdfDePrueba.CrearConLineas(observada, "Contenido", "Factura");
        string ruta = Path.Combine(observada, "FCV0000025378.pdf");
        File.Move(pdf, ruta);
        var carpetas = new CarpetasObservadasRepository();
        carpetas.Guardar([
            new(
                Guid.NewGuid(),
                "Facturas",
                observada,
                false,
                true,
                DateTime.Now.AddMinutes(-1),
                "TipoPorCarpeta",
                "factura_propia",
                "Proveedor Uno",
                AccionAlLlegar: "Avisar"
            ),
        ]);
        using var servicio = new ObservadorCarpetasService(carpetas);

        await servicio.RevisarAhoraAsync();
        await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);

        var actividad = Assert.Single(carpetas.LeerActividad());
        Assert.Equal("Factura propia", actividad.Tipo);
        Assert.Equal("Proveedor Uno", actividad.Emisor);
        var pendiente = Assert.Single(carpetas.LeerPorAtender());
        Assert.Equal("0000025378", pendiente.Numero);
        carpetas.MarcarListo(pendiente.Id);
        Assert.Empty(new CarpetasObservadasRepository().LeerPorAtender());
    }

    [Fact]
    public async Task TipoPorCarpeta_sin_digitos_registra_motivo_y_no_publica()
    {
        string observada = Path.Combine(_raiz, "sin-numero");
        Directory.CreateDirectory(observada);
        string pdf = CreadorPdfDePrueba.CrearConLineas(observada, "Contenido", "Factura");
        string ruta = Path.Combine(observada, "sin-numero.pdf");
        File.Move(pdf, ruta);
        var carpetas = new CarpetasObservadasRepository();
        carpetas.Guardar([
            new(
                Guid.NewGuid(),
                "Facturas",
                observada,
                false,
                true,
                DateTime.Now.AddMinutes(-1),
                "TipoPorCarpeta",
                "factura_propia",
                "Proveedor Uno"
            ),
        ]);
        using var servicio = new ObservadorCarpetasService(carpetas);

        await servicio.RevisarAhoraAsync();

        Assert.Equal("Sin número en el nombre", Assert.Single(carpetas.LeerActividad()).Motivo);
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM auditoria WHERE accion='publicar_documento';";
        Assert.Equal(0L, (long)comando.ExecuteScalar()!);
    }

    [Fact]
    public async Task TipoPorCarpeta_publica_numeros_repetidos_y_lo_anota()
    {
        string observada = Path.Combine(_raiz, "repetidos");
        Directory.CreateDirectory(observada);
        string primero = CreadorPdfDePrueba.CrearConLineas(observada, "Uno", "Factura");
        string segundo = CreadorPdfDePrueba.CrearConLineas(observada, "Dos", "Factura");
        File.Move(primero, Path.Combine(observada, "FCV0000025378.pdf"));
        File.Move(segundo, Path.Combine(observada, "copia-25378.pdf"));
        var carpetas = new CarpetasObservadasRepository();
        carpetas.Guardar([
            new(
                Guid.NewGuid(),
                "Facturas",
                observada,
                false,
                true,
                DateTime.Now.AddMinutes(-1),
                "TipoPorCarpeta",
                "factura_propia",
                "Proveedor Uno"
            ),
        ]);
        using var servicio = new ObservadorCarpetasService(carpetas);

        await servicio.RevisarAhoraAsync();
        await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);

        Assert.Equal(2, carpetas.LeerActividad().Count(a => a.Tipo == "Factura propia"));
        Assert.Contains(carpetas.LeerActividad(), a => a.Motivo == "Número repetido");
    }

    [Fact]
    public async Task Imprimir_omite_una_copia_cedible()
    {
        string observada = Path.Combine(_raiz, "impresion-cedible");
        Directory.CreateDirectory(observada);
        string original = CreadorPdfDePrueba.CrearConLineas(observada, "Contenido", "Factura");
        string cedible = CreadorPdfDePrueba.CrearConLineas(observada, "Contenido", "Factura");
        File.Move(original, Path.Combine(observada, "FCV123.pdf"));
        File.Move(cedible, Path.Combine(observada, "FCV123_CEDIBLE.pdf"));
        var carpetas = new CarpetasObservadasRepository();
        carpetas.Guardar([
            new(
                Guid.NewGuid(),
                "Facturas",
                observada,
                false,
                true,
                DateTime.Now.AddMinutes(-1),
                "TipoPorCarpeta",
                "factura_propia",
                "Proveedor Uno",
                AccionAlLlegar: "ImprimirPrimeraPagina"
            ),
        ]);
        var accion = new AccionQueCuenta();
        using var servicio = new ObservadorCarpetasService(
            carpetas,
            new ImpresionAlArchivarService(accion)
        );

        await servicio.RevisarAhoraAsync();
        await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);

        Assert.Single(accion.Impresos);
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

    private int CrearConfiguracionConNumeroEnZona()
    {
        int id = new ConfiguracionDocumentoRepository().GuardarNueva(
            "Proveedor",
            "Factura del proveedor",
            _raiz,
            FormatoCarpeta.Directo,
            null,
            false,
            [Marca(CampoMarca.Emisor, 1), Marca(CampoMarca.Tipo, 2)]
        );
        var numero = Assert.Single(
            DiccionarioDatosEnlazantes.Todos,
            d => d.Id == "factura_proveedor"
        );
        var banda = CreadorPdfDePrueba.ObtenerBandaDeLinea(3);
        DatosEnlazantesConfiguracionService.Guardar(
            "Proveedor",
            "Factura del proveedor",
            "Recibido",
            "Factura del proveedor · Proveedor",
            id,
            [
                new(
                    numero.Id,
                    numero.Nombre,
                    numero.Grupo,
                    true,
                    true,
                    0,
                    banda.X,
                    banda.Y,
                    banda.Ancho,
                    banda.Alto,
                    "12345",
                    true,
                    true
                ),
            ]
        );
        Assert.Contains(
            DatosEnlazantesConfiguracionService.Leer("Proveedor", "Factura del proveedor", id),
            dato => dato.Id == numero.Id && dato.DefineTipo && dato.Marcado
        );
        return id;
    }

    private static ZonaControlCarpeta Zona(int indice, string esperado)
    {
        var banda = CreadorPdfDePrueba.ObtenerBandaDeLinea(indice);
        return new(1, banda.X, banda.Y, banda.Ancho, banda.Alto, esperado);
    }

    private static string CrearFactura(string carpeta, string numero, bool cedible) =>
        CrearFacturaConCliente(carpeta, "Empresa Uno", numero, cedible);

    private static string CrearFacturaConCliente(
        string carpeta,
        string cliente,
        string numero,
        bool cedible
    )
    {
        string creado = CreadorPdfDePrueba.CrearConLineas(
            carpeta,
            cliente,
            "Proveedor",
            "Factura del proveedor",
            numero,
            cedible ? "CEDIBLE" : "ORIGINAL"
        );
        if (!cedible)
            return creado;
        string destino = Path.Combine(carpeta, $"factura_{numero}_CEDIBLE.pdf");
        File.Move(creado, destino);
        return destino;
    }

    private int ContarPublicaciones()
    {
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM auditoria WHERE accion='publicar_documento';";
        return Convert.ToInt32(comando.ExecuteScalar());
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
