using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed record CotizacionCadena(
    long VersionId,
    string Tipo,
    string Nombre,
    string Ruta,
    string Numero
);

public sealed class RepositorioSituacionCadenaDiccionario(SqliteConnection conexion)
{
    public void RecalcularTodas(DateTime ahora)
    {
        var ids = new List<long>();
        using (var q = conexion.CreateCommand())
        {
            q.CommandText = "SELECT id FROM cadenas_diccionario WHERE estado='activa';";
            using var r = q.ExecuteReader();
            while (r.Read())
                ids.Add(r.GetInt64(0));
        }
        foreach (long id in ids)
            Recalcular(id, ahora);
    }

    public void Recalcular(long cadenaId, DateTime ahora)
    {
        bool completa = EsCompleta(cadenaId);
        string situacion;
        using (var q = conexion.CreateCommand())
        {
            q.CommandText =
                "SELECT situacion,situacion_desde FROM cadenas_diccionario WHERE id=$c;";
            q.Parameters.AddWithValue("$c", cadenaId);
            using var r = q.ExecuteReader();
            if (!r.Read())
                return;
            string actual = r.GetString(0);
            DateTime desde = DateTime.TryParse(
                r.GetString(1),
                CultureInfo.InvariantCulture,
                out var f
            )
                ? f
                : ahora;
            situacion = completa
                ? (ahora - desde).TotalDays >= 90
                    ? "cerrada"
                    : "completa"
                : "activa";
            if (actual != situacion)
                RegistrarSituacion(
                    cadenaId,
                    situacion,
                    situacion == "cerrada"
                        ? "Completa hace 90 días sin documentos nuevos"
                        : situacion,
                    ahora
                );
        }
        using var actualizar = conexion.CreateCommand();
        actualizar.CommandText =
            "UPDATE cadenas_diccionario SET situacion=$s,situacion_desde=CASE WHEN situacion<>$s THEN $f ELSE situacion_desde END WHERE id=$c;";
        actualizar.Parameters.AddWithValue("$s", situacion);
        actualizar.Parameters.AddWithValue("$f", ahora.ToString("o", CultureInfo.InvariantCulture));
        actualizar.Parameters.AddWithValue("$c", cadenaId);
        actualizar.ExecuteNonQuery();
        if (situacion == "cerrada")
        {
            using var cerrarAvisos = conexion.CreateCommand();
            cerrarAvisos.CommandText =
                "UPDATE alertas_esquema_diccionario SET estado='cerrada',actualizada_en=$f WHERE cadena_id=$c AND estado='abierta';";
            cerrarAvisos.Parameters.AddWithValue(
                "$f",
                ahora.ToString("o", CultureInfo.InvariantCulture)
            );
            cerrarAvisos.Parameters.AddWithValue("$c", cadenaId);
            cerrarAvisos.ExecuteNonQuery();
        }
        ActualizarRutCliente(cadenaId);
    }

    public bool DocumentoAgregado(long cadenaId, long versionId, DateTime ahora)
    {
        string? actual;
        using (var q = conexion.CreateCommand())
        {
            q.CommandText = "SELECT situacion FROM cadenas_diccionario WHERE id=$c;";
            q.Parameters.AddWithValue("$c", cadenaId);
            actual = q.ExecuteScalar() as string;
        }
        if (actual is "completa" or "cerrada")
        {
            string tipo = CodigoTipo(versionId);
            string numero = Numero(versionId);
            RegistrarSituacion(
                cadenaId,
                "activa",
                $"Reabierta: llegó {tipo} {numero}".Trim(),
                ahora
            );
            using var q = conexion.CreateCommand();
            q.CommandText =
                "UPDATE cadenas_diccionario SET situacion='activa',situacion_desde=$f WHERE id=$c;";
            q.Parameters.AddWithValue("$f", ahora.ToString("o", CultureInfo.InvariantCulture));
            q.Parameters.AddWithValue("$c", cadenaId);
            q.ExecuteNonQuery();
            return true;
        }
        return false;
    }

    public IReadOnlyList<CotizacionCadena> Sugerir(long cadenaId, DateTime hoy)
    {
        string? rut;
        string proveedor;
        using (var q = conexion.CreateCommand())
        {
            q.CommandText =
                "SELECT c.rut_cliente,e.proveedor FROM cadenas_diccionario c JOIN esquemas_diccionario e ON e.id=c.esquema_id WHERE c.id=$c;";
            q.Parameters.AddWithValue("$c", cadenaId);
            using var r = q.ExecuteReader();
            if (!r.Read())
                return [];
            rut = r.IsDBNull(0) ? null : r.GetString(0);
            proveedor = r.GetString(1);
        }
        var sugerencias = new List<CotizacionCadena>();
        if (!string.IsNullOrWhiteSpace(rut))
            sugerencias.AddRange(
                ConsultarCotizaciones("cotizacion_propia", cadenaId, hoy.AddDays(-90), rut, null)
            );
        sugerencias.AddRange(
            ConsultarCotizaciones("cotizacion_proveedor", cadenaId, null, null, proveedor)
        );
        return sugerencias;
    }

