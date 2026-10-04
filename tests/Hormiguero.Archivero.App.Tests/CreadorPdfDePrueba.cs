using System.Text;

namespace Archivero.Tests;

internal static class CreadorPdfDePrueba
{
    /// <summary>
    /// Crea un PDF mínimo (una página, A4) con dos líneas de texto en posiciones bien
    /// separadas: una cerca del borde superior (banda Y 0-0.3) y otra cerca del borde
    /// inferior (banda Y 0.8-1.0).
    /// </summary>
    public static string Crear(string carpetaDestino, string lineaSuperior, string lineaInferior) =>
        ConstruirPdf(carpetaDestino, [(lineaSuperior, 750), (lineaInferior, 100)]);

    /// <summary>
    /// Crea un PDF mínimo (una página, A4) con varias líneas de texto, cada una en su propia
    /// banda vertical (ver ObtenerBandaDeLinea), bien separadas entre sí.
    /// </summary>
    public static string CrearConLineas(string carpetaDestino, params string[] lineas) =>
        ConstruirPdf(carpetaDestino, lineas.Select((linea, indice) => (linea, 780 - indice * 160)).ToArray());

    /// <summary>
    /// Rectángulo (fracción de página) que cubre con margen la línea de índice <paramref name="indice"/>
    /// generada por CrearConLineas, sin superponerse con las líneas vecinas.
    /// </summary>
    public static Archivero.Servicios.Pdf.RectanguloFraccion ObtenerBandaDeLinea(int indice) =>
        new(0, 0.02 + indice * 0.19, 1, 0.15);

    private static string ConstruirPdf(string carpetaDestino, (string Texto, int Y)[] lineas)
    {
        var ruta = Path.Combine(carpetaDestino, $"prueba_{Guid.NewGuid():N}.pdf");

        var contenido = string.Concat(lineas.Select(l =>
            $"BT /F1 24 Tf 50 {l.Y} Td ({Escapar(l.Texto)}) Tj ET\n"));
        var contenidoBytes = Encoding.ASCII.GetBytes(contenido);

        using var stream = new MemoryStream();
        var offsets = new long[6];

        void Escribir(string texto) => stream.Write(Encoding.ASCII.GetBytes(texto));

        Escribir("%PDF-1.4\n");

        offsets[1] = stream.Position;
        Escribir("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        offsets[2] = stream.Position;
        Escribir("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        offsets[3] = stream.Position;
        Escribir("3 0 obj\n<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 4 0 R >> >> /MediaBox [0 0 595 842] /Contents 5 0 R >>\nendobj\n");

        offsets[4] = stream.Position;
        Escribir("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

        offsets[5] = stream.Position;
        Escribir($"5 0 obj\n<< /Length {contenidoBytes.Length} >>\nstream\n");
        stream.Write(contenidoBytes, 0, contenidoBytes.Length);
        Escribir("\nendstream\nendobj\n");

        var xrefOffset = stream.Position;
        Escribir("xref\n0 6\n0000000000 65535 f \n");
        for (var i = 1; i <= 5; i++)
        {
            Escribir($"{offsets[i]:D10} 00000 n \n");
        }

        Escribir($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");

        File.WriteAllBytes(ruta, stream.ToArray());
        return ruta;
    }

    private static string Escapar(string texto) =>
        texto.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
