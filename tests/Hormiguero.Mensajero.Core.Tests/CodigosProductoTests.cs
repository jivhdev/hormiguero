using Hormiguero.Mensajero.Core;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Hormiguero.Mensajero.Core.Tests;

public sealed class CodigosProductoTests
{
    [Fact]
    public void Lee_todas_las_paginas_en_orden_y_marca_repetidos()
    {
        using var pdf = new MemoryStream(CrearPdf());

        IReadOnlyList<CodigoProducto> resultado = CodigosProducto.LeerPdf(pdf);

        Assert.Equal(["90001", "20490", "80536", "90001"], resultado.Select(item => item.Codigo));
        Assert.Equal([false, false, false, true], resultado.Select(item => item.Repetido));
    }

    [Fact]
    public void Omite_precios_fechas_telefonos_y_rut_en_filas_con_otras_cifras()
    {
        IReadOnlyList<CodigoProducto> resultado = CodigosProducto.AnalizarTexto(
            "20490 2 12.345 06-10-2026 9 9275 6239 12345678-5 80536"
        );

        Assert.Equal(["20490", "80536"], resultado.Select(item => item.Codigo));
    }

    [Fact]
    public void Usa_el_largo_configurado_y_rechaza_un_largo_invalido()
    {
        IReadOnlyList<CodigoProducto> resultado = CodigosProducto.AnalizarTexto(
            "1234 12345 123456",
            4
        );

        Assert.Equal(["1234"], resultado.Select(item => item.Codigo));
        Assert.Throws<ArgumentOutOfRangeException>(() => CodigosProducto.AnalizarTexto("123", 0));
    }

    [Fact]
    public void Rechaza_pdf_danado_con_un_error_visible()
    {
        using var pdf = new MemoryStream([1, 2, 3, 4]);

        Assert.Throws<InvalidDataException>(() => CodigosProducto.LeerPdf(pdf));
    }

    private static byte[] CrearPdf()
    {
        using var constructor = new PdfDocumentBuilder();
        PdfDocumentBuilder.AddedFont fuente = constructor.AddStandard14Font(
            Standard14Font.Helvetica
        );
        PdfPageBuilder primera = constructor.AddPage(595, 842);
        primera.AddText("90001", 12, new PdfPoint(50, 750), fuente);
        primera.AddText(
            "20490 2 12.345 06-10-2026 9 9275 6239 12345678-5 80536",
            12,
            new PdfPoint(50, 720),
            fuente
        );
        PdfPageBuilder segunda = constructor.AddPage(595, 842);
        segunda.AddText("90001", 12, new PdfPoint(50, 750), fuente);
        return constructor.Build();
    }

    [Fact]
    public void Un_numero_de_fila_9_no_convierte_el_codigo_siguiente_en_telefono()
    {
        var codigos = CodigosProducto.AnalizarTexto(
            "8\n80164\n650\n$249\n9\n80163\n950\n$149\n10\n80230\n230"
        );

        Assert.Equal(["80164", "80163", "80230"], codigos.Select(c => c.Codigo));
    }

    [Fact]
    public void Fila_codigo_y_cantidad_en_la_misma_linea_no_son_telefono()
    {
        var codigos = CodigosProducto.AnalizarTexto(
            "9 80163 950 $149\n9.685 80230 CODO\nFono 9 9275 6239"
        );

        Assert.Equal(["80163", "80230"], codigos.Select(c => c.Codigo));
    }
}
