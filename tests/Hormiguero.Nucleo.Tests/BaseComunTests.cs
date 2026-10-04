using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Tests;

public class BaseComunTests
{
    private static string RutaTemporal() =>
        Path.Combine(
            Path.GetTempPath(),
            "HormigueroTests",
            Guid.NewGuid().ToString("N"),
            "hormiguero.db"
        );

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

    [Fact]
    public void Crea_la_base_y_la_carpeta()
    {
        string ruta = RutaTemporal();
        try
        {
            Assert.False(File.Exists(ruta));

            using var conexion = BaseComun.Abrir(ruta);

            Assert.True(File.Exists(ruta));
            using (var comando = conexion.CreateCommand())
            {
                comando.CommandText =
                    "INSERT INTO configuracion(clave, valor) VALUES ('prueba', '1');";
                comando.ExecuteNonQuery();
            }
            using (var comando = conexion.CreateCommand())
            {
                comando.CommandText = "SELECT valor FROM configuracion WHERE clave = 'prueba';";
                Assert.Equal("1", comando.ExecuteScalar());
            }
        }
        finally
        {
            Borrar(ruta);
        }
    }

    [Fact]
    public void Queda_en_modo_WAL()
    {
        string ruta = RutaTemporal();
        try
        {
            using var conexion = BaseComun.Abrir(ruta);

            using var comando = conexion.CreateCommand();
            comando.CommandText = "PRAGMA journal_mode;";
            Assert.Equal("wal", comando.ExecuteScalar());
        }
        finally
        {
            Borrar(ruta);
        }
    }

    [Fact]
    public void No_repite_migraciones()
    {
        string ruta = RutaTemporal();
        try
        {
            using (BaseComun.Abrir(ruta)) { }

            using (var conexion = BaseComun.Abrir(ruta))
            {
                using var comando = conexion.CreateCommand();
                comando.CommandText = "SELECT COUNT(*) FROM migraciones;";
                Assert.Equal(
                    (long)Migraciones.Todas.Count,
                    Convert.ToInt64(comando.ExecuteScalar())
                );
            }
        }
        finally
        {
            Borrar(ruta);
        }
    }

    [Fact]
    public void Rechaza_migracion_que_borra_o_renombra()
    {
        string ruta = RutaTemporal();
        try
        {
            using var conexion = BaseComun.Abrir(ruta);

            string[] sqlDestructivos =
            [
                "DROP TABLE configuracion;",
                "ALTER TABLE configuracion RENAME TO configuracion_vieja;",
            ];
            foreach (string sql in sqlDestructivos)
            {
                var migracion = new[] { (2, sql) };
                Assert.Throws<InvalidOperationException>(() =>
                    Migraciones.Aplicar(conexion, migracion)
                );
            }

            using (var comando = conexion.CreateCommand())
            {
                comando.CommandText = "SELECT COUNT(*) FROM migraciones;";
                Assert.Equal(
                    (long)Migraciones.Todas.Count,
                    Convert.ToInt64(comando.ExecuteScalar())
                );
            }
            using (var comando = conexion.CreateCommand())
            {
                comando.CommandText = "SELECT COUNT(*) FROM configuracion;";
                Assert.Equal(0L, Convert.ToInt64(comando.ExecuteScalar()));
            }
        }
        finally
        {
            Borrar(ruta);
        }
    }

    [Fact]
    public async Task Dos_conexiones_leen_y_escriben_a_la_vez()
    {
        string ruta = RutaTemporal();
        try
        {
            using var escritor = BaseComun.Abrir(ruta);
            using var lector = BaseComun.Abrir(ruta);

            var tareaEscritor = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    using var comando = escritor.CreateCommand();
                    comando.CommandText =
                        "INSERT INTO configuracion(clave, valor) VALUES ($clave, $valor);";
                    comando.Parameters.AddWithValue("$clave", $"clave{i}");
                    comando.Parameters.AddWithValue("$valor", $"valor{i}");
                    comando.ExecuteNonQuery();
                }
            });

            var tareaLector = Task.Run(() =>
            {
                for (int intento = 0; ; intento++)
                {
                    Assert.True(intento < 100_000, "El lector nunca vio las 100 filas.");

                    using var comando = lector.CreateCommand();
                    comando.CommandText = "SELECT COUNT(*) FROM configuracion;";
                    if (Convert.ToInt64(comando.ExecuteScalar()) == 100)
                    {
                        return;
                    }
                }
            });

            await Task.WhenAll(tareaEscritor, tareaLector);

            using (var comando = escritor.CreateCommand())
            {
                comando.CommandText = "SELECT COUNT(*) FROM configuracion;";
                Assert.Equal(100L, Convert.ToInt64(comando.ExecuteScalar()));
            }
        }
        finally
        {
            Borrar(ruta);
        }
    }
}
