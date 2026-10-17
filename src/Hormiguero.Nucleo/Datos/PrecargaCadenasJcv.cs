using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public static class PrecargaCadenasJcv
{
    private static bool ActualizarVersionDos(
        SqliteConnection conexion,
        JsonElement raiz,
        string app,
        string ruta
    )
    {
        try
        {
            using var tx = conexion.BeginTransaction();
            foreach (var esquema in raiz.GetProperty("esquemas").EnumerateArray())
            {
                string proveedor = ProveedorDeDocumento.NormalizarProveedor(
                    esquema.GetProperty("proveedor").GetString()!
                );
                using var buscar = conexion.CreateCommand();
                buscar.Transaction = tx;
                buscar.CommandText =
                    "SELECT id FROM esquemas_diccionario WHERE proveedor=$p COLLATE NOCASE;";
                buscar.Parameters.AddWithValue("$p", proveedor);
                if (buscar.ExecuteScalar() is not object idObjeto)
                    continue;
                long esquemaId = Convert.ToInt64(idObjeto);
                var lugares = new Dictionary<string, long>(StringComparer.Ordinal);
                using (var l = conexion.CreateCommand())
                {
                    l.Transaction = tx;
                    l.CommandText =
                        "SELECT dato_tipo_id,id FROM lugares_esquema_diccionario WHERE esquema_id=$e;";
                    l.Parameters.AddWithValue("$e", esquemaId);
                    using var lector = l.ExecuteReader();
                    while (lector.Read())
                        lugares[lector.GetString(0)] = lector.GetInt64(1);
                }
                foreach (var lugar in esquema.GetProperty("lugares").EnumerateArray())
                {
                    string tipo = lugar.GetProperty("tipo").GetString()!;
                    if (!lugares.TryGetValue(tipo, out long lugarId))
                        continue;
                    using var actualizar = conexion.CreateCommand();
                    actualizar.Transaction = tx;
                    actualizar.CommandText =
                        "UPDATE lugares_esquema_diccionario SET datos_requeridos_json=$r,carpeta_busqueda=$c WHERE id=$l;";
                    actualizar.Parameters.AddWithValue(
                        "$r",
                        lugar.TryGetProperty("datosRequeridos", out var datos)
                            ? datos.GetRawText()
                            : "[]"
                    );
                    actualizar.Parameters.AddWithValue(
                        "$c",
                        lugar.TryGetProperty("carpetaBusqueda", out var carpeta)
                        && carpeta.ValueKind == JsonValueKind.String
                            ? carpeta.GetString()!
                            : DBNull.Value
                    );
                    actualizar.Parameters.AddWithValue("$l", lugarId);
                    actualizar.ExecuteNonQuery();
                }
                foreach (var enlace in esquema.GetProperty("enlaces").EnumerateArray())
                {
                    string tipoA = enlace.GetProperty("a").GetString()!;
                    string tipoB = enlace.GetProperty("b").GetString()!;
                    if (
                        !lugares.TryGetValue(tipoA, out long a)
                        || !lugares.TryGetValue(tipoB, out long b)
                    )
                        continue;
                    using var agregar = conexion.CreateCommand();
                    agregar.Transaction = tx;
                    agregar.CommandText =
                        "INSERT INTO enlaces_esquema_diccionario(lugar_a,lugar_b,dato_diccionario_id,cardinalidad) SELECT $a,$b,$d,$c WHERE NOT EXISTS(SELECT 1 FROM enlaces_esquema_diccionario WHERE lugar_a=$a AND lugar_b=$b AND dato_diccionario_id=$d);";
                    agregar.Parameters.AddWithValue("$a", a);
                    agregar.Parameters.AddWithValue("$b", b);
                    agregar.Parameters.AddWithValue("$d", enlace.GetProperty("dato").GetString()!);
                    agregar.Parameters.AddWithValue(
                        "$c",
                        enlace.GetProperty("cardinalidad").GetString()!
                    );
                    agregar.ExecuteNonQuery();
                }
                if (esquema.TryGetProperty("datosIngresados", out var ingresados))
                {
                    using var borrar = conexion.CreateCommand();
                    borrar.Transaction = tx;
                    borrar.CommandText =
                        "DELETE FROM datos_ingresados_esquema_diccionario WHERE esquema_id=$e;";
                    borrar.Parameters.AddWithValue("$e", esquemaId);
                    borrar.ExecuteNonQuery();
                    int orden = 0;
                    foreach (var dato in ingresados.EnumerateArray())
                    {
                        using var insertar = conexion.CreateCommand();
                        insertar.Transaction = tx;
                        insertar.CommandText =
                            "INSERT INTO datos_ingresados_esquema_diccionario(esquema_id,nombre,orden) VALUES($e,$n,$o);";
                        insertar.Parameters.AddWithValue("$e", esquemaId);
                        insertar.Parameters.AddWithValue("$n", dato.GetString()!);
                        insertar.Parameters.AddWithValue("$o", orden++);
                        insertar.ExecuteNonQuery();
                    }
                }
            }
            using (var marcar = conexion.CreateCommand())
            {
                marcar.Transaction = tx;
                marcar.CommandText =
                    "INSERT INTO configuracion(clave,valor) VALUES('precarga_cadenas_jcv_version','2') ON CONFLICT(clave) DO UPDATE SET valor='2';";
                marcar.ExecuteNonQuery();
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
            {
                if (!File.Exists(ruta))
                    return false;
                using var existente = JsonDocument.Parse(
                    File.ReadAllText(ruta).TrimStart('\uFEFF')
                );
                if (existente.RootElement.GetProperty("version").GetInt32() < 2)
                    return false;
                using (var versionAplicada = conexion.CreateCommand())
                {
                    versionAplicada.CommandText =
                        "SELECT valor FROM configuracion WHERE clave='precarga_cadenas_jcv_version';";
                    if (Convert.ToInt32(versionAplicada.ExecuteScalar() ?? 0) >= 2)
                        return false;
                }
                return ActualizarVersionDos(conexion, existente.RootElement, app, ruta);
            }
        }
        if (!File.Exists(ruta))
            return false;
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(ruta).TrimStart('\uFEFF'));
            var raiz = json.RootElement;
            int versionJson = raiz.TryGetProperty("version", out var versionElemento)
                ? versionElemento.GetInt32()
                : 1;
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
                        "INSERT INTO lugares_esquema_diccionario(esquema_id,orden,dato_tipo_id,inicia_cadena,nombre,datos_requeridos_json,carpeta_busqueda) VALUES($e,$o,$d,$i,$n,$r,$c) RETURNING id;";
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
                    l.Parameters.AddWithValue(
                        "$r",
                        lugar.TryGetProperty("datosRequeridos", out var requeridos)
                            ? requeridos.GetRawText()
                            : "[]"
                    );
                    l.Parameters.AddWithValue(
                        "$c",
                        lugar.TryGetProperty("carpetaBusqueda", out var carpeta)
                        && carpeta.ValueKind == JsonValueKind.String
                            ? carpeta.GetString()!
                            : DBNull.Value
                    );
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
                if (esquema.TryGetProperty("datosIngresados", out var datosIngresados))
                {
                    int ordenDato = 0;
                    foreach (var dato in datosIngresados.EnumerateArray())
                    {
                        using var d = conexion.CreateCommand();
                        d.Transaction = tx;
                        d.CommandText =
                            "INSERT INTO datos_ingresados_esquema_diccionario(esquema_id,nombre,orden) VALUES($e,$n,$o);";
                        d.Parameters.AddWithValue("$e", esquemaId);
                        d.Parameters.AddWithValue("$n", dato.GetString()!);
                        d.Parameters.AddWithValue("$o", ordenDato++);
                        d.ExecuteNonQuery();
                    }
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
            if (versionJson >= 2)
            {
                using var marcar = conexion.CreateCommand();
                marcar.Transaction = tx;
                marcar.CommandText =
                    "INSERT INTO configuracion(clave,valor) VALUES('precarga_cadenas_jcv_version','2') ON CONFLICT(clave) DO UPDATE SET valor='2';";
                marcar.ExecuteNonQuery();
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
