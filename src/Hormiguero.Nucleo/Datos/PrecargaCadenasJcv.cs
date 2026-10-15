using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public static class PrecargaCadenasJcv
{
    public static IReadOnlyList<(string Id, string Nombre)> TiposActivos(string rutaBase)
    {
        using var conexion = BaseComun.Abrir(rutaBase);
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT DISTINCT d.id,d.nombre FROM diccionario_datos d JOIN lugares_esquema_diccionario l ON l.dato_tipo_id=d.id JOIN esquemas_diccionario e ON e.id=l.esquema_id AND e.activo=1 WHERE l.activo=1 ORDER BY d.orden;";
        using var r = q.ExecuteReader();
        var filas = new List<(string, string)>();
        while (r.Read())
            filas.Add((r.GetString(0), r.GetString(1)));
        return filas;
    }

    public static IReadOnlyList<(string Id, string Nombre)> ProveedoresActivos(string rutaBase)
    {
        using var conexion = BaseComun.Abrir(rutaBase);
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT proveedor,nombre FROM esquemas_diccionario WHERE activo=1 ORDER BY proveedor;";
        using var r = q.ExecuteReader();
        var filas = new List<(string, string)>();
        while (r.Read())
            filas.Add((r.GetString(0), r.GetString(1)));
        return filas;
    }

    public static bool IntentarImportar(SqliteConnection conexion, string ruta, string app)
    {
        using (var q = conexion.CreateCommand())
        {
            q.CommandText = "SELECT COUNT(*) FROM esquemas_diccionario;";
            if (Convert.ToInt64(q.ExecuteScalar()) != 0)
                return false;
        }
        if (!File.Exists(ruta))
            return false;
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(ruta).TrimStart('\uFEFF'));
            var raiz = json.RootElement;
            using var tx = conexion.BeginTransaction();
            foreach (var esquema in raiz.GetProperty("esquemas").EnumerateArray())
            {
                string proveedor = esquema.GetProperty("proveedor").GetString()!;
                using var q = conexion.CreateCommand();
                q.Transaction = tx;
                q.CommandText =
                    "INSERT INTO esquemas_diccionario(proveedor,nombre,activo,creada_en,actualizada_en) VALUES($p,$n,1,$f,$f) RETURNING id;";
                q.Parameters.AddWithValue(
                    "$p",
                    ProveedorDeDocumento.NormalizarProveedor(proveedor)
                );
                q.Parameters.AddWithValue("$n", esquema.GetProperty("nombre").GetString()!);
                q.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
                long esquemaId = Convert.ToInt64(q.ExecuteScalar());
                var lugares = new Dictionary<string, (long Id, int Orden)>();
                foreach (var lugar in esquema.GetProperty("lugares").EnumerateArray())
                {
                    using var l = conexion.CreateCommand();
                    l.Transaction = tx;
                    l.CommandText =
                        "INSERT INTO lugares_esquema_diccionario(esquema_id,orden,dato_tipo_id,inicia_cadena,nombre) VALUES($e,$o,$d,$i,$n) RETURNING id;";
                    l.Parameters.AddWithValue("$e", esquemaId);
                    l.Parameters.AddWithValue("$o", lugar.GetProperty("orden").GetInt32());
                    l.Parameters.AddWithValue("$d", lugar.GetProperty("tipo").GetString()!);
                    l.Parameters.AddWithValue(
                        "$i",
                        lugar.TryGetProperty("inicia", out var inicia) && inicia.GetBoolean()
                            ? 1
                            : 0
                    );
                    l.Parameters.AddWithValue("$n", lugar.GetProperty("nombre").GetString()!);
                    lugares.Add(
                        lugar.GetProperty("tipo").GetString()!,
                        (Convert.ToInt64(l.ExecuteScalar()), lugar.GetProperty("orden").GetInt32())
                    );
                }
                foreach (
                    var reconocimiento in esquema.GetProperty("reconocimientos").EnumerateArray()
                )
                {
                    string tipo = reconocimiento.GetProperty("tipo").GetString()!,
                        valor = reconocimiento.GetProperty("valor").GetString()!;
                    string normalizado =
                        tipo == "rut"
                            ? ProveedorDeDocumento.NormalizarRut(valor)
                            : ProveedorDeDocumento.NormalizarNombre(valor);
                    using var r = conexion.CreateCommand();
                    r.Transaction = tx;
                    r.CommandText =
                        "INSERT OR IGNORE INTO reconocimientos_esquema_diccionario(esquema_id,tipo,valor_normalizado) VALUES($e,$t,$v);";
                    r.Parameters.AddWithValue("$e", esquemaId);
                    r.Parameters.AddWithValue("$t", tipo);
                    r.Parameters.AddWithValue("$v", normalizado);
                    r.ExecuteNonQuery();
                }
                foreach (var enlace in esquema.GetProperty("enlaces").EnumerateArray())
                {
                    using var e = conexion.CreateCommand();
                    e.Transaction = tx;
                    e.CommandText =
                        "INSERT INTO enlaces_esquema_diccionario(lugar_a,lugar_b,dato_diccionario_id,cardinalidad) VALUES($a,$b,$d,$c);";
                    e.Parameters.AddWithValue(
                        "$a",
                        lugares[enlace.GetProperty("a").GetString()!].Id
                    );
                    e.Parameters.AddWithValue(
                        "$b",
                        lugares[enlace.GetProperty("b").GetString()!].Id
                    );
                    e.Parameters.AddWithValue("$d", enlace.GetProperty("dato").GetString()!);
                    e.Parameters.AddWithValue(
                        "$c",
                        enlace.GetProperty("cardinalidad").GetString()!
                    );
                    e.ExecuteNonQuery();
                }
                var modos = new Dictionary<string, long>();
                foreach (
                    string modo in esquema
                        .GetProperty("modos")
                        .EnumerateArray()
                        .Select(x => x.GetString()!)
                )
                {
                    using var m = conexion.CreateCommand();
                    m.Transaction = tx;
                    m.CommandText =
                        "INSERT INTO modos_esquema_diccionario(esquema_id,nombre) VALUES($e,$n) RETURNING id;";
                    m.Parameters.AddWithValue("$e", esquemaId);
                    m.Parameters.AddWithValue("$n", modo);
                    modos.Add(modo, Convert.ToInt64(m.ExecuteScalar()));
                }
                if (esquema.TryGetProperty("proponerModo", out var propuestas))
                {
                    int ordenPropuesta = 0;
                    foreach (var propuesta in propuestas.EnumerateArray())
                    {
                        using var p = conexion.CreateCommand();
                        p.Transaction = tx;
                        p.CommandText =
                            "INSERT INTO reglas_propuesta_modo_esquema_diccionario(esquema_id,orden,contiene,modo) VALUES($e,$o,$c,$m);";
                        p.Parameters.AddWithValue("$e", esquemaId);
                        p.Parameters.AddWithValue("$o", ordenPropuesta++);
                        p.Parameters.AddWithValue(
                            "$c",
                            propuesta.GetProperty("contiene").GetString()!
                        );
                        p.Parameters.AddWithValue("$m", propuesta.GetProperty("modo").GetString()!);
                        p.ExecuteNonQuery();
                    }
                }
                if (
                    esquema.TryGetProperty("decide", out var decide)
                    && decide.ValueKind == JsonValueKind.String
                )
                {
                    using var d = conexion.CreateCommand();
                    d.Transaction = tx;
                    d.CommandText =
                        "UPDATE esquemas_diccionario SET lugar_decide_modo_id=$l WHERE id=$e;";
                    d.Parameters.AddWithValue("$l", lugares[decide.GetString()!].Id);
                    d.Parameters.AddWithValue("$e", esquemaId);
                    d.ExecuteNonQuery();
                }
                foreach (var aviso in esquema.GetProperty("avisos").EnumerateArray())
                {
                    string tipo = aviso.GetProperty("tipo").GetString()!;
                    var parametros = JsonNode(aviso.GetProperty("parametros"), lugares);
                    using var a = conexion.CreateCommand();
                    a.Transaction = tx;
                    a.CommandText =
                        "INSERT INTO reglas_alerta_esquema_diccionario(esquema_id,lugar_id,modo_id,tipo,parametros_json) VALUES($e,$l,$m,$t,$j);";
                    a.Parameters.AddWithValue("$e", esquemaId);
                    a.Parameters.AddWithValue(
                        "$l",
                        lugares[aviso.GetProperty("lugar").GetString()!].Id
                    );
                    string? modo =
                        aviso.TryGetProperty("modo", out var modoJson)
                        && modoJson.ValueKind == JsonValueKind.String
                            ? modoJson.GetString()
                            : null;
                    a.Parameters.AddWithValue("$m", modo is null ? DBNull.Value : modos[modo]);
                    a.Parameters.AddWithValue("$t", tipo);
                    a.Parameters.AddWithValue("$j", parametros);
                    a.ExecuteNonQuery();
                }
            }
            tx.Commit();
            return true;
        }
        catch (Exception error)
        {
            using var auditoria = conexion.CreateCommand();
            auditoria.CommandText =
                "INSERT INTO auditoria(fecha,app,accion,origen,destino,resultado) VALUES($f,$a,'precarga_cadenas_jcv',$o,$d,'fallida');";
            auditoria.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
            auditoria.Parameters.AddWithValue("$a", app);
            auditoria.Parameters.AddWithValue("$o", ruta);
            auditoria.Parameters.AddWithValue("$d", error.Message);
            auditoria.ExecuteNonQuery();
            return false;
        }
    }

    private static string JsonNode(
        JsonElement parametros,
        Dictionary<string, (long Id, int Orden)> lugares
    )
    {
        var raiz = System.Text.Json.Nodes.JsonNode.Parse(parametros.GetRawText())!;
        foreach (
            string propiedad in new[]
            {
                "desdeLugarId",
                "hastaLugarId",
                "cuandoLugarId",
                "condicionLugarId",
            }
        )
            if (
                raiz[propiedad] is System.Text.Json.Nodes.JsonValue valor
                && valor.TryGetValue<string>(out var tipo)
                && lugares.TryGetValue(tipo, out var lugar)
            )
                raiz[propiedad] = lugar.Id;
        return raiz.ToJsonString();
    }
}
