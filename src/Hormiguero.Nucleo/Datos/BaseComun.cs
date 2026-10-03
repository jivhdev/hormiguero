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

        Migraciones.Aplicar(conexion, Migraciones.Todas);

        return conexion;
    }
}
