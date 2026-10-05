using System.Text.RegularExpressions;

namespace Hormiguero.Mensajero.Core.ClickFactura;

public static partial class RutFactura
{
    [GeneratedRegex(@"[^\dkK-]", RegexOptions.CultureInvariant)]
    private static partial Regex CaracteresNoPermitidos();

    [GeneratedRegex(@"^\d{7,8}-[\dK]$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoValido();

    public static string Limpiar(string? rut)
    {
        if (string.IsNullOrEmpty(rut))
            return "";

        string limpio = CaracteresNoPermitidos().Replace(rut.Trim(), "");
        int separador = limpio.LastIndexOf('-');
        string cuerpo =
            separador >= 0 ? limpio[..separador] : limpio[..Math.Max(0, limpio.Length - 1)];
        string digito = separador >= 0 ? limpio[(separador + 1)..] : limpio[^1..];
        return $"{cuerpo}-{digito.ToUpperInvariant()}";
    }

    public static string NormalizarSeguro(string? rut)
    {
        if (string.IsNullOrWhiteSpace(rut))
            return "";

        try
        {
            return Limpiar(rut);
        }
        catch (ArgumentOutOfRangeException)
        {
            return "";
        }
    }

    public static bool Validar(string rutLimpio)
    {
        if (!FormatoValido().IsMatch(rutLimpio))
            return false;

        string[] partes = rutLimpio.Split('-');
        int suma = 0;
        int multiplicador = 2;
        foreach (char digito in partes[0].Reverse())
        {
            suma += (digito - '0') * multiplicador;
            multiplicador = multiplicador == 7 ? 9 : multiplicador + 1;
        }

        int verificador = 11 - (suma % 11);
        string calculado = verificador switch
        {
            11 => "0",
            10 => "K",
            _ => verificador.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        return partes[1] == calculado;
    }
}
