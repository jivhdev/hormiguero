using System.Security.Cryptography;
using System.Text.Json;
using Hormiguero.Mensajero.Core.ClickFactura;

namespace Hormiguero.Mensajero.Core.Tests.ClickFactura;

public sealed class EquivalenciaClickFacturaTests
{
    private static JsonDocument Esperado =>
        JsonDocument.Parse(
            File.ReadAllText(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "Equivalencia",
                    "ClickFactura",
                    "esperado.json"
                )
            )
        );

    [Fact]
    public void LecturaExcelCoincideCaracterPorCaracter()
    {
        using JsonDocument esperado = Esperado;
        string ruta = Path.Combine(
            AppContext.BaseDirectory,
            "Equivalencia",
            "ClickFactura",
            "entrada.xlsx"
        );
        DocumentoFactura[] documentos = LectorExcelFactura.Leer(ruta).ToArray();
        JsonElement[] filas = esperado
            .RootElement.GetProperty("documentos_excel")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(filas.Length, documentos.Length);
        for (int indice = 0; indice < filas.Length; indice++)
        {
            Assert.Equal(filas[indice].GetProperty("td").GetString(), documentos[indice].Tipo);
            Assert.Equal(
                filas[indice].GetProperty("numero").GetString(),
                documentos[indice].Numero
            );
            Assert.Equal(
                filas[indice].GetProperty("entidad").GetString(),
                documentos[indice].Entidad
            );
        }
    }

    [Fact]
    public void NormalizacionYValidacionRutCoinciden()
    {
        using JsonDocument esperado = Esperado;
        foreach (JsonElement caso in esperado.RootElement.GetProperty("ruts").EnumerateArray())
        {
            string entrada = caso.GetProperty("entrada").GetString()!;
            if (caso.TryGetProperty("error", out _))
                Assert.Throws<ArgumentOutOfRangeException>(() => RutFactura.Limpiar(entrada));
            else
                Assert.Equal(caso.GetProperty("limpio").GetString(), RutFactura.Limpiar(entrada));
        }

        foreach (
            JsonElement caso in esperado
                .RootElement.GetProperty("validaciones_rut")
                .EnumerateArray()
        )
            Assert.Equal(
                caso.GetProperty("valido").GetBoolean(),
                RutFactura.Validar(caso.GetProperty("rut").GetString()!)
            );
    }

    [Fact]
    public void MensajesDeCorreoCoincidenCaracterPorCaracter()
    {
        using JsonDocument esperado = Esperado;
        foreach (JsonElement caso in esperado.RootElement.GetProperty("correos").EnumerateArray())
        {
            DocumentoFactura[] documentos = caso.GetProperty("documentos")
                .EnumerateArray()
                .Select(elemento => new DocumentoFactura(
                    elemento.GetProperty("td").GetString()!,
                    elemento.GetProperty("numero").GetString()!,
                    elemento.GetProperty("entidad").GetString()!
                ))
                .ToArray();
            MensajeFactura mensaje = GeneradorCorreoFactura.Generar(
                caso.GetProperty("razon_social").GetString()!,
                caso.GetProperty("descripcion").GetString()!,
                documentos
            );
            Assert.Equal(caso.GetProperty("asunto").GetString(), mensaje.Asunto);
            Assert.Equal(caso.GetProperty("cuerpo").GetString(), mensaje.Cuerpo);
        }
    }

    [Fact]
    public void BusquedaPdfRespetaNombresCediblesYBusquedaAmpliada()
    {
        using JsonDocument esperado = Esperado;
        JsonElement casos = esperado.RootElement.GetProperty("busquedas_pdf");
        string baseDocumentos = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string carpeta = Path.Combine(baseDocumentos, "FCV", "2026", "202603");
        string carpetaNcv = Path.Combine(baseDocumentos, "NCV", "2026", "202603");
        Directory.CreateDirectory(carpeta);
        Directory.CreateDirectory(carpetaNcv);
        try
        {
            File.WriteAllText(Path.Combine(carpeta, "FCV0000000123.pdf"), "pdf sintético");
            File.WriteAllText(
                Path.Combine(carpeta, "FCV0000000123_CEDIBLE.pdf"),
                "cedible sintético"
            );
            File.WriteAllText(Path.Combine(carpetaNcv, "NCV0000000042.pdf"), "pdf sintético");
            Assert.Equal(
                casos.GetProperty("fcv").GetString(),
                Path.GetFileName(
                    BuscadorPdfFactura.BuscarPdf(baseDocumentos, "FCV", "0000000123", 2026, 3)
                )
            );
            Assert.Equal(
                casos.GetProperty("ncv").GetString(),
                Path.GetFileName(BuscadorPdfFactura.BuscarPdf(baseDocumentos, "NCV", "42", 2026, 3))
            );
            Assert.Null(BuscadorPdfFactura.BuscarPdf(baseDocumentos, "FCV", "999", 2026, 3));
            Assert.Equal(
                casos.GetProperty("ampliada").GetString(),
                Path.GetFileName(
                    BuscadorPdfFactura.BuscarEnTodasLasCarpetas(baseDocumentos, "FCV", "123")
                )
            );
        }
        finally
        {
            Directory.Delete(baseDocumentos, recursive: true);
        }
    }

    [Fact]
    public void AnalisisAgrupaDocumentosYMensajesComoElOriginal()
    {
        using JsonDocument esperado = Esperado;
        JsonElement esperadoAnalisis = esperado.RootElement.GetProperty("analisis");
        string baseDocumentos = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string carpetaFcv = Path.Combine(baseDocumentos, "FCV", "2026", "202603");
        string carpetaNcv = Path.Combine(baseDocumentos, "NCV", "2026", "202603");
        Directory.CreateDirectory(carpetaFcv);
        Directory.CreateDirectory(carpetaNcv);
        try
        {
            File.WriteAllText(Path.Combine(carpetaFcv, "FCV0000000123.pdf"), "pdf sintético");
            File.WriteAllText(Path.Combine(carpetaNcv, "NCV0000000042.pdf"), "pdf sintético");
            DocumentoFactura[] documentos = esperado
                .RootElement.GetProperty("documentos_excel")
                .EnumerateArray()
                .Select(elemento => new DocumentoFactura(
                    elemento.GetProperty("td").GetString()!,
                    elemento.GetProperty("numero").GetString()!,
                    elemento.GetProperty("entidad").GetString()!
                ))
                .ToArray();
            var clientes = new Dictionary<string, ClienteFactura>(StringComparer.Ordinal)
            {
                ["76000000-1"] = new("76000000-1", "EMPRESA DE PRUEBA", "prueba@ejemplo.cl"),
                ["77000000-K"] = new("77000000-K", "Empresa Ñandú", "nandu@ejemplo.cl"),
                ["78000000-0"] = new("78000000-0", "Áridos Uno", "aridos@ejemplo.cl"),
            };
            ResultadoAnalisis resultado = AnalizadorFactura.Analizar(
                documentos,
                clientes,
                "prueba de equivalencia",
                documento =>
                    BuscadorPdfFactura.BuscarEnPeriodos(
                        baseDocumentos,
                        documento.Tipo,
                        documento.Numero,
                        [(2026, 3)]
                    )
            );
            Assert.Equal(
                esperadoAnalisis.GetProperty("total_documentos").GetInt32(),
                resultado.TotalDocumentos
            );
            Assert.Equal(
                esperadoAnalisis.GetProperty("total_encontrados").GetInt32(),
                resultado.TotalEncontrados
            );
            Assert.Equal(
                esperadoAnalisis.GetProperty("total_faltantes").GetInt32(),
                resultado.TotalFaltantes
            );
            Assert.Equal(
                esperadoAnalisis
                    .GetProperty("ruts_no_registrados")
                    .EnumerateArray()
                    .Select(rut => rut.GetString()),
                resultado.RutsNoRegistrados
            );
            JsonElement[] clientesEsperados = esperadoAnalisis
                .GetProperty("clientes")
                .EnumerateArray()
                .ToArray();
            Assert.Equal(clientesEsperados.Length, resultado.ClientesProcesados.Count);
            for (int indice = 0; indice < clientesEsperados.Length; indice++)
            {
                JsonElement clienteEsperado = clientesEsperados[indice];
                ClienteAnalizado cliente = resultado.ClientesProcesados[indice];
                Assert.Equal(clienteEsperado.GetProperty("rut").GetString(), cliente.Rut);
                Assert.Equal(
                    clienteEsperado.GetProperty("razon_social").GetString(),
                    cliente.RazonSocial
                );
                Assert.Equal(clienteEsperado.GetProperty("correo").GetString(), cliente.Correo);
                Assert.Equal(
                    clienteEsperado.GetProperty("mensaje").GetProperty("asunto").GetString(),
                    cliente.Mensaje.Asunto
                );
                Assert.Equal(
                    clienteEsperado.GetProperty("mensaje").GetProperty("cuerpo").GetString(),
                    cliente.Mensaje.Cuerpo
                );
                JsonElement[] docsEsperados = clienteEsperado
                    .GetProperty("documentos")
                    .EnumerateArray()
                    .ToArray();
                Assert.Equal(docsEsperados.Length, cliente.Documentos.Count);
                for (int doc = 0; doc < docsEsperados.Length; doc++)
                {
                    Assert.Equal(
                        docsEsperados[doc].GetProperty("td").GetString(),
                        cliente.Documentos[doc].Tipo
                    );
                    Assert.Equal(
                        docsEsperados[doc].GetProperty("numero").GetString(),
                        cliente.Documentos[doc].Numero
                    );
                    Assert.Equal(
                        docsEsperados[doc].GetProperty("entidad").GetString(),
                        cliente.Documentos[doc].Entidad
                    );
                    string? ruta =
                        docsEsperados[doc].GetProperty("ruta_pdf").ValueKind == JsonValueKind.Null
                            ? null
                            : docsEsperados[doc].GetProperty("ruta_pdf").GetString();
                    Assert.Equal(
                        ruta,
                        cliente.Documentos[doc].RutaPdf is null
                            ? null
                            : Path.GetFileName(cliente.Documentos[doc].RutaPdf)
                    );
                }
            }
        }
        finally
        {
            Directory.Delete(baseDocumentos, recursive: true);
        }
    }

    [Fact]
    public void CopiaTemporalConservaLosArchivosOriginales()
    {
        using JsonDocument esperado = Esperado;
        JsonElement caso = esperado.RootElement.GetProperty("copia_temporal");
        string raiz = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string origen = Path.Combine(raiz, "origen");
        string baseDestino = Path.Combine(raiz, "destino");
        Directory.CreateDirectory(origen);
        Directory.CreateDirectory(baseDestino);
        string rutaOrigen = Path.Combine(origen, "FCV0000000123.pdf");
        File.WriteAllBytes(
            rutaOrigen,
            "Adjunto PDF inventado, no corresponde a un documento real."u8.ToArray()
        );
        string rutaNota = Path.Combine(origen, "NCV0000000042.pdf");
        File.WriteAllBytes(rutaNota, "Segunda entrada sintética."u8.ToArray());
        try
        {
            string carpeta = PreparadorEnvioFactura.CrearCarpetaTemporal(
                baseDestino,
                "76.000.000-1",
                new DateTime(2026, 10, 4, 12, 30, 45)
            );
            Assert.Equal(caso.GetProperty("carpeta").GetString(), Path.GetFileName(carpeta));
            string[] copiados = PreparadorEnvioFactura
                .CopiarPdfs([rutaOrigen, rutaNota], carpeta)
                .ToArray();
            Assert.Equal(
                caso.GetProperty("archivos")
                    .EnumerateArray()
                    .Select(archivo => archivo.GetString())
                    .Order(),
                copiados.Select(Path.GetFileName).Order()
            );
            foreach (string copia in copiados)
            {
                string nombre = Path.GetFileName(copia);
                string hash = Convert
                    .ToHexString(SHA256.HashData(File.ReadAllBytes(copia)))
                    .ToLowerInvariant();
                Assert.Equal(
                    caso.GetProperty("huellas_sha256").GetProperty(nombre).GetString(),
                    hash
                );
            }
            Assert.True(File.Exists(rutaOrigen));
            Assert.True(File.Exists(rutaNota));
            PreparadorEnvioFactura.LimpiarCarpetaTemporal(carpeta);
            Assert.False(Directory.Exists(carpeta));
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }
}
