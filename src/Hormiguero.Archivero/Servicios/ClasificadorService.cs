using System.IO;
using Archivero.Datos;
using Hormiguero.Nucleo.Utilidades;

namespace Archivero.Servicios;

public class ArchivoDuplicadoException : Exception
{
    public string RutaDestino { get; }

    public ArchivoDuplicadoException(string rutaDestino)
        : base($"Ya existe un archivo en \"{rutaDestino}\".")
    {
        RutaDestino = rutaDestino;
    }
}

public static class ClasificadorService
{
    /// <summary>Se dispara con la ruta final cada vez que un documento quedó guardado (fase B-4).</summary>
    public static event Action<string>? DocumentoGuardado;

    /// <summary>
    /// Calcula dónde iría el archivo (carpeta de fecha + nombre según la configuración), sin
    /// tocar el disco. Se usa tanto para clasificar como para saber, ante un duplicado, qué
    /// ruta exacta está en conflicto (REQ-002).
    /// </summary>
    public static string CalcularRutaDestino(
        string rutaArchivoOrigen,
        ConfiguracionDocumento configuracion,
        DateTime? fechaExtraida,
        string? nombreExtraido
    )
    {
        var subcarpeta = FormatoCarpetaService.ConstruirSubcarpeta(
            configuracion.FormatoCarpeta,
            configuracion.PatronCarpeta,
            fechaExtraida ?? DateTime.Now
        );

        // Caso-9, mejora 1: cada nivel real de subcarpeta se valida/sanea por separado antes de
        // combinarlo -- puede venir de un patrón personalizado escrito a mano (Caso-3).
        var subcarpetaSaneada = string.IsNullOrEmpty(subcarpeta)
            ? subcarpeta
            : Path.Combine(
                subcarpeta
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Select(ValidadorRutaService.ValidarYSanearSegmento)
                    .ToArray()
            );

        var carpetaFinal = string.IsNullOrEmpty(subcarpetaSaneada)
            ? configuracion.CarpetaDestino
            : Path.Combine(configuracion.CarpetaDestino, subcarpetaSaneada);

        var extension = Path.GetExtension(rutaArchivoOrigen);
        var nombreOrigen =
            configuracion.Renombrar && !string.IsNullOrWhiteSpace(nombreExtraido)
                ? nombreExtraido
                : Path.GetFileNameWithoutExtension(rutaArchivoOrigen);
        var nombreSinExtension = ValidadorRutaService.ValidarYSanearSegmento(nombreOrigen);

        var nombreArchivo = $"{nombreSinExtension}{extension}";
        var rutaDestino = Path.Combine(carpetaFinal, nombreArchivo);

        // (e) última línea de defensa, siempre: la ruta final ya resuelta tiene que quedar
        // efectivamente dentro de la carpeta configurada, sin excepción.
        var rutaResuelta = Path.GetFullPath(rutaDestino);
        ValidadorRutaService.ValidarContenidaEnCarpeta(
            rutaResuelta,
            Path.GetFullPath(configuracion.CarpetaDestino)
        );
        ValidadorRutaService.ValidarLargos(nombreSinExtension, rutaResuelta);

        return rutaDestino;
    }

    public static string Clasificar(
        string rutaArchivoOrigen,
        ConfiguracionDocumento configuracion,
        DateTime? fechaExtraida,
        string? nombreExtraido
    )
    {
        var rutaDestino = CalcularRutaDestino(
            rutaArchivoOrigen,
            configuracion,
            fechaExtraida,
            nombreExtraido
        );
        Directory.CreateDirectory(Path.GetDirectoryName(rutaDestino)!);

        if (File.Exists(rutaDestino))
        {
            throw new ArchivoDuplicadoException(rutaDestino);
        }

        CopiarVerificarBorrar(rutaArchivoOrigen, rutaDestino);
        return rutaDestino;
    }

    /// <summary>Resolución de duplicado (REQ-002), opción "Reemplazar": borra lo que había y guarda lo nuevo.</summary>
    public static void ReemplazarYClasificar(string rutaArchivoOrigen, string rutaDestino)
    {
        // Fase B-5 (D-67): antes se borraba lo anterior y después se copiaba lo nuevo; si
        // la copia fallaba, se perdía el documento que ya estaba guardado. Ahora lo nuevo se
        // copia y verifica aparte, y recién entonces reemplaza a lo anterior.
        string temporal = rutaDestino + ".reemplazo";
        Trasladar(rutaArchivoOrigen, temporal);
        File.Move(temporal, rutaDestino, overwrite: true);
        Avisar(rutaDestino);
    }

    /// <summary>Resolución de duplicado (REQ-002), opción "Guardar en otra ubicación como excepción".</summary>
    public static void GuardarComoExcepcion(string rutaArchivoOrigen, string rutaDestino)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(rutaDestino)!);

        if (File.Exists(rutaDestino))
        {
            throw new ArchivoDuplicadoException(rutaDestino);
        }

        CopiarVerificarBorrar(rutaArchivoOrigen, rutaDestino);
    }

    private static void CopiarVerificarBorrar(string origen, string destino)
    {
        Trasladar(origen, destino);
        Avisar(destino);
    }

    // Fase B-5 (D-66): mover = copiar a un temporal, verificar la huella, renombrar y recién
    // borrar el original (núcleo). Nunca queda un archivo a medias con el nombre final.
    private static void Trasladar(string origen, string destino)
    {
        Traslado traslado = MovedorSeguro.Mover(origen, destino);
        switch (traslado.Resultado)
        {
            case ResultadoTraslado.Movido:
                return;

            case ResultadoTraslado.YaEstabaIgual:
            case ResultadoTraslado.DestinoConOtroContenido:
                throw new ArchivoDuplicadoException(destino);

            default:
                throw new IOException(
                    "No se pudo guardar; el archivo original quedó donde estaba. "
                        + (traslado.Detalle ?? string.Empty)
                );
        }
    }

    private static void Avisar(string destino)
    {
        try
        {
            DocumentoGuardado?.Invoke(destino);
        }
        catch (Exception) { }
    }
}
