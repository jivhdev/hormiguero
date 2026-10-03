using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Exceptions;

namespace Hormiguero.Nucleo.Pdf;

public enum EstadoPdf
{
    Correcto,
    Danado,
    Protegido,
}

// X e Y son la esquina inferior izquierda de la palabra, en puntos PDF
// (origen abajo-izquierda), tal como los entrega PdfPig.
public record PalabraPdf(string Texto, int Pagina, double X, double Y, double Ancho, double Alto);

public record InfoPdf(
    EstadoPdf Estado,
    int Paginas,
    IReadOnlyList<bool> PaginaTieneTexto,
    IReadOnlyList<PalabraPdf> Palabras
)
{
    public bool TieneTexto => PaginaTieneTexto.Any(tiene => tiene);
}

public static class LectorPdf
{
    public static InfoPdf Leer(Stream contenido)
    {
        try
        {
            using var documento = PdfDocument.Open(contenido);

            var paginaTieneTexto = new List<bool>();
            var palabras = new List<PalabraPdf>();
            foreach (Page pagina in documento.GetPages())
            {
                List<Word> palabrasDeLaPagina = pagina.GetWords().ToList();
                paginaTieneTexto.Add(
                    palabrasDeLaPagina.Sum(palabra => palabra.Text.Count(char.IsLetterOrDigit)) > 20
                );
                foreach (Word palabra in palabrasDeLaPagina)
                {
                    palabras.Add(
                        new PalabraPdf(
                            palabra.Text,
                            pagina.Number,
                            palabra.BoundingBox.Left,
                            palabra.BoundingBox.Bottom,
                            palabra.BoundingBox.Width,
                            palabra.BoundingBox.Height
                        )
                    );
                }
            }

            return new InfoPdf(
                EstadoPdf.Correcto,
                documento.NumberOfPages,
                paginaTieneTexto,
                palabras
            );
        }
        catch (PdfDocumentEncryptedException)
        {
            return new InfoPdf(EstadoPdf.Protegido, 0, [], []);
        }
        catch
        {
            return new InfoPdf(EstadoPdf.Danado, 0, [], []);
        }
    }
}
