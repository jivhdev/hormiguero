using PDFtoImage;
using SkiaSharp;

namespace Hormiguero.Nucleo.Pdf;

// Píxeles en BGRA de 32 bits, para que cualquier interfaz (WPF)
// muestre la imagen sin depender de SkiaSharp.
public record ImagenPagina(int Ancho, int Alto, byte[] PixelesBgra);

public static class DibujoPdf
{
    private static readonly object candado = new();

    public static int Paginas(byte[] pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        lock (candado)
        {
            return Conversion.GetPageCount(pdf);
        }
    }

    public static ImagenPagina Dibujar(byte[] pdf, int pagina, double zoom)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentOutOfRangeException.ThrowIfNegative(pagina);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoom);

        // PDFium no admite llamadas simultáneas: se atienden de a una.
        lock (candado)
        {
            int paginas = Conversion.GetPageCount(pdf);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pagina, paginas);

            using SKBitmap dibujado = Conversion.ToImage(
                pdf,
                pagina,
                options: new RenderOptions(Dpi: (int)Math.Round(96 * zoom))
            );
            // PDFtoImage podría cambiar su formato de píxeles: la copia
            // garantiza el BGRA de 32 bits que promete ImagenPagina.
            using SKBitmap copia =
                dibujado.Copy(SKColorType.Bgra8888)
                ?? throw new InvalidOperationException("La página no se pudo convertir a BGRA.");
            return new ImagenPagina(copia.Width, copia.Height, copia.Bytes);
        }
    }
}
