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
