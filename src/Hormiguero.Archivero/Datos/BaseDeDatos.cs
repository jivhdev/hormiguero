using System.IO;
using Microsoft.Data.Sqlite;

namespace Archivero.Datos;

public static class BaseDeDatos
{
    public static string RutaArchivo { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Archivero",
        "archivero.db");

    public static SqliteConnection CrearConexion()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        var conexion = new SqliteConnection($"Data Source={RutaArchivo}");
        conexion.Open();
        return conexion;
    }

    public static void AsegurarEsquema()
    {
        using var conexion = CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Configuracion (
                Clave TEXT PRIMARY KEY,
                Valor TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS EntidadesConocidas (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Categoria TEXT NOT NULL CHECK (Categoria IN ('Emisor', 'Tipo')),
                Nombre TEXT NOT NULL,
                UNIQUE (Categoria, Nombre)
            );

            CREATE TABLE IF NOT EXISTS Configuraciones (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EmisorId INTEGER NOT NULL REFERENCES EntidadesConocidas (Id),
                TipoId INTEGER NOT NULL REFERENCES EntidadesConocidas (Id),
                CarpetaDestino TEXT NOT NULL,
                FormatoCarpeta TEXT NOT NULL CHECK (FormatoCarpeta IN ('Directo', 'Anio', 'AnioSemestre', 'AnioTrimestre', 'AnioMes', 'AnioQuincena', 'AnioSemana', 'AnioMesDia', 'MesSinAnio', 'SemanaDelMes', 'Personalizado')),
                PatronCarpeta TEXT NULL,
                Renombrar INTEGER NOT NULL DEFAULT 0,
                AbrirDespuesDeGuardar INTEGER NOT NULL DEFAULT 0,
                UNIQUE (EmisorId, TipoId)
            );

            CREATE TABLE IF NOT EXISTS PatronesReconocimiento (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ConfiguracionId INTEGER NOT NULL REFERENCES Configuraciones (Id)
            );

            CREATE TABLE IF NOT EXISTS Marcas (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PatronId INTEGER NOT NULL REFERENCES PatronesReconocimiento (Id),
                Campo TEXT NOT NULL CHECK (Campo IN ('Emisor', 'Tipo', 'Fecha', 'NombreArchivo')),
                Pagina INTEGER NOT NULL,
                X REAL NOT NULL,
                Y REAL NOT NULL,
                Ancho REAL NOT NULL,
                Alto REAL NOT NULL,
                TextoReferencia TEXT NULL,
                UNIQUE (PatronId, Campo)
            );

            CREATE TABLE IF NOT EXISTS Pendientes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RutaArchivo TEXT NOT NULL UNIQUE,
                FechaDetectado TEXT NOT NULL,
                Motivo TEXT NOT NULL DEFAULT 'NuevoDocumento'
            );

            CREATE TABLE IF NOT EXISTS Borradores (
                RutaArchivo TEXT PRIMARY KEY,
                Datos TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS UbicacionesSinTexto (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CarpetaMadre TEXT NOT NULL,
                FormatoCarpeta TEXT NOT NULL,
                PatronCarpeta TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS AtajosGuardadoRapido (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nombre TEXT NOT NULL UNIQUE,
                CarpetaMadre TEXT NOT NULL,
                FormatoCarpeta TEXT NOT NULL,
                PatronCarpeta TEXT NULL,
                ReglaNombre TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS GuardadosRecientes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RutaFinal TEXT NOT NULL,
                FechaHora TEXT NOT NULL
            );
            """;
        comando.ExecuteNonQuery();

        AgregarColumnaSiFalta(conexion, "Marcas", "TextoReferencia", "TEXT NULL");
        AgregarColumnaSiFalta(conexion, "Pendientes", "Motivo", "TEXT NOT NULL DEFAULT 'NuevoDocumento'");
        AgregarColumnaSiFalta(conexion, "Configuraciones", "AbrirDespuesDeGuardar", "INTEGER NOT NULL DEFAULT 0");

        MigrarCheckFormatoCarpeta(conexion);

        // Después de la migración de Caso-3 a propósito: esa migración recrea Configuraciones
        // copiando una lista fija de columnas, y una columna agregada antes se perdería.
        AgregarColumnaSiFalta(conexion, "Configuraciones", "PreguntarNombre", "INTEGER NOT NULL DEFAULT 0");
    }

    /// <summary>
    /// Migración de Caso-3: las bases creadas antes del rediseño tienen un CHECK que solo acepta
    /// ('Directo', 'Anio', 'AnioMes'). SQLite no permite modificar un CHECK con ALTER TABLE, así
    /// que la tabla se recrea y se copian los datos (procedimiento estándar de migración de
    /// SQLite, con las foreign keys apagadas solo durante la reconstrucción). Es idempotente:
    /// si la tabla ya admite los tipos nuevos, no hace nada.
    /// </summary>
    private static void MigrarCheckFormatoCarpeta(SqliteConnection conexion)
    {
        string? sqlActual;
        using (var leerSql = conexion.CreateCommand())
        {
            leerSql.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'Configuraciones';";
            sqlActual = leerSql.ExecuteScalar() as string;
        }

        if (sqlActual is null || sqlActual.Contains("'AnioSemestre'", StringComparison.Ordinal))
        {
            return;
        }

        using (var apagarFks = conexion.CreateCommand())
        {
            // Fuera de la transacción a propósito: PRAGMA foreign_keys no tiene efecto adentro.
            apagarFks.CommandText = "PRAGMA foreign_keys = OFF;";
            apagarFks.ExecuteNonQuery();
        }

        try
        {
            using var transaccion = conexion.BeginTransaction();

            using (var crear = conexion.CreateCommand())
            {
                crear.Transaction = transaccion;
                crear.CommandText =
                    """
                    CREATE TABLE Configuraciones_Nueva (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        EmisorId INTEGER NOT NULL REFERENCES EntidadesConocidas (Id),
                        TipoId INTEGER NOT NULL REFERENCES EntidadesConocidas (Id),
                        CarpetaDestino TEXT NOT NULL,
                        FormatoCarpeta TEXT NOT NULL CHECK (FormatoCarpeta IN ('Directo', 'Anio', 'AnioSemestre', 'AnioTrimestre', 'AnioMes', 'AnioQuincena', 'AnioSemana', 'AnioMesDia', 'MesSinAnio', 'SemanaDelMes', 'Personalizado')),
                        PatronCarpeta TEXT NULL,
                        Renombrar INTEGER NOT NULL DEFAULT 0,
                        AbrirDespuesDeGuardar INTEGER NOT NULL DEFAULT 0,
                        UNIQUE (EmisorId, TipoId)
                    );

                    INSERT INTO Configuraciones_Nueva (Id, EmisorId, TipoId, CarpetaDestino, FormatoCarpeta, PatronCarpeta, Renombrar, AbrirDespuesDeGuardar)
                    SELECT Id, EmisorId, TipoId, CarpetaDestino, FormatoCarpeta, PatronCarpeta, Renombrar, AbrirDespuesDeGuardar
                    FROM Configuraciones;

                    DROP TABLE Configuraciones;

                    ALTER TABLE Configuraciones_Nueva RENAME TO Configuraciones;
                    """;
                crear.ExecuteNonQuery();
            }

            transaccion.Commit();
        }
        finally
        {
            using var encenderFks = conexion.CreateCommand();
            encenderFks.CommandText = "PRAGMA foreign_keys = ON;";
            encenderFks.ExecuteNonQuery();
        }

        using var verificarFks = conexion.CreateCommand();
        verificarFks.CommandText = "PRAGMA foreign_key_check;";
        using var lector = verificarFks.ExecuteReader();
        if (lector.Read())
        {
            throw new InvalidOperationException("La migración de Configuraciones rompió una referencia existente.");
        }
    }

    /// <summary>
    /// Migración mínima para bases ya existentes: agrega una columna nueva si todavía no está,
    /// sin tocar los datos ya guardados. SQLite no soporta "ADD COLUMN IF NOT EXISTS" directo.
    /// </summary>
    private static void AgregarColumnaSiFalta(SqliteConnection conexion, string tabla, string columna, string definicionSql)
    {
        using (var verificar = conexion.CreateCommand())
        {
            verificar.CommandText = $"PRAGMA table_info({tabla});";
            using var lector = verificar.ExecuteReader();
            while (lector.Read())
            {
                if (string.Equals(lector.GetString(1), columna, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        using var alterar = conexion.CreateCommand();
        alterar.CommandText = $"ALTER TABLE {tabla} ADD COLUMN {columna} {definicionSql};";
        alterar.ExecuteNonQuery();
    }
}
