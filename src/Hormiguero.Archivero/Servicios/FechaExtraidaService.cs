using System.Globalization;
using System.Text.RegularExpressions;

namespace Archivero.Servicios;

/// <summary>
/// Interpreta el texto extraído de la coordenada de Fecha, probando varios formatos comunes
/// en documentos reales (no solo el que reconoce DateTime.TryParse por defecto).
/// </summary>
public static partial class FechaExtraidaService
{
    private static readonly string[] FormatosNumericos =
    [
        "dd/MM/yyyy", "dd-MM-yyyy", "dd.MM.yyyy",
        "dd/MM/yy", "dd-MM-yy", "dd.MM.yy",
        "yyyy-MM-dd", "yyyy/MM/dd",
        "MM/dd/yyyy", "M/d/yyyy",
    ];

    private static readonly Dictionary<string, int> MesesPorNombre = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ENERO"] = 1, ["ENE"] = 1,
        ["FEBRERO"] = 2, ["FEB"] = 2,
        ["MARZO"] = 3, ["MAR"] = 3,
        ["ABRIL"] = 4, ["ABR"] = 4,
        ["MAYO"] = 5, ["MAY"] = 5,
        ["JUNIO"] = 6, ["JUN"] = 6,
        ["JULIO"] = 7, ["JUL"] = 7,
        ["AGOSTO"] = 8, ["AGO"] = 8,
        ["SEPTIEMBRE"] = 9, ["SETIEMBRE"] = 9, ["SEP"] = 9, ["SET"] = 9,
        ["OCTUBRE"] = 10, ["OCT"] = 10,
        ["NOVIEMBRE"] = 11, ["NOV"] = 11,
        ["DICIEMBRE"] = 12, ["DIC"] = 12,
    };

    // "30 de abril de 2026"
    [GeneratedRegex(@"^\s*(\d{1,2})\s+de\s+([A-Za-zñÑ]+)\.?\s+de\s+(\d{4})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ConPalabraDe();

    // "30-ABR-2026", "30/abr/26", "30.ABR.2026", "30 ABR 2026"
    [GeneratedRegex(@"^\s*(\d{1,2})\s*[-/. ]\s*([A-Za-zñÑ]+)\.?\s*[-/. ]\s*(\d{2,4})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ConSeparador();

    public static bool TryParsear(string texto, out DateTime fecha)
    {
        texto = texto.Trim();

        foreach (var formato in FormatosNumericos)
        {
            if (DateTime.TryParseExact(texto, formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha))
            {
                return true;
            }
        }

        if (TryParsearConNombreDeMes(texto, ConPalabraDe(), out fecha) ||
            TryParsearConNombreDeMes(texto, ConSeparador(), out fecha))
        {
            return true;
        }

        // Ultimo recurso: el parseo general de .NET, por si reconoce algo que las reglas de
        // arriba no contemplaron.
        return DateTime.TryParse(texto, CultureInfo.GetCultureInfo("es-ES"), DateTimeStyles.None, out fecha);
    }

    private static bool TryParsearConNombreDeMes(string texto, Regex patron, out DateTime fecha)
    {
        fecha = default;
        var coincidencia = patron.Match(texto);
        if (!coincidencia.Success)
        {
            return false;
        }

        if (!int.TryParse(coincidencia.Groups[1].Value, out var dia))
        {
            return false;
        }

        if (!MesesPorNombre.TryGetValue(coincidencia.Groups[2].Value, out var mes))
        {
            return false;
        }

        var textoAnio = coincidencia.Groups[3].Value;
        if (!int.TryParse(textoAnio, out var anio))
        {
            return false;
        }

        if (textoAnio.Length <= 2)
        {
            anio += anio < 70 ? 2000 : 1900;
        }

        try
        {
            fecha = new DateTime(anio, mes, dia);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
