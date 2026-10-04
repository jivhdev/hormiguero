using System.IO;
using Hormiguero.Nucleo.Pdf;

namespace Archivero.Servicios.Pdf;

public record PaginaRenderizada(byte[] PixelesBgra, int Ancho, int Alto);

public record RectanguloFraccion(double X, double Y, double Ancho, double Alto);

public static class LectorPdf
{
    public static int ContarPaginas(string rutaPdf)
    {
        return DibujoPdf.Paginas(LeerBytes(rutaPdf));
    }

    public static PaginaRenderizada RenderizarPagina(string rutaPdf, int numeroPagina)
    {
        ImagenPagina pagina = DibujoPdf.Dibujar(LeerBytes(rutaPdf), numeroPagina, 150.0 / 96.0);
        return new PaginaRenderizada(pagina.PixelesBgra, pagina.Ancho, pagina.Alto);
    }

    public static bool TieneTextoExtraible(string rutaPdf)
    {
        InfoPdf info = LeerInfo(rutaPdf);
        if (info.Estado != EstadoPdf.Correcto)
        {
            throw new InvalidDataException("El PDF no se pudo leer.");
        }

        return info.Palabras.Count > 0;
    }

    public static string ExtraerTexto(
        string rutaPdf,
        int numeroPagina,
        RectanguloFraccion rectangulo
    )
    {
        InfoPdf info = LeerInfo(rutaPdf);
        return ZonaPdf
            .TextoEnFraccion(
                info,
                numeroPagina,
                rectangulo.X,
                rectangulo.Y,
                rectangulo.Ancho,
                rectangulo.Alto
            )
            .Trim();
    }

    private static InfoPdf LeerInfo(string rutaPdf)
    {
        using var contenido = new MemoryStream(LeerBytes(rutaPdf));
        return Hormiguero.Nucleo.Pdf.LectorPdf.Leer(contenido);
    }

    private static byte[] LeerBytes(string rutaPdf)
    {
        using var archivo = new FileStream(
            rutaPdf,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete
        );
        using var contenido = new MemoryStream();
        archivo.CopyTo(contenido);
        return contenido.ToArray();
    }
}
