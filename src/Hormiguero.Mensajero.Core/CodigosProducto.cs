using System.Text.RegularExpressions;
using Hormiguero.Nucleo.Pdf;

namespace Hormiguero.Mensajero.Core;

public sealed record CodigoProducto(string Codigo, int Aparicion, bool Repetido);

public static partial class CodigosProducto
{
    public const int LargoPredeterminado = 5;

    public static IReadOnlyList<CodigoProducto> LeerPdf(Stream pdf, int largo = LargoPredeterminado)
    {
        InfoPdf info = LectorPdf.Leer(pdf);
        if (info.Estado != EstadoPdf.Correcto)
        {
            throw new InvalidDataException(
                info.Estado == EstadoPdf.Protegido
                    ? "El PDF está protegido y no se puede leer."
                    : "El PDF está dañado o no se pudo leer."
            );
        }

        return AnalizarTexto(
            string.Join(" ", info.Palabras.Select(palabra => palabra.Texto)),
            largo
        );
    }

    public static IReadOnlyList<CodigoProducto> AnalizarTexto(
        string texto,
        int largo = LargoPredeterminado
    )
    {
        ArgumentNullException.ThrowIfNull(texto);
        if (largo is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(
                nameof(largo),
                "El largo debe estar entre 1 y 20."
            );
        }

        string patron = $@"(?<![\p{{L}}\d.,])\d{{{largo}}}(?![\p{{L}}\d.,])";
        var rut = RutRegex();
        var telefono = TelefonoRegex();
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var resultado = new List<CodigoProducto>();
        foreach (Match coincidencia in Regex.Matches(texto, patron))
        {
            int inicio = coincidencia.Index;
            int fin = inicio + coincidencia.Length;
            if (
                rut.Matches(texto)
                    .Cast<Match>()
                    .Any(m => inicio >= m.Index && fin <= m.Index + m.Length)
                || telefono
                    .Matches(texto)
                    .Cast<Match>()
                    .Any(m => inicio >= m.Index && fin <= m.Index + m.Length)
            )
            {
                continue;
            }

            string codigo = coincidencia.Value;
            resultado.Add(new(codigo, resultado.Count + 1, !vistos.Add(codigo)));
        }
        return resultado;
    }

    [GeneratedRegex(
        @"(?<!\d)(?:\d{7,8}|\d{1,2}(?:\.\d{3}){2})-[0-9kK](?![\w])",
        RegexOptions.CultureInvariant
    )]
    private static partial Regex RutRegex();

    // Celular chileno: 9 + 4 dígitos + 4 dígitos, sin cruzar líneas. Así "9 80163 950"
    // (fila, código, cantidad) o "9.685 80230" (precio, código) no pasan por teléfonos.
    [GeneratedRegex(
        @"(?<!\d)(?:\+?56[ \t]?)?9[ \t.-]?\d{4}[ \t.-]?\d{4}(?!\d)",
        RegexOptions.CultureInvariant
    )]
    private static partial Regex TelefonoRegex();
}
