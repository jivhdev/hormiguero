namespace Hormiguero.Nucleo.Pdf;

// Rectángulo en puntos PDF, con origen abajo a la izquierda (como PdfPig).
// X e Y son la esquina inferior izquierda de la zona.
public record Zona(int Pagina, double X, double Y, double Ancho, double Alto);

public static class ZonaPdf
{
    public static string TextoEnFraccion(
        InfoPdf info,
        int paginaDesdeCero,
        double x,
        double y,
        double ancho,
        double alto
    )
    {
        ArgumentNullException.ThrowIfNull(info);
        // Página inexistente o zona sin tamaño (un clic sin arrastrar): sin texto, como
        // hacía Archivero con su librería anterior.
        if (
            paginaDesdeCero < 0
            || paginaDesdeCero >= info.TamanosPagina.Count
            || ancho <= 0
            || alto <= 0
        )
        {
            return "";
        }

        (double anchoPagina, double altoPagina) = info.TamanosPagina[paginaDesdeCero];
        return Texto(
            info,
            new Zona(
                paginaDesdeCero + 1,
                x * anchoPagina,
                (1 - y - alto) * altoPagina,
                ancho * anchoPagina,
                alto * altoPagina
            )
        );
    }

    public static string Texto(InfoPdf info, Zona zona)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(zona);
        if (zona.Ancho <= 0 || zona.Alto <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zona));
        }

        var dentro = info
            .Palabras.Where(palabra => palabra.Pagina == zona.Pagina)
            .Where(palabra =>
            {
                double centroX = palabra.X + palabra.Ancho / 2;
                double centroY = palabra.Y + palabra.Alto / 2;
                return (
                    centroX >= zona.X
                    && centroX <= zona.X + zona.Ancho
                    && centroY >= zona.Y
                    && centroY <= zona.Y + zona.Alto
                );
            })
            .OrderByDescending(palabra => palabra.Y + palabra.Alto / 2)
            .ThenBy(palabra => palabra.X + palabra.Ancho / 2)
            .ToList();

        if (dentro.Count == 0)
        {
            return "";
        }

        var lineas = new List<List<PalabraPdf>> { new() { dentro[0] } };
        foreach (PalabraPdf palabra in dentro.Skip(1))
        {
            List<PalabraPdf> lineaActual = lineas[^1];
            PalabraPdf anterior = lineaActual[^1];
            double centroActual = palabra.Y + palabra.Alto / 2;
            double centroAnterior = anterior.Y + anterior.Alto / 2;
            double alturaMasBaja = Math.Min(palabra.Alto, anterior.Alto);
            if (Math.Abs(centroActual - centroAnterior) < alturaMasBaja / 2)
            {
                lineaActual.Add(palabra);
            }
            else
            {
                lineas.Add([palabra]);
            }
        }

        var lineasOrdenadas = lineas
            .Select(linea => linea.OrderBy(palabra => palabra.X + palabra.Ancho / 2).ToList())
            .Select(linea => string.Join(" ", linea.Select(palabra => palabra.Texto)));
        return string.Join(" ", lineasOrdenadas);
    }
}
