using PDFtoImage;
using SkiaSharp;

namespace Buscadero.Core.Pdf;

public static class RenderizadorPdf
{
    public const int DpiBase = 150;

    public static int ObtenerTotalPaginas(string ruta)
    {
        using var pdf = File.Open(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Conversion.GetPageCount(pdf);
    }

    public static PaginaRenderizada RenderizarPagina(string ruta, int indicePagina)
    {
        using var pdf = File.Open(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var mapaBits = Conversion.ToImage(
            pdf,
            page: indicePagina,
            options: new RenderOptions(Dpi: DpiBase)
        );
        using var imagen = SKImage.FromBitmap(mapaBits);
        using var datos = imagen.Encode(SKEncodedImageFormat.Png, 90);

        return new PaginaRenderizada
        {
            Png = datos.ToArray(),
            Ancho = mapaBits.Width,
            Alto = mapaBits.Height,
        };
    }
}
