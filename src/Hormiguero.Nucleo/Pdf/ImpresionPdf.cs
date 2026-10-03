using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using PDFtoImage;

namespace Hormiguero.Nucleo.Pdf;

public static class ImpresionPdf
{
    public static IReadOnlyList<int> PaginasAImprimir(int totalPaginas, int cuantas)
    {
        if (totalPaginas <= 0 || cuantas <= 0)
        {
            return Array.Empty<int>();
        }

        int aImprimir = cuantas;
        if (aImprimir > totalPaginas)
        {
            aImprimir = totalPaginas;
        }

        var paginas = new List<int>(aImprimir);
        for (int i = 0; i < aImprimir; i++)
        {
            paginas.Add(i);
        }

        return paginas;
    }

    public static void Imprimir(byte[] pdf, int cuantas, string? impresora)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        using var documento = new PrintDocument();
        if (!string.IsNullOrWhiteSpace(impresora))
        {
            bool existe = false;
            foreach (string nombre in PrinterSettings.InstalledPrinters)
            {
                if (string.Equals(nombre, impresora, StringComparison.OrdinalIgnoreCase))
                {
                    existe = true;
                    break;
                }
            }

            if (!existe)
            {
                throw new InvalidOperationException("La impresora no existe o no está instalada.");
            }

            documento.PrinterSettings.PrinterName = impresora;
        }

        int totalPaginas = Conversion.GetPageCount(pdf);
        var paginasAImprimir = PaginasAImprimir(totalPaginas, cuantas);
        if (paginasAImprimir.Count == 0)
        {
            return;
        }

        int indice = 0;
        documento.PrintPage += (sender, e) =>
        {
            if (indice >= paginasAImprimir.Count)
            {
                e.HasMorePages = false;
                return;
            }

            int pagina = paginasAImprimir[indice];
            // Se dibuja a la resolucion de la impresora (con tope de 300 ppp para no agotar memoria): a 96 ppp sale borroso.
            int ppp = Math.Clamp(e.PageSettings.PrinterResolution.X, 150, 300);
            var imagenPagina = DibujoPdf.Dibujar(pdf, pagina, ppp / 96.0);
            using var bitmap = new Bitmap(
                imagenPagina.Ancho,
                imagenPagina.Alto,
                PixelFormat.Format32bppArgb
            );
            var datos = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb
            );
            try
            {
                IntPtr scan0 = datos.Scan0;
                if (scan0 != IntPtr.Zero)
                {
                    System.Runtime.InteropServices.Marshal.Copy(
                        imagenPagina.PixelesBgra,
                        0,
                        scan0,
                        imagenPagina.PixelesBgra.Length
                    );
                }
            }
            finally
            {
                if (datos != null)
                {
                    bitmap.UnlockBits(datos);
                }
            }

            var rect = e.MarginBounds;
            float anchoImagen = bitmap.Width;
            float altoImagen = bitmap.Height;
            float anchoRect = rect.Width;
            float altoRect = rect.Height;
            float escala = Math.Min(anchoRect / anchoImagen, altoRect / altoImagen);
            if (escala <= 0)
            {
                escala = 1;
            }

            float anchoDibujado = anchoImagen * escala;
            float altoDibujado = altoImagen * escala;
            float x = rect.Left + (anchoRect - anchoDibujado) / 2;
            float y = rect.Top + (altoRect - altoDibujado) / 2;
            if (e.Graphics != null)
            {
                e.Graphics.DrawImage(bitmap, x, y, anchoDibujado, altoDibujado);
            }
            indice++;
            e.HasMorePages = indice < paginasAImprimir.Count;
        };

        documento.Print();
    }
}
