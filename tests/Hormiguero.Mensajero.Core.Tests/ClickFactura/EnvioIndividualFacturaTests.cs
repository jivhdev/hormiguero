using Hormiguero.Mensajero.Core.ClickFactura;

namespace Hormiguero.Mensajero.Core.Tests.ClickFactura;

public sealed class EnvioIndividualFacturaTests
{
    [Fact]
    public void Filtrar_documentos_cliente_no_depende_de_un_periodo()
    {
        DocumentoFactura[] documentos =
        [
            new("FCV", "1", "76.000.000-1"),
            new("NCV", "2", "77000000-2"),
            new("FCV", "3", "76000000-1"),
        ];

        Assert.Equal(
            [documentos[0], documentos[2]],
            AnalizadorFactura.FiltrarDocumentosCliente(documentos, "76000000-1")
        );
    }

    [Fact]
    public void Envio_individual_nombra_los_documentos_y_no_una_semana()
    {
        var mensaje = GeneradorCorreoFactura.GenerarIndividual(
            "Cliente Uno",
            [new("FCV", "25378", "x"), new("FCV", "25390", "x"), new("NCV", "120", "x")]
        );

        Assert.Equal("FACTURAS Y NOTAS DE CRÉDITO N° 25378, 25390 Y 120", mensaje.Asunto);
        Assert.Contains(
            "facturas y notas de crédito N° 25378, 25390 y 120 para Cliente Uno",
            mensaje.Cuerpo
        );
        Assert.DoesNotContain("SEMANA", mensaje.Asunto);
    }
}