    public void Agregar(long cadenaId, long versionId)
    {
        string? tipo = Tipo(versionId);
        if (tipo is not ("cotizacion_propia" or "cotizacion_proveedor"))
            throw new InvalidOperationException("Solo se pueden agregar COV o COVP a mano.");
        using var q = conexion.CreateCommand();
        q.CommandText =
            "INSERT INTO documentos_manuales_cadena_diccionario(cadena_id,version_id,tipo,agregado_en) SELECT $c,$v,$t,$f WHERE EXISTS(SELECT 1 FROM cadenas_diccionario WHERE id=$c AND estado='activa') AND NOT EXISTS(SELECT 1 FROM ubicaciones_diccionario_todas WHERE version_id=$v) AND NOT EXISTS(SELECT 1 FROM documentos_manuales_cadena_diccionario WHERE version_id=$v);";
        q.Parameters.AddWithValue("$c", cadenaId);
        q.Parameters.AddWithValue("$v", versionId);
        q.Parameters.AddWithValue("$t", tipo);
        q.Parameters.AddWithValue("$f", DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
        if (q.ExecuteNonQuery() == 0)
            throw new InvalidOperationException(
                "El documento ya está en una cadena o esta no está activa."
            );
        if (!DocumentoAgregado(cadenaId, versionId, DateTime.Now))
            Recalcular(cadenaId, DateTime.Now);
    }

    public void Quitar(long cadenaId, long versionId)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "DELETE FROM documentos_manuales_cadena_diccionario WHERE cadena_id=$c AND version_id=$v;";
        q.Parameters.AddWithValue("$c", cadenaId);
        q.Parameters.AddWithValue("$v", versionId);
        if (q.ExecuteNonQuery() == 0)
            throw new InvalidOperationException(
                "La cotización no fue agregada a mano a esta cadena."
            );
        Recalcular(cadenaId, DateTime.Now);
    }

