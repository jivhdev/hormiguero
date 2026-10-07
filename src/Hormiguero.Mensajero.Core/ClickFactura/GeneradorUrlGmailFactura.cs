namespace Hormiguero.Mensajero.Core.ClickFactura;

public sealed record UrlGmailFactura(string Url, bool OmitioCuerpo);

public static class GeneradorUrlGmailFactura
{
    private const int LargoMaximoConCuerpo = 1800;
    private const string BaseUrl = "https://mail.google.com/mail/?view=cm&fs=1";

    public static UrlGmailFactura Generar(
        IReadOnlyList<string> correos,
        string asunto,
        string cuerpo,
        string? cuenta = null
    )
    {
        ArgumentNullException.ThrowIfNull(correos);
        ArgumentNullException.ThrowIfNull(asunto);
        ArgumentNullException.ThrowIfNull(cuerpo);

        string parametros =
            $"&to={Uri.EscapeDataString(string.Join(",", correos))}"
            + $"&su={Uri.EscapeDataString(asunto)}";
        if (!string.IsNullOrWhiteSpace(cuenta))
            parametros += $"&authuser={Uri.EscapeDataString(cuenta.Trim())}";

        string conCuerpo = $"{BaseUrl}{parametros}&body={Uri.EscapeDataString(cuerpo)}";
        if (conCuerpo.Length <= LargoMaximoConCuerpo)
            return new UrlGmailFactura(conCuerpo, false);

        return new UrlGmailFactura($"{BaseUrl}{parametros}", true);
    }
}
