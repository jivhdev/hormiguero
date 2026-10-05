using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public static class BaseComun
{
    public static string RutaPorDefecto { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Hormiguero",
            "hormiguero.db"
        );

    public static SqliteConnection Abrir(string ruta)
    {
        string? carpeta = Path.GetDirectoryName(ruta);
        if (!string.IsNullOrEmpty(carpeta))
        {
            Directory.CreateDirectory(carpeta);
        }

        var constructor = new SqliteConnectionStringBuilder { DataSource = ruta };
        var conexion = new SqliteConnection(constructor.ToString());
        conexion.Open();

        try
        {
            using (var comando = conexion.CreateCommand())
            {
                comando.CommandText = "PRAGMA journal_mode=WAL;";
                comando.ExecuteScalar();
            }

            using (var comando = conexion.CreateCommand())
            {
                comando.CommandText = "PRAGMA busy_timeout=5000;";
                comando.ExecuteNonQuery();
            }

            using (var comando = conexion.CreateCommand())
            {
                comando.CommandText = "PRAGMA foreign_keys=ON;";
                comando.ExecuteNonQuery();
            }

            Migraciones.Aplicar(conexion, Migraciones.Todas);
            _ = new RepositorioCalendariosFeriados(conexion);
        }
        catch
        {
            conexion.Dispose();
            throw;
        }

        try
        {
            // Un fallo del respaldo no puede impedir usar la base: se ignora y se sigue.
            Respaldo.HacerSiCorresponde(
                conexion,
                Path.Combine(carpeta ?? ".", "respaldos"),
                DateTime.Now
            );
        }
        catch { }

        return conexion;
    }
}
