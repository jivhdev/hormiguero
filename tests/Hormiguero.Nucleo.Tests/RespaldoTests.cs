using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Tests;

public class RespaldoTests
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

    private static SqliteConnection AbrirConDatos(string ruta)
    {
        var conexion = BaseComun.Abrir(ruta);
        using var comando = conexion.CreateCommand();
        comando.CommandText = "INSERT INTO configuracion(clave, valor) VALUES ('prueba', '1');";
        comando.ExecuteNonQuery();
        return conexion;
    }

    private static SqliteConnection CrearRespaldo(
        SqliteConnection origen,
        string carpeta,
        DateTime ahora
    )
    {
        string? ruta = Respaldo.HacerSiCorresponde(origen, carpeta, ahora);
        Assert.NotNull(ruta);
        var destino = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = ruta }.ToString()
        );
        destino.Open();
        return destino;
    }

    [Fact]
    public void Crea_respaldo_si_no_hay()
    {
        string ruta = RutaTemporal();
        try
        {
            using var origen = AbrirConDatos(ruta);
            using var comando = origen.CreateCommand();
            comando.CommandText = "SELECT COUNT(*) FROM configuracion;";
            Assert.Equal(1L, Convert.ToInt64(comando.ExecuteScalar()));

            string carpeta = Path.Combine(Path.GetDirectoryName(ruta)!, "respaldos-de-prueba");
            Assert.False(Directory.Exists(carpeta));

            DateTime ahora = DateTime.Now;
            using var respaldo = CrearRespaldo(origen, carpeta, ahora);

            Assert.True(Directory.Exists(carpeta));
            string[] archivos = Directory.GetFiles(carpeta, "hormiguero-*.db");
            string esperado = $"hormiguero-{ahora:yyyyMMdd-HHmmss}.db";
            Assert.Equal([esperado], archivos.Select(Path.GetFileName));

            using var comandoRespaldo = respaldo.CreateCommand();
            comandoRespaldo.CommandText = "SELECT COUNT(*) FROM configuracion;";
            Assert.Equal(1L, Convert.ToInt64(comandoRespaldo.ExecuteScalar()));
        }
        finally
        {
            Borrar(ruta);
        }
    }

    [Fact]
    public void No_repite_antes_de_24_horas()
    {
        string ruta = RutaTemporal();
        try
        {
            using var origen = AbrirConDatos(ruta);
            string carpeta = Path.Combine(Path.GetDirectoryName(ruta)!, "respaldos-de-prueba");

            DateTime ahora = DateTime.Now;
            using (CrearRespaldo(origen, carpeta, ahora)) { }

            // Un respaldo de hace 23 h aún está fresco: no corresponde otro.
            Assert.Null(Respaldo.HacerSiCorresponde(origen, carpeta, ahora.AddHours(23)));
            Assert.Single(Directory.GetFiles(carpeta, "hormiguero-*.db"));
        }
        finally
        {
            Borrar(ruta);
        }
    }

    [Fact]
    public void Deja_solo_los_7_mas_nuevos()
    {
        string ruta = RutaTemporal();
        try
        {
            using var origen = AbrirConDatos(ruta);
            string carpeta = Path.Combine(Path.GetDirectoryName(ruta)!, "respaldos-de-prueba");
            Directory.CreateDirectory(carpeta);

            DateTime baseTiempo = DateTime.Now;
            for (int i = 1; i <= 9; i++)
            {
                File.WriteAllText(Path.Combine(carpeta, $"hormiguero-202601{i:00}-120000.db"), "");
                File.SetLastWriteTime(
                    Path.Combine(carpeta, $"hormiguero-202601{i:00}-120000.db"),
                    baseTiempo.AddDays(-10 + i)
                );
            }

            DateTime ahora = baseTiempo.AddDays(1);
            using (CrearRespaldo(origen, carpeta, ahora)) { }

            string[] archivos = Directory.GetFiles(carpeta, "hormiguero-*.db");
            Assert.Equal(7, archivos.Length);
            string[] nombres = archivos
                .Select(archivo => Path.GetFileName(archivo)!)
                .Order()
                .ToArray();
            Assert.Equal(
                new[]
                {
                    "hormiguero-20260104-120000.db",
                    "hormiguero-20260105-120000.db",
                    "hormiguero-20260106-120000.db",
                    "hormiguero-20260107-120000.db",
                    "hormiguero-20260108-120000.db",
                    "hormiguero-20260109-120000.db",
                    $"hormiguero-{ahora:yyyyMMdd-HHmmss}.db",
                },
                nombres
            );
        }
        finally
        {
            Borrar(ruta);
        }
    }

    [Fact]
    public void No_borra_archivos_ajenos()
    {
        string ruta = RutaTemporal();
        try
        {
            using var origen = AbrirConDatos(ruta);
            string carpeta = Path.Combine(Path.GetDirectoryName(ruta)!, "respaldos-de-prueba");
            Directory.CreateDirectory(carpeta);

            DateTime ahora = DateTime.Now;
            for (int i = 1; i <= 9; i++)
            {
                string rutaAjena = Path.Combine(carpeta, $"hormiguero-202601{i:00}-120000.viejo");
                File.WriteAllText(rutaAjena, "");
                File.SetLastWriteTime(rutaAjena, ahora.AddDays(-10 + i));
            }
            string rutaLargaVieja = Path.Combine(carpeta, "otro-archivo.db");
            File.WriteAllText(rutaLargaVieja, "");
            File.SetLastWriteTime(rutaLargaVieja, ahora.AddDays(-20));

            using (CrearRespaldo(origen, carpeta, ahora)) { }

            Assert.Equal(9, Directory.GetFiles(carpeta, "*.viejo").Length);
            Assert.True(File.Exists(rutaLargaVieja));
            Assert.Single(Directory.GetFiles(carpeta, "hormiguero-*.db"));
        }
        finally
        {
            Borrar(ruta);
        }
    }

    [Fact]
    public void Un_error_de_respaldo_no_impide_abrir()
    {
        string ruta = RutaTemporal();
        try
        {
            string carpetaBase = Path.GetDirectoryName(ruta)!;
            string carpetaRespaldos = Path.Combine(carpetaBase, "respaldos");

            // Un archivo donde debería ir la carpeta respaldos impide crearla y
            // fuerza el error del respaldo; la base debe abrir igual.
            Directory.CreateDirectory(carpetaBase);
            File.WriteAllText(carpetaRespaldos, "");

            using var conexion = BaseComun.Abrir(ruta);

            using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT COUNT(*) FROM configuracion;";
            Assert.Equal(0L, Convert.ToInt64(comando.ExecuteScalar()));
        }
        finally
        {
            Borrar(ruta);
        }
    }
}
