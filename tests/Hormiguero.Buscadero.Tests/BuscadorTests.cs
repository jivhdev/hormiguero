using System.Diagnostics;
using Hormiguero.Buscadero.Logica;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Buscadero.Tests;

public class BuscadorTests
{
    private static string RutaTemporal() =>
        Path.Combine(
            Path.GetTempPath(),
            "HormigueroBuscaderoTests",
            Guid.NewGuid().ToString("N"),
            "hormiguero.db"
        );

    private static void Borrar(string ruta)
    {
        SqliteConnection.ClearAllPools();
        string? carpeta = Path.GetDirectoryName(ruta);
        if (!string.IsNullOrEmpty(carpeta) && Directory.Exists(carpeta))
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }

    private static void CrearCarpetasYArchivos(params (string Carpeta, string Archivo)[] items)
    {
        foreach (var (carpeta, archivo) in items)
        {
            Directory.CreateDirectory(carpeta);
            string rutaCompleta = Path.Combine(carpeta, archivo);
            if (!File.Exists(rutaCompleta))
            {
                File.WriteAllText(rutaCompleta, "contenido de prueba");
            }
        }
    }

    private static void BorrarCarpetas(params string[] carpetas)
    {
        foreach (string carpeta in carpetas)
        {
            if (Directory.Exists(carpeta))
            {
                Directory.Delete(carpeta, recursive: true);
            }
        }
    }

    [Fact]
    public void Normaliza_el_texto()
    {
        Assert.Equal("104523", Buscador.Normalizar(" 0104523 "));
        Assert.Equal("104523", Buscador.Normalizar("OCC104523"));
        Assert.Equal("104523", Buscador.Normalizar("OCC 0104523"));
        Assert.Equal("123", Buscador.Normalizar("000123"));
        Assert.Equal("0", Buscador.Normalizar("0"));
        Assert.Equal("0", Buscador.Normalizar("000"));
        Assert.Null(Buscador.Normalizar("hola"));
        Assert.Null(Buscador.Normalizar(""));
        Assert.Null(Buscador.Normalizar("   "));
        Assert.Null(Buscador.Normalizar("ABC"));
    }

    [Fact]
    public void Coincidencia_exacta()
    {
        string rutaDb = RutaTemporal();
        try
        {
            using var conexion = BaseComun.Abrir(rutaDb);
            var documentos = new Documentos(conexion);
            var carpetas = new CarpetasConfiguradas(conexion);

            string carpeta1 = Path.Combine(
                Path.GetTempPath(),
                "HormigueroBuscaderoTests",
                Guid.NewGuid().ToString("N"),
                "carpeta1"
            );
            Directory.CreateDirectory(carpeta1);
            carpetas.Agregar(carpeta1);

            documentos.Guardar(
                new DocumentoIndexado(
                    Path.Combine(carpeta1, "factura_104523.pdf"),
                    carpeta1,
                    "factura_104523.pdf",
                    1000L,
                    new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    null,
                    "correcto",
                    true,
                    new List<(string Numero, string Prefijo, string Sufijo, string Origen)>
                    {
                        ("104523", "F", "A", "nombre"),
                    }
                )
            );

            var buscador = new Buscador(conexion);
            var resultado = buscador.Buscar(
                "104523",
                AlcanceBusqueda.TodasLasCarpetas,
                null,
                CancellationToken.None
            );

            Assert.Single(resultado);
            Assert.Equal("104523", resultado[0].Numeros[0].Numero);
        }
        finally
        {
            Borrar(rutaDb);
        }
    }

