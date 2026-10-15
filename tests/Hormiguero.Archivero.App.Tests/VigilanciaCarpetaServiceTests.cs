using Archivero.Datos;
using Archivero.Servicios;
using Archivero.Servicios.Pdf;

namespace Archivero.Tests;

public class VigilanciaCarpetaServiceTests : IDisposable
{
    private readonly string _rutaDbTemporal;
    private readonly string _rutaLogOriginal;

    public VigilanciaCarpetaServiceTests()
    {
        _rutaDbTemporal = Path.Combine(
            Path.GetTempPath(),
            $"archivero-tests-{Guid.NewGuid():N}.db"
        );
        BaseDeDatos.RutaArchivo = _rutaDbTemporal;
        BaseDeDatos.AsegurarEsquema();

        // Caso-9, mejora 2: redirigir el log de auditoria para no escribir en el real del
        // usuario al correr los tests.
        _rutaLogOriginal = AuditoriaService.RutaLog;
        AuditoriaService.RutaLog = Path.Combine(
            Path.GetTempPath(),
            $"archivero-tests-{Guid.NewGuid():N}",
            "auditoria.log"
        );
    }

    [Fact]
    public void Iniciar_ConLaCarpetaObservadaInexistente_AvisaEnVezDeQuedarseCallado()
    {
        // Bug real reportado por Javier: la carpeta observada se borró del disco por fuera de
        // Archivero, y al reabrirlo no avisaba nada -- se quedaba observando en silencio total,
        // sin importar qué archivos se dejaran ahí. REQ-005 pide avisar, no fallar en silencio.
        var carpetaQueNoExiste = Path.Combine(
            Path.GetTempPath(),
            "archivero-tests-inexistente-" + Guid.NewGuid().ToString("N")
        );
        using var vigilancia = new VigilanciaCarpetaService(carpetaQueNoExiste);

        var avisoDisparado = false;
        vigilancia.CarpetaObservadaNoDisponible += () => avisoDisparado = true;

        vigilancia.Iniciar();

        Assert.True(avisoDisparado);
    }

