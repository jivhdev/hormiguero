using System.IO;
using System.Text;

namespace Archivero.Servicios;

/// <summary>
/// Log de auditoría de texto plano (Caso-9, mejora 2): una línea por evento, para trazabilidad
/// antes de repartir Archivero a otros usuarios. Nunca interrumpe la app -- cualquier falla al
/// escribir se pierde en silencio, nunca detiene el guardado real de un documento ni ninguna
/// otra funcionalidad.
/// </summary>
public static class AuditoriaService
{
    /// <summary>Cuando el log supera este tamaño, rota antes de seguir escribiendo.</summary>
    public const long TamanioMaximoBytes = 10 * 1024 * 1024;

    /// <summary>Nunca se conservan más de esta cantidad de archivos rotados (.1, .2, .3).</summary>
    public const int MaximoArchivosRotados = 3;

    private static readonly object CandadoEscritura = new();

    /// <summary>%LocalAppData%\Archivero\auditoria.log, junto a la base SQLite (mismo criterio de ADR-002). Configurable para tests.</summary>
    public static string RutaLog { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Archivero", "auditoria.log");

    /// <summary>
    /// Escribe una línea: "[fecha-hora ISO-8601] TIPO_DE_EVENTO — detalle". Nunca lanza -- si
    /// falla escribir (disco lleno, permisos, lo que sea), se pierde ese registro puntual y la
    /// app sigue funcionando con normalidad.
    /// </summary>
    public static void Registrar(string tipoEvento, string detalle)
    {
        try
        {
            lock (CandadoEscritura)
            {
                var ruta = RutaLog;
                Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
                RotarSiHaceFalta(ruta);

                var fecha = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                var linea = $"[{fecha}] {Sanear(tipoEvento)} — {Sanear(detalle)}{Environment.NewLine}";
                File.AppendAllText(ruta, linea);
            }
        }
        catch
        {
            // Mejora 2: una falla al escribir el log nunca debe interrumpir la app.
        }
    }

    /// <summary>
    /// Cuando auditoria.log ya llegó a <see cref="TamanioMaximoBytes"/>: el actual pasa a
    /// ".1" (corriendo ".1"→".2"→".3" primero), y se descarta cualquier ".3" ya existente --
    /// nunca se conservan más de <see cref="MaximoArchivosRotados"/> archivos rotados.
    /// </summary>
    private static void RotarSiHaceFalta(string ruta)
    {
        if (!File.Exists(ruta) || new FileInfo(ruta).Length < TamanioMaximoBytes)
        {
            return;
        }

        var rutaMasVieja = $"{ruta}.{MaximoArchivosRotados}";
        if (File.Exists(rutaMasVieja))
        {
            File.Delete(rutaMasVieja);
        }

        for (var i = MaximoArchivosRotados - 1; i >= 1; i--)
        {
            var origen = $"{ruta}.{i}";
            if (File.Exists(origen))
            {
                File.Move(origen, $"{ruta}.{i + 1}");
            }
        }

        File.Move(ruta, $"{ruta}.1");
    }

    /// <summary>
    /// Evita que un valor variable inyecte una línea falsa en el log: saltos de línea y
    /// caracteres de control se reemplazan por un espacio, para que un valor nunca pueda partir
    /// una línea en dos ni fingir ser una entrada distinta.
    /// </summary>
    public static string Sanear(string texto)
    {
        // "\r\n" cuenta como un solo salto de línea (un espacio), no dos.
        var sinCrLf = texto.Replace("\r\n", " ");

        var resultado = new StringBuilder(sinCrLf.Length);
        foreach (var c in sinCrLf)
        {
            resultado.Append(c is '\n' or '\r' || c <= 31 || c == 127 ? ' ' : c);
        }

        return resultado.ToString();
    }
}
