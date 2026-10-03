using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Hormiguero.Nucleo.Tests;

public static class PdfDePrueba
{
    public static byte[] ConTexto()
    {
        using var constructor = new PdfDocumentBuilder();
        PdfDocumentBuilder.AddedFont fuente = constructor.AddStandard14Font(
            Standard14Font.Helvetica
        );
        PdfPageBuilder pagina = constructor.AddPage(595, 842);
        pagina.AddText(
            "Orden de compra OCC 104523 recibida conforme",
            12,
            new PdfPoint(50, 750),
            fuente
        );

        return constructor.Build();
    }

    public static byte[] SinTexto()
    {
        using var constructor = new PdfDocumentBuilder();
        for (int i = 0; i < 2; i++)
        {
            PdfPageBuilder pagina = constructor.AddPage(595, 842);
            pagina.DrawRectangle(new PdfPoint(100, 400), 200, 100, 1, false);
        }

        return constructor.Build();
    }

    public static byte[] Truncado()
    {
        byte[] completo = ConTexto();

        return completo[..(completo.Length / 2)];
    }
}
