namespace Hormiguero.Mensajero.Core.ClickFactura;

public static class GeneradorCorreoFactura
{
    public static string GenerarAsunto(
        string descripcionSemana,
        IReadOnlyList<DocumentoFactura> documentos
    )
    {
        bool tieneFactura = documentos.Any(documento => documento.Tipo == "FCV");
        bool tieneNota = documentos.Any(documento => documento.Tipo == "NCV");
        string tipo =
            tieneFactura && tieneNota ? "FACTURAS Y NOTAS DE CRÉDITO"
            : tieneNota ? "NOTAS DE CRÉDITO"
            : "FACTURAS";
        return $"{tipo} {descripcionSemana.ToUpperInvariant()}";
    }

    // Envío a un solo cliente (sin semana): el asunto y el cuerpo nombran los documentos.
    public static MensajeFactura GenerarIndividual(
        string razonSocial,
        IReadOnlyList<DocumentoFactura> documentos
    )
    {
        bool tieneFactura = documentos.Any(documento => documento.Tipo == "FCV");
        bool tieneNota = documentos.Any(documento => documento.Tipo == "NCV");
        string tipo =
            tieneFactura && tieneNota ? "facturas y notas de crédito"
            : tieneNota ? "notas de crédito"
            : "facturas";
        string numeros = UnirConY(
            documentos.Select(documento => documento.Numero).Distinct().ToList()
        );
        return new(
            $"{tipo.ToUpperInvariant()} N° {numeros.ToUpperInvariant()}",
            $"Estimados,\n\nAdjunto las {tipo} N° {numeros} para {razonSocial}.\n\nQuedo atento a cualquier consulta.\n\nSaludos cordiales."
        );
    }

    private static string UnirConY(IReadOnlyList<string> valores) =>
        valores.Count <= 1
            ? string.Join("", valores)
            : $"{string.Join(", ", valores.Take(valores.Count - 1))} y {valores[^1]}";

    public static string GenerarCuerpo(
        string razonSocial,
        string descripcionSemana,
        IReadOnlyList<DocumentoFactura> documentos
    )
    {
        bool tieneFactura = documentos.Any(documento => documento.Tipo == "FCV");
        bool tieneNota = documentos.Any(documento => documento.Tipo == "NCV");
        string tipo =
            tieneFactura && tieneNota ? "facturas y notas de crédito"
            : tieneNota ? "notas de crédito"
            : "facturas";
        return $"Estimados,\n\nAdjunto las {tipo} correspondientes a la {descripcionSemana} para {razonSocial}.\n\nQuedo atento a cualquier consulta.\n\nSaludos cordiales.";
    }

    public static MensajeFactura Generar(
        string razonSocial,
        string descripcionSemana,
        IReadOnlyList<DocumentoFactura> documentos
    ) =>
        new(
            GenerarAsunto(descripcionSemana, documentos),
            GenerarCuerpo(razonSocial, descripcionSemana, documentos)
        );
}
