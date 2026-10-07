using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed record EnlaceCadena(
    long Id,
    long VagonCadenaId,
    long VersionId,
    string Origen,
    long? ReglaId,
    string Estado,
    DateTime CreadaEn,
    DateTime? CambiadaEn,
    string? Motivo = null
);

public sealed record DatosDudosoCadenaSimple(
    long EnlaceId,
    string Motivo,
    long VersionId,
    string Ruta,
    string Nombre
);

public sealed record DocumentoDudosoCadenaSimple(long VagonId, long VersionId, string Nombre);

public sealed class RepositorioReglasYEnlaces(SqliteConnection conexion)
{
    public long CrearEnlace(
        long vagonCadenaId,
        long versionId,
        string origen,
        long? reglaId = null,
        string estado = "activo"
    )
    {
        using var tx = conexion.BeginTransaction();
        string fecha = DateTime.Now.ToString("o");
        long id;
        using (var cmd = conexion.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                "INSERT INTO enlaces_cadena(vagon_cadena_id,version_id,origen,regla_id,estado,creada_en) VALUES($v,$d,$o,$r,$e,$f) RETURNING id;";
            cmd.Parameters.AddWithValue("$v", vagonCadenaId);
            cmd.Parameters.AddWithValue("$d", versionId);
            cmd.Parameters.AddWithValue("$o", origen);
            cmd.Parameters.AddWithValue("$r", (object?)reglaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$e", estado);
            cmd.Parameters.AddWithValue("$f", fecha);
            id = Convert.ToInt64(cmd.ExecuteScalar());
        }
        if (estado == "activo")
            ActualizarVersionVagon(tx, vagonCadenaId, versionId);
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "crear_enlace",
            $"enlace:{id}",
            $"vagon:{vagonCadenaId}"
        );
        tx.Commit();
        return id;
    }

    public long CrearDudoso(long vagonCadenaId, long versionId, long? reglaId, string motivo)
    {
        using var tx = conexion.BeginTransaction();
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO enlaces_cadena(vagon_cadena_id,version_id,origen,regla_id,estado,creada_en,motivo) VALUES($v,$d,'automatico',$r,'dudoso',$f,$m) RETURNING id;";
        cmd.Parameters.AddWithValue("$v", vagonCadenaId);
        cmd.Parameters.AddWithValue("$d", versionId);
        cmd.Parameters.AddWithValue("$r", (object?)reglaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        cmd.Parameters.AddWithValue("$m", motivo);
        long id = Convert.ToInt64(cmd.ExecuteScalar());
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "proponer_enlace_dudoso",
            $"enlace:{id}",
            $"vagon:{vagonCadenaId}",
            app: "Buscadero"
        );
        tx.Commit();
        return id;
    }

