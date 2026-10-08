using Hormiguero.Mensajero.Core;

namespace Hormiguero.Mensajero.Core.ClickFactura;

public static class AnalizadorFactura
{
    public static IReadOnlyList<DocumentoFactura> FiltrarDocumentosCliente(
        IReadOnlyList<DocumentoFactura> documentos,
        string rut
    )
    {
        string rutNormalizado = RutFactura.NormalizarSeguro(rut);
        return documentos
            .Where(documento => RutFactura.NormalizarSeguro(documento.Entidad) == rutNormalizado)
            .ToArray();
    }

    public static ResultadoAnalisis Analizar(
        IReadOnlyList<DocumentoFactura> documentos,
        IReadOnlyDictionary<string, ClienteFactura> clientesPorRut,
        string descripcionSemana,
        Func<DocumentoFactura, string?> buscarPdf
    )
    {
        var clientesNormalizados = new Dictionary<string, ClienteFactura>(StringComparer.Ordinal);
        foreach (ClienteFactura cliente in clientesPorRut.Values)
        {
            string rut = RutFactura.NormalizarSeguro(cliente.Rut);
            if (rut.Length > 0)
                clientesNormalizados.TryAdd(rut, cliente);
        }

        var normalizados = documentos
            .Select(
                (documento, indice) =>
                {
                    DocumentoFactura seguro = documento is null
                        ? new DocumentoFactura("", "", "")
                        : new DocumentoFactura(
                            documento.Tipo ?? "",
                            documento.Numero ?? "",
                            documento.Entidad ?? ""
                        );
                    string rut = RutFactura.NormalizarSeguro(seguro.Entidad);
                    string? error =
                        rut.Length == 0 ? "El documento no contiene un RUT válido." : null;
                    if (documento is null)
                        error = "La fila no contiene un documento.";
                    else if (
                        string.IsNullOrWhiteSpace(seguro.Tipo)
                        || string.IsNullOrWhiteSpace(seguro.Numero)
                    )
                        error = "El documento no contiene tipo o nÃºmero.";
                    return (Documento: seguro, Rut: rut, Error: error, Indice: indice);
                }
            )
            .ToArray();

        string[] rutsSinRegistrar = normalizados
            .Where(elemento => elemento.Error is null)
            .Select(elemento => elemento.Rut)
            .Distinct(StringComparer.Ordinal)
            .Where(rut => !clientesNormalizados.ContainsKey(rut))
            .ToArray();
        if (rutsSinRegistrar.Length > 0)
        {
            return new ResultadoAnalisis(
                [],
                rutsSinRegistrar,
                documentos.Count,
                0,
                0,
                "Hay RUTs sin registrar. Debe registrarlos antes de continuar."
            );
        }

        var agrupados = new Dictionary<
            string,
            List<(DocumentoFactura Documento, string? Ruta, string? Error)>
        >(StringComparer.Ordinal);
        int totalEncontrados = 0;
        foreach (var elemento in normalizados)
        {
            string claveRut = elemento.Error is null ? elemento.Rut : "";
            if (
                !agrupados.TryGetValue(
                    claveRut,
                    out List<(DocumentoFactura, string?, string?)>? lista
                )
            )
                agrupados[claveRut] = lista = [];

            string? ruta = null;
            string? error = elemento.Error;
            if (error is null)
            {
                try
                {
                    ruta = buscarPdf(elemento.Documento);
                }
                catch (Exception excepcion)
                {
                    error = $"No se pudo buscar el PDF: {excepcion.Message}";
                    MensajeroLog.Registrar(
                        "ERROR_DOCUMENTO",
                        $"Fila {elemento.Indice + 1}, {elemento.Documento.Tipo} {elemento.Documento.Numero}, RUT {elemento.Rut}: {excepcion}"
                    );
                }
            }
            else
            {
                MensajeroLog.Registrar(
                    "ERROR_DOCUMENTO",
                    $"Fila {elemento.Indice + 1}, {elemento.Documento.Tipo} {elemento.Documento.Numero}: {error}"
                );
            }

            lista.Add((elemento.Documento, ruta, error));
            if (ruta is not null)
                totalEncontrados++;
        }

        var clientes = agrupados
            .Select(par =>
            {
                ClienteFactura cliente =
                    par.Key.Length == 0
                        ? new ClienteFactura("", "Documento sin RUT válido", "")
                        : clientesNormalizados[par.Key];
                DocumentoAnalizado[] docs = par
                    .Value.Select(elemento => new DocumentoAnalizado(
                        elemento.Documento.Tipo,
                        elemento.Documento.Numero,
                        elemento.Documento.Entidad,
                        elemento.Ruta,
                        elemento.Error
                    ))
                    .ToArray();
                DocumentoAnalizado[] encontrados = docs.Where(documento =>
                        documento.RutaPdf is not null
                    )
                    .ToArray();
                DocumentoAnalizado[] faltantes = docs.Where(documento => documento.RutaPdf is null)
                    .ToArray();
                var entradaCorreo = docs.Select(documento => new DocumentoFactura(
                        documento.Tipo,
                        documento.Numero,
                        documento.Entidad
                    ))
                    .ToArray();
                return new ClienteAnalizado(
                    par.Key,
                    cliente.RazonSocial,
                    cliente.Correo,
                    docs,
                    encontrados,
                    faltantes,
                    GeneradorCorreoFactura.Generar(
                        cliente.RazonSocial,
                        descripcionSemana,
                        entradaCorreo
                    )
                );
            })
            .OrderByDescending(cliente => cliente.Documentos.Count)
            .ToArray();

        return new ResultadoAnalisis(
            clientes,
            [],
            documentos.Count,
            totalEncontrados,
            clientes.Sum(cliente => cliente.PdfsFaltantes.Count)
        );
    }
}
