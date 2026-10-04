using Microsoft.Data.Sqlite;

namespace Buscadero.Core.Lineas;

public sealed class RepositorioLineas
{
    private readonly string _cadenaConexion;

    public RepositorioLineas(string rutaBaseDeDatos)
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
        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText = """
                CREATE TABLE IF NOT EXISTS PlantillasLinea (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Nombre TEXT NOT NULL,
                    FechaCreacion TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS PlantillaVagones (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PlantillaId INTEGER NOT NULL,
                    PadreId INTEGER NULL,
                    Orden INTEGER NOT NULL,
                    Nombre TEXT NOT NULL,
                    EsMultiple INTEGER NOT NULL DEFAULT 0,
                    EsAnexo INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS InstanciasLinea (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PlantillaIdOrigen INTEGER NULL,
                    NombrePlantillaOrigen TEXT NULL,
                    Nombre TEXT NOT NULL,
                    FechaCreacion TEXT NOT NULL,
                    EstructuraJson TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS InstanciaVagones (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    InstanciaId INTEGER NOT NULL,
                    PadreId INTEGER NULL,
                    PlantillaVagonId INTEGER NULL,
                    Orden INTEGER NOT NULL,
                    Nombre TEXT NOT NULL,
                    EsMultiple INTEGER NOT NULL DEFAULT 0,
                    EsAnexo INTEGER NOT NULL DEFAULT 0,
                    RutaDocumento TEXT NULL,
                    NombreDocumento TEXT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_PlantillaVagones_Plantilla ON PlantillaVagones (PlantillaId);
                CREATE INDEX IF NOT EXISTS IX_InstanciaVagones_Instancia ON InstanciaVagones (InstanciaId);
                """;
            comando.ExecuteNonQuery();
        }

        AsegurarColumna(conexion, "PlantillaVagones", "ModeloCadenaHijaId", "INTEGER NULL");
        AsegurarColumna(conexion, "PlantillasLinea", "EsModeloHijo", "INTEGER NOT NULL DEFAULT 0");
        AsegurarColumna(conexion, "InstanciasLinea", "CadenaMadreId", "INTEGER NULL");
        AsegurarColumna(conexion, "InstanciasLinea", "InstanciaVagonPadreId", "INTEGER NULL");
        // Caso-12: preferencia de nombre de las cadenas de este modelo. Los modelos ya
        // existentes quedan en 0 (Generico), que es el comportamiento de siempre.
        AsegurarColumna(
            conexion,
            "PlantillasLinea",
            "PreferenciaNombre",
            "INTEGER NOT NULL DEFAULT 0"
        );
        AsegurarColumna(conexion, "PlantillasLinea", "VagonNombreId", "INTEGER NULL");
    }

    private static void AsegurarColumna(
        SqliteConnection conexion,
        string tabla,
        string columna,
        string definicion
    )
    {
        using (var consulta = conexion.CreateCommand())
        {
            consulta.CommandText = $"PRAGMA table_info({tabla});";
            using var lector = consulta.ExecuteReader();
            while (lector.Read())
            {
                if (string.Equals(lector.GetString(1), columna, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        using var alter = conexion.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tabla} ADD COLUMN {columna} {definicion};";
        alter.ExecuteNonQuery();
    }

    private SqliteConnection AbrirConexion()
    {
        var conexion = new SqliteConnection(_cadenaConexion);
        conexion.Open();
        return conexion;
    }

    // ----- Plantillas (modelos de cadena) -----

    public PlantillaLinea AgregarPlantilla(string nombre, DateTime fechaCreacion, bool esModeloHijo)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "INSERT INTO PlantillasLinea (Nombre, FechaCreacion, EsModeloHijo) VALUES ($n, $f, $hijo); SELECT last_insert_rowid();";
        comando.Parameters.AddWithValue("$n", nombre);
        comando.Parameters.AddWithValue("$f", fechaCreacion.ToString("O"));
        comando.Parameters.AddWithValue("$hijo", esModeloHijo ? 1 : 0);
        var id = (long)comando.ExecuteScalar()!;
        return new PlantillaLinea
        {
            Id = id,
            Nombre = nombre,
            FechaCreacion = fechaCreacion,
            EsModeloHijo = esModeloHijo,
        };
    }

    public IReadOnlyList<PlantillaLinea> ObtenerPlantillas()
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT Id, Nombre, FechaCreacion, EsModeloHijo, PreferenciaNombre, VagonNombreId FROM PlantillasLinea ORDER BY Nombre;";
        return LeerPlantillas(comando);
    }

    public PlantillaLinea? ObtenerPlantilla(long id)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT Id, Nombre, FechaCreacion, EsModeloHijo, PreferenciaNombre, VagonNombreId FROM PlantillasLinea WHERE Id = $id;";
        comando.Parameters.AddWithValue("$id", id);
        return LeerPlantillas(comando).FirstOrDefault();
    }

    public void ActualizarPreferenciaNombre(
        long plantillaId,
        PreferenciaNombreCadena preferencia,
        long? vagonNombreId
    )
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "UPDATE PlantillasLinea SET PreferenciaNombre = $pref, VagonNombreId = $vagon WHERE Id = $id;";
        comando.Parameters.AddWithValue("$pref", (int)preferencia);
        comando.Parameters.AddWithValue("$vagon", (object?)vagonNombreId ?? DBNull.Value);
        comando.Parameters.AddWithValue("$id", plantillaId);
        comando.ExecuteNonQuery();
    }

    private static IReadOnlyList<PlantillaLinea> LeerPlantillas(SqliteCommand comando)
    {
        using var lector = comando.ExecuteReader();
        var resultado = new List<PlantillaLinea>();
        while (lector.Read())
        {
            resultado.Add(
                new PlantillaLinea
                {
                    Id = lector.GetInt64(0),
                    Nombre = lector.GetString(1),
                    FechaCreacion = DateTime.Parse(lector.GetString(2)),
                    EsModeloHijo = lector.GetInt32(3) != 0,
                    PreferenciaNombre = (PreferenciaNombreCadena)lector.GetInt32(4),
                    VagonNombreId = lector.IsDBNull(5) ? null : lector.GetInt64(5),
                }
            );
        }

        return resultado;
    }

    public void RenombrarPlantilla(long id, string nombre)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "UPDATE PlantillasLinea SET Nombre = $n WHERE Id = $id;";
        comando.Parameters.AddWithValue("$n", nombre);
        comando.Parameters.AddWithValue("$id", id);
        comando.ExecuteNonQuery();
    }

    public void BorrarPlantilla(long id)
    {
        using var conexion = AbrirConexion();
        using var transaccion = conexion.BeginTransaction();
        Ejecutar(
            conexion,
            transaccion,
            "DELETE FROM PlantillaVagones WHERE PlantillaId = $id;",
            ("$id", id)
        );
        Ejecutar(
            conexion,
            transaccion,
            "UPDATE PlantillaVagones SET ModeloCadenaHijaId = NULL WHERE ModeloCadenaHijaId = $id;",
            ("$id", id)
        );
        Ejecutar(conexion, transaccion, "DELETE FROM PlantillasLinea WHERE Id = $id;", ("$id", id));
        transaccion.Commit();
    }

    // ----- Documentos de un modelo -----

    public PlantillaVagon AgregarVagon(
        long plantillaId,
        long? padreId,
        string nombre,
        bool esMultiple,
        bool esAnexo,
        long? modeloCadenaHijaId
    )
    {
        var orden = SiguienteOrdenPlantilla(plantillaId, padreId);
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            INSERT INTO PlantillaVagones (PlantillaId, PadreId, Orden, Nombre, EsMultiple, EsAnexo, ModeloCadenaHijaId)
            VALUES ($p, $padre, $orden, $n, $multi, $anexo, $modelo);
            SELECT last_insert_rowid();
            """;
        comando.Parameters.AddWithValue("$p", plantillaId);
        comando.Parameters.AddWithValue("$padre", (object?)padreId ?? DBNull.Value);
        comando.Parameters.AddWithValue("$orden", orden);
        comando.Parameters.AddWithValue("$n", nombre);
        comando.Parameters.AddWithValue("$multi", esMultiple ? 1 : 0);
        comando.Parameters.AddWithValue("$anexo", esAnexo ? 1 : 0);
        comando.Parameters.AddWithValue("$modelo", (object?)modeloCadenaHijaId ?? DBNull.Value);
        var id = (long)comando.ExecuteScalar()!;

        return new PlantillaVagon
        {
            Id = id,
            PlantillaId = plantillaId,
            PadreId = padreId,
            Orden = orden,
            Nombre = nombre,
            EsMultiple = esMultiple,
            EsAnexo = esAnexo,
            ModeloCadenaHijaId = modeloCadenaHijaId,
        };
    }

    private int SiguienteOrdenPlantilla(long plantillaId, long? padreId)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = padreId is null
            ? "SELECT COALESCE(MAX(Orden), -1) + 1 FROM PlantillaVagones WHERE PlantillaId = $p AND PadreId IS NULL;"
            : "SELECT COALESCE(MAX(Orden), -1) + 1 FROM PlantillaVagones WHERE PlantillaId = $p AND PadreId = $padre;";
        comando.Parameters.AddWithValue("$p", plantillaId);
        if (padreId is not null)
        {
            comando.Parameters.AddWithValue("$padre", padreId.Value);
        }

        return Convert.ToInt32(comando.ExecuteScalar());
    }

    public void ActualizarVagon(
        long id,
        string nombre,
        bool esMultiple,
        bool esAnexo,
        long? modeloCadenaHijaId
    )
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            UPDATE PlantillaVagones
            SET Nombre = $n, EsMultiple = $multi, EsAnexo = $anexo, ModeloCadenaHijaId = $modelo
            WHERE Id = $id;
            """;
        comando.Parameters.AddWithValue("$n", nombre);
        comando.Parameters.AddWithValue("$multi", esMultiple ? 1 : 0);
        comando.Parameters.AddWithValue("$anexo", esAnexo ? 1 : 0);
        comando.Parameters.AddWithValue("$modelo", (object?)modeloCadenaHijaId ?? DBNull.Value);
        comando.Parameters.AddWithValue("$id", id);
        comando.ExecuteNonQuery();
    }

    public void BorrarVagon(long id)
    {
        var vagones = ObtenerVagonesPlantillaPorId(id);
        var aBorrar = Descendientes(id, vagones);
        using var conexion = AbrirConexion();
        using var transaccion = conexion.BeginTransaction();
        foreach (var vagonId in aBorrar)
        {
            Ejecutar(
                conexion,
                transaccion,
                "DELETE FROM PlantillaVagones WHERE Id = $id;",
                ("$id", vagonId)
            );
        }

        transaccion.Commit();
    }

    public IReadOnlyList<PlantillaVagon> ObtenerVagonesPlantilla(long plantillaId)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT Id, PlantillaId, PadreId, Orden, Nombre, EsMultiple, EsAnexo, ModeloCadenaHijaId
            FROM PlantillaVagones WHERE PlantillaId = $p ORDER BY Orden;
            """;
        comando.Parameters.AddWithValue("$p", plantillaId);
        return LeerVagonesPlantilla(comando);
    }

    public PlantillaVagon? ObtenerVagon(long id)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT Id, PlantillaId, PadreId, Orden, Nombre, EsMultiple, EsAnexo, ModeloCadenaHijaId
            FROM PlantillaVagones WHERE Id = $id;
            """;
        comando.Parameters.AddWithValue("$id", id);
        return LeerVagonesPlantilla(comando).FirstOrDefault();
    }

    private IReadOnlyList<PlantillaVagon> ObtenerVagonesPlantillaPorId(long id)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT Id, PlantillaId, PadreId, Orden, Nombre, EsMultiple, EsAnexo, ModeloCadenaHijaId
            FROM PlantillaVagones
            WHERE PlantillaId = (SELECT PlantillaId FROM PlantillaVagones WHERE Id = $id)
            ORDER BY Orden;
            """;
        comando.Parameters.AddWithValue("$id", id);
        return LeerVagonesPlantilla(comando);
    }

    private static IReadOnlyList<PlantillaVagon> LeerVagonesPlantilla(SqliteCommand comando)
    {
        using var lector = comando.ExecuteReader();
        var resultado = new List<PlantillaVagon>();
        while (lector.Read())
        {
            resultado.Add(
                new PlantillaVagon
                {
                    Id = lector.GetInt64(0),
                    PlantillaId = lector.GetInt64(1),
                    PadreId = lector.IsDBNull(2) ? null : lector.GetInt64(2),
                    Orden = lector.GetInt32(3),
                    Nombre = lector.GetString(4),
                    EsMultiple = lector.GetInt32(5) != 0,
                    EsAnexo = lector.GetInt32(6) != 0,
                    ModeloCadenaHijaId = lector.IsDBNull(7) ? null : lector.GetInt64(7),
                }
            );
        }

        return resultado;
    }

    // ----- Instancias (cadenas documentales) -----

    public InstanciaLinea AgregarInstancia(
        long? plantillaIdOrigen,
        string? nombrePlantillaOrigen,
        string nombre,
        DateTime fechaCreacion,
        string estructuraJson,
        long? cadenaMadreId,
        long? instanciaVagonPadreId
    )
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            INSERT INTO InstanciasLinea (PlantillaIdOrigen, NombrePlantillaOrigen, Nombre, FechaCreacion, EstructuraJson, CadenaMadreId, InstanciaVagonPadreId)
            VALUES ($origen, $nombreOrigen, $n, $f, $estructura, $madre, $padre);
            SELECT last_insert_rowid();
            """;
        comando.Parameters.AddWithValue("$origen", (object?)plantillaIdOrigen ?? DBNull.Value);
        comando.Parameters.AddWithValue(
            "$nombreOrigen",
            (object?)nombrePlantillaOrigen ?? DBNull.Value
        );
        comando.Parameters.AddWithValue("$n", nombre);
        comando.Parameters.AddWithValue("$f", fechaCreacion.ToString("O"));
        comando.Parameters.AddWithValue("$estructura", estructuraJson);
        comando.Parameters.AddWithValue("$madre", (object?)cadenaMadreId ?? DBNull.Value);
        comando.Parameters.AddWithValue("$padre", (object?)instanciaVagonPadreId ?? DBNull.Value);
        var id = (long)comando.ExecuteScalar()!;

        return new InstanciaLinea
        {
            Id = id,
            PlantillaIdOrigen = plantillaIdOrigen,
            NombrePlantillaOrigen = nombrePlantillaOrigen,
            Nombre = nombre,
            FechaCreacion = fechaCreacion,
            EstructuraJson = estructuraJson,
            CadenaMadreId = cadenaMadreId,
            InstanciaVagonPadreId = instanciaVagonPadreId,
        };
    }

    public IReadOnlyList<InstanciaLinea> ObtenerInstancias()
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT Id, PlantillaIdOrigen, NombrePlantillaOrigen, Nombre, FechaCreacion, EstructuraJson, CadenaMadreId, InstanciaVagonPadreId
            FROM InstanciasLinea
            WHERE CadenaMadreId IS NULL
            ORDER BY FechaCreacion DESC;
            """;
        return LeerInstancias(comando);
    }

    public InstanciaLinea? ObtenerInstancia(long id)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT Id, PlantillaIdOrigen, NombrePlantillaOrigen, Nombre, FechaCreacion, EstructuraJson, CadenaMadreId, InstanciaVagonPadreId
            FROM InstanciasLinea WHERE Id = $id;
            """;
        comando.Parameters.AddWithValue("$id", id);
        return LeerInstancias(comando).FirstOrDefault();
    }

    public IReadOnlyList<InstanciaLinea> ObtenerCadenasHijas(long instanciaVagonPadreId)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT Id, PlantillaIdOrigen, NombrePlantillaOrigen, Nombre, FechaCreacion, EstructuraJson, CadenaMadreId, InstanciaVagonPadreId
            FROM InstanciasLinea
            WHERE InstanciaVagonPadreId = $padre
            ORDER BY FechaCreacion;
            """;
        comando.Parameters.AddWithValue("$padre", instanciaVagonPadreId);
        return LeerInstancias(comando);
    }

    private static IReadOnlyList<InstanciaLinea> LeerInstancias(SqliteCommand comando)
    {
        using var lector = comando.ExecuteReader();
        var resultado = new List<InstanciaLinea>();
        while (lector.Read())
        {
            resultado.Add(
                new InstanciaLinea
                {
                    Id = lector.GetInt64(0),
                    PlantillaIdOrigen = lector.IsDBNull(1) ? null : lector.GetInt64(1),
                    NombrePlantillaOrigen = lector.IsDBNull(2) ? null : lector.GetString(2),
                    Nombre = lector.GetString(3),
                    FechaCreacion = DateTime.Parse(lector.GetString(4)),
                    EstructuraJson = lector.GetString(5),
                    CadenaMadreId = lector.IsDBNull(6) ? null : lector.GetInt64(6),
                    InstanciaVagonPadreId = lector.IsDBNull(7) ? null : lector.GetInt64(7),
                }
            );
        }

        return resultado;
    }

    public void BorrarInstancia(long id)
    {
        using var conexion = AbrirConexion();
        using var transaccion = conexion.BeginTransaction();
        EliminarInstanciaRecursiva(conexion, transaccion, id);
        transaccion.Commit();
    }

    private static void EliminarInstanciaRecursiva(
        SqliteConnection conexion,
        SqliteTransaction transaccion,
        long id
    )
    {
        var hijas = new List<long>();
        using (var consulta = conexion.CreateCommand())
        {
            consulta.Transaction = transaccion;
            consulta.CommandText = "SELECT Id FROM InstanciasLinea WHERE CadenaMadreId = $id;";
            consulta.Parameters.AddWithValue("$id", id);
            using var lector = consulta.ExecuteReader();
            while (lector.Read())
            {
                hijas.Add(lector.GetInt64(0));
            }
        }

        foreach (var hija in hijas)
        {
            EliminarInstanciaRecursiva(conexion, transaccion, hija);
        }

        Ejecutar(
            conexion,
            transaccion,
            "DELETE FROM InstanciaVagones WHERE InstanciaId = $id;",
            ("$id", id)
        );
        Ejecutar(conexion, transaccion, "DELETE FROM InstanciasLinea WHERE Id = $id;", ("$id", id));
    }

    // ----- Documentos de una cadena (instancia) -----

    public InstanciaVagon AgregarVagonInstancia(
        long instanciaId,
        long? padreId,
        long? plantillaVagonId,
        string nombre,
        bool esMultiple,
        bool esAnexo
    )
    {
        var orden = SiguienteOrdenInstancia(instanciaId, padreId);
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            INSERT INTO InstanciaVagones (InstanciaId, PadreId, PlantillaVagonId, Orden, Nombre, EsMultiple, EsAnexo)
            VALUES ($i, $padre, $plantilla, $orden, $n, $multi, $anexo);
            SELECT last_insert_rowid();
            """;
        comando.Parameters.AddWithValue("$i", instanciaId);
        comando.Parameters.AddWithValue("$padre", (object?)padreId ?? DBNull.Value);
        comando.Parameters.AddWithValue("$plantilla", (object?)plantillaVagonId ?? DBNull.Value);
        comando.Parameters.AddWithValue("$orden", orden);
        comando.Parameters.AddWithValue("$n", nombre);
        comando.Parameters.AddWithValue("$multi", esMultiple ? 1 : 0);
        comando.Parameters.AddWithValue("$anexo", esAnexo ? 1 : 0);
        var id = (long)comando.ExecuteScalar()!;

        return new InstanciaVagon
        {
            Id = id,
            InstanciaId = instanciaId,
            PadreId = padreId,
            PlantillaVagonId = plantillaVagonId,
            Orden = orden,
            Nombre = nombre,
            EsMultiple = esMultiple,
            EsAnexo = esAnexo,
        };
    }

    private int SiguienteOrdenInstancia(long instanciaId, long? padreId)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = padreId is null
            ? "SELECT COALESCE(MAX(Orden), -1) + 1 FROM InstanciaVagones WHERE InstanciaId = $i AND PadreId IS NULL;"
            : "SELECT COALESCE(MAX(Orden), -1) + 1 FROM InstanciaVagones WHERE InstanciaId = $i AND PadreId = $padre;";
        comando.Parameters.AddWithValue("$i", instanciaId);
        if (padreId is not null)
        {
            comando.Parameters.AddWithValue("$padre", padreId.Value);
        }

        return Convert.ToInt32(comando.ExecuteScalar());
    }

    public IReadOnlyList<InstanciaVagon> ObtenerVagonesInstancia(long instanciaId)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT Id, InstanciaId, PadreId, PlantillaVagonId, Orden, Nombre, EsMultiple, EsAnexo, RutaDocumento, NombreDocumento
            FROM InstanciaVagones
            WHERE InstanciaId = $i
            ORDER BY Orden;
            """;
        comando.Parameters.AddWithValue("$i", instanciaId);
        return LeerVagonesInstancia(comando);
    }

    public InstanciaVagon? ObtenerVagonInstancia(long id)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT Id, InstanciaId, PadreId, PlantillaVagonId, Orden, Nombre, EsMultiple, EsAnexo, RutaDocumento, NombreDocumento
            FROM InstanciaVagones
            WHERE Id = $id;
            """;
        comando.Parameters.AddWithValue("$id", id);
        return LeerVagonesInstancia(comando).FirstOrDefault();
    }

    private static IReadOnlyList<InstanciaVagon> LeerVagonesInstancia(SqliteCommand comando)
    {
        using var lector = comando.ExecuteReader();
        var resultado = new List<InstanciaVagon>();
        while (lector.Read())
        {
            resultado.Add(
                new InstanciaVagon
                {
                    Id = lector.GetInt64(0),
                    InstanciaId = lector.GetInt64(1),
                    PadreId = lector.IsDBNull(2) ? null : lector.GetInt64(2),
                    PlantillaVagonId = lector.IsDBNull(3) ? null : lector.GetInt64(3),
                    Orden = lector.GetInt32(4),
                    Nombre = lector.GetString(5),
                    EsMultiple = lector.GetInt32(6) != 0,
                    EsAnexo = lector.GetInt32(7) != 0,
                    RutaDocumento = lector.IsDBNull(8) ? null : lector.GetString(8),
                    NombreDocumento = lector.IsDBNull(9) ? null : lector.GetString(9),
                }
            );
        }

        return resultado;
    }

    public void ActualizarDocumentoVagon(long id, string? rutaDocumento, string? nombreDocumento)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "UPDATE InstanciaVagones SET RutaDocumento = $ruta, NombreDocumento = $nombre WHERE Id = $id;";
        comando.Parameters.AddWithValue("$ruta", (object?)rutaDocumento ?? DBNull.Value);
        comando.Parameters.AddWithValue("$nombre", (object?)nombreDocumento ?? DBNull.Value);
        comando.Parameters.AddWithValue("$id", id);
        comando.ExecuteNonQuery();
    }

    public void BorrarVagonInstancia(long id)
    {
        using var conexion = AbrirConexion();
        using var transaccion = conexion.BeginTransaction();
        var vagones = ObtenerVagonesInstanciaPorVagon(conexion, transaccion, id);
        var aBorrar = DescendientesInstancia(id, vagones);
        foreach (var vagonId in aBorrar)
        {
            EliminarCadenasHijasDeVagon(conexion, transaccion, vagonId);
            Ejecutar(
                conexion,
                transaccion,
                "DELETE FROM InstanciaVagones WHERE Id = $id;",
                ("$id", vagonId)
            );
        }

        transaccion.Commit();
    }

    private static void EliminarCadenasHijasDeVagon(
        SqliteConnection conexion,
        SqliteTransaction transaccion,
        long instanciaVagonId
    )
    {
        var hijas = new List<long>();
        using (var consulta = conexion.CreateCommand())
        {
            consulta.Transaction = transaccion;
            consulta.CommandText =
                "SELECT Id FROM InstanciasLinea WHERE InstanciaVagonPadreId = $id;";
            consulta.Parameters.AddWithValue("$id", instanciaVagonId);
            using var lector = consulta.ExecuteReader();
            while (lector.Read())
            {
                hijas.Add(lector.GetInt64(0));
            }
        }

        foreach (var hija in hijas)
        {
            EliminarInstanciaRecursiva(conexion, transaccion, hija);
        }
    }

    private static IReadOnlyList<InstanciaVagon> ObtenerVagonesInstanciaPorVagon(
        SqliteConnection conexion,
        SqliteTransaction transaccion,
        long id
    )
    {
        using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText = """
            SELECT Id, InstanciaId, PadreId, PlantillaVagonId, Orden, Nombre, EsMultiple, EsAnexo, RutaDocumento, NombreDocumento
            FROM InstanciaVagones
            WHERE InstanciaId = (SELECT InstanciaId FROM InstanciaVagones WHERE Id = $id)
            ORDER BY Orden;
            """;
        comando.Parameters.AddWithValue("$id", id);
        return LeerVagonesInstancia(comando);
    }

    // ----- Helpers -----

    private static IReadOnlyList<long> Descendientes(
        long raizId,
        IReadOnlyList<PlantillaVagon> todos
    )
    {
        var resultado = new List<long> { raizId };
        var pendientes = new Queue<long>();
        pendientes.Enqueue(raizId);
        while (pendientes.Count > 0)
        {
            var actual = pendientes.Dequeue();
            foreach (var hijo in todos.Where(v => v.PadreId == actual))
            {
                resultado.Add(hijo.Id);
                pendientes.Enqueue(hijo.Id);
            }
        }

        return resultado;
    }

    private static IReadOnlyList<long> DescendientesInstancia(
        long raizId,
        IReadOnlyList<InstanciaVagon> todos
    )
    {
        var resultado = new List<long> { raizId };
        var pendientes = new Queue<long>();
        pendientes.Enqueue(raizId);
        while (pendientes.Count > 0)
        {
            var actual = pendientes.Dequeue();
            foreach (var hijo in todos.Where(v => v.PadreId == actual))
            {
                resultado.Add(hijo.Id);
                pendientes.Enqueue(hijo.Id);
            }
        }

        return resultado;
    }

    private static void Ejecutar(
        SqliteConnection conexion,
        SqliteTransaction transaccion,
        string sql,
        params (string Nombre, long Valor)[] parametros
    )
    {
        using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nombre, valor);
        }

        comando.ExecuteNonQuery();
    }
}
