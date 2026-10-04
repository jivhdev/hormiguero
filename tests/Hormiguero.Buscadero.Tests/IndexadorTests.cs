using Hormiguero.Buscadero.Logica;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Buscadero.Tests;

public class IndexadorTests
{
    private static string CarpetaTemporal() =>
        Path.Combine(
            Path.GetTempPath(),
            "HormigueroBuscaderoTests",
            Guid.NewGuid().ToString("N"),
            "carpeta"
        );

    private static string RutaTemporal() =>
        Path.Combine(
            Path.GetTempPath(),
            "HormigueroBuscaderoTests",
            Guid.NewGuid().ToString("N"),
            "hormiguero.db"
        );

    private static void Escribir(string carpeta, string nombre, byte[] contenido)
    {
        Directory.CreateDirectory(carpeta);
        File.WriteAllBytes(Path.Combine(carpeta, nombre), contenido);
    }

    private static void Borrar(string ruta)
    {
        // El agrupamiento de conexiones de Microsoft.Data.Sqlite deja el archivo
        // abierto en Windows; hay que soltarlo antes de borrar la carpeta temporal.
        SqliteConnection.ClearAllPools();
        string? carpeta = Path.GetDirectoryName(ruta);
        if (!string.IsNullOrEmpty(carpeta) && Directory.Exists(carpeta))
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }

    // El núcleo devuelve una fila por número coincidente (el mismo PDF sale una
    // vez por el nombre y otra por su texto); el agrupado es de REQ-004.
    private static IReadOnlyList<DocumentoIndexado> PorNumero(
        SqliteConnection conexion,
        string numero
    ) =>
        new Documentos(conexion)
            .BuscarPorNumero(numero)
            .GroupBy(documento => documento.Ruta)
            .Select(grupo => grupo.First())
            .ToList();

    [Fact]
    public void Indexa_pdf_e_ignora_otros()
    {
        string carpeta = CarpetaTemporal();
        string rutaBase = RutaTemporal();
        try
        {
            Escribir(carpeta, "OCC104523.pdf", PdfDePrueba.ConTexto());
            Escribir(carpeta, "informe.pdf", PdfDePrueba.ConTexto());
            Escribir(carpeta, "nota.txt", [125, 65, 32, 80, 68, 70, 32, 109, 97, 115]);

            using var conexion = BaseComun.Abrir(rutaBase);
            var indexador = new Indexador(conexion);

            ResumenIndexado resumen = indexador.Revisar(carpeta, CancellationToken.None);

            Assert.Equal(2, resumen.Nuevos);
            Assert.Equal(0, resumen.Actualizados);
            Assert.Equal(0, resumen.Quitados);
            Assert.Equal(0, resumen.SinCambios);

            // Uno por el nombre del archivo y otro por su texto.
            Assert.Equal(
                new[] { "OCC104523.pdf", "informe.pdf" },
                PorNumero(conexion, "104523")
                    .Select(doc => doc.Nombre)
                    .OrderBy(nombre => nombre, StringComparer.Ordinal)
            );
        }
        finally
        {
            Borrar(carpeta);
            Borrar(rutaBase);
        }
    }

    [Fact]
    public void Encuentra_numero_en_el_texto()
    {
        string carpeta = CarpetaTemporal();
        string rutaBase = RutaTemporal();
        try
        {
            Escribir(carpeta, "informe.pdf", PdfDePrueba.ConTexto());

            using var conexion = BaseComun.Abrir(rutaBase);
            var indexador = new Indexador(conexion);
            indexador.Revisar(carpeta, CancellationToken.None);

            DocumentoIndexado documento = Assert.Single(
                new Documentos(conexion).BuscarPorNumero("104523")
            );

            // Sin número en el nombre: solo aparece por el texto (C6).
            Assert.Equal("informe.pdf", documento.Nombre);
            Assert.Equal("correcto", documento.Estado);
            Assert.True(documento.TieneTexto);
            Assert.NotNull(documento.Huella);
            (string numero, string prefijo, string sufijo, string origen) = Assert.Single(
                documento.Numeros
            );
            Assert.Equal("104523", numero);
            Assert.Equal("texto", origen);
        }
        finally
        {
            Borrar(carpeta);
            Borrar(rutaBase);
        }
    }

    [Fact]
    public void Escaneado_se_indexa_solo_por_el_nombre()
    {
        string carpeta = CarpetaTemporal();
        string rutaBase = RutaTemporal();
        try
        {
            Escribir(carpeta, "5521.pdf", PdfDePrueba.SinTexto());

            using var conexion = BaseComun.Abrir(rutaBase);
            var indexador = new Indexador(conexion);
            indexador.Revisar(carpeta, CancellationToken.None);

            DocumentoIndexado documento = Assert.Single(
                new Documentos(conexion).BuscarPorNumero("5521")
            );

            Assert.Equal("correcto", documento.Estado);
            Assert.False(documento.TieneTexto);
            (string numero, string prefijo, string sufijo, string origen) = Assert.Single(
                documento.Numeros
            );
            Assert.Equal("5521", numero);
            Assert.Equal("nombre", origen);
        }
        finally
        {
            Borrar(carpeta);
            Borrar(rutaBase);
        }
    }

