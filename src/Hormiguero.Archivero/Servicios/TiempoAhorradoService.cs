using Archivero.Datos;

namespace Archivero.Servicios;

/// <summary>
/// Indicador de tiempo humano ahorrado (Caso-8): X = cantidad total histórica de documentos
/// guardados automáticamente (REQ-002 — nunca cuenta el flujo manual de PDFs sin texto
/// extraíble de Caso-4, que sí requiere intervención del usuario). No se compara "rápido vs.
/// lento": el ahorro es que el usuario ya no gasta esos segundos de atención en copiar/limpiar/
/// pegar el nombre y mover el archivo a mano, sin importar cuánto tarde el sistema en hacerlo solo.
///
/// X no se puede sacar contando filas de GuardadosRecientes: esa tabla se recorta a los últimos
/// 20 (Caso-6, punto 2), así que dejaría de ser un total histórico. Se lleva aparte, como un
/// contador propio en Configuracion, incrementado en el mismo momento en que se agrega a
/// GuardadosRecientes (un guardado automático real).
/// </summary>
public static class TiempoAhorradoService
{
    /// <summary>Segundos de atención manual que se ahorran por cada documento archivado solo. Único lugar donde se declara este número.</summary>
    public const int SegundosAhorradosPorDocumento = 3;

    private const string ClaveContadorTotal = "TotalDocumentosArchivadosAutomaticamente";

    public static void RegistrarDocumentoArchivado(ConfiguracionRepository configuracion) =>
        configuracion.IncrementarContador(ClaveContadorTotal);

    public static int ObtenerTotalDocumentos(ConfiguracionRepository configuracion) =>
        configuracion.ObtenerContador(ClaveContadorTotal);

    public static (string Titulo, string Aclaracion) FormatearResumen(int totalDocumentos)
    {
        var minutos = totalDocumentos * SegundosAhorradosPorDocumento / 60.0;
        var titulo = $"{totalDocumentos} {Pluralizar(totalDocumentos, "documento", "documentos")} archivado{(totalDocumentos == 1 ? "" : "s")} automáticamente — tiempo humano ahorrado: ~{FormatearTiempo(minutos)}";
        var aclaracion = $"(estimado a ~{SegundosAhorradosPorDocumento} segundos de atención manual ahorrados por documento)";
        return (titulo, aclaracion);
    }

    /// <summary>Minutos si es menos de una hora; horas y minutos si es una hora o más (Caso-8, sugerencia de legibilidad).</summary>
    private static string FormatearTiempo(double minutos)
    {
        var minutosRedondeados = (int)Math.Round(minutos, MidpointRounding.AwayFromZero);

        if (minutosRedondeados < 60)
        {
            return $"{minutosRedondeados} {Pluralizar(minutosRedondeados, "minuto", "minutos")}";
        }

        var horas = minutosRedondeados / 60;
        var minutosRestantes = minutosRedondeados % 60;
        var textoHoras = $"{horas} {Pluralizar(horas, "hora", "horas")}";

        return minutosRestantes == 0
            ? textoHoras
            : $"{textoHoras} y {minutosRestantes} {Pluralizar(minutosRestantes, "minuto", "minutos")}";
    }

    private static string Pluralizar(int cantidad, string singular, string plural) => cantidad == 1 ? singular : plural;
}