    [Fact]
    public void Iniciar_ConLaCarpetaObservadaExistente_NoAvisa()
    {
        var carpeta = Path.Combine(
            Path.GetTempPath(),
            "archivero-tests-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(carpeta);

        try
        {
            using var vigilancia = new VigilanciaCarpetaService(carpeta);

            var avisoDisparado = false;
            vigilancia.CarpetaObservadaNoDisponible += () => avisoDisparado = true;

            vigilancia.Iniciar();

            Assert.False(avisoDisparado);
        }
        finally
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }

    [Fact]
    public void Manejador_Deleted_registra_el_error_y_continua()
    {
        var fallo = new IOException("fallo simulado en Deleted");
        VigilanciaCarpetaService.EjecutarManejadorProtegido(
            "Deleted",
            "archivo.pdf",
            () => throw fallo
        );

        Assert.Contains("ERROR_MANEJADOR_VIGILANTE", File.ReadAllText(AuditoriaService.RutaLog));
        Assert.Contains(fallo.Message, File.ReadAllText(AuditoriaService.RutaLog));
    }

    [Fact]
    public void Duplicado_sin_texto_se_puede_comparar_y_guardar_ambos()
    {
        var (observada, destino) = CrearCarpetas();
        var rutaNueva = CreadorPdfDePrueba.CrearSoloImagen(observada, "1190036 (1).pdf");
        var rutaDestino = CreadorPdfDePrueba.CrearSoloImagen(destino, "1190036.pdf");
        var pendientes = new PendienteRepository();
        using var vigilancia = new VigilanciaCarpetaService(observada);

        Assert.False(LectorPdf.TieneTextoExtraible(rutaNueva));
        Assert.False(LectorPdf.TieneTextoExtraible(rutaDestino));
        vigilancia.Iniciar();
        Assert.Equal(
            MotivoPendiente.SinTextoExtraible,
            Assert.Single(pendientes.ObtenerTodos()).Motivo
        );

        Assert.Throws<ArchivoDuplicadoException>(() =>
            ClasificadorService.Clasificar(
                rutaNueva,
                new ConfiguracionDocumento
                {
                    Emisor = "(sin texto)",
                    Tipo = "(sin texto)",
                    CarpetaDestino = destino,
                    FormatoCarpeta = FormatoCarpeta.Directo,
                    Renombrar = true,
                    Patrones = [],
                },
                null,
                "1190036"
            )
        );
        pendientes.Agregar(rutaNueva, MotivoPendiente.Duplicado);
        pendientes.GuardarDestinoDuplicado(rutaNueva, rutaDestino);
        Assert.Equal(rutaDestino, pendientes.ObtenerDestinoDuplicado(rutaNueva));

        Assert.NotEmpty(LectorPdf.RenderizarPagina(rutaNueva, 0).PixelesBgra);
        Assert.NotEmpty(LectorPdf.RenderizarPagina(rutaDestino, 0).PixelesBgra);
        var rutaAmbos = ClasificadorService.GuardarAmbos(rutaNueva, rutaDestino);

        Assert.True(File.Exists(rutaDestino));
        Assert.True(File.Exists(rutaAmbos));
        Assert.False(File.Exists(rutaNueva));
        pendientes.Quitar(rutaNueva);
        Assert.Empty(pendientes.ObtenerTodos());
        Assert.Null(pendientes.ObtenerDestinoDuplicado(rutaNueva));
    }

    [Fact]
    public async Task Dos_archivos_casi_simultaneos_con_el_mismo_destino_quedan_como_duplicado()
    {
        var (observada, destino) = CrearCarpetas();
        var primera = CreadorPdfDePrueba.CrearSoloImagen(observada, "1190036.pdf");
        var segunda = CreadorPdfDePrueba.CrearSoloImagen(observada, "1190036 (1).pdf");
        var configuracion = new ConfiguracionDocumento
        {
            Emisor = "(sin texto)",
            Tipo = "(sin texto)",
            CarpetaDestino = destino,
            FormatoCarpeta = FormatoCarpeta.Directo,
            Renombrar = true,
            Patrones = [],
        };

        var resultados = await Task.WhenAll(
            Task.Run(() => ClasificarConResultado(primera, configuracion)),
            Task.Run(() => ClasificarConResultado(segunda, configuracion))
        );

        Assert.Single(resultados, resultado => resultado is null);
        Assert.Single(resultados, resultado => resultado is ArchivoDuplicadoException);
        Assert.True(File.Exists(Path.Combine(destino, "1190036.pdf")));
        Assert.Single(Directory.GetFiles(observada, "*.pdf"));
    }

    private static Exception? ClasificarConResultado(
        string ruta,
        ConfiguracionDocumento configuracion
    )
    {
        try
        {
            ClasificadorService.Clasificar(ruta, configuracion, null, "1190036");
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    // ----- Caso-11, punto 3: pendientes viejos se reclasifican con una configuración nueva -----

    private static Marca MarcaDeLinea(CampoMarca campo, int indiceLinea)
    {
        var banda = CreadorPdfDePrueba.ObtenerBandaDeLinea(indiceLinea);
        return new Marca(campo, 0, banda.X, banda.Y, banda.Ancho, banda.Alto);
    }

    private static (string Observada, string Destino) CrearCarpetas()
    {
        var raiz = Path.Combine(
            Path.GetTempPath(),
            "archivero-tests-" + Guid.NewGuid().ToString("N")
        );
        var observada = Path.Combine(raiz, "observada");
        var destino = Path.Combine(raiz, "destino");
        Directory.CreateDirectory(observada);
        Directory.CreateDirectory(destino);
        return (observada, destino);
    }

    [Fact]
    public void ReprocesarPendientes_ConUnPendienteQueAhoraCoincide_LoGuardaSoloYLoSacaDePendientes()
    {
        // Bug real de Javier: un documento ya estaba en "pendientes por reconocer" cuando se
        // creó la configuración de su mismo Emisor+Tipo, y se quedó ahí -- tuvo que sacarlo y
        // volverlo a meter en la carpeta para que Archivero lo procesara.
        var (observada, destino) = CrearCarpetas();
        var ruta = CreadorPdfDePrueba.CrearConLineas(
            observada,
            "Banco de Prueba SA",
            "Resumen de cuenta"
        );
        var pendientes = new PendienteRepository();
        pendientes.Agregar(ruta, MotivoPendiente.NuevoDocumento);

        new ConfiguracionDocumentoRepository().GuardarNueva(
            "Banco de Prueba SA",
            "Resumen de cuenta",
            destino,
            FormatoCarpeta.Directo,
            null,
            false,
            [MarcaDeLinea(CampoMarca.Emisor, 0), MarcaDeLinea(CampoMarca.Tipo, 1)]
        );

        using var vigilancia = new VigilanciaCarpetaService(observada);
        string? rutaGuardada = null;
        vigilancia.ArchivoGuardadoAutomaticamente += (_, final) => rutaGuardada = final;

        vigilancia.ReprocesarPendientes();

        Assert.False(File.Exists(ruta));
        Assert.NotNull(rutaGuardada);
        Assert.True(File.Exists(rutaGuardada));
        Assert.Empty(pendientes.ObtenerTodos());
    }

    [Fact]
    public void ReprocesarPendientes_ConUnPendienteQueSigueSinCoincidir_LoDejaPendienteYNoLoToca()
    {
        var (observada, _) = CrearCarpetas();
        var ruta = CreadorPdfDePrueba.CrearConLineas(observada, "Otro Proveedor", "Otro Tipo");
        var pendientes = new PendienteRepository();
        pendientes.Agregar(ruta, MotivoPendiente.NuevoDocumento);

        using var vigilancia = new VigilanciaCarpetaService(observada);
        vigilancia.ReprocesarPendientes();

        Assert.True(File.Exists(ruta));
        Assert.Single(pendientes.ObtenerTodos());
    }

    [Fact]
    public void ReprocesarPendientes_ConUnPendienteCuyoArchivoYaNoExiste_LoSacaDeLaLista()
    {
        var (observada, _) = CrearCarpetas();
        var pendientes = new PendienteRepository();
        pendientes.Agregar(
            Path.Combine(observada, "ya-no-esta.pdf"),
            MotivoPendiente.NuevoDocumento
        );

        using var vigilancia = new VigilanciaCarpetaService(observada);
        vigilancia.ReprocesarPendientes();

        Assert.Empty(pendientes.ObtenerTodos());
    }

    // ----- Caso-11, punto 5: archivos dañados visibles, nunca ignorados en silencio -----

    [Theory]
    [InlineData("esto no es un pdf")]
    [InlineData("")]
    [InlineData("%PDF-1.4\n1 0 obj << /Type /Catalog")]
    public void Iniciar_ConUnPdfDanado_LoDejaEnPendientesComoArchivoDanadoSinTocarlo(
        string contenido
    )
    {
        // Antes: RNF-2 descartaba el archivo en silencio -- quedaba invisible para el usuario,
        // igual que los "documentos fantasma" de Caso-1.
        var (observada, _) = CrearCarpetas();
        var ruta = Path.Combine(observada, "danado.pdf");
        File.WriteAllText(ruta, contenido);

        using var vigilancia = new VigilanciaCarpetaService(observada);
        vigilancia.Iniciar();

        var pendiente = Assert.Single(new PendienteRepository().ObtenerTodos());
        Assert.Equal(ruta, pendiente.RutaArchivo);
        Assert.Equal(MotivoPendiente.ArchivoDanado, pendiente.Motivo);
        Assert.Equal(contenido, File.ReadAllText(ruta));
    }

    // ----- Caso-11, punto 1: "Preguntar el nombre cada vez" -----

    [Fact]
    public void Iniciar_ConUnaConfiguracionQuePreguntaElNombre_LoDejaPendienteDeConfirmarYAvisaSinTocarlo()
    {
        var (observada, destino) = CrearCarpetas();
        var ruta = CreadorPdfDePrueba.CrearConLineas(
            observada,
            "Banco de Prueba SA",
            "Resumen de cuenta"
        );

        new ConfiguracionDocumentoRepository().GuardarNueva(
            "Banco de Prueba SA",
            "Resumen de cuenta",
            destino,
            FormatoCarpeta.Directo,
            null,
            false,
            [MarcaDeLinea(CampoMarca.Emisor, 0), MarcaDeLinea(CampoMarca.Tipo, 1)],
            preguntarNombre: true
        );

        using var vigilancia = new VigilanciaCarpetaService(observada);
        var avisos = 0;
        vigilancia.ArchivoRequiereAtencion += (_, _) => avisos++;

        vigilancia.Iniciar();

        Assert.True(File.Exists(ruta));
        Assert.Empty(Directory.GetFiles(destino));
        Assert.Equal(
            MotivoPendiente.NombrePorConfirmar,
            Assert.Single(new PendienteRepository().ObtenerTodos()).Motivo
        );
        Assert.Equal(1, avisos);
    }

    [Fact]
    public void ReprocesarPendientes_ConUnaConfiguracionQuePreguntaElNombre_CambiaElMotivoSinTocarElArchivo()
    {
        var (observada, destino) = CrearCarpetas();
        var ruta = CreadorPdfDePrueba.CrearConLineas(
            observada,
            "Banco de Prueba SA",
            "Resumen de cuenta"
        );
        var pendientes = new PendienteRepository();
        pendientes.Agregar(ruta, MotivoPendiente.NuevoDocumento);

        new ConfiguracionDocumentoRepository().GuardarNueva(
            "Banco de Prueba SA",
            "Resumen de cuenta",
            destino,
            FormatoCarpeta.Directo,
            null,
            false,
            [MarcaDeLinea(CampoMarca.Emisor, 0), MarcaDeLinea(CampoMarca.Tipo, 1)],
            preguntarNombre: true
        );

        using var vigilancia = new VigilanciaCarpetaService(observada);
        vigilancia.ReprocesarPendientes();

        Assert.True(File.Exists(ruta));
        Assert.Empty(Directory.GetFiles(destino));
        Assert.Equal(
            MotivoPendiente.NombrePorConfirmar,
            Assert.Single(pendientes.ObtenerTodos()).Motivo
        );
    }

    public void Dispose()
    {
        AuditoriaService.RutaLog = _rutaLogOriginal;
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (var intento = 0; intento < 10 && File.Exists(_rutaDbTemporal); intento++)
        {
            try
            {
                File.Delete(_rutaDbTemporal);
            }
            catch (IOException) when (intento < 9)
            {
                Thread.Sleep(100);
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            }
        }
    }
}
