using System.Text.RegularExpressions;

namespace Hormiguero.Mensajero.Core.ClickFactura;

public static partial class CorreoFactura
{
    [GeneratedRegex(@"^[^\s@,;]+@[^\s@,;.]+(?:\.[^\s@,;\.]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex PatronCorreo();

    public static IReadOnlyList<string> Normalizar(string correos)
    {
        ArgumentNullException.ThrowIfNull(correos);
        string[] partes = Regex
            .Split(correos.Trim(), @"[,;\s]+", RegexOptions.CultureInvariant)
            .Where(parte => parte.Length > 0)
            .ToArray();
        if (partes.Length == 0)
            throw new FormatException("Ingresa al menos un correo electrónico.");

        string? invalido = partes.FirstOrDefault(parte => !EsValido(parte));
        if (invalido is not null)
            throw new FormatException($"El correo «{invalido}» no parece válido.");
        return partes;
    }

    // Para mostrar y copiar: separa sin validar y nunca falla (los clientes importados de
    // ClickFactura pueden traer correos escritos de cualquier forma; se respetan tal cual).
    public static IReadOnlyList<string> Separar(string? correos) =>
        string.IsNullOrWhiteSpace(correos)
            ? []
            : Regex
                .Split(correos.Trim(), @"\s*[,;\r\n]+\s*", RegexOptions.CultureInvariant)
                .Where(parte => parte.Length > 0)
                .ToArray();

    public static string NormalizarParaGuardar(string correos) =>
        string.Join("; ", Normalizar(correos));

    public static bool EsValido(string correo) =>
        !string.IsNullOrWhiteSpace(correo) && PatronCorreo().IsMatch(correo);
}
