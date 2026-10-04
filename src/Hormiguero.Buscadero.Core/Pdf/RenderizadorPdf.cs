using System.Runtime.InteropServices;
using Hormiguero.Nucleo.Pdf;
using SkiaSharp;

namespace Buscadero.Core.Pdf;

public static class RenderizadorPdf
{
    public const int DpiBase = 150;

    public static int ObtenerTotalPaginas(string ruta)
    {
        return DibujoPdf.Paginas(LeerArchivo(ruta));
    }

    public static PaginaRenderizada RenderizarPagina(string ruta, int indicePagina)
    {
        ImagenPagina imagen = DibujoPdf.Dibujar(LeerArchivo(ruta), indicePagina, DpiBase / 96.0);

        using var mapaBits = new SKBitmap(
            imagen.Ancho,
            imagen.Alto,
            SKColorType.Bgra8888,
            SKAlphaType.Premul
        );
        Marshal.Copy(imagen.PixelesBgra, 0, mapaBits.GetPixels(), imagen.PixelesBgra.Length);
        using var datos = mapaBits.Encode(SKEncodedImageFormat.Png, 90);

        return new PaginaRenderizada
        {
            Png = datos.ToArray(),
            Ancho = imagen.Ancho,
            Alto = imagen.Alto,
        };
    }

    private static byte[] LeerArchivo(string ruta)
    {
        using var pdf = File.Open(
            ruta,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete
        );
        using var memoria = new MemoryStream();
        pdf.CopyTo(memoria);
        return memoria.ToArray();
    }
}