    [Fact]
    public void Primera_coincidencia_y_todas()
    {
        string rutaDb = RutaTemporal();
        string carpeta1 = Path.Combine(
            Path.GetTempPath(),
            "HormigueroBuscaderoTests",
            Guid.NewGuid().ToString("N"),
            "carpeta1"
        );
        string carpeta2 = Path.Combine(
            Path.GetTempPath(),
            "HormigueroBuscaderoTests",
            Guid.NewGuid().ToString("N"),
            "carpeta2"
        );

        try
        {
            using var conexion = BaseComun.Abrir(rutaDb);
            var documentos = new Documentos(conexion);
            var carpetas = new CarpetasConfiguradas(conexion);

            Directory.CreateDirectory(carpeta1);
            Directory.CreateDirectory(carpeta2);
            carpetas.Agregar(carpeta1);
            carpetas.Agregar(carpeta2);

            documentos.Guardar(
                new DocumentoIndexado(
                    Path.Combine(carpeta1, "doc_123.pdf"),
                    carpeta1,
                    "doc_123.pdf",
                    1000L,
                    new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    null,
                    "correcto",
                    true,
                    new List<(string Numero, string Prefijo, string Sufijo, string Origen)>
                    {
                        ("123", "", "", "nombre"),
                    }
                )
            );

            documentos.Guardar(
                new DocumentoIndexado(
                    Path.Combine(carpeta2, "otro_123.pdf"),
                    carpeta2,
                    "otro_123.pdf",
                    2000L,
                    new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    null,
                    "correcto",
                    true,
                    new List<(string Numero, string Prefijo, string Sufijo, string Origen)>
                    {
                        ("123", "", "", "nombre"),
                    }
                )
            );

            var buscador = new Buscador(conexion);

            var primera = buscador.Buscar(
                "123",
                AlcanceBusqueda.PrimeraCoincidencia,
                null,
                CancellationToken.None
            );
            Assert.Single(primera);
            Assert.Equal(carpeta1, primera[0].CarpetaRaiz);

            var todas = buscador.Buscar(
                "123",
                AlcanceBusqueda.TodasLasCarpetas,
                null,
                CancellationToken.None
            );
            Assert.Equal(2, todas.Count);
            Assert.Contains(todas, d => d.CarpetaRaiz == carpeta1);
            Assert.Contains(todas, d => d.CarpetaRaiz == carpeta2);
        }
        finally
        {
            Borrar(rutaDb);
            BorrarCarpetas(carpeta1, carpeta2);
        }
    }

    [Fact]
    public void Carpeta_especifica()
    {
        string rutaDb = RutaTemporal();
        string carpeta1 = Path.Combine(
            Path.GetTempPath(),
            "HormigueroBuscaderoTests",
            Guid.NewGuid().ToString("N"),
            "carpeta1"
        );
        string carpeta2 = Path.Combine(
            Path.GetTempPath(),
            "HormigueroBuscaderoTests",
            Guid.NewGuid().ToString("N"),
            "carpeta2"
        );

        try
        {
            using var conexion = BaseComun.Abrir(rutaDb);
            var documentos = new Documentos(conexion);
            var carpetas = new CarpetasConfiguradas(conexion);

            Directory.CreateDirectory(carpeta1);
            Directory.CreateDirectory(carpeta2);
            carpetas.Agregar(carpeta1);
            carpetas.Agregar(carpeta2);

            documentos.Guardar(
                new DocumentoIndexado(
                    Path.Combine(carpeta1, "doc_123.pdf"),
                    carpeta1,
                    "doc_123.pdf",
                    1000L,
                    new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    null,
                    "correcto",
                    true,
                    new List<(string Numero, string Prefijo, string Sufijo, string Origen)>
                    {
                        ("123", "", "", "nombre"),
                    }
                )
            );

            documentos.Guardar(
                new DocumentoIndexado(
                    Path.Combine(carpeta2, "otro_123.pdf"),
                    carpeta2,
                    "otro_123.pdf",
                    2000L,
                    new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    null,
                    "correcto",
                    true,
                    new List<(string Numero, string Prefijo, string Sufijo, string Origen)>
                    {
                        ("123", "", "", "nombre"),
                    }
                )
            );

            var buscador = new Buscador(conexion);

            var enCarpeta1 = buscador.Buscar(
                "123",
                AlcanceBusqueda.CarpetaEspecifica,
                carpeta1,
                CancellationToken.None
            );
            Assert.Single(enCarpeta1);
            Assert.Equal(carpeta1, enCarpeta1[0].CarpetaRaiz);

            var enCarpeta2 = buscador.Buscar(
                "123",
                AlcanceBusqueda.CarpetaEspecifica,
                carpeta2,
                CancellationToken.None
            );
            Assert.Single(enCarpeta2);
            Assert.Equal(carpeta2, enCarpeta2[0].CarpetaRaiz);

            var carpetaInexistente = buscador.Buscar(
                "123",
                AlcanceBusqueda.CarpetaEspecifica,
                @"C:\NoExiste",
                CancellationToken.None
            );
            Assert.Empty(carpetaInexistente);

            Assert.Throws<ArgumentException>(() =>
                buscador.Buscar(
                    "123",
                    AlcanceBusqueda.CarpetaEspecifica,
                    null,
                    CancellationToken.None
                )
            );
            Assert.Throws<ArgumentException>(() =>
                buscador.Buscar(
                    "123",
                    AlcanceBusqueda.CarpetaEspecifica,
                    "",
                    CancellationToken.None
                )
            );
            Assert.Throws<ArgumentException>(() =>
                buscador.Buscar(
                    "123",
                    AlcanceBusqueda.CarpetaEspecifica,
                    "   ",
                    CancellationToken.None
                )
            );
        }
        finally
        {
            Borrar(rutaDb);
            BorrarCarpetas(carpeta1, carpeta2);
        }
    }

