namespace Hormiguero.Mensajero.Core.ClickFactura;

public sealed record DocumentoFactura(string Tipo, string Numero, string Entidad);

public sealed record ClienteFactura(string Rut, string RazonSocial, string Correo);

public sealed record ClienteFacturaGestion(
    string Rut,
    string RazonSocial,
    string Correo,
    bool Activo
);

public sealed record DocumentoAnalizado(
    string Tipo,
    string Numero,
    string Entidad,
    string? RutaPdf,
    string? Error = null
);

public sealed record MensajeFactura(string Asunto, string Cuerpo);

public sealed record ClienteAnalizado(
    string Rut,
    string RazonSocial,
    string Correo,
    IReadOnlyList<DocumentoAnalizado> Documentos,
    IReadOnlyList<DocumentoAnalizado> PdfsEncontrados,
    IReadOnlyList<DocumentoAnalizado> PdfsFaltantes,
    MensajeFactura Mensaje
);

public sealed record ResultadoAnalisis(
    IReadOnlyList<ClienteAnalizado> ClientesProcesados,
    IReadOnlyList<string> RutsNoRegistrados,
    int TotalDocumentos,
    int TotalEncontrados,
    int TotalFaltantes,
    string? Error = null
);
