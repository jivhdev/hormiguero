using System.IO;
using Archivero.Datos;

namespace Archivero.Servicios;

/// <summary>Se lanza cuando un texto o una ruta calculada no pasa la validación de seguridad de Caso-9. El documento nunca se guarda; queda pendiente con <see cref="Motivo"/>.</summary>
public class ValidacionSeguridadException : Exception
{
    public MotivoPendiente Motivo { get; }

    public ValidacionSeguridadException(MotivoPendiente motivo) : base(motivo.DescripcionLegible())
    {
        Motivo = motivo;
    }
}

/// <summary>
/// Validación centralizada de todo texto que termina formando parte de un nombre de archivo o
/// una ruta (Caso-9, mejora 1): texto extraído del PDF, texto tipeado por el usuario, y
/// componentes de carpeta derivados de un patrón. Un único lugar, para no duplicar esta lógica
/// en el guardado automático (REQ-002), el asistente de identificación (REQ-003), y el flujo
/// sin texto (Caso-4).
/// </summary>
public static class ValidadorRutaService
{
    public const int MaximoCaracteresNombreArchivo = 200;
    public const int MaximoCaracteresRutaCompleta = 240;

    private static readonly char[] CaracteresInvalidosWindows = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly HashSet<string> NombresReservados = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    /// Valida y sanea un único segmento de nombre (una carpeta, o el nombre de archivo sin
    /// extensión) -- Caso-9, mejora 1 (a, b, d):
    /// a) un carácter de control (0-31, 127) o byte nulo nunca se sanea: rechaza el texto entero.
    /// b) los caracteres inválidos de Windows en un nombre se reemplazan por "_", salvo que el
    ///    resultado sea un nombre reservado de Windows (CON, PRN, COM1...), que rechaza en vez
    ///    de sanear.
    /// c) cualquier otro carácter imprimible se deja tal cual.
    /// d) espacios al principio, y espacios/puntos al final, se recortan.
    /// </summary>
    public static string ValidarYSanearSegmento(string texto)
    {
        if (texto.Any(c => c <= 31 || c == 127))
        {
            throw new ValidacionSeguridadException(MotivoPendiente.TextoConCaracteresInvalidos);
        }

        var saneado = new string(texto.Select(c => Array.IndexOf(CaracteresInvalidosWindows, c) >= 0 ? '_' : c).ToArray());
        saneado = saneado.TrimStart(' ').TrimEnd(' ', '.');

        var nombreSinExtension = Path.GetFileNameWithoutExtension(saneado);
        if (NombresReservados.Contains(saneado) || NombresReservados.Contains(nombreSinExtension))
        {
            throw new ValidacionSeguridadException(MotivoPendiente.NombreReservadoPorWindows);
        }

        return saneado;
    }

    /// <summary>
    /// Caso-9, mejora 1(e): última línea de defensa contra path traversal. La ruta final ya
    /// resuelta (sin ".." ni relativos, vía <see cref="Path.GetFullPath(string)"/>) tiene que
    /// quedar efectivamente dentro de la carpeta configurada -- comparación por segmentos
    /// completos de ruta, nunca por substring de texto. Se aplica siempre, incluso si las
    /// validaciones anteriores ya pasaron: cubre tanto texto malicioso/corrupto como un
    /// eventual bug futuro de cálculo de patrón.
    /// </summary>
    public static void ValidarContenidaEnCarpeta(string rutaResuelta, string carpetaBaseResuelta)
    {
        var baseSinBarraFinal = carpetaBaseResuelta.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var contenida = string.Equals(rutaResuelta, baseSinBarraFinal, StringComparison.OrdinalIgnoreCase)
            || rutaResuelta.StartsWith(baseSinBarraFinal + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        if (!contenida)
        {
            throw new ValidacionSeguridadException(MotivoPendiente.RutaFueraDeCarpetaConfigurada);
        }
    }

    /// <summary>Caso-9, mejora 1(f): nombre de archivo (sin extensión) máximo 200 caracteres; ruta completa final máximo 240.</summary>
    public static void ValidarLargos(string nombreSinExtension, string rutaCompleta)
    {
        if (nombreSinExtension.Length > MaximoCaracteresNombreArchivo || rutaCompleta.Length > MaximoCaracteresRutaCompleta)
        {
            throw new ValidacionSeguridadException(MotivoPendiente.NombreORutaDemasiadoLarga);
        }
    }
}