    public IReadOnlyList<CotizacionCadena> ListarAgregadas(long cadenaId)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT m.version_id,m.tipo,d.nombre,d.ruta,COALESCE((SELECT numero FROM numeros_documento n WHERE n.documento_id=v.documento_id LIMIT 1),'') FROM documentos_manuales_cadena_diccionario m JOIN versiones_documento v ON v.id=m.version_id JOIN documentos d ON d.id=v.documento_id WHERE m.cadena_id=$c ORDER BY m.id;";
        q.Parameters.AddWithValue("$c", cadenaId);
        using var r = q.ExecuteReader();
        var resultado = new List<CotizacionCadena>();
        while (r.Read())
            resultado.Add(
                new(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4))
            );
        return resultado;
    }

    private bool EsCompleta(long cadenaId)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT EXISTS(SELECT 1 FROM ubicaciones_diccionario_todas u JOIN lugares_esquema_diccionario l ON l.id=u.lugar_id WHERE u.cadena_id=$c AND u.estado='activo' AND l.dato_tipo_id='guia_proveedor') AND NOT EXISTS(SELECT 1 FROM ubicaciones_diccionario_todas g JOIN lugares_esquema_diccionario gl ON gl.id=g.lugar_id WHERE g.cadena_id=$c AND g.estado='activo' AND gl.dato_tipo_id='guia_proveedor' AND NOT EXISTS(SELECT 1 FROM ubicaciones_diccionario_todas f JOIN lugares_esquema_diccionario fl ON fl.id=f.lugar_id WHERE f.cadena_id=$c AND f.estado='activo' AND fl.dato_tipo_id='factura_propia' AND f.linea_id IS g.linea_id)) AND NOT EXISTS(SELECT 1 FROM ubicaciones_diccionario_todas n JOIN lugares_esquema_diccionario nl ON nl.id=n.lugar_id WHERE n.cadena_id=$c AND n.estado='activo' AND nl.dato_tipo_id='nota_credito_proveedor' AND NOT EXISTS(SELECT 1 FROM ubicaciones_diccionario_todas v JOIN lugares_esquema_diccionario vl ON vl.id=v.lugar_id WHERE v.cadena_id=$c AND v.estado='activo' AND vl.dato_tipo_id='nota_credito_propia' AND v.linea_id IS n.linea_id));";
        q.Parameters.AddWithValue("$c", cadenaId);
        return Convert.ToInt64(q.ExecuteScalar()) != 0;
    }

    private void ActualizarRutCliente(long cadenaId)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "UPDATE cadenas_diccionario SET rut_cliente=(SELECT x.valor_clave FROM ubicaciones_diccionario_todas u JOIN valores_documento x ON x.version_id=u.version_id AND x.estado='vigente' AND x.dato_diccionario_id='rut_cliente' JOIN lugares_esquema_diccionario l ON l.id=u.lugar_id WHERE u.cadena_id=$c AND u.estado='activo' AND l.dato_tipo_id IN ('nota_venta_propia','oc_cliente','factura_propia') ORDER BY CASE l.dato_tipo_id WHEN 'nota_venta_propia' THEN 0 WHEN 'oc_cliente' THEN 1 ELSE 2 END,u.orden LIMIT 1) WHERE id=$c;";
        q.Parameters.AddWithValue("$c", cadenaId);
        q.ExecuteNonQuery();
    }

    private IReadOnlyList<CotizacionCadena> ConsultarCotizaciones(
        string tipo,
        long cadena,
        DateTime? desde,
        string? rut,
        string? proveedor
    )
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT DISTINCT v.id,$t,d.nombre,d.ruta,COALESCE((SELECT numero FROM numeros_documento n WHERE n.documento_id=d.id LIMIT 1),'') FROM versiones_documento v JOIN documentos d ON d.id=v.documento_id JOIN valores_documento tipo ON tipo.version_id=v.id AND tipo.estado='vigente' AND tipo.dato_diccionario_id=$t WHERE v.estado='vigente' AND d.estado_baja='activo' AND ($desde IS NULL OR v.registrada_en >= $desde) AND ($rut IS NULL OR EXISTS(SELECT 1 FROM valores_documento x WHERE x.version_id=v.id AND x.estado='vigente' AND x.dato_diccionario_id='rut_cliente' AND x.valor_clave=$rut)) AND NOT EXISTS(SELECT 1 FROM ubicaciones_diccionario_todas u WHERE u.version_id=v.id) AND NOT EXISTS(SELECT 1 FROM documentos_manuales_cadena_diccionario m WHERE m.version_id=v.id) ORDER BY v.registrada_en DESC;";
        q.Parameters.AddWithValue("$t", tipo);
        q.Parameters.AddWithValue(
            "$desde",
            desde?.ToString("o", CultureInfo.InvariantCulture) ?? (object)DBNull.Value
        );
        q.Parameters.AddWithValue("$rut", (object?)rut ?? DBNull.Value);
        using var r = q.ExecuteReader();
        var filas = new List<CotizacionCadena>();
        while (r.Read())
            filas.Add(
                new(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4))
            );
        return proveedor is null
            ? filas
            : filas
                .Where(f =>
                    ProveedorDeDocumento.Determinar(conexion, f.VersionId)?.Clave == proveedor
                )
                .ToArray();
    }

    private string? Tipo(long versionId)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT t.dato_diccionario_id FROM valores_documento x JOIN campos_documento f ON f.id=x.campo_id JOIN tipos_documento_datos t ON t.identificacion_id=f.identificacion_id AND t.define_tipo=1 AND t.activo=1 AND t.dato_diccionario_id=x.dato_diccionario_id WHERE x.version_id=$v AND x.estado='vigente' ORDER BY x.id LIMIT 1;";
        q.Parameters.AddWithValue("$v", versionId);
        return q.ExecuteScalar() as string;
    }

    private string Numero(long versionId)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT COALESCE((SELECT numero FROM numeros_documento n JOIN versiones_documento v ON v.documento_id=n.documento_id WHERE v.id=$v LIMIT 1),(SELECT valor_original FROM valores_documento WHERE version_id=$v AND estado='vigente' AND dato_diccionario_id IS NOT NULL ORDER BY id LIMIT 1),'');";
        q.Parameters.AddWithValue("$v", versionId);
        return Convert.ToString(q.ExecuteScalar()) ?? "";
    }

    private string CodigoTipo(long versionId)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT COALESCE(d.codigo_referencia,d.id) FROM valores_documento x JOIN diccionario_datos d ON d.id=x.dato_diccionario_id JOIN campos_documento f ON f.id=x.campo_id JOIN tipos_documento_datos t ON t.identificacion_id=f.identificacion_id AND t.define_tipo=1 AND t.activo=1 AND t.dato_diccionario_id=d.id WHERE x.version_id=$v AND x.estado='vigente' ORDER BY x.id LIMIT 1;";
        q.Parameters.AddWithValue("$v", versionId);
        return Convert.ToString(q.ExecuteScalar()) ?? "documento";
    }

    private void RegistrarSituacion(long cadenaId, string situacion, string detalle, DateTime ahora)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "INSERT INTO historial_situacion_cadena_diccionario(cadena_id,situacion,detalle,fecha) VALUES($c,$s,$d,$f);";
        q.Parameters.AddWithValue("$c", cadenaId);
        q.Parameters.AddWithValue("$s", situacion);
        q.Parameters.AddWithValue("$d", detalle);
        q.Parameters.AddWithValue("$f", ahora.ToString("o", CultureInfo.InvariantCulture));
        q.ExecuteNonQuery();
    }
}
