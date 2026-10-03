using Hormiguero.Nucleo.Pdf;
using Xunit;

namespace Hormiguero.Nucleo.Tests;

public class ImpresionPdfTests
{
    [Theory]
    [InlineData(1, 2, new int[] { 0 })]
    [InlineData(5, 2, new int[] { 0, 1 })]
    [InlineData(0, 2, new int[] { })]
    public void Calcula_paginas_a_imprimir(int total, int cuantas, int[] esperado)
    {
        var resultado = ImpresionPdf.PaginasAImprimir(total, cuantas);
        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void Impresora_inexistente_falla_claro()
    {
        byte[] pdf = PdfDePrueba.ConTexto();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ImpresionPdf.Imprimir(pdf, 1, "ImpresoraQueNoExiste_12345")
        );
        Assert.Contains("impresora", ex.Message.ToLowerInvariant());
    }
}
