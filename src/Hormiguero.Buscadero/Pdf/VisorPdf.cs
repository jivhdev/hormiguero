using System.IO;
using System.Windows.Media.Imaging;
using Buscadero.Core.Pdf;

namespace Buscadero.App.Pdf;

public static class VisorPdf
{
    public static int ObtenerTotalPaginas(string ruta) => RenderizadorPdf.ObtenerTotalPaginas(ruta);

    public static BitmapSource RenderizarPagina(string ruta, int indicePagina)
    {
        var pagina = RenderizadorPdf.RenderizarPagina(ruta, indicePagina);

        using var memoria = new MemoryStream(pagina.Png);
        var imagen = new BitmapImage();
        imagen.BeginInit();
        imagen.CacheOption = BitmapCacheOption.OnLoad;
        imagen.StreamSource = memoria;
        imagen.EndInit();
        imagen.Freeze();
        return imagen;
    }
}
