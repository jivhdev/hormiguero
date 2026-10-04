using System.IO;
using System.Security.Cryptography;
using Archivero.Datos;

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
    /// <summary>
    /// Calcula dónde iría el archivo (carpeta de fecha + nombre según la configuración), sin
    /// tocar el disco. Se usa tanto para clasificar como para saber, ante un duplicado, qué
    /// ruta exacta está en conflicto (REQ-002).
    /// </summary>
    public static string CalcularRutaDestino(
        string rutaArchivoOrigen,
        ConfiguracionDocumento configuracion,
        DateTime? fechaExtraida,
        string? nombreExtraido)
    {
        var subcarpeta = FormatoCarpetaService.ConstruirSubcarpeta(
            configuracion.FormatoCarpeta, configuracion.PatronCarpeta, fechaExtraida ?? DateTime.Now);

        // Caso-9, mejora 1: cada nivel real de subcarpeta se valida/sanea por separado antes de
        // combinarlo -- puede venir de un patrón personalizado escrito a mano (Caso-3).
        var subcarpetaSaneada = string.IsNullOrEmpty(subcarpeta)
            ? subcarpeta
            : Path.Combine(subcarpeta
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Select(ValidadorRutaService.ValidarYSanearSegmento)
                .ToArray());

        var carpetaFinal = string.IsNullOrEmpty(subcarpetaSaneada)
            ? configuracion.CarpetaDestino
            : Path.Combine(configuracion.CarpetaDestino, subcarpetaSaneada);

        var extension = Path.GetExtension(rutaArchivoOrigen);
        var nombreOrigen = configuracion.Renombrar && !string.IsNullOrWhiteSpace(nombreExtraido)
            ? nombreExtraido
            : Path.GetFileNameWithoutExtension(rutaArchivoOrigen);
        var nombreSinExtension = ValidadorRutaService.ValidarYSanearSegmento(nombreOrigen);

        var nombreArchivo = $"{nombreSinExtension}{extension}";
        var rutaDestino = Path.Combine(carpetaFinal, nombreArchivo);

        // (e) última línea de defensa, siempre: la ruta final ya resuelta tiene que quedar
        // efectivamente dentro de la carpeta configurada, sin excepción.
        var rutaResuelta = Path.GetFullPath(rutaDestino);
        ValidadorRutaService.ValidarContenidaEnCarpeta(rutaResuelta, Path.GetFullPath(configuracion.CarpetaDestino));
        ValidadorRutaService.ValidarLargos(nombreSinExtension, rutaResuelta);

        return rutaDestino;
    }

    public static string Clasificar(
        string rutaArchivoOrigen,
        ConfiguracionDocumento configuracion,
        DateTime? fechaExtraida,
        string? nombreExtraido)
    {
        var rutaDestino = CalcularRutaDestino(rutaArchivoOrigen, configuracion, fechaExtraida, nombreExtraido);
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
        File.Delete(rutaDestino);
        CopiarVerificarBorrar(rutaArchivoOrigen, rutaDestino);
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
        File.Copy(origen, destino);

        if (!ArchivosSonIdenticos(origen, destino))
        {
            File.Delete(destino);
            throw new IOException("La copia no coincide con el original; no se borró el archivo original.");
        }

        File.Delete(origen);
    }

    private static bool ArchivosSonIdenticos(string rutaA, string rutaB)
    {
        using var streamA = File.OpenRead(rutaA);
        using var streamB = File.OpenRead(rutaB);

        var hashA = SHA256.HashData(streamA);
        var hashB = SHA256.HashData(streamB);

        return hashA.AsSpan().SequenceEqual(hashB);
    }
}