    [Fact]
    public void Segunda_revision_no_relee()
    {
        string carpeta = CarpetaTemporal();
        string rutaBase = RutaTemporal();
        try
        {
            Escribir(carpeta, "OCC104523.pdf", PdfDePrueba.ConTexto());
            Escribir(carpeta, "informe.pdf", PdfDePrueba.ConTexto());

            using var conexion = BaseComun.Abrir(rutaBase);
            var indexador = new Indexador(conexion);
            indexador.Revisar(carpeta, CancellationToken.None);

            ResumenIndexado resumen = indexador.Revisar(carpeta, CancellationToken.None);

            Assert.Equal(2, resumen.SinCambios);
            Assert.Equal(0, resumen.Nuevos);
            Assert.Equal(0, resumen.Actualizados);
            Assert.Equal(0, resumen.Quitados);
            Assert.Equal(2, PorNumero(conexion, "104523").Count);
        }
        finally
        {
            Borrar(carpeta);
            Borrar(rutaBase);
        }
    }

    [Fact]
    public void Quita_los_borrados()
    {
        string carpeta = CarpetaTemporal();
        string rutaBase = RutaTemporal();
        try
        {
            Escribir(carpeta, "OCC104523.pdf", PdfDePrueba.ConTexto());
            Escribir(carpeta, "informe.pdf", PdfDePrueba.ConTexto());

            using var conexion = BaseComun.Abrir(rutaBase);
            var indexador = new Indexador(conexion);
            indexador.Revisar(carpeta, CancellationToken.None);

            File.Delete(Path.Combine(carpeta, "informe.pdf"));
            ResumenIndexado resumen = indexador.Revisar(carpeta, CancellationToken.None);

            Assert.Equal(1, resumen.Quitados);
            Assert.Equal(1, resumen.SinCambios);
            Assert.Equal(0, resumen.Nuevos);
            Assert.Equal("OCC104523.pdf", Assert.Single(PorNumero(conexion, "104523")).Nombre);
        }
        finally
        {
            Borrar(carpeta);
            Borrar(rutaBase);
        }
    }

    [Fact]
    public void Pdf_danado_no_detiene()
    {
        string carpeta = CarpetaTemporal();
        string rutaBase = RutaTemporal();
        try
        {
            // En una subcarpeta: también entra al índice.
            Escribir(Path.Combine(carpeta, "borrador"), "roto776655.pdf", PdfDePrueba.Truncado());
            Escribir(carpeta, "OCC104523.pdf", PdfDePrueba.ConTexto());

            using var conexion = BaseComun.Abrir(rutaBase);
            var indexador = new Indexador(conexion);

            ResumenIndexado resumen = indexador.Revisar(carpeta, CancellationToken.None);

            Assert.Equal(2, resumen.Nuevos);
            Assert.Equal("danado", Assert.Single(PorNumero(conexion, "776655")).Estado);

            // El resto de la carpeta se sigue indexando.
            Assert.Equal("correcto", Assert.Single(PorNumero(conexion, "104523")).Estado);
        }
        finally
        {
            Borrar(carpeta);
            Borrar(rutaBase);
        }
    }

    [Fact]
    public void Solo_en_linea_no_se_lee()
    {
        string carpeta = CarpetaTemporal();
        string rutaBase = RutaTemporal();
        try
        {
            string ruta = Path.Combine(carpeta, "nube556677.pdf");
            Escribir(carpeta, "nube556677.pdf", PdfDePrueba.ConTexto());
            File.SetAttributes(ruta, File.GetAttributes(ruta) | FileAttributes.Offline);

            using var conexion = BaseComun.Abrir(rutaBase);
            var indexador = new Indexador(conexion);
            indexador.Revisar(carpeta, CancellationToken.None);

            DocumentoIndexado documento = Assert.Single(
                new Documentos(conexion).BuscarPorNumero("556677")
            );

            Assert.Equal("solo_en_linea", documento.Estado);
            Assert.Null(documento.Huella);
            Assert.False(documento.TieneTexto);
            Assert.All(documento.Numeros, numero => Assert.Equal("nombre", numero.Origen));
        }
        finally
        {
            Borrar(carpeta);
            Borrar(rutaBase);
        }
    }

    [Fact]
    public void Se_puede_cancelar()
    {
        string carpeta = CarpetaTemporal();
        string rutaBase = RutaTemporal();
        try
        {
            Escribir(carpeta, "OCC104523.pdf", PdfDePrueba.ConTexto());
            Escribir(carpeta, "informe.pdf", PdfDePrueba.ConTexto());

            using var conexion = BaseComun.Abrir(rutaBase);
            var indexador = new Indexador(conexion);
            using var cancelar = new CancellationTokenSource();
            cancelar.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                indexador.Revisar(carpeta, cancelar.Token)
            );

            // El índice retoma donde quedó: la revisión siguiente completa el trabajo.
            Assert.Equal(2, indexador.Revisar(carpeta, CancellationToken.None).Nuevos);
        }
        finally
        {
            Borrar(carpeta);
            Borrar(rutaBase);
        }
    }
}
