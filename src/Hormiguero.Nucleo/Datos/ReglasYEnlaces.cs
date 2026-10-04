using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed record ReglaVagon(
    long Id,
    long VagonModeloId,
    long IdentificacionId,
    long CampoOrigenId,
    long VagonComparacionId,
    long CampoComparacionId,
    string Operacion,
    bool NormalizarEspacios,
    bool IgnorarGuiones,
    bool IgnorarCerosIniciales,
    int LargoMinimo,
    string Estado
);

public sealed record EnlaceCadena(
    long Id,
    long VagonCadenaId,
    long VersionId,
    string Origen,
    long? ReglaId,
    string Estado,
    DateTime CreadaEn,
    DateTime? CambiadaEn
);

public sealed class RepositorioReglasYEnlaces(SqliteConnection conexion)
{
    public long GuardarRegla(ReglaVagon regla)
    {
        if (regla.LargoMinimo < 0)
            throw new ArgumentOutOfRangeException(nameof(regla));
        using var tx = conexion.BeginTransaction();
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            regla.Id == 0
                ? "INSERT INTO reglas_vagon(vagon_modelo_id,identificacion_id,campo_origen_id,vagon_comparacion_id,campo_comparacion_id,operacion,normalizar_espacios,ignorar_guiones,ignorar_ceros_iniciales,largo_minimo) VALUES($v,$i,$c,$vc,$cc,$o,$n,$g,$z,$l) RETURNING id;"
                : "UPDATE reglas_vagon SET vagon_modelo_id=$v,identificacion_id=$i,campo_origen_id=$c,vagon_comparacion_id=$vc,campo_comparacion_id=$cc,operacion=$o,normalizar_espacios=$n,ignorar_guiones=$g,ignorar_ceros_iniciales=$z,largo_minimo=$l WHERE id=$id RETURNING id;";
        cmd.Parameters.AddWithValue("$v", regla.VagonModeloId);
        cmd.Parameters.AddWithValue("$i", regla.IdentificacionId);
        cmd.Parameters.AddWithValue("$c", regla.CampoOrigenId);
        cmd.Parameters.AddWithValue("$vc", regla.VagonComparacionId);
        cmd.Parameters.AddWithValue("$cc", regla.CampoComparacionId);
        cmd.Parameters.AddWithValue("$o", regla.Operacion);
        cmd.Parameters.AddWithValue("$n", regla.NormalizarEspacios ? 1 : 0);
        cmd.Parameters.AddWithValue("$g", regla.IgnorarGuiones ? 1 : 0);
        cmd.Parameters.AddWithValue("$z", regla.IgnorarCerosIniciales ? 1 : 0);
        cmd.Parameters.AddWithValue("$l", regla.LargoMinimo);
        cmd.Parameters.AddWithValue("$id", regla.Id);
        long id = Convert.ToInt64(
            cmd.ExecuteScalar()
                ?? throw new InvalidOperationException("No existe la regla de vagón.")
        );
        AuditoriaDatos.Registrar(conexion, tx, "guardar_regla", $"regla:{id}");
        tx.Commit();
        return id;
    }

    public ReglaVagon? ObtenerRegla(long vagonModeloId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id,vagon_modelo_id,identificacion_id,campo_origen_id,vagon_comparacion_id,campo_comparacion_id,operacion,normalizar_espacios,ignorar_guiones,ignorar_ceros_iniciales,largo_minimo,estado FROM reglas_vagon WHERE vagon_modelo_id=$v AND estado='activa';";
        cmd.Parameters.AddWithValue("$v", vagonModeloId);
        using var r = cmd.ExecuteReader();
        return r.Read()
            ? new(
                r.GetInt64(0),
                r.GetInt64(1),
                r.GetInt64(2),
                r.GetInt64(3),
                r.GetInt64(4),
                r.GetInt64(5),
                r.GetString(6),
                r.GetInt32(7) != 0,
                r.GetInt32(8) != 0,
                r.GetInt32(9) != 0,
                r.GetInt32(10),
                r.GetString(11)
            )
            : null;
    }

    public bool AnularRegla(long id)
    {
        using var tx = conexion.BeginTransaction();
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "UPDATE reglas_vagon SET estado='anulada',fecha_anulacion=$f WHERE id=$id AND estado='activa';";
        cmd.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        cmd.Parameters.AddWithValue("$id", id);
        bool cambio = cmd.ExecuteNonQuery() > 0;
        if (cambio)
            AuditoriaDatos.Registrar(conexion, tx, "anular_regla", $"regla:{id}");
        tx.Commit();
        return cambio;
    }

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
            "SELECT id,vagon_cadena_id,version_id,origen,regla_id,estado,creada_en,cambiada_en FROM enlaces_cadena WHERE vagon_cadena_id=$v ORDER BY id;";
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
                    r.IsDBNull(7) ? null : DateTime.Parse(r.GetString(7))
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