    [Fact]
    public void Busqueda_en_vivo_por_nombre()
    {
        string rutaDb = RutaTemporal();
        string carpeta1 = Path.Combine(
            Path.GetTempPath(),
            "HormigueroBuscaderoTests",
            Guid.NewGuid().ToString("N"),
            "carpeta1"
        );

        try
        {
            using var conexion = BaseComun.Abrir(rutaDb);
            var carpetas = new CarpetasConfiguradas(conexion);
            Directory.CreateDirectory(carpeta1);
            carpetas.Agregar(carpeta1);

            string archivoEnVivo = Path.Combine(carpeta1, "factura_999999.pdf");
            File.WriteAllText(archivoEnVivo, "contenido");

            var buscador = new Buscador(conexion);
            var resultado = buscador.Buscar(
                "999999",
                AlcanceBusqueda.TodasLasCarpetas,
                null,
                CancellationToken.None
            );

            Assert.Single(resultado);
            Assert.Equal("sin_indexar", resultado[0].Estado);
            Assert.Equal(archivoEnVivo, resultado[0].Ruta);
            Assert.False(resultado[0].TieneTexto);
            Assert.Empty(resultado[0].Numeros);
        }
        finally
        {
            Borrar(rutaDb);
            BorrarCarpetas(carpeta1);
        }
    }

    [Fact]
    public void Sin_digitos_no_busca()
    {
        string rutaDb = RutaTemporal();
        try
        {
            using var conexion = BaseComun.Abrir(rutaDb);
            var buscador = new Buscador(conexion);

            var resultado = buscador.Buscar(
                "hola",
                AlcanceBusqueda.TodasLasCarpetas,
                null,
                CancellationToken.None
            );
            Assert.Empty(resultado);

            resultado = buscador.Buscar(
                "",
                AlcanceBusqueda.TodasLasCarpetas,
                null,
                CancellationToken.None
            );
            Assert.Empty(resultado);

            resultado = buscador.Buscar(
                "   ",
                AlcanceBusqueda.TodasLasCarpetas,
                null,
                CancellationToken.None
            );
            Assert.Empty(resultado);
        }
        finally
        {
            Borrar(rutaDb);
        }
    }

    [Fact]
    public void Se_puede_cancelar()
    {
        string rutaDb = RutaTemporal();
        string carpeta1 = Path.Combine(
            Path.GetTempPath(),
            "HormigueroBuscaderoTests",
            Guid.NewGuid().ToString("N"),
            "carpeta1"
        );
        string carpeta2 = Path.Combine(
            Path.GetTempPath(),
            "HormigueroBuscaderoTests",
            Guid.NewGuid().ToString("N"),
            "carpeta2"
        );

        try
        {
            using var conexion = BaseComun.Abrir(rutaDb);
            var carpetas = new CarpetasConfiguradas(conexion);
            Directory.CreateDirectory(carpeta1);
            Directory.CreateDirectory(carpeta2);
            carpetas.Agregar(carpeta1);
            carpetas.Agregar(carpeta2);

            for (int i = 0; i < 100; i++)
            {
                File.WriteAllText(Path.Combine(carpeta1, $"doc_{i}.pdf"), "contenido");
                File.WriteAllText(Path.Combine(carpeta2, $"doc_{i}.pdf"), "contenido");
            }

            var buscador = new Buscador(conexion);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                buscador.Buscar("123", AlcanceBusqueda.TodasLasCarpetas, null, cts.Token)
            );
        }
        finally
        {
            Borrar(rutaDb);
            BorrarCarpetas(carpeta1, carpeta2);
        }
    }

    [Fact]
    public void Indice_con_41234_buscar_123_no_lo_tiene()
    {
        string rutaDb = RutaTemporal();
        try
        {
            using var conexion = BaseComun.Abrir(rutaDb);
            var documentos = new Documentos(conexion);
            var carpetas = new CarpetasConfiguradas(conexion);

            string carpeta1 = Path.Combine(
                Path.GetTempPath(),
                "HormigueroBuscaderoTests",
                Guid.NewGuid().ToString("N"),
                "carpeta1"
            );
            Directory.CreateDirectory(carpeta1);
            carpetas.Agregar(carpeta1);

            documentos.Guardar(
                new DocumentoIndexado(
                    Path.Combine(carpeta1, "doc_41234.pdf"),
                    carpeta1,
                    "doc_41234.pdf",
                    1000L,
                    new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    null,
                    "correcto",
                    true,
                    new List<(string Numero, string Prefijo, string Sufijo, string Origen)>
                    {
                        ("41234", "", "", "nombre"),
                    }
                )
            );

            var buscador = new Buscador(conexion);
            var resultado = buscador.Buscar(
                "123",
                AlcanceBusqueda.TodasLasCarpetas,
                null,
                CancellationToken.None
            );

            Assert.Empty(resultado);
        }
        finally
        {
            Borrar(rutaDb);
        }
    }
}
