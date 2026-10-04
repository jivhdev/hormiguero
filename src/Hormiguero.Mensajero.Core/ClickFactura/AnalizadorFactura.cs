namespace Hormiguero.Mensajero.Core.ClickFactura;

public static class AnalizadorFactura
{
    public static ResultadoAnalisis Analizar(
        IReadOnlyList<DocumentoFactura> documentos,
        IReadOnlyDictionary<string, ClienteFactura> clientesPorRut,
        string descripcionSemana,
        Func<DocumentoFactura, string?> buscarPdf
    )
    {
        var normalizados = documentos
            .Select(documento => (Documento: documento, Rut: RutFactura.Limpiar(documento.Entidad)))
            .ToArray();
        string[] rutsSinRegistrar = normalizados
            .Select(elemento => elemento.Rut)
            .Distinct(StringComparer.Ordinal)
            .Where(rut => !clientesPorRut.ContainsKey(rut))
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

        var agrupados = new Dictionary<string, List<(DocumentoFactura Documento, string? Ruta)>>(
            StringComparer.Ordinal
        );
        var clavesEncontradas = new HashSet<(string Tipo, string Numero, string Entidad)>();
        foreach ((DocumentoFactura documento, string rut) in normalizados)
        {
            if (!agrupados.TryGetValue(rut, out List<(DocumentoFactura, string?)>? lista))
                agrupados[rut] = lista = [];
            string? ruta = buscarPdf(documento);
            lista.Add((documento, ruta));
            if (ruta is not null)
                clavesEncontradas.Add((documento.Tipo, documento.Numero, documento.Entidad));
        }

        var clientes = agrupados
            .Select(par =>
            {
                ClienteFactura cliente = clientesPorRut[par.Key];
                DocumentoAnalizado[] docs = par
                    .Value.Select(elemento => new DocumentoAnalizado(
                        elemento.Documento.Tipo,
                        elemento.Documento.Numero,
                        elemento.Documento.Entidad,
                        elemento.Ruta
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

        int totalEncontrados = clavesEncontradas.Count;
        int totalFaltantes = clientes.Sum(cliente => cliente.PdfsFaltantes.Count);
        return new ResultadoAnalisis(
            clientes,
            [],
            documentos.Count,
            totalEncontrados,
            totalFaltantes
        );
    }
}
