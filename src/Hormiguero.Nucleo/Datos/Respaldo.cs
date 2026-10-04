using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public static class Respaldo
{
    public static string? HacerSiCorresponde(
        SqliteConnection conexion,
        string carpetaRespaldos,
        DateTime ahora,
        string nombre = "hormiguero"
    )
    {
        Directory.CreateDirectory(carpetaRespaldos);

        var respaldos = Listar(carpetaRespaldos, nombre);
        if (respaldos.Any(archivo => archivo.LastWriteTime > ahora.AddHours(-24)))
        {
            return null;
        }

        string rutaDestino = Path.Combine(carpetaRespaldos, $"{nombre}-{ahora:yyyyMMdd-HHmmss}.db");
        using var destino = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = rutaDestino }.ToString()
        );
        destino.Open();
        // Copia en caliente con la API de SQLite: la base vive en WAL y copiar el
        // archivo a mano daría un respaldo sin lo que aún no se ha volcado.
        conexion.BackupDatabase(destino);

        foreach (FileInfo archivo in Listar(carpetaRespaldos, nombre).Skip(7))
        {
            archivo.Delete();
        }

        return rutaDestino;
    }

    private static List<FileInfo> Listar(string carpetaRespaldos, string nombre) =>
        Directory
            .EnumerateFiles(carpetaRespaldos, $"{nombre}-*.db")
            .Select(ruta => new FileInfo(ruta))
            .OrderByDescending(archivo => archivo.LastWriteTime)
            .ToList();
}
