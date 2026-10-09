using System.IO;
using Archivero.Datos;
using Archivero.Servicios.Pdf;

namespace Archivero.Servicios;

public enum ResultadoGuardadoAutomatico
{
    Guardado,
    ValorInvalido,
    Duplicado,
    CarpetaNoDisponible,
    PeriodoNuevo,
    ValidacionFallida,
    NombrePorConfirmar,
}

public record ResultadoProcesamiento(
    ResultadoGuardadoAutomatico Resultado,
    string? RutaFinal = null,
    string? Detalle = null,
    MotivoPendiente? MotivoValidacion = null
);

public record CamposExtraidos(DateTime? Fecha, string? NombreExtraido);

public static class GuardadoAutomaticoService
{
    /// <summary>
    /// Extrae Fecha y/o Nombre de archivo (según lo que pida la configuración) usando el
    /// patrón que ya coincidió, y valida que tengan una forma válida. Se expone aparte de
    /// <see cref="Procesar"/> para poder recalcularlos al reabrir un pendiente por duplicado
    /// (REQ-002), sin tener que guardar esos valores en la base.
    /// </summary>
    public static (CamposExtraidos? Campos, string? Error) ExtraerCamposParaClasificar(
        string rutaArchivo,
        ConfiguracionDocumento configuracionConPatronCoincidente
    )
    {
        var marcas = configuracionConPatronCoincidente.Patrones.Single().Marcas;

        DateTime? fecha = null;
        if (configuracionConPatronCoincidente.FormatoCarpeta != FormatoCarpeta.Directo)
        {
            var marcaFecha = marcas.FirstOrDefault(m => m.Campo == CampoMarca.Fecha);
            if (marcaFecha is null)
            {
                return (null, "El patrón no tiene marca de Fecha.");
            }

            var textoFecha = LectorPdf.ExtraerTexto(
                rutaArchivo,
                marcaFecha.Pagina,
                ARect(marcaFecha)
            );
            if (!FechaExtraidaService.TryParsear(textoFecha, out var fechaParseada))
            {
                return (null, $"Fecha extraída inválida: \"{textoFecha}\".");
            }

            fecha = fechaParseada;
        }

        string? nombreExtraido = null;
        if (configuracionConPatronCoincidente.Renombrar)
        {
            var marcaNombre = marcas.FirstOrDefault(m => m.Campo == CampoMarca.NombreArchivo);
            if (marcaNombre is null)
            {
                return (null, "El patrón no tiene marca de Nombre de archivo.");
            }

            nombreExtraido = LectorPdf.ExtraerTexto(
                rutaArchivo,
                marcaNombre.Pagina,
                ARect(marcaNombre)
            );
            if (string.IsNullOrWhiteSpace(nombreExtraido))
            {
                return (null, "No se pudo extraer un nombre de archivo válido.");
            }
        }

        return (new CamposExtraidos(fecha, nombreExtraido), null);
    }

    public static ResultadoProcesamiento Procesar(
        string rutaArchivo,
        ConfiguracionDocumento configuracionConPatronCoincidente,
        ImpresionAlArchivarService? impresion = null
    )
    {
        var (campos, error) = ExtraerCamposParaClasificar(
            rutaArchivo,
            configuracionConPatronCoincidente
        );
        if (campos is null)
        {
            return new ResultadoProcesamiento(
                ResultadoGuardadoAutomatico.ValorInvalido,
                Detalle: error
            );
        }

        if (FaltaProveedorLegible(rutaArchivo, configuracionConPatronCoincidente))
            return new ResultadoProcesamiento(
                ResultadoGuardadoAutomatico.ValidacionFallida,
                Detalle: "Falta el proveedor",
                MotivoValidacion: MotivoPendiente.FaltaProveedor
            );

        // Caso-11, punto 1: se pregunta antes que el período nuevo, para que el nombre confirmado
        // llegue también a la pantalla de crear período (si no, guardaría con el nombre original).
        if (configuracionConPatronCoincidente.PreguntarNombre)
        {
            return new ResultadoProcesamiento(ResultadoGuardadoAutomatico.NombrePorConfirmar);
        }

        return Guardar(rutaArchivo, configuracionConPatronCoincidente, campos, impresion);
    }

    /// <summary>
    /// Caso-11, punto 1: termina el guardado automático de un documento cuya configuración pide
    /// el nombre cada vez, con el nombre que el usuario escribió o confirmó. Carpeta, período,
    /// duplicados y validaciones son los mismos de siempre.
    /// </summary>
    public static ResultadoProcesamiento GuardarConNombreConfirmado(
        string rutaArchivo,
        ConfiguracionDocumento configuracionConPatronCoincidente,
        string nombreConfirmado,
        ImpresionAlArchivarService? impresion = null
    )
    {
        var (campos, error) = ExtraerCamposParaClasificar(
            rutaArchivo,
            configuracionConPatronCoincidente with
            {
                Renombrar = false,
            }
        );
        if (campos is null)
        {
            return new ResultadoProcesamiento(
                ResultadoGuardadoAutomatico.ValorInvalido,
                Detalle: error
            );
        }
        if (FaltaProveedorLegible(rutaArchivo, configuracionConPatronCoincidente))
            return new ResultadoProcesamiento(
                ResultadoGuardadoAutomatico.ValidacionFallida,
                Detalle: "Falta el proveedor",
                MotivoValidacion: MotivoPendiente.FaltaProveedor
            );

        var configuracionConNombre = configuracionConPatronCoincidente with
        {
            Renombrar = true,
            PreguntarNombre = false,
        };
        return Guardar(
            rutaArchivo,
            configuracionConNombre,
            campos with
            {
                NombreExtraido = nombreConfirmado,
            },
            impresion
        );
    }

