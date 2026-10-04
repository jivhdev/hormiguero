using Microsoft.Data.Sqlite;

namespace Archivero.Datos;

public class ConfiguracionDocumentoRepository
{
    private readonly EntidadRepository _entidades = new();

    public bool ExisteCoincidenciaExacta(string emisor, string tipo) =>
        BuscarPorEmisorYTipo(emisor, tipo) is not null;

    public ConfiguracionDocumento? BuscarPorEmisorYTipo(string emisor, string tipo)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            SELECT c.Id, ce.Nombre, ct.Nombre, c.CarpetaDestino, c.FormatoCarpeta, c.PatronCarpeta, c.Renombrar, c.AbrirDespuesDeGuardar, c.PreguntarNombre
            FROM Configuraciones c
            JOIN EntidadesConocidas ce ON ce.Id = c.EmisorId
            JOIN EntidadesConocidas ct ON ct.Id = c.TipoId
            WHERE ce.Nombre = $emisor AND ct.Nombre = $tipo;
            """;
        comando.Parameters.AddWithValue("$emisor", emisor);
        comando.Parameters.AddWithValue("$tipo", tipo);

        using var lector = comando.ExecuteReader();
        if (!lector.Read())
        {
            return null;
        }

        var configuracionId = lector.GetInt32(0);
        var configuracion = LeerConfiguracion(lector);
        lector.Close();

        return configuracion with { Patrones = ObtenerPatrones(conexion, configuracionId) };
    }

    public List<ConfiguracionDocumento> ObtenerTodas()
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            SELECT c.Id, ce.Nombre, ct.Nombre, c.CarpetaDestino, c.FormatoCarpeta, c.PatronCarpeta, c.Renombrar, c.AbrirDespuesDeGuardar, c.PreguntarNombre
            FROM Configuraciones c
            JOIN EntidadesConocidas ce ON ce.Id = c.EmisorId
            JOIN EntidadesConocidas ct ON ct.Id = c.TipoId
            ORDER BY ce.Nombre, ct.Nombre;
            """;

