using ClosedXML.Excel;
using Hormiguero.Mensajero.Core;
using Hormiguero.Mensajero.Core.ClickFactura;

namespace Hormiguero.Mensajero.Core.Tests.ClickFactura;

public sealed class CausasX1Tests
{
    [Theory]
    [InlineData("FCV")]
    [InlineData("NCV")]
    public void BuscaDocumentosAlElegirLaCarpetaDeTipo(string tipoElegido)
    {
        string raiz = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string carpetaFcv = Path.Combine(raiz, "FCV", "2026", "202603");
        string carpetaNcv = Path.Combine(raiz, "NCV", "2026", "202603");
        Directory.CreateDirectory(carpetaFcv);
        Directory.CreateDirectory(carpetaNcv);
        File.WriteAllText(Path.Combine(carpetaFcv, "FCV0000000123.pdf"), "pdf sintético");
        File.WriteAllText(Path.Combine(carpetaNcv, "NCV0000000123.pdf"), "pdf sintético");
        try
        {
            string elegida = Path.Combine(raiz, tipoElegido.ToLowerInvariant());
            Assert.Equal(raiz, BuscadorPdfFactura.NormalizarBaseDocumentos(elegida));
            Assert.True(BuscadorPdfFactura.TieneCarpetasTipos(elegida));
            Assert.NotNull(BuscadorPdfFactura.BuscarPdf(elegida, "FCV", "123", 2026, 3));
            Assert.NotNull(BuscadorPdfFactura.BuscarPdf(elegida, "NCV", "123", 2026, 3));
            Assert.NotNull(BuscadorPdfFactura.BuscarEnTodasLasCarpetas(elegida, "FCV", "123"));
            Assert.NotNull(BuscadorPdfFactura.BuscarEnTodasLasCarpetas(elegida, "NCV", "123"));
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    [Fact]
    public void CacheAceptaConsultasYDocumentosDuplicados()
    {
        string raiz = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string carpeta = Path.Combine(raiz, "FCV", "2026", "202603");
        Directory.CreateDirectory(carpeta);
        File.WriteAllText(Path.Combine(carpeta, "FCV0000000123.pdf"), "pdf sintético");
        try
        {
            var cachePeriodo =
                new Dictionary<(string Tipo, string Numero, int Anio, int Mes), string>();
            var cacheAmpliada = new Dictionary<(string Tipo, string Numero), string>();
            Assert.NotNull(BuscadorPdfFactura.BuscarPdf(raiz, "FCV", "123", 2026, 3, cachePeriodo));
            Assert.NotNull(BuscadorPdfFactura.BuscarPdf(raiz, "FCV", "123", 2026, 3, cachePeriodo));
            Assert.NotNull(
                BuscadorPdfFactura.BuscarEnTodasLasCarpetas(raiz, "FCV", "123", cacheAmpliada)
            );
            Assert.NotNull(
                BuscadorPdfFactura.BuscarEnTodasLasCarpetas(raiz, "FCV", "123", cacheAmpliada)
            );
            var documentos = new[]
            {
                new DocumentoFactura("FCV", "123", "76000000-1"),
                new DocumentoFactura("FCV", "123", "76000000-1"),
            };
            var lote = BuscadorPdfFactura.BuscarLote(raiz, documentos, [(2026, 3)]);
            Assert.Single(lote.Encontrados);
            Assert.Empty(lote.NoEncontrados);
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    [Fact]
    public void AnalisisNormalizaRutYContinuaCuandoFallaUnDocumento()
    {
        string rutaLogAnterior = MensajeroLog.RutaLog;
        string rutaLog = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"),
            "mensajero.log"
        );
        MensajeroLog.RutaLog = rutaLog;
        try
        {
            var documentos = new DocumentoFactura[]
            {
                new("FCV", "1", "76.000.000-1"),
                new("FCV", "2", "76000000-1"),
                new("NCV", "3", "76.000.000-1"),
                new("FCV", "4", null!),
                new(null!, "5", "76.000.000-1"),
                null!,
            };
            var clientes = new Dictionary<string, ClienteFactura>(StringComparer.Ordinal)
            {
                ["76000000-1"] = new("76000000-1", "Cliente de prueba", "a@ejemplo.cl"),
                ["77.000.000-K"] = new("77.000.000-K", "Cliente duplicado", "b@ejemplo.cl"),
            };
            int llamadas = 0;
            ResultadoAnalisis resultado = AnalizadorFactura.Analizar(
                documentos,
                clientes,
                "semana de prueba",
                documento =>
                {
                    llamadas++;
                    if (documento.Numero == "2")
                        throw new IOException("carpeta inaccesible de prueba");
                    return documento.Numero == "1" ? "factura.pdf" : null;
                }
            );

            Assert.Empty(resultado.RutsNoRegistrados);
            Assert.Equal(documentos.Length, resultado.TotalDocumentos);
            Assert.Equal(3, llamadas);
            Assert.Equal(1, resultado.TotalEncontrados);
            Assert.Equal(5, resultado.TotalFaltantes);
            Assert.Contains(resultado.ClientesProcesados, cliente => cliente.Rut == "76000000-1");
            Assert.Contains(
                resultado.ClientesProcesados.SelectMany(cliente => cliente.Documentos),
                documento => documento.Error?.Contains("carpeta inaccesible de prueba") == true
            );
            Assert.Contains("ERROR_DOCUMENTO", File.ReadAllText(rutaLog));
            Assert.Contains("carpeta inaccesible de prueba", File.ReadAllText(rutaLog));
        }
        finally
        {
            MensajeroLog.RutaLog = rutaLogAnterior;
            string? carpetaLog = Path.GetDirectoryName(rutaLog);
            if (carpetaLog is not null && Directory.Exists(carpetaLog))
                Directory.Delete(carpetaLog, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, false, 8)]
    [InlineData(false, true, 16)]
    [InlineData(true, false, 16)]
    [InlineData(true, true, 16)]
    public void AnalizaXlsSinteticoConVeinticincoFilasYVeintiunClientes(
        bool busquedaAmpliada,
        bool dosMeses,
        int encontradosEsperados
    )
    {
        string raiz = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string rutaXls = Path.Combine(raiz, "facturas_sinteticas.xlsx");
        Directory.CreateDirectory(raiz);
        var datos = Enumerable
            .Range(0, 21)
            .Select(indice =>
            {
                string cuerpo = (70000000 + indice).ToString();
                string rut = $"{cuerpo}-{indice % 10}";
                return (
                    Rut: rut,
                    Entidad: $"{cuerpo[..2]}.{cuerpo[2..5]}.{cuerpo[5..]}-{indice % 10}"
                );
            })
            .ToArray();
        var filas = datos
            .Select(
                (dato, indice) =>
                    new DocumentoFactura(
                        indice % 2 == 0 ? "FCV" : "NCV",
                        (1000 + indice).ToString(),
                        dato.Entidad
                    )
            )
            .ToList();
        filas.AddRange(filas.Take(4));
        string carpetaFcvMarzo = Path.Combine(raiz, "FCV", "2026", "202603");
        string carpetaNcvAbril = Path.Combine(raiz, "NCV", "2026", "202604");
        Directory.CreateDirectory(carpetaFcvMarzo);
        Directory.CreateDirectory(carpetaNcvAbril);

        using (var libro = new XLWorkbook())
        {
            IXLWorksheet hoja = libro.AddWorksheet("Facturas");
            hoja.Cell(1, 1).Value = "TD";
            hoja.Cell(1, 2).Value = "Número";
            hoja.Cell(1, 3).Value = "Entidad";
            for (int indice = 0; indice < filas.Count; indice++)
            {
                hoja.Cell(indice + 2, 1).Value = filas[indice].Tipo;
                hoja.Cell(indice + 2, 2).Value = 1000 + (indice % 21);
                hoja.Cell(indice + 2, 3).Value = filas[indice].Entidad;
            }
            libro.SaveAs(rutaXls);
        }

        for (int indice = 0; indice < datos.Length; indice++)
        {
            if (indice % 3 == 0)
                continue;
            string carpeta = indice % 2 == 0 ? carpetaFcvMarzo : carpetaNcvAbril;
            string tipo = indice % 2 == 0 ? "FCV" : "NCV";
            File.WriteAllText(
                Path.Combine(carpeta, $"{tipo}{1000 + indice:0000000000}.pdf"),
                "pdf sintético"
            );
        }

        try
        {
            DocumentoFactura[] documentos = LectorExcelFactura.Leer(rutaXls).ToArray();
            Assert.Equal(25, documentos.Length);
            var clientes = datos.ToDictionary(
                dato => dato.Entidad,
                dato => new ClienteFactura(
                    dato.Entidad,
                    "Cliente de prueba",
                    "facturas@ejemplo.cl"
                ),
                StringComparer.Ordinal
            );
            IReadOnlyList<(int Anio, int Mes)> periodos = dosMeses
                ? [(2026, 3), (2026, 4)]
                : [(2026, 3)];
            ResultadoAnalisis resultado = AnalizadorFactura.Analizar(
                documentos,
                clientes,
                "semana sintética",
                documento =>
                    busquedaAmpliada
                        ? BuscadorPdfFactura.BuscarEnTodasLasCarpetas(
                            raiz,
                            documento.Tipo,
                            documento.Numero
                        )
                        : BuscadorPdfFactura.BuscarEnPeriodos(
                            raiz,
                            documento.Tipo,
                            documento.Numero,
                            periodos
                        )
            );

            Assert.Empty(resultado.RutsNoRegistrados);
            Assert.Equal(25, resultado.TotalDocumentos);
            Assert.Equal(encontradosEsperados, resultado.TotalEncontrados);
            Assert.Equal(25 - encontradosEsperados, resultado.TotalFaltantes);
            Assert.Equal(21, resultado.ClientesProcesados.Count);
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }
}