    private static ResultadoProcesamiento Guardar(
        string rutaArchivo,
        ConfiguracionDocumento configuracionConPatronCoincidente,
        CamposExtraidos campos,
        ImpresionAlArchivarService? impresion
    )
    {
        // Caso-1, punto 2 (ultimo parrafo): si la carpeta del periodo actual todavia no existe,
        // Archivero no la crea sola -- eso pasa a ser una decision activa del usuario (pendiente
        // con su propia pantalla), nunca una suposicion automatica del programa.
        if (
            configuracionConPatronCoincidente.FormatoCarpeta != FormatoCarpeta.Directo
            && Directory.Exists(configuracionConPatronCoincidente.CarpetaDestino)
        )
        {
            var rutaDestinoCalculada = ClasificadorService.CalcularRutaDestino(
                rutaArchivo,
                configuracionConPatronCoincidente,
                campos.Fecha,
                campos.NombreExtraido
            );
            var carpetaPeriodo = Path.GetDirectoryName(rutaDestinoCalculada)!;

            if (!Directory.Exists(carpetaPeriodo))
            {
                return new ResultadoProcesamiento(
                    ResultadoGuardadoAutomatico.PeriodoNuevo,
                    Detalle: carpetaPeriodo
                );
            }
        }

        try
        {
            var rutaFinal = ClasificadorService.Clasificar(
                rutaArchivo,
                configuracionConPatronCoincidente,
                campos.Fecha,
                campos.NombreExtraido
            );

            AuditoriaService.Registrar(
                "DOCUMENTO_GUARDADO",
                $"Emisor={configuracionConPatronCoincidente.Emisor}; Tipo={configuracionConPatronCoincidente.Tipo}; Ruta={rutaFinal}"
            );

            string? detallePublicacion = null;
            if (PublicadorDatosDocumentoService.PublicacionAutomaticaActiva)
            {
                try
                {
                    PublicadorDatosDocumentoService.PublicarGuardado(
                        rutaFinal,
                        configuracionConPatronCoincidente,
                        campos
                    );
                }
                catch (Exception error)
                {
                    detallePublicacion =
                        $"El documento se guardó, pero no se publicaron sus datos en Hormiguero: {error.Message}";
                    AuditoriaService.Registrar(
                        "PUBLICACION_HORMIGUERO_FALLIDA",
                        $"{rutaFinal}: {error.Message}"
                    );
                }
            }

            if (configuracionConPatronCoincidente.AbrirDespuesDeGuardar)
            {
                AbrirEnVisorDelSistema(rutaFinal);
            }

            string? avisoImpresion = (impresion ?? new(new AccionImpresionWindows())).Procesar(
                rutaFinal,
                configuracionConPatronCoincidente
            );
            if (!string.IsNullOrWhiteSpace(avisoImpresion))
                detallePublicacion = string.Join(
                    "\n",
                    new[] { detallePublicacion, avisoImpresion }.Where(x =>
                        !string.IsNullOrWhiteSpace(x)
                    )
                );

            return new ResultadoProcesamiento(
                ResultadoGuardadoAutomatico.Guardado,
                rutaFinal,
                detallePublicacion
            );
        }
        catch (ArchivoDuplicadoException ex)
        {
            return new ResultadoProcesamiento(
                ResultadoGuardadoAutomatico.Duplicado,
                Detalle: ex.Message
            );
        }
        catch (ValidacionSeguridadException ex)
        {
            // Caso-9, mejora 1(g): nunca un guardado silencioso -- el documento queda pendiente
            // con un motivo específico y legible, nunca una excepción sin manejar.
            return new ResultadoProcesamiento(
                ResultadoGuardadoAutomatico.ValidacionFallida,
                Detalle: ex.Message,
                MotivoValidacion: ex.Motivo
            );
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ResultadoProcesamiento(
                ResultadoGuardadoAutomatico.CarpetaNoDisponible,
                Detalle: ex.Message
            );
        }
    }

    private static RectanguloFraccion ARect(Marca marca) =>
        new(marca.X, marca.Y, marca.Ancho, marca.Alto);

    private static bool FaltaProveedorLegible(
        string rutaArchivo,
        ConfiguracionDocumento configuracion
    )
    {
        if (!AsistenteClasificacionService.EsCompraPropiaPorTipo(configuracion.Tipo))
            return false;
        var patron = configuracion.Patrones.FirstOrDefault();
        if (patron is null)
            return true;
        var datos = DatosEnlazantesConfiguracionService
            .Leer(configuracion.Emisor, configuracion.Tipo, patron.Id)
            .Where(d =>
                d.Incluido && d.Marcado && AsistenteClasificacionService.EsDatoProveedor(d.Id)
            )
            .Select(d =>
                (
                    d.Id,
                    LectorPdf.ExtraerTexto(
                        rutaArchivo,
                        d.Pagina,
                        new RectanguloFraccion(d.X, d.Y, d.Ancho, d.Alto)
                    )
                )
            );
        return !AsistenteClasificacionService.TieneProveedorLegible(datos);
    }

    /// <summary>
    /// Caso-1, punto 5: reemplaza la apertura automática que hacía PDFCreator antes de que
    /// Archivero moviera el archivo. Si no se puede abrir (ej. no hay un visor de PDF asociado),
    /// no afecta el resultado del guardado -- el archivo ya quedó guardado igual.
    /// </summary>
    private static void AbrirEnVisorDelSistema(string rutaArchivo)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(rutaArchivo) { UseShellExecute = true }
            );
        }
        catch { }
    }
}