        var resultado = new List<ConfiguracionDocumento>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            resultado.Add(LeerConfiguracion(lector));
        }

        return resultado;
    }

    public void ActualizarDestino(int configuracionId, string carpetaDestino, FormatoCarpeta formatoCarpeta, string? patronCarpeta, bool renombrar, bool abrirDespuesDeGuardar, bool preguntarNombre)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            UPDATE Configuraciones
            SET CarpetaDestino = $carpetaDestino, FormatoCarpeta = $formato, PatronCarpeta = $patron, Renombrar = $renombrar,
                AbrirDespuesDeGuardar = $abrirDespuesDeGuardar, PreguntarNombre = $preguntarNombre
            WHERE Id = $id;
            """;
        comando.Parameters.AddWithValue("$preguntarNombre", preguntarNombre ? 1 : 0);
        comando.Parameters.AddWithValue("$carpetaDestino", carpetaDestino);
        comando.Parameters.AddWithValue("$formato", formatoCarpeta.ToString());
        comando.Parameters.AddWithValue("$patron", (object?)patronCarpeta ?? DBNull.Value);
        comando.Parameters.AddWithValue("$renombrar", renombrar ? 1 : 0);
        comando.Parameters.AddWithValue("$abrirDespuesDeGuardar", abrirDespuesDeGuardar ? 1 : 0);
        comando.Parameters.AddWithValue("$id", configuracionId);
        comando.ExecuteNonQuery();
    }

    public List<ConfiguracionDocumento> ObtenerTodasConPatrones()
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            SELECT c.Id, ce.Nombre, ct.Nombre, c.CarpetaDestino, c.FormatoCarpeta, c.PatronCarpeta, c.Renombrar, c.AbrirDespuesDeGuardar, c.PreguntarNombre
            FROM Configuraciones c
            JOIN EntidadesConocidas ce ON ce.Id = c.EmisorId
            JOIN EntidadesConocidas ct ON ct.Id = c.TipoId;
            """;

        var configuraciones = new List<ConfiguracionDocumento>();
        using (var lector = comando.ExecuteReader())
        {
            while (lector.Read())
            {
                configuraciones.Add(LeerConfiguracion(lector));
            }
        }

        return configuraciones
            .Select(c => c with { Patrones = ObtenerPatrones(conexion, c.Id) })
            .ToList();
    }

    public int GuardarNueva(
        string emisor,
        string tipo,
        string carpetaDestino,
        FormatoCarpeta formatoCarpeta,
        string? patronCarpeta,
        bool renombrar,
        List<Marca> marcas,
        bool abrirDespuesDeGuardar = false,
        bool preguntarNombre = false)
    {
        var emisorId = _entidades.ObtenerOCrear(CategoriaEntidad.Emisor, emisor);
        var tipoId = _entidades.ObtenerOCrear(CategoriaEntidad.Tipo, tipo);

        using var conexion = BaseDeDatos.CrearConexion();
        using var transaccion = conexion.BeginTransaction();

        int configuracionId;
        using (var insertarConfig = conexion.CreateCommand())
        {
            insertarConfig.Transaction = transaccion;
            insertarConfig.CommandText =
                """
                INSERT INTO Configuraciones (EmisorId, TipoId, CarpetaDestino, FormatoCarpeta, PatronCarpeta, Renombrar, AbrirDespuesDeGuardar, PreguntarNombre)
                VALUES ($emisorId, $tipoId, $carpetaDestino, $formato, $patron, $renombrar, $abrirDespuesDeGuardar, $preguntarNombre);
                SELECT last_insert_rowid();
                """;
            insertarConfig.Parameters.AddWithValue("$preguntarNombre", preguntarNombre ? 1 : 0);
            insertarConfig.Parameters.AddWithValue("$emisorId", emisorId);
            insertarConfig.Parameters.AddWithValue("$tipoId", tipoId);
            insertarConfig.Parameters.AddWithValue("$carpetaDestino", carpetaDestino);
            insertarConfig.Parameters.AddWithValue("$formato", formatoCarpeta.ToString());
            insertarConfig.Parameters.AddWithValue("$patron", (object?)patronCarpeta ?? DBNull.Value);
            insertarConfig.Parameters.AddWithValue("$renombrar", renombrar ? 1 : 0);
            insertarConfig.Parameters.AddWithValue("$abrirDespuesDeGuardar", abrirDespuesDeGuardar ? 1 : 0);

            configuracionId = (int)(long)insertarConfig.ExecuteScalar()!;
        }

        AgregarPatron(conexion, transaccion, configuracionId, marcas);

        transaccion.Commit();
        return configuracionId;
    }

    /// <summary>
    /// Borra una configuración de documento completa (Caso-1, punto 4): sus patrones y marcas,
    /// y la configuración en sí. No toca ningún archivo ya guardado en disco.
    /// </summary>
    public void EliminarConfiguracion(int configuracionId)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var transaccion = conexion.BeginTransaction();

        using (var borrarMarcas = conexion.CreateCommand())
        {
            borrarMarcas.Transaction = transaccion;
            borrarMarcas.CommandText =
                """
                DELETE FROM Marcas
                WHERE PatronId IN (SELECT Id FROM PatronesReconocimiento WHERE ConfiguracionId = $configuracionId);
                """;
            borrarMarcas.Parameters.AddWithValue("$configuracionId", configuracionId);
            borrarMarcas.ExecuteNonQuery();
        }

        using (var borrarPatrones = conexion.CreateCommand())
        {
            borrarPatrones.Transaction = transaccion;
            borrarPatrones.CommandText = "DELETE FROM PatronesReconocimiento WHERE ConfiguracionId = $configuracionId;";
            borrarPatrones.Parameters.AddWithValue("$configuracionId", configuracionId);
            borrarPatrones.ExecuteNonQuery();
        }

        using (var borrarConfiguracion = conexion.CreateCommand())
        {
            borrarConfiguracion.Transaction = transaccion;
            borrarConfiguracion.CommandText = "DELETE FROM Configuraciones WHERE Id = $configuracionId;";
            borrarConfiguracion.Parameters.AddWithValue("$configuracionId", configuracionId);
            borrarConfiguracion.ExecuteNonQuery();
        }

        transaccion.Commit();
    }

    public void AgregarPatronAConfiguracionExistente(int configuracionId, List<Marca> marcas)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var transaccion = conexion.BeginTransaction();
        AgregarPatron(conexion, transaccion, configuracionId, marcas);
        transaccion.Commit();
    }

    /// <summary>Reemplaza todas las marcas de un patron ya existente (edicion, REQ-004).</summary>
    public void ActualizarPatron(int patronId, List<Marca> marcas)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var transaccion = conexion.BeginTransaction();

        using (var borrar = conexion.CreateCommand())
        {
            borrar.Transaction = transaccion;
            borrar.CommandText = "DELETE FROM Marcas WHERE PatronId = $patronId;";
            borrar.Parameters.AddWithValue("$patronId", patronId);
            borrar.ExecuteNonQuery();
        }

        foreach (var marca in marcas)
        {
            using var insertarMarca = conexion.CreateCommand();
            insertarMarca.Transaction = transaccion;
            insertarMarca.CommandText =
                """
                INSERT INTO Marcas (PatronId, Campo, Pagina, X, Y, Ancho, Alto, TextoReferencia)
                VALUES ($patronId, $campo, $pagina, $x, $y, $ancho, $alto, $textoReferencia);
                """;
            insertarMarca.Parameters.AddWithValue("$patronId", patronId);
            insertarMarca.Parameters.AddWithValue("$campo", marca.Campo.ToString());
            insertarMarca.Parameters.AddWithValue("$pagina", marca.Pagina);
            insertarMarca.Parameters.AddWithValue("$x", marca.X);
            insertarMarca.Parameters.AddWithValue("$y", marca.Y);
            insertarMarca.Parameters.AddWithValue("$ancho", marca.Ancho);
            insertarMarca.Parameters.AddWithValue("$alto", marca.Alto);
            insertarMarca.Parameters.AddWithValue("$textoReferencia", (object?)marca.TextoReferencia ?? DBNull.Value);
            insertarMarca.ExecuteNonQuery();
        }

        transaccion.Commit();
    }

    private static void AgregarPatron(SqliteConnection conexion, SqliteTransaction transaccion, int configuracionId, List<Marca> marcas)
    {
        int patronId;
        using (var insertarPatron = conexion.CreateCommand())
        {
            insertarPatron.Transaction = transaccion;
            insertarPatron.CommandText =
                """
                INSERT INTO PatronesReconocimiento (ConfiguracionId) VALUES ($configuracionId);
                SELECT last_insert_rowid();
                """;
            insertarPatron.Parameters.AddWithValue("$configuracionId", configuracionId);
            patronId = (int)(long)insertarPatron.ExecuteScalar()!;
        }

        foreach (var marca in marcas)
        {
            using var insertarMarca = conexion.CreateCommand();
            insertarMarca.Transaction = transaccion;
            insertarMarca.CommandText =
                """
                INSERT INTO Marcas (PatronId, Campo, Pagina, X, Y, Ancho, Alto, TextoReferencia)
                VALUES ($patronId, $campo, $pagina, $x, $y, $ancho, $alto, $textoReferencia);
                """;
            insertarMarca.Parameters.AddWithValue("$patronId", patronId);
            insertarMarca.Parameters.AddWithValue("$campo", marca.Campo.ToString());
            insertarMarca.Parameters.AddWithValue("$pagina", marca.Pagina);
            insertarMarca.Parameters.AddWithValue("$x", marca.X);
            insertarMarca.Parameters.AddWithValue("$y", marca.Y);
            insertarMarca.Parameters.AddWithValue("$ancho", marca.Ancho);
            insertarMarca.Parameters.AddWithValue("$alto", marca.Alto);
            insertarMarca.Parameters.AddWithValue("$textoReferencia", (object?)marca.TextoReferencia ?? DBNull.Value);
            insertarMarca.ExecuteNonQuery();
        }
    }

    public List<string> ObtenerPatronesDeCarpetaConocidos()
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT DISTINCT PatronCarpeta FROM Configuraciones WHERE PatronCarpeta IS NOT NULL;";

        var resultado = new List<string>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            resultado.Add(lector.GetString(0));
        }

        return resultado;
    }

    private static ConfiguracionDocumento LeerConfiguracion(SqliteDataReader lector) => new()
    {
        Id = lector.GetInt32(0),
        Emisor = lector.GetString(1),
        Tipo = lector.GetString(2),
        CarpetaDestino = lector.GetString(3),
        FormatoCarpeta = Enum.Parse<FormatoCarpeta>(lector.GetString(4)),
        PatronCarpeta = lector.IsDBNull(5) ? null : lector.GetString(5),
        Renombrar = lector.GetInt32(6) != 0,
        AbrirDespuesDeGuardar = lector.GetInt32(7) != 0,
        PreguntarNombre = lector.GetInt32(8) != 0,
        Patrones = []
    };

    private static List<PatronReconocimiento> ObtenerPatrones(SqliteConnection conexion, int configuracionId)
    {
        // Se buscan primero los patrones y despues las marcas por separado (en vez de un solo
        // JOIN): un patron "sin texto" (Caso-1, punto 1) no tiene ninguna marca, y un INNER JOIN
        // desde Marcas lo haria desaparecer por completo de los resultados.
        var patrones = new Dictionary<int, List<Marca>>();

        using (var comandoPatrones = conexion.CreateCommand())
        {
            comandoPatrones.CommandText = "SELECT Id FROM PatronesReconocimiento WHERE ConfiguracionId = $configuracionId ORDER BY Id;";
            comandoPatrones.Parameters.AddWithValue("$configuracionId", configuracionId);
            using var lectorPatrones = comandoPatrones.ExecuteReader();
            while (lectorPatrones.Read())
            {
                patrones[lectorPatrones.GetInt32(0)] = [];
            }
        }

        using (var comandoMarcas = conexion.CreateCommand())
        {
            comandoMarcas.CommandText =
                """
                SELECT m.PatronId, m.Campo, m.Pagina, m.X, m.Y, m.Ancho, m.Alto, m.TextoReferencia
                FROM Marcas m
                JOIN PatronesReconocimiento p ON p.Id = m.PatronId
                WHERE p.ConfiguracionId = $configuracionId
                ORDER BY m.PatronId;
                """;
            comandoMarcas.Parameters.AddWithValue("$configuracionId", configuracionId);
            using var lectorMarcas = comandoMarcas.ExecuteReader();
            while (lectorMarcas.Read())
            {
                var patronId = lectorMarcas.GetInt32(0);
                patrones[patronId].Add(new Marca(
                    Enum.Parse<CampoMarca>(lectorMarcas.GetString(1)),
                    lectorMarcas.GetInt32(2),
                    lectorMarcas.GetDouble(3),
                    lectorMarcas.GetDouble(4),
                    lectorMarcas.GetDouble(5),
                    lectorMarcas.GetDouble(6),
                    lectorMarcas.IsDBNull(7) ? null : lectorMarcas.GetString(7)));
            }
        }

        return patrones.Select(p => new PatronReconocimiento(p.Key, p.Value)).ToList();
    }
}
