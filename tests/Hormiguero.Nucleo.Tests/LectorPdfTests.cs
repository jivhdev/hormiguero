using Hormiguero.Nucleo.Pdf;

namespace Hormiguero.Nucleo.Tests;

public class LectorPdfTests
{
    [Fact]
    public void Lee_palabras_con_coordenadas()
    {
        using var contenido = new MemoryStream(PdfDePrueba.ConTexto());

        InfoPdf info = LectorPdf.Leer(contenido);

        Assert.Equal(EstadoPdf.Correcto, info.Estado);
        Assert.Equal(1, info.Paginas);
        Assert.Equal([true], info.PaginaTieneTexto);
        Assert.True(info.TieneTexto);
        PalabraPdf numero = Assert.Single(info.Palabras, palabra => palabra.Texto == "104523");
        Assert.Equal(1, numero.Pagina);
        Assert.True(numero.X > 0 && numero.Y > 0);
        Assert.True(numero.Ancho > 0 && numero.Alto > 0);
    }

    [Fact]
    public void Detecta_pdf_sin_texto()
    {
        using var contenido = new MemoryStream(PdfDePrueba.SinTexto());

        InfoPdf info = LectorPdf.Leer(contenido);

        Assert.Equal(EstadoPdf.Correcto, info.Estado);
        Assert.Equal(2, info.Paginas);
        Assert.Equal([false, false], info.PaginaTieneTexto);
        Assert.False(info.TieneTexto);
        Assert.Empty(info.Palabras);
    }

    [Fact]
    public void Pdf_truncado_es_danado()
    {
        using var contenido = new MemoryStream(PdfDePrueba.Truncado());

        InfoPdf info = LectorPdf.Leer(contenido);

        Assert.Equal(EstadoPdf.Danado, info.Estado);
        Assert.Equal(0, info.Paginas);
        Assert.False(info.TieneTexto);
    }

    [Fact]
    public void Stream_vacio_es_danado()
    {
        using var contenido = new MemoryStream();

        InfoPdf info = LectorPdf.Leer(contenido);

        Assert.Equal(EstadoPdf.Danado, info.Estado);
        Assert.Equal(0, info.Paginas);
        Assert.False(info.TieneTexto);
    }
}
