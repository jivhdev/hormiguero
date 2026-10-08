using Buscadero.Core.Pdf;
using Hormiguero.Nucleo.Pdf;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Buscadero.Core.Tests;

public sealed class SeleccionTextoPdfTests
{
    [Fact]
    public void Seleccionar_rectangulo_devuelve_palabras_y_texto_en_orden_de_lectura()
    {
        using var constructor = new PdfDocumentBuilder();
        var fuente = constructor.AddStandard14Font(Standard14Font.Helvetica);
        var pagina = constructor.AddPage(595, 842);
        pagina.AddText("Alfa Beta Gamma", 12, new PdfPoint(50, 750), fuente);
        pagina.AddText("Delta Epsilon Zeta", 12, new PdfPoint(50, 700), fuente);
        using var contenido = new MemoryStream(constructor.Build());
        var info = LectorPdf.Leer(contenido);

        var resultado = SeleccionTextoPdf.Seleccionar(info, 0, 0.05, 0.07, 0.85, 0.12);

        Assert.Equal("Alfa Beta Gamma Delta Epsilon Zeta", resultado.Texto);
        Assert.Equal(
            ["Alfa", "Beta", "Gamma", "Delta", "Epsilon", "Zeta"],
            resultado.Palabras.Select(palabra => palabra.Texto)
        );
    }

    [Fact]
    public void Seleccionar_rectangulo_parcial_solo_incluye_palabras_cubiertas()
    {
        using var constructor = new PdfDocumentBuilder();
        var fuente = constructor.AddStandard14Font(Standard14Font.Helvetica);
        var pagina = constructor.AddPage(595, 842);
        pagina.AddText("Alfa Beta Gamma", 12, new PdfPoint(50, 750), fuente);
        using var contenido = new MemoryStream(constructor.Build());
        var info = LectorPdf.Leer(contenido);

        var resultado = SeleccionTextoPdf.Seleccionar(info, 0, 0.07, 0.07, 0.11, 0.04);

        Assert.Equal("Alfa Beta", resultado.Texto);
    }
}
