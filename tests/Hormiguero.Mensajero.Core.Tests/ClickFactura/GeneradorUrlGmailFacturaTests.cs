using Hormiguero.Mensajero.Core.ClickFactura;

namespace Hormiguero.Mensajero.Core.Tests.ClickFactura;

public sealed class GeneradorUrlGmailFacturaTests
{
    [Fact]
    public void Generar_EscapaTextoYConservaVariosDestinatarios()
    {
        UrlGmailFactura resultado = GeneradorUrlGmailFactura.Generar(
            ["uno@ejemplo.cl", "dos@ejemplo.cl"],
            "Facturación ñ & #",
            "Línea uno\nLínea dos"
        );

        Assert.False(resultado.OmitioCuerpo);
        Assert.Contains("to=uno%40ejemplo.cl%2Cdos%40ejemplo.cl", resultado.Url);
        Assert.Contains("su=Facturaci%C3%B3n%20%C3%B1%20%26%20%23", resultado.Url);
        Assert.Contains("body=L%C3%ADnea%20uno%0AL%C3%ADnea%20dos", resultado.Url);
    }

    [Fact]
    public void Generar_AgregaCuentaOpcionalEscapada()
    {
        UrlGmailFactura resultado = GeneradorUrlGmailFactura.Generar(
            ["uno@ejemplo.cl"],
            "Asunto",
            "Cuerpo",
            "javier+facturas@ejemplo.cl"
        );

        Assert.Contains("&authuser=javier%2Bfacturas%40ejemplo.cl", resultado.Url);
    }

    [Fact]
    public void Generar_OmiteCuerpoSiUrlSuperaLimite()
    {
        UrlGmailFactura resultado = GeneradorUrlGmailFactura.Generar(
            ["uno@ejemplo.cl"],
            "Asunto",
            new string('á', 600)
        );

        Assert.True(resultado.OmitioCuerpo);
        Assert.DoesNotContain("body=", resultado.Url);
        Assert.Contains("to=uno%40ejemplo.cl", resultado.Url);
        Assert.Contains("su=Asunto", resultado.Url);
    }
}