    public IReadOnlyList<DatosDudosoCadenaSimple> ListarDudososCadenasSimples()
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT e.id,e.motivo,e.version_id,d.ruta,d.nombre FROM enlaces_cadena e JOIN vagones_cadena v ON v.id=e.vagon_cadena_id JOIN cadenas c ON c.id=v.cadena_id JOIN versiones_documento ver ON ver.id=e.version_id JOIN documentos d ON d.id=ver.documento_id WHERE e.estado='dudoso' AND c.modelo_id IS NULL ORDER BY e.id;";
        using var lector = comando.ExecuteReader();
        var resultado = new List<DatosDudosoCadenaSimple>();
        while (lector.Read())
            resultado.Add(
                new(
                    lector.GetInt64(0),
                    lector.IsDBNull(1) ? "Revisar coincidencia" : lector.GetString(1),
                    lector.GetInt64(2),
                    lector.GetString(3),
                    lector.GetString(4)
                )
            );
        return resultado;
    }

    public DocumentoDudosoCadenaSimple? ObtenerDocumentoDudosoCadenaSimple(long enlaceId)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT e.vagon_cadena_id,e.version_id,d.nombre FROM enlaces_cadena e JOIN vagones_cadena v ON v.id=e.vagon_cadena_id JOIN cadenas c ON c.id=v.cadena_id JOIN versiones_documento ver ON ver.id=e.version_id JOIN documentos d ON d.id=ver.documento_id WHERE e.id=$e AND e.estado='dudoso' AND c.modelo_id IS NULL;";
        comando.Parameters.AddWithValue("$e", enlaceId);
        using var lector = comando.ExecuteReader();
        return lector.Read()
            ? new(lector.GetInt64(0), lector.GetInt64(1), lector.GetString(2))
            : null;
    }

    public long? ObtenerVagonDudosoCadenaSimple(long enlaceId)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT e.vagon_cadena_id FROM enlaces_cadena e JOIN vagones_cadena v ON v.id=e.vagon_cadena_id JOIN cadenas c ON c.id=v.cadena_id WHERE e.id=$id AND e.estado='dudoso' AND c.modelo_id IS NULL;";
        comando.Parameters.AddWithValue("$id", enlaceId);
        return comando.ExecuteScalar() is long id ? id : null;
    }

    public bool RechazarDudoso(long id)
    {
        using var tx = conexion.BeginTransaction();
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "UPDATE enlaces_cadena SET estado='rechazado',cambiada_en=$f WHERE id=$id AND estado='dudoso';";
        cmd.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        cmd.Parameters.AddWithValue("$id", id);
        bool cambio = cmd.ExecuteNonQuery() > 0;
        if (cambio)
            AuditoriaDatos.Registrar(
                conexion,
                tx,
                "rechazar_propuesta",
                $"enlace:{id}",
                app: "Buscadero"
            );
        tx.Commit();
        return cambio;
    }

    public bool CambiarEstadoEnlace(long id, string estado)
    {
        using var tx = conexion.BeginTransaction();
        long? vagon = null,
            version = null;
        using (var consulta = conexion.CreateCommand())
        {
            consulta.Transaction = tx;
            consulta.CommandText =
                "SELECT vagon_cadena_id,version_id FROM enlaces_cadena WHERE id=$id;";
            consulta.Parameters.AddWithValue("$id", id);
            using var r = consulta.ExecuteReader();
            if (!r.Read())
            {
                tx.Commit();
                return false;
            }
            vagon = r.GetInt64(0);
            version = r.GetInt64(1);
        }
        using (var cmd = conexion.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE enlaces_cadena SET estado=$e,cambiada_en=$f WHERE id=$id;";
            cmd.Parameters.AddWithValue("$e", estado);
            cmd.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        ActualizarVersionVagon(tx, vagon.Value, estado == "activo" ? version : null);
        AuditoriaDatos.Registrar(conexion, tx, "cambiar_estado_enlace", $"enlace:{id}", estado);
        tx.Commit();
        return true;
    }

    public bool DeshacerEnlace(long id)
    {
        using var tx = conexion.BeginTransaction();
        long? vagon = null;
        using (var q = conexion.CreateCommand())
        {
            q.Transaction = tx;
            q.CommandText =
                "SELECT vagon_cadena_id FROM enlaces_cadena WHERE id=$id AND estado<>'anulado';";
            q.Parameters.AddWithValue("$id", id);
            var dato = q.ExecuteScalar();
            if (dato is null)
            {
                tx.Commit();
                return false;
            }
            vagon = Convert.ToInt64(dato);
        }
        using (var cmd = conexion.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                "UPDATE enlaces_cadena SET estado='anulado',cambiada_en=$f WHERE id=$id;";
            cmd.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        ActualizarVersionVagon(tx, vagon.Value, null);
        AuditoriaDatos.Registrar(conexion, tx, "deshacer_enlace", $"enlace:{id}");
        tx.Commit();
        return true;
    }

    public IReadOnlyList<EnlaceCadena> HistorialEnlaces(long vagonCadenaId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id,vagon_cadena_id,version_id,origen,regla_id,estado,creada_en,cambiada_en,motivo FROM enlaces_cadena WHERE vagon_cadena_id=$v ORDER BY id;";
        cmd.Parameters.AddWithValue("$v", vagonCadenaId);
        using var r = cmd.ExecuteReader();
        var l = new List<EnlaceCadena>();
        while (r.Read())
            l.Add(
                new(
                    r.GetInt64(0),
                    r.GetInt64(1),
                    r.GetInt64(2),
                    r.GetString(3),
                    r.IsDBNull(4) ? null : r.GetInt64(4),
                    r.GetString(5),
                    DateTime.Parse(r.GetString(6)),
                    r.IsDBNull(7) ? null : DateTime.Parse(r.GetString(7)),
                    r.IsDBNull(8) ? null : r.GetString(8)
                )
            );
        return l;
    }

    private void ActualizarVersionVagon(SqliteTransaction tx, long vagonId, long? versionId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE vagones_cadena SET version_id=$v WHERE id=$id;";
        cmd.Parameters.AddWithValue("$v", (object?)versionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", vagonId);
        cmd.ExecuteNonQuery();
    }
}
