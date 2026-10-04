using Docnet.Core;
using Docnet.Core.Models;

namespace Archivero.Servicios.Pdf;

public record PaginaRenderizada(byte[] PixelesBgra, int Ancho, int Alto);

public record RectanguloFraccion(double X, double Y, double Ancho, double Alto);

public static class LectorPdf
{
    private const int AnchoLienzo = 1240;
    private const int AltoLienzo = 1754;

    public static int ContarPaginas(string rutaPdf)
    {
        using var docReader = DocLib.Instance.GetDocReader(rutaPdf, new PageDimensions(AnchoLienzo, AltoLienzo));
        return docReader.GetPageCount();
    }

    public static PaginaRenderizada RenderizarPagina(string rutaPdf, int numeroPagina)
    {
        using var docReader = DocLib.Instance.GetDocReader(rutaPdf, new PageDimensions(AnchoLienzo, AltoLienzo));
        using var pageReader = docReader.GetPageReader(numeroPagina);

        return new PaginaRenderizada(
            pageReader.GetImage(),
            pageReader.GetPageWidth(),
            pageReader.GetPageHeight());
    }

    public static bool TieneTextoExtraible(string rutaPdf)
    {
        using var docReader = DocLib.Instance.GetDocReader(rutaPdf, new PageDimensions(AnchoLienzo, AltoLienzo));
        for (var i = 0; i < docReader.GetPageCount(); i++)
        {
            using var pageReader = docReader.GetPageReader(i);
            if (!string.IsNullOrWhiteSpace(pageReader.GetText()))
            {
                return true;
            }
        }

        return false;
    }

    public static string ExtraerTexto(string rutaPdf, int numeroPagina, RectanguloFraccion rectangulo)
    {
        using var docReader = DocLib.Instance.GetDocReader(rutaPdf, new PageDimensions(AnchoLienzo, AltoLienzo));
        using var pageReader = docReader.GetPageReader(numeroPagina);

        return ExtraerTextoDePagina(pageReader.GetCharacters(), pageReader.GetPageWidth(), pageReader.GetPageHeight(), rectangulo);
    }

    internal static string ExtraerTextoDePagina(
        IEnumerable<Docnet.Core.Models.Character> caracteres,
        int anchoPagina,
        int altoPagina,
        RectanguloFraccion rectangulo)
    {
        var izquierda = rectangulo.X * anchoPagina;
        var arriba = rectangulo.Y * altoPagina;
        var derecha = (rectangulo.X + rectangulo.Ancho) * anchoPagina;
        var abajo = (rectangulo.Y + rectangulo.Alto) * altoPagina;

        var texto = new System.Text.StringBuilder();

        foreach (var caracter in caracteres)
        {
            var centroX = (caracter.Box.Left + caracter.Box.Right) / 2.0;
            var centroY = (caracter.Box.Top + caracter.Box.Bottom) / 2.0;

            if (centroX >= izquierda && centroX <= derecha && centroY >= arriba && centroY <= abajo)
            {
                texto.Append(caracter.Char);
            }
        }

        return texto.ToString().Trim();
    }
}
