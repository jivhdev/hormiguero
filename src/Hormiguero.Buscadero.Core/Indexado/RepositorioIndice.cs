using Microsoft.Data.Sqlite;

namespace Buscadero.Core.Indexado;

public sealed class RepositorioIndice
{
    private readonly string _cadenaConexion;

    public RepositorioIndice(string rutaBaseDeDatos)
    {
        _cadenaConexion = new SqliteConnectionStringBuilder
        {
            DataSource = rutaBaseDeDatos,
        }.ToString();
        Inicializar();
    }

    private void Inicializar()
    {
        using var conexion = AbrirConexion();
        // Fase B-2a: modo WAL para que buscar y actualizar el índice en segundo plano
        // puedan ocurrir a la vez (queda guardado en la base).
        using (var wal = conexion.CreateCommand())
        {
            wal.CommandText = "PRAGMA journal_mode=WAL;";
            wal.ExecuteScalar();
        }
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            CREATE TABLE IF NOT EXISTS ArchivosIndexados (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Ruta TEXT NOT NULL,
                RutaClave TEXT NOT NULL UNIQUE,
                Nombre TEXT NOT NULL,
                CarpetaContenedora TEXT NOT NULL,
                CarpetaClave TEXT NOT NULL,
                FechaModificacion INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Archivos_CarpetaClave ON ArchivosIndexados (CarpetaClave);

            CREATE TABLE IF NOT EXISTS CarpetasIndexadas (
                RutaClave TEXT PRIMARY KEY,
                Ruta TEXT NOT NULL,
                PadreClave TEXT NULL,
                FechaModificacion INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS FrecuenciaCarpetas (
                RutaClave TEXT PRIMARY KEY,
                Ruta TEXT NOT NULL,
                Veces INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ImportacionGuardados (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                UltimoId INTEGER NOT NULL
            );
            """;
        comando.ExecuteNonQuery();
    }

    public long LeerUltimoGuardadoImportado()
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT UltimoId FROM ImportacionGuardados WHERE Id = 1;";
        return (long?)comando.ExecuteScalar() ?? 0;
    }

    public void GuardarUltimoGuardadoImportado(long id)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            INSERT INTO ImportacionGuardados (Id, UltimoId)
            VALUES (1, $id)
            ON CONFLICT(Id) DO UPDATE SET UltimoId = $id;
            """;
        comando.Parameters.AddWithValue("$id", id);
        comando.ExecuteNonQuery();
    }

    public void AgregarArchivo(string ruta)
    {
        var carpeta = Path.GetDirectoryName(ruta) ?? string.Empty;
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            INSERT INTO ArchivosIndexados (Ruta, RutaClave, Nombre, CarpetaContenedora, CarpetaClave, FechaModificacion)
            VALUES ($ruta, $rutaClave, $nombre, $carpeta, $carpetaClave, $fecha)
            ON CONFLICT(RutaClave) DO UPDATE SET
                Ruta = $ruta,
                Nombre = $nombre,
                CarpetaContenedora = $carpeta,
                CarpetaClave = $carpetaClave,
                FechaModificacion = $fecha;
            """;
        comando.Parameters.AddWithValue("$ruta", ruta);
        comando.Parameters.AddWithValue("$rutaClave", ClaveRuta(ruta));
        comando.Parameters.AddWithValue("$nombre", Path.GetFileName(ruta));
        comando.Parameters.AddWithValue("$carpeta", carpeta);
        comando.Parameters.AddWithValue("$carpetaClave", ClaveRuta(carpeta));
        comando.Parameters.AddWithValue("$fecha", File.GetLastWriteTimeUtc(ruta).Ticks);
        comando.ExecuteNonQuery();
    }

    private SqliteConnection AbrirConexion()
    {
        var conexion = new SqliteConnection(_cadenaConexion);
        conexion.Open();
        return conexion;
    }

    public IReadOnlyDictionary<string, long> ObtenerFechasCarpetas()
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT RutaClave, FechaModificacion FROM CarpetasIndexadas;";
        using var lector = comando.ExecuteReader();

        var resultado = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        while (lector.Read())
        {
            resultado[lector.GetString(0)] = lector.GetInt64(1);
        }

        return resultado;
    }

    public IReadOnlyList<string> ObtenerCarpetasHijas(string padreClave)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT RutaClave FROM CarpetasIndexadas WHERE PadreClave = $padre;";
        comando.Parameters.AddWithValue("$padre", padreClave);
        using var lector = comando.ExecuteReader();

        var resultado = new List<string>();
        while (lector.Read())
        {
            resultado.Add(lector.GetString(0));
        }

        return resultado;
    }

    public void GuardarCarpeta(
        string ruta,
        string rutaClave,
        string? padreClave,
        long fechaModificacion
    )
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            INSERT INTO CarpetasIndexadas (RutaClave, Ruta, PadreClave, FechaModificacion)
            VALUES ($clave, $ruta, $padre, $fecha)
            ON CONFLICT(RutaClave) DO UPDATE SET
                Ruta = $ruta,
                PadreClave = $padre,
                FechaModificacion = $fecha;
            """;
        comando.Parameters.AddWithValue("$clave", rutaClave);
        comando.Parameters.AddWithValue("$ruta", ruta);
        comando.Parameters.AddWithValue("$padre", (object?)padreClave ?? DBNull.Value);
        comando.Parameters.AddWithValue("$fecha", fechaModificacion);
        comando.ExecuteNonQuery();
    }

    public void ReemplazarArchivos(string rutaClaveCarpeta, IReadOnlyList<ArchivoIndexado> archivos)
    {
        using var conexion = AbrirConexion();
        using var transaccion = conexion.BeginTransaction();

        using (var borrar = conexion.CreateCommand())
        {
            borrar.Transaction = transaccion;
            borrar.CommandText = "DELETE FROM ArchivosIndexados WHERE CarpetaClave = $clave;";
            borrar.Parameters.AddWithValue("$clave", rutaClaveCarpeta);
            borrar.ExecuteNonQuery();
        }

        foreach (var archivo in archivos)
        {
            using var insertar = conexion.CreateCommand();
            insertar.Transaction = transaccion;
            insertar.CommandText = """
                INSERT INTO ArchivosIndexados (Ruta, RutaClave, Nombre, CarpetaContenedora, CarpetaClave, FechaModificacion)
                VALUES ($ruta, $rutaClave, $nombre, $carpeta, $carpetaClave, $fecha);
                """;
            insertar.Parameters.AddWithValue("$ruta", archivo.Ruta);
            insertar.Parameters.AddWithValue("$rutaClave", ClaveRuta(archivo.Ruta));
            insertar.Parameters.AddWithValue("$nombre", archivo.Nombre);
            insertar.Parameters.AddWithValue("$carpeta", archivo.CarpetaContenedora);
            insertar.Parameters.AddWithValue("$carpetaClave", rutaClaveCarpeta);
            insertar.Parameters.AddWithValue("$fecha", archivo.FechaModificacion);
            insertar.ExecuteNonQuery();
        }

        transaccion.Commit();
    }

    public void EliminarSubarbol(string rutaClave)
    {
        var prefijo = EscaparLike(rutaClave) + "\\\\%";
        using var conexion = AbrirConexion();
        using var transaccion = conexion.BeginTransaction();

        using (var archivos = conexion.CreateCommand())
        {
            archivos.Transaction = transaccion;
            archivos.CommandText = """
                DELETE FROM ArchivosIndexados
                WHERE CarpetaClave = $clave OR CarpetaClave LIKE $prefijo ESCAPE '\';
                """;
            archivos.Parameters.AddWithValue("$clave", rutaClave);
            archivos.Parameters.AddWithValue("$prefijo", prefijo);
            archivos.ExecuteNonQuery();
        }

        using (var carpetas = conexion.CreateCommand())
        {
            carpetas.Transaction = transaccion;
            carpetas.CommandText = """
                DELETE FROM CarpetasIndexadas
                WHERE RutaClave = $clave OR RutaClave LIKE $prefijo ESCAPE '\';
                """;
            carpetas.Parameters.AddWithValue("$clave", rutaClave);
            carpetas.Parameters.AddWithValue("$prefijo", prefijo);
            carpetas.ExecuteNonQuery();
        }

        transaccion.Commit();
    }

    public IReadOnlyList<ArchivoIndexado> ObtenerArchivos(string? filtroCarpeta = null)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();

        var filtroClave = string.IsNullOrWhiteSpace(filtroCarpeta)
            ? null
            : ClaveRuta(filtroCarpeta);

        var sql =
            "SELECT Id, Ruta, Nombre, CarpetaContenedora, FechaModificacion FROM ArchivosIndexados";
        if (filtroClave is not null)
        {
            sql += " WHERE (CarpetaClave = $filtro OR CarpetaClave LIKE $prefijo ESCAPE '\\')";
        }

        sql += " ORDER BY FechaModificacion DESC, Ruta ASC;";
        comando.CommandText = sql;
        if (filtroClave is not null)
        {
            comando.Parameters.AddWithValue("$filtro", filtroClave);
            comando.Parameters.AddWithValue("$prefijo", EscaparLike(filtroClave) + "\\\\%");
        }

        using var lector = comando.ExecuteReader();
        var resultado = new List<ArchivoIndexado>();
        while (lector.Read())
        {
            resultado.Add(
                new ArchivoIndexado
                {
                    Id = lector.GetInt64(0),
                    Ruta = lector.GetString(1),
                    Nombre = lector.GetString(2),
                    CarpetaContenedora = lector.GetString(3),
                    FechaModificacion = lector.GetInt64(4),
                }
            );
        }

        return resultado;
    }

    public void RegistrarUsoFiltro(string ruta)
    {
        var clave = ClaveRuta(ruta);
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            INSERT INTO FrecuenciaCarpetas (RutaClave, Ruta, Veces)
            VALUES ($clave, $ruta, 1)
            ON CONFLICT(RutaClave) DO UPDATE SET
                Ruta = $ruta,
                Veces = Veces + 1;
            """;
        comando.Parameters.AddWithValue("$clave", clave);
        comando.Parameters.AddWithValue("$ruta", ruta);
        comando.ExecuteNonQuery();
    }

    public IReadOnlyList<string> ObtenerTodasLasCarpetas()
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT Ruta FROM CarpetasIndexadas ORDER BY Ruta ASC;";
        using var lector = comando.ExecuteReader();

        var resultado = new List<string>();
        while (lector.Read())
        {
            resultado.Add(lector.GetString(0));
        }

        return resultado;
    }

    public IReadOnlyList<string> ObtenerCarpetasSugeridas(int maximo)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT c.Ruta
            FROM CarpetasIndexadas c
            LEFT JOIN FrecuenciaCarpetas f ON f.RutaClave = c.RutaClave
            ORDER BY COALESCE(f.Veces, 0) DESC, c.Ruta ASC
            LIMIT $maximo;
            """;
        comando.Parameters.AddWithValue("$maximo", maximo);
        using var lector = comando.ExecuteReader();

        var resultado = new List<string>();
        while (lector.Read())
        {
            resultado.Add(lector.GetString(0));
        }

        return resultado;
    }

    public static string ClaveRuta(string ruta) =>
        ruta.Trim().TrimEnd('\\', '/').ToLowerInvariant();

    private static string EscaparLike(string valor) =>
        valor.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
