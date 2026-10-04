using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public static class Migraciones
{
    public static readonly IReadOnlyList<(int Version, string Sql)> Todas =
    [
        (
            1,
            "CREATE TABLE IF NOT EXISTS configuracion(clave TEXT PRIMARY KEY, valor TEXT NOT NULL);"
        ),
        (
            2,
            "CREATE TABLE IF NOT EXISTS documentos("
                + "id INTEGER PRIMARY KEY, "
                + "ruta TEXT NOT NULL UNIQUE COLLATE NOCASE, "
                + "carpeta_raiz TEXT NOT NULL COLLATE NOCASE, "
                + "nombre TEXT NOT NULL, "
                + "tamano INTEGER NOT NULL, "
                + "modificado TEXT NOT NULL, "
                + "huella TEXT, "
                + "estado TEXT NOT NULL, "
                + "tiene_texto INTEGER NOT NULL, "
                + "indexado_en TEXT NOT NULL); "
                + "CREATE TABLE IF NOT EXISTS numeros_documento("
                + "documento_id INTEGER NOT NULL REFERENCES documentos(id) ON DELETE CASCADE, "
                + "numero TEXT NOT NULL, "
                + "prefijo TEXT NOT NULL, "
                + "sufijo TEXT NOT NULL, "
                + "origen TEXT NOT NULL); "
                + "CREATE INDEX IF NOT EXISTS idx_numeros_documento_numero ON numeros_documento(numero); "
                + "CREATE INDEX IF NOT EXISTS idx_documentos_huella ON documentos(huella); "
                + "CREATE INDEX IF NOT EXISTS idx_documentos_carpeta_raiz ON documentos(carpeta_raiz);"
        ),
        (
            3,
            // ADR-001 de Archivero: configuraciones de identificación y registro de auditoría.
            "CREATE TABLE IF NOT EXISTS identificaciones("
                + "id INTEGER PRIMARY KEY, "
                + "tipo TEXT NOT NULL COLLATE NOCASE, "
                + "emisor TEXT NOT NULL COLLATE NOCASE, "
                + "datos TEXT NOT NULL, "
                + "actualizada TEXT NOT NULL, "
                + "UNIQUE(tipo, emisor)); "
                + "CREATE TABLE IF NOT EXISTS auditoria("
                + "id INTEGER PRIMARY KEY, "
                + "fecha TEXT NOT NULL, "
                + "app TEXT NOT NULL, "
                + "accion TEXT NOT NULL, "
                + "origen TEXT NOT NULL, "
                + "destino TEXT, "
                + "huella TEXT, "
                + "resultado TEXT NOT NULL); "
                + "CREATE INDEX IF NOT EXISTS idx_auditoria_fecha ON auditoria(fecha);"
        ),
    ];

    public static void Aplicar(
        SqliteConnection conexion,
        IReadOnlyList<(int Version, string Sql)> migraciones
    )
    {
        // ADR-001: la regla se revisa antes de tocar la base para que un rechazo
        // no deje nada aplicado a medias.
        foreach (var (_, sql) in migraciones)
        {
            if (
                sql.Contains("DROP ", StringComparison.OrdinalIgnoreCase)
                || sql.Contains("RENAME", StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new InvalidOperationException(
                    "La migración contiene DROP o RENAME; solo se permite agregar (ADR-001)."
                );
            }
        }

        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText =
                "CREATE TABLE IF NOT EXISTS migraciones(version INTEGER PRIMARY KEY, aplicada_en TEXT NOT NULL);";
            comando.ExecuteNonQuery();
        }

        var versionesAplicadas = new HashSet<int>();
        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText = "SELECT version FROM migraciones;";
            using var lector = comando.ExecuteReader();
            while (lector.Read())
            {
                versionesAplicadas.Add(lector.GetInt32(0));
            }
        }

        foreach (var (version, sql) in migraciones)
        {
            if (versionesAplicadas.Contains(version))
            {
                continue;
            }

            using var transaccion = conexion.BeginTransaction();
            using (var comando = conexion.CreateCommand())
            {
                comando.Transaction = transaccion;
                comando.CommandText = sql;
                comando.ExecuteNonQuery();
            }

            using (var comando = conexion.CreateCommand())
            {
                comando.Transaction = transaccion;
                comando.CommandText =
                    "INSERT INTO migraciones(version, aplicada_en) VALUES ($version, $aplicada_en);";
                comando.Parameters.AddWithValue("$version", version);
                comando.Parameters.AddWithValue("$aplicada_en", DateTime.Now.ToString("o"));
                comando.ExecuteNonQuery();
            }

            transaccion.Commit();
        }
    }
}
