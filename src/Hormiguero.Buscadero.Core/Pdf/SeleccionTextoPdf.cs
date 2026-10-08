using Hormiguero.Nucleo.Pdf;

namespace Buscadero.Core.Pdf;

public sealed record ResultadoSeleccionTexto(IReadOnlyList<PalabraPdf> Palabras, string Texto);

public static class SeleccionTextoPdf
{
    public static ResultadoSeleccionTexto Seleccionar(
        InfoPdf info,
        int pagina,
        double x,
        double y,
        double ancho,
        double alto
    )
    {
        ArgumentNullException.ThrowIfNull(info);
        if (pagina < 0 || pagina >= info.TamanosPagina.Count || ancho <= 0 || alto <= 0)
        {
            return new([], string.Empty);
        }

        var (anchoPagina, altoPagina) = info.TamanosPagina[pagina];
        double izquierda = x * anchoPagina;
        double abajo = (1 - y - alto) * altoPagina;
        double derecha = izquierda + ancho * anchoPagina;
        double arriba = abajo + alto * altoPagina;
        var palabras = info
            .Palabras.Where(palabra => palabra.Pagina == pagina + 1)
            .Where(palabra =>
            {
                double centroX = palabra.X + palabra.Ancho / 2;
                double centroY = palabra.Y + palabra.Alto / 2;
                return centroX >= izquierda
                    && centroX <= derecha
                    && centroY >= abajo
                    && centroY <= arriba;
            })
            .OrderByDescending(palabra => palabra.Y + palabra.Alto / 2)
            .ThenBy(palabra => palabra.X + palabra.Ancho / 2)
            .ToList();

        var lineas = new List<List<PalabraPdf>>();
        foreach (PalabraPdf palabra in palabras)
        {
            if (lineas.Count == 0)
            {
                lineas.Add([palabra]);
                continue;
            }
            List<PalabraPdf> lineaActual = lineas[^1];
            PalabraPdf anterior = lineaActual[^1];
            double centroActual = palabra.Y + palabra.Alto / 2;
            double centroAnterior = anterior.Y + anterior.Alto / 2;
            double alturaMasBaja = Math.Min(palabra.Alto, anterior.Alto);
            if (Math.Abs(centroActual - centroAnterior) < alturaMasBaja / 2)
                lineaActual.Add(palabra);
            else
                lineas.Add([palabra]);
        }
        palabras = lineas
            .SelectMany(linea => linea.OrderBy(palabra => palabra.X + palabra.Ancho / 2))
            .ToList();

        return new(palabras, ZonaPdf.TextoEnFraccion(info, pagina, x, y, ancho, alto));
    }
}
