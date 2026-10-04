using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Tests;

public class DocumentosTests
{
    private static string RutaTemporal()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "HormigueroTests",
            Guid.NewGuid().ToString("N"),
            "hormiguero.db"
        );
    }

    private static void Borrar(string ruta)
    {
        SqliteConnection.ClearAllPools();
        string? carpeta = Path.GetDirectoryName(ruta);
        if (!string.IsNullOrEmpty(carpeta) && Directory.Exists(carpeta))
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }

    [Fact]
    public void Guardar_reemplaza_por_ruta()
    {
        string ruta = RutaTemporal();
        try
        {
            using var conexion = BaseComun.Abrir(ruta);
            var documentos = new Documentos(conexion);

            var doc = new DocumentoIndexado(
                @"C:\documentos\factura.pdf",
                @"C:\documentos",
                "factura.pdf",
                1000L,
                new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                "huella1",
                "correcto",
                true,
                new List<(string Numero, string Prefijo, string Sufijo, string Origen)>
                {
                    ("123", "F", "A", "nombre"),
                }
            );
            documentos.Guardar(doc);

            var doc2 = new DocumentoIndexado(
                @"C:\documentos\factura.pdf",
                @"C:\documentos",
                "factura.pdf",
                2000L,
                new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                "huella2",
                "correcto",
                true,
                new List<(string Numero, string Prefijo, string Sufijo, string Origen)>
                {
                    ("456", "F", "B", "texto"),
                }
            );
            documentos.Guardar(doc2);

            using (var comando = conexion.CreateCommand())
            {
                comando.CommandText = "SELECT COUNT(*) FROM documentos;";
                Assert.Equal(1L, Convert.ToInt64(comando.ExecuteScalar()));
            }
            using (var comando = conexion.CreateCommand())
            {
                comando.CommandText = "SELECT COUNT(*) FROM numeros_documento;";
                Assert.Equal(1L, Convert.ToInt64(comando.ExecuteScalar()));
            }
            using (var comando = conexion.CreateCommand())
            {
                comando.CommandText = "SELECT numero FROM numeros_documento;";
                Assert.Equal("456", comando.ExecuteScalar()!.ToString());
            }
        }
        finally
        {
            Borrar(ruta);
        }
    }

    [Fact]
    public void Busqueda_exacta_del_numero()
    {
        string ruta = RutaTemporal();
        try
        {
            using var conexion = BaseComun.Abrir(ruta);
            var documentos = new Documentos(conexion);

            documentos.Guardar(
                new DocumentoIndexado(
                    @"C:\docs\uno.pdf",
                    @"C:\docs",
                    "uno.pdf",
                    100L,
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
                    @"C:\docs\dos.pdf",
                    @"C:\docs",
                    "dos.pdf",
                    200L,
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

            var resultado = documentos.BuscarPorNumero("123");
            Assert.Single(resultado);
            Assert.Equal(@"C:\docs\uno.pdf", resultado[0].Ruta);
        }
        finally
        {
            Borrar(ruta);
        }
    }

    [Fact]
    public void Quitar_borra_sus_numeros()
    {
        string ruta = RutaTemporal();
        try
        {
            using var conexion = BaseComun.Abrir(ruta);
            var documentos = new Documentos(conexion);

            documentos.Guardar(
                new DocumentoIndexado(
                    @"C:\docs\a.pdf",
                    @"C:\docs",
                    "a.pdf",
                    100L,
                    new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    null,
                    "correcto",
                    "correcto" == "correcto" ? false : false,
                    new List<(string Numero, string Prefijo, string Sufijo, string Origen)>
                    {
                        ("999", "", "", "nombre"),
                    }
                )
            );

            documentos.Quitar(@"C:\docs\a.pdf");

            using (var comando = conexion.CreateCommand())
            {
                comando.CommandText = "SELECT COUNT(*) FROM documentos;";
                Assert.Equal(0L, Convert.ToInt64(comando.ExecuteScalar()));
            }
            using (var comando = conexion.CreateCommand())
            {
                comando.CommandText = "SELECT COUNT(*) FROM numeros_documento;";
                Assert.Equal(0L, Convert.ToInt64(comando.ExecuteScalar()));
            }
        }
        finally
        {
            Borrar(ruta);
        }
    }

    [Fact]
    public void Firmas_por_carpeta()
    {
        string ruta = RutaTemporal();
        try
        {
            using var conexion = BaseComun.Abrir(ruta);
            var documentos = new Documentos(conexion);

            documentos.Guardar(
                new DocumentoIndexado(
                    @"C:\docs1\a.pdf",
                    @"C:\docs1",
                    "a.pdf",
                    100L,
                    new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                    null,
                    "correcto",
                    true,
                    new List<(string Numero, string Prefijo, string Sufijo, string Origen)>()
                )
            );
            documentos.Guardar(
                new DocumentoIndexado(
                    @"C:\docs2\b.pdf",
                    @"C:\docs2",
                    "b.pdf",
                    200L,
                    new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc),
                    null,
                    "correcto",
                    true,
                    new List<(string Numero, string Prefijo, string Sufijo, string Origen)>()
                )
            );

            var firmas = documentos.Firmas(@"C:\docs1");
            Assert.Single(firmas);
            Assert.True(firmas.ContainsKey(@"C:\docs1\a.pdf"));
        }
        finally
        {
            Borrar(ruta);
        }
    }

    [Fact]
    public void Rutas_con_caracteres_especiales()
    {
        string ruta = RutaTemporal();
        try
        {
            using var conexion = BaseComun.Abrir(ruta);
            var documentos = new Documentos(conexion);

            string rutaDoc = @"C:\docs\a ""b"" %c.pdf";
            documentos.Guardar(
                new DocumentoIndexado(
                    rutaDoc,
                    @"C:\docs",
                    @"a ""b"" %c.pdf",
                    150L,
                    new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                    "h1",
                    "correcto",
                    true,
                    new List<(string Numero, string Prefijo, string Sufijo, string Origen)>
                    {
                        ("777", "", "", "nombre"),
                    }
                )
            );

            var resultado = documentos.BuscarPorNumero("777");
            Assert.Single(resultado);
            Assert.Equal(rutaDoc, resultado[0].Ruta);
        }
        finally
        {
            Borrar(ruta);
        }
    }
}
