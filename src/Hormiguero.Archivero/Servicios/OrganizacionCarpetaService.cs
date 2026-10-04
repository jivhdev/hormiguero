using System.IO;
using Archivero.Datos;

namespace Archivero.Servicios;

public record TipoOrganizacion(FormatoCarpeta Formato, string Nombre);

/// <summary>Un patrón interno (lo que se guarda en la base) y su ejemplo concreto ya formateado
/// con la fecha de referencia (lo único que ve el usuario — Caso-3, punto 3c: nada de códigos
/// de formato abstractos).</summary>
public record EjemploPatron(string Patron, string Texto);

/// <summary>
/// Catálogo de tipos de organización de subcarpetas de Caso-3 (Paso 3): la lista completa en el
/// orden exacto del documento (granularidad creciente) y, por tipo, los ejemplos de patrón de la
/// tabla del punto 3c. La fecha de referencia de los ejemplos es la del documento si ya se marcó,
/// o la de hoy mientras tanto (punto 3d).
/// </summary>
public static class OrganizacionCarpetaService
{
    public const string NombrePersonalizado = "Patrón personalizado";
    public const string NombrePersonalizadoEnLista = "Ninguna de estas — patrón personalizado";

    public static readonly IReadOnlyList<TipoOrganizacion> TodosLosTipos =
    [
        new(FormatoCarpeta.Directo, "Directo en la carpeta"),
        new(FormatoCarpeta.Anio, "Por año"),
        new(FormatoCarpeta.AnioSemestre, "Por año y semestre"),
        new(FormatoCarpeta.AnioTrimestre, "Por año y trimestre"),
        new(FormatoCarpeta.AnioMes, "Por año y mes"),
        new(FormatoCarpeta.AnioQuincena, "Por año y quincena"),
        new(FormatoCarpeta.AnioSemana, "Por año y semana"),
        new(FormatoCarpeta.AnioMesDia, "Por año, mes y día"),
        new(FormatoCarpeta.MesSinAnio, "Por mes (sin año)"),
        new(FormatoCarpeta.SemanaDelMes, "Por semana del mes"),
    ];

    public static readonly TipoOrganizacion OpcionPersonalizada = new(FormatoCarpeta.Personalizado, NombrePersonalizadoEnLista);

    /// <summary>
    /// Ejemplos de patrón por tipo, como niveles de carpeta (se unen con "\" al guardar y se
    /// muestran unidos con "/" — tabla de Caso-3 punto 3c). El último de "Por año y mes"
    /// (2026/202603) es la variante que Javier pidió explícitamente y Caso-3 conserva.
    /// </summary>
    private static readonly Dictionary<FormatoCarpeta, string[][]> NivelesPorTipo = new()
    {
        [FormatoCarpeta.Anio] =
        [
            ["yyyy"],
        ],
        [FormatoCarpeta.AnioSemestre] =
        [
            ["yyyy", "'S'S"],
            ["yyyy-'S'S"],
            ["yyyy", "'Semestre 'S"],
        ],
        [FormatoCarpeta.AnioTrimestre] =
        [
            ["yyyy", "'T'T"],
            ["yyyy-'T'T"],
            ["yyyy", "'Q'T"],
        ],
        [FormatoCarpeta.AnioMes] =
        [
            ["yyyy", "MM"],
            ["yyyyMM"],
            ["yyyy-MM"],
            ["MM-yyyy"],
            ["yyyy", "MMMM"],
            ["yyyy", "yyyyMM"],
        ],
        [FormatoCarpeta.AnioQuincena] =
        [
            ["yyyy", "MM", "'Q'H"],
            ["yyyy-MM-'Q'H"],
            ["yyyy", "MMMM' - 'O' quincena'"],
        ],
        [FormatoCarpeta.AnioSemana] =
        [
            ["yyyy", "'Semana 'WW"],
            ["yyyy-'W'WW"],
        ],
        [FormatoCarpeta.AnioMesDia] =
        [
            ["yyyy", "MM", "dd"],
            ["yyyyMMdd"],
            ["yyyy-MM-dd"],
        ],
        [FormatoCarpeta.MesSinAnio] =
        [
            ["MM"],
            ["MMMM"],
            ["'Mes 'MM"],
        ],
        [FormatoCarpeta.SemanaDelMes] =
        [
            ["MM", "'Semana 'N"],
            ["MMMM", "'Semana 'N"],
            ["'Mes 'MM", "'Semana 'N"],
        ],
    };

    /// <summary>Tipos cuya subcarpeta no depende del año: sin fecha del documento, se usa la de hoy.</summary>
    public static bool FechaEsOpcional(FormatoCarpeta formato) =>
        formato is FormatoCarpeta.MesSinAnio or FormatoCarpeta.SemanaDelMes;

    public static string NombreDe(FormatoCarpeta formato) =>
        formato == FormatoCarpeta.Personalizado
            ? NombrePersonalizado
            : TodosLosTipos.FirstOrDefault(t => t.Formato == formato)?.Nombre ?? formato.ToString();

    /// <summary>Ejemplos concretos del tipo para la fecha de referencia, en el orden de la tabla de 3c.</summary>
    public static List<EjemploPatron> ObtenerEjemplos(FormatoCarpeta formato, DateTime fechaReferencia) =>
        NivelesPorTipo.GetValueOrDefault(formato, [])
            .Select(niveles =>
            {
                var patron = string.Join("\\", niveles);
                return new EjemploPatron(patron, FormatearEjemplo(patron, fechaReferencia));
            })
            .ToList();

    /// <summary>Texto concreto que un patrón produce para una fecha, con los niveles unidos por "/" (para mostrar).</summary>
    public static string FormatearEjemplo(string patron, DateTime fechaReferencia) =>
        string.Join("/", patron.Split('\\').Select(nivel => FormatoCarpetaService.FormatearNivel(nivel, fechaReferencia)));

    /// <summary>Convierte lo que el usuario escribe a mano en el patrón interno (acepta "/" o "\" como separador de niveles).</summary>
    public static string NormalizarPatronPersonalizado(string texto) =>
        texto.Trim().Replace('/', '\\');

    /// <summary>Valida un patrón personalizado: que se pueda formatear y que no produzca caracteres inválidos para una carpeta.</summary>
    public static bool EsPatronValido(string patron, DateTime fechaReferencia, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(patron))
        {
            error = "El patrón está vacío.";
            return false;
        }

        try
        {
            var subcarpeta = FormatoCarpetaService.ConstruirSubcarpeta(FormatoCarpeta.Personalizado, patron, fechaReferencia);
            var nombres = subcarpeta.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (nombres.Any(string.IsNullOrWhiteSpace))
            {
                error = "Hay un nivel de carpeta vacío en el patrón.";
                return false;
            }

            var invalidos = Path.GetInvalidFileNameChars().Where(c => nombres.Any(n => n.Contains(c))).ToList();
            if (invalidos.Count > 0)
            {
                error = $"El patrón produce caracteres que Windows no permite en una carpeta: {string.Join(" ", invalidos.Select(c => $"\"{c}\""))}.";
                return false;
            }
        }
        catch (Exception ex)
        {
            error = $"No se pudo interpretar el patrón: {ex.Message}";
            return false;
        }

        return true;
    }
}
