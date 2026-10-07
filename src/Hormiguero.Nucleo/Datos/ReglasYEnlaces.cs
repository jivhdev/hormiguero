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
    DateTime? CambiadaEn,
    string? Motivo = null
);

public sealed record DatosDudoso(
    long EnlaceId,
    string NombreDocumento,
    string RutaDocumento,
    string NombreVagon,
    string? ValorPropuesto,
    string? ValorComparado,
    string? Motivo
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
    public void AnularReglasDeVagones(IEnumerable<long> vagones)
    {
        var ids = vagones.Distinct().ToArray();
        if (ids.Length == 0)
            return;
        using var tx = conexion.BeginTransaction();
        foreach (long id in ids)
        {
            using var cmd = conexion.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                "SELECT id FROM reglas_vagon WHERE estado='activa' AND (vagon_modelo_id=$v OR vagon_comparacion_id=$v)";
            cmd.Parameters.AddWithValue("$v", id);
            using var r = cmd.ExecuteReader();
            var reglas = new List<long>();
            while (r.Read())
                reglas.Add(r.GetInt64(0));
            r.Close();
            foreach (long regla in reglas)
            {
                using (var archivar = conexion.CreateCommand())
                {
                    archivar.Transaction = tx;
                    archivar.CommandText =
                        "INSERT OR IGNORE INTO reglas_vagon_anuladas(id,vagon_modelo_id,identificacion_id,campo_origen_id,vagon_comparacion_id,campo_comparacion_id,operacion,normalizar_espacios,ignorar_guiones,ignorar_ceros_iniciales,largo_minimo,anulada_en) SELECT id,vagon_modelo_id,identificacion_id,campo_origen_id,vagon_comparacion_id,campo_comparacion_id,operacion,normalizar_espacios,ignorar_guiones,ignorar_ceros_iniciales,largo_minimo,$f FROM reglas_vagon WHERE id=$id";
                    archivar.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
                    archivar.Parameters.AddWithValue("$id", regla);
                    archivar.ExecuteNonQuery();
                }
                using (var desligar = conexion.CreateCommand())
                {
                    desligar.Transaction = tx;
                    desligar.CommandText =
                        "UPDATE enlaces_cadena SET regla_id=NULL WHERE regla_id=$id";
                    desligar.Parameters.AddWithValue("$id", regla);
                    desligar.ExecuteNonQuery();
                }
                AuditoriaDatos.Registrar(
                    conexion,
                    tx,
                    "anular_regla_por_baja_vagon",
                    $"regla:{regla}",
                    $"vagon:{id}"
                );
                using var eliminar = conexion.CreateCommand();
                eliminar.Transaction = tx;
                eliminar.CommandText = "DELETE FROM reglas_vagon WHERE id=$id";
                eliminar.Parameters.AddWithValue("$id", regla);
                eliminar.ExecuteNonQuery();
            }
        }
        tx.Commit();
    }

    public long GuardarRegla(ReglaVagon regla)
    {
        if (regla.LargoMinimo < 0)
            throw new ArgumentOutOfRangeException(nameof(regla));
        if (!string.Equals(regla.Operacion, "igual", StringComparison.Ordinal))
            throw new ArgumentException(
                "La regla solo admite comparar por igualdad.",
                nameof(regla)
            );
        using (var campo = conexion.CreateCommand())
        {
            campo.CommandText =
                "SELECT 1 FROM campos_documento WHERE id=$campo AND identificacion_id=$identificacion AND activo=1;";
            campo.Parameters.AddWithValue("$campo", regla.CampoOrigenId);
            campo.Parameters.AddWithValue("$identificacion", regla.IdentificacionId);
            if (campo.ExecuteScalar() is null)
                throw new ArgumentException(
                    "El dato de origen no pertenece a la configuración indicada."
                );
        }
        ValidarRelacionModelos(regla.VagonModeloId, regla.VagonComparacionId);
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

    private void ValidarRelacionModelos(long destinoId, long comparacionId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "WITH RECURSIVE relacionados(id) AS ("
            + "SELECT modelo_id FROM vagones_modelo WHERE id=$destino "
            + "UNION SELECT v.modelo_cadena_hija_id FROM vagones_modelo v JOIN relacionados r ON v.modelo_id=r.id WHERE v.modelo_cadena_hija_id IS NOT NULL "
            + "UNION SELECT v.modelo_id FROM vagones_modelo v JOIN relacionados r ON v.modelo_cadena_hija_id=r.id) "
            + "SELECT (SELECT modelo_id FROM vagones_modelo WHERE id=$destino), "
            + "(SELECT modelo_id FROM vagones_modelo WHERE id=$comparacion), "
            + "EXISTS(SELECT 1 FROM relacionados WHERE id=(SELECT modelo_id FROM vagones_modelo WHERE id=$comparacion));";
        cmd.Parameters.AddWithValue("$destino", destinoId);
        cmd.Parameters.AddWithValue("$comparacion", comparacionId);
        using var r = cmd.ExecuteReader();
        if (!r.Read() || r.IsDBNull(0) || r.IsDBNull(1) || r.GetInt32(2) == 0)
            throw new ArgumentException(
                "El vagón de comparación debe pertenecer al mismo modelo o a una cadena hija relacionada."
            );
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

    public IReadOnlyList<EnlaceCadena> ListarDudosos()
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id,vagon_cadena_id,version_id,origen,regla_id,estado,creada_en,cambiada_en,motivo FROM enlaces_cadena WHERE estado='dudoso' ORDER BY id;";
        using var r = cmd.ExecuteReader();
        var resultado = new List<EnlaceCadena>();
        while (r.Read())
            resultado.Add(
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
        return resultado;
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

    public DatosDudoso? ObtenerDatosDudoso(long id)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT e.id,d.nombre,v.ruta_observada,g.nombre,(SELECT x.valor_original FROM valores_documento x JOIN reglas_vagon r ON r.campo_origen_id=x.campo_id AND r.id=e.regla_id WHERE x.version_id=e.version_id AND x.estado='vigente' LIMIT 1),(SELECT x.valor_original FROM enlaces_cadena ce JOIN reglas_vagon r ON r.vagon_modelo_id=(SELECT vagon_modelo_id FROM vagones_cadena WHERE id=e.vagon_cadena_id) AND r.id=e.regla_id JOIN vagones_cadena vc ON vc.cadena_id=(SELECT cadena_id FROM vagones_cadena WHERE id=e.vagon_cadena_id) AND vc.vagon_modelo_id=r.vagon_comparacion_id JOIN valores_documento x ON x.version_id=ce.version_id AND x.campo_id=r.campo_comparacion_id WHERE ce.vagon_cadena_id=vc.id AND ce.estado='activo' AND x.estado='vigente' LIMIT 1),e.motivo "
            + "FROM enlaces_cadena e JOIN vagones_cadena g ON g.id=e.vagon_cadena_id JOIN versiones_documento v ON v.id=e.version_id JOIN documentos d ON d.id=v.documento_id WHERE e.id=$id AND e.estado='dudoso';";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read()
            ? new(
                r.GetInt64(0),
                r.GetString(1),
                r.GetString(2),
                r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetString(6)
            )
            : null;
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

    public bool AceptarDudoso(long id)
    {
        using var tx = conexion.BeginTransaction();
        long vagon;
        long version;
        using (var q = conexion.CreateCommand())
        {
            q.Transaction = tx;
            q.CommandText =
                "SELECT vagon_cadena_id,version_id FROM enlaces_cadena WHERE id=$id AND estado='dudoso';";
            q.Parameters.AddWithValue("$id", id);
            using var r = q.ExecuteReader();
            if (!r.Read())
            {
                tx.Commit();
                return false;
            }
            vagon = r.GetInt64(0);
            version = r.GetInt64(1);
        }
        using (var ocupado = conexion.CreateCommand())
        {
            ocupado.Transaction = tx;
            ocupado.CommandText =
                "SELECT 1 FROM enlaces_cadena WHERE vagon_cadena_id=$v AND estado='activo' LIMIT 1;";
            ocupado.Parameters.AddWithValue("$v", vagon);
            if (ocupado.ExecuteScalar() is not null)
                throw new InvalidOperationException("El vagón ya tiene un documento enlazado.");
        }
        using (var vigente = conexion.CreateCommand())
        {
            vigente.Transaction = tx;
            vigente.CommandText =
                "SELECT 1 FROM versiones_documento WHERE id=$id AND estado='vigente';";
            vigente.Parameters.AddWithValue("$id", version);
            if (vigente.ExecuteScalar() is null)
                throw new InvalidOperationException("La versión propuesta ya no está vigente.");
        }
        using (var cmd = conexion.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                "UPDATE enlaces_cadena SET estado='activo',cambiada_en=$f WHERE id=$id; UPDATE vagones_cadena SET version_id=$d WHERE id=$v;";
            cmd.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$d", version);
            cmd.Parameters.AddWithValue("$v", vagon);
            cmd.ExecuteNonQuery();
        }
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "aceptar_propuesta",
            $"enlace:{id}",
            $"vagon:{vagon}",
            app: "Buscadero"
        );
        tx.Commit();
        return true;
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
