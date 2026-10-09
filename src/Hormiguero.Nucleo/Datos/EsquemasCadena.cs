using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed record LugarEsquema(
    long Id,
    long EsquemaId,
    int Orden,
    long IdentificacionId,
    bool IniciaCadena,
    string Nombre
);

public sealed record ParejaEsquema(long Id, long LugarA, long LugarB, string DatoDiccionarioId);

public sealed record ModoEsquema(long Id, long EsquemaId, string Nombre);

public sealed record ReglaAlertaEsquema(
    long Id,
    long EsquemaId,
    long LugarId,
    long? ModoId,
    string Tipo,
    string ParametrosJson
);

public sealed record DefinicionLugarEsquema(
    int Orden,
    long IdentificacionId,
    bool IniciaCadena,
    string Nombre
);

public sealed record DefinicionParejaEsquema(int OrdenA, int OrdenB, string DatoDiccionarioId);

public sealed record EsquemaCadena(
    long Id,
    string Proveedor,
    string Nombre,
    bool Activo,
    long? LugarDecideModo,
    IReadOnlyList<LugarEsquema> Lugares,
    IReadOnlyList<ParejaEsquema> Parejas,
    IReadOnlyList<ModoEsquema> Modos,
    IReadOnlyList<ReglaAlertaEsquema> ReglasAlerta
);

public sealed record ProveedorDocumento(string Clave, string Nombre);

public static class ProveedorDeDocumento
{
    public static string NormalizarProveedor(string proveedor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proveedor);
        return string.Join(
                ' ',
                proveedor.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            )
            .ToUpperInvariant();
    }

    public static ProveedorDocumento? Determinar(SqliteConnection conexion, long versionId)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT i.emisor, i.grupo_documento, "
            + "EXISTS(SELECT 1 FROM valores_documento x WHERE x.version_id=$v AND x.estado='vigente' AND x.dato_diccionario_id='oc_propia'), "
            + "(SELECT x.valor_original FROM valores_documento x WHERE x.version_id=$v AND x.estado='vigente' AND x.dato_diccionario_id='rut_proveedor' ORDER BY x.id LIMIT 1), "
            + "(SELECT x.valor_original FROM valores_documento x WHERE x.version_id=$v AND x.estado='vigente' AND x.dato_diccionario_id='nombre_proveedor' ORDER BY x.id LIMIT 1), "
            + "EXISTS(SELECT 1 FROM valores_documento x JOIN diccionario_datos d ON d.id=x.dato_diccionario_id WHERE x.version_id=$v AND x.estado='vigente' AND d.grupo='Del proveedor') "
            + "FROM versiones_documento ver JOIN campos_documento c ON c.id=(SELECT campo_id FROM valores_documento WHERE version_id=ver.id AND estado='vigente' ORDER BY id LIMIT 1) JOIN identificaciones i ON i.id=c.identificacion_id WHERE ver.id=$v;";
        comando.Parameters.AddWithValue("$v", versionId);
        using var lector = comando.ExecuteReader();
        if (!lector.Read())
            return null;
        string emisor = lector.GetString(0);
        bool esPropio = lector.GetString(1) == "Emitido";
        bool esCompra = lector.GetInt64(2) != 0;
        string? rut = lector.IsDBNull(3) ? null : lector.GetString(3);
        string? nombre = lector.IsDBNull(4) ? null : lector.GetString(4);
        bool esDelProveedor = lector.GetInt64(5) != 0;
        string? proveedor =
            esDelProveedor ? emisor
            : esPropio && esCompra ? rut ?? nombre
            : rut ?? nombre;
        if (string.IsNullOrWhiteSpace(proveedor))
            return null;
        return new(NormalizarProveedor(proveedor), proveedor.Trim());
    }
}

public sealed class RepositorioEsquemas(SqliteConnection conexion)
{
    public long Guardar(
        string proveedor,
        string nombre,
        IReadOnlyList<DefinicionLugarEsquema> lugares,
        IReadOnlyList<DefinicionParejaEsquema>? parejas = null,
        IReadOnlyList<string>? modos = null,
        int? lugarDecideModoOrden = null,
        bool activo = true
    )
    {
        string clave = ProveedorDeDocumento.NormalizarProveedor(proveedor);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        if (lugares.Count == 0)
            throw new ArgumentException("El esquema debe tener lugares.", nameof(lugares));
        if (lugares.Any(l => l.Orden < 0 || string.IsNullOrWhiteSpace(l.Nombre)))
            throw new ArgumentException("Cada lugar debe tener orden y nombre.", nameof(lugares));
        using var tx = conexion.BeginTransaction();
        long esquemaId;
        using (var comando = conexion.CreateCommand())
        {
            comando.Transaction = tx;
            comando.CommandText =
                "INSERT INTO esquemas_cadena(proveedor,nombre,activo,creada_en,actualizada_en) VALUES($p,$n,$a,$f,$f) "
                + "ON CONFLICT(proveedor) DO UPDATE SET nombre=excluded.nombre,activo=excluded.activo,actualizada_en=excluded.actualizada_en RETURNING id;";
            comando.Parameters.AddWithValue("$p", clave);
            comando.Parameters.AddWithValue("$n", nombre.Trim());
            comando.Parameters.AddWithValue("$a", activo ? 1 : 0);
            comando.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
            esquemaId = Convert.ToInt64(comando.ExecuteScalar());
        }
        using (var limpiar = conexion.CreateCommand())
        {
            limpiar.Transaction = tx;
            limpiar.CommandText =
                "UPDATE esquemas_cadena SET lugar_decide_modo=NULL WHERE id=$e; DELETE FROM parejas_esquema WHERE lugar_a IN (SELECT id FROM lugares_esquema WHERE esquema_id=$e) OR lugar_b IN (SELECT id FROM lugares_esquema WHERE esquema_id=$e); DELETE FROM reglas_alerta_esquema WHERE esquema_id=$e;";
            limpiar.Parameters.AddWithValue("$e", esquemaId);
            limpiar.ExecuteNonQuery();
        }
        var ids = new Dictionary<int, long>();
        var existentes = new Dictionary<long, long>();
        using (var consulta = conexion.CreateCommand())
        {
            consulta.Transaction = tx;
            consulta.CommandText =
                "SELECT id,identificacion_id FROM lugares_esquema WHERE esquema_id=$e;";
            consulta.Parameters.AddWithValue("$e", esquemaId);
            using var lector = consulta.ExecuteReader();
            while (lector.Read())
                existentes[lector.GetInt64(1)] = lector.GetInt64(0);
        }
        foreach (long id in existentes.Values)
        {
            using var reordenar = conexion.CreateCommand();
            reordenar.Transaction = tx;
            reordenar.CommandText = "UPDATE lugares_esquema SET orden=-id WHERE id=$id;";
            reordenar.Parameters.AddWithValue("$id", id);
            reordenar.ExecuteNonQuery();
        }
        foreach (var lugar in lugares.OrderBy(l => l.Orden))
        {
            using var comando = conexion.CreateCommand();
            comando.Transaction = tx;
            long id;
            bool existente = existentes.TryGetValue(lugar.IdentificacionId, out id);
            comando.CommandText = existente
                ? "UPDATE lugares_esquema SET orden=$o,inicia_cadena=$c,nombre=$n WHERE id=$id;"
                : "INSERT INTO lugares_esquema(esquema_id,orden,identificacion_id,inicia_cadena,nombre) VALUES($e,$o,$i,$c,$n) RETURNING id;";
            comando.Parameters.AddWithValue("$e", esquemaId);
            comando.Parameters.AddWithValue("$o", lugar.Orden);
            comando.Parameters.AddWithValue("$i", lugar.IdentificacionId);
            comando.Parameters.AddWithValue("$c", lugar.IniciaCadena ? 1 : 0);
            comando.Parameters.AddWithValue("$n", lugar.Nombre.Trim());
            if (existente)
            {
                comando.Parameters.AddWithValue("$id", id);
                comando.ExecuteNonQuery();
            }
            else
                id = Convert.ToInt64(comando.ExecuteScalar());
            ids.Add(lugar.Orden, id);
            existentes.Remove(lugar.IdentificacionId);
        }
        foreach (long id in existentes.Values)
        {
            using var eliminar = conexion.CreateCommand();
            eliminar.Transaction = tx;
            eliminar.CommandText = "DELETE FROM lugares_esquema WHERE id=$id;";
            eliminar.Parameters.AddWithValue("$id", id);
            eliminar.ExecuteNonQuery();
        }
        var modosElegidos = (modos ?? [])
            .Select(m => m.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        using (var leerModos = conexion.CreateCommand())
        {
            leerModos.Transaction = tx;
            leerModos.CommandText = "SELECT id,nombre FROM modos_esquema WHERE esquema_id=$e;";
            leerModos.Parameters.AddWithValue("$e", esquemaId);
            using var lector = leerModos.ExecuteReader();
            var obsoletos = new List<long>();
            while (lector.Read())
                if (!modosElegidos.Contains(lector.GetString(1)))
                    obsoletos.Add(lector.GetInt64(0));
            lector.Close();
            foreach (long id in obsoletos)
            {
                using var eliminar = conexion.CreateCommand();
                eliminar.Transaction = tx;
                eliminar.CommandText = "DELETE FROM modos_esquema WHERE id=$id;";
                eliminar.Parameters.AddWithValue("$id", id);
                eliminar.ExecuteNonQuery();
            }
        }
        foreach (var pareja in parejas ?? [])
        {
            if (
                !ids.TryGetValue(pareja.OrdenA, out long lugarA)
                || !ids.TryGetValue(pareja.OrdenB, out long lugarB)
            )
                throw new ArgumentException(
                    "La pareja debe referir a lugares del esquema.",
                    nameof(parejas)
                );
            using var comando = conexion.CreateCommand();
            comando.Transaction = tx;
            comando.CommandText =
                "INSERT INTO parejas_esquema(lugar_a,lugar_b,dato_diccionario_id) VALUES($a,$b,$d);";
            comando.Parameters.AddWithValue("$a", lugarA);
            comando.Parameters.AddWithValue("$b", lugarB);
            comando.Parameters.AddWithValue("$d", pareja.DatoDiccionarioId);
            comando.ExecuteNonQuery();
        }
        foreach (string modo in modos ?? [])
        {
            using var comando = conexion.CreateCommand();
            comando.Transaction = tx;
            comando.CommandText =
                "INSERT INTO modos_esquema(esquema_id,nombre) VALUES($e,$n) ON CONFLICT(esquema_id,nombre) DO NOTHING;";
            comando.Parameters.AddWithValue("$e", esquemaId);
            comando.Parameters.AddWithValue("$n", modo.Trim());
            comando.ExecuteNonQuery();
        }
        using (var decidir = conexion.CreateCommand())
        {
            decidir.Transaction = tx;
            decidir.CommandText = "UPDATE esquemas_cadena SET lugar_decide_modo=$l WHERE id=$e;";
            decidir.Parameters.AddWithValue(
                "$l",
                lugarDecideModoOrden is null ? DBNull.Value : ids[lugarDecideModoOrden.Value]
            );
            decidir.Parameters.AddWithValue("$e", esquemaId);
            decidir.ExecuteNonQuery();
        }
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "guardar_esquema_cadena",
            $"esquema:{esquemaId}",
            clave
        );
        tx.Commit();
        return esquemaId;
    }

    public EsquemaCadena? ObtenerPorProveedor(string proveedor)
    {
        string clave = ProveedorDeDocumento.NormalizarProveedor(proveedor);
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,proveedor,nombre,activo,lugar_decide_modo FROM esquemas_cadena WHERE proveedor=$p;";
        comando.Parameters.AddWithValue("$p", clave);
        using var lector = comando.ExecuteReader();
        if (!lector.Read())
            return null;
        long id = lector.GetInt64(0);
        var resultado = new EsquemaCadena(
            id,
            lector.GetString(1),
            lector.GetString(2),
            lector.GetInt64(3) != 0,
            lector.IsDBNull(4) ? null : lector.GetInt64(4),
            LeerLugares(id),
            LeerParejas(id),
            LeerModos(id),
            LeerReglas(id)
        );
        return resultado;
    }

    public IReadOnlyList<EsquemaCadena> Listar() =>
        LeerIds().Select(id => ObtenerPorId(id)!).ToArray();

    public long GuardarReglaAlerta(
        long esquemaId,
        int lugarOrden,
        string? nombreModo,
        string tipo,
        string parametrosJson
    )
    {
        ValidarReglaAlerta(tipo, parametrosJson);
        using var tx = conexion.BeginTransaction();
        long lugarId;
        using (var consulta = conexion.CreateCommand())
        {
            consulta.Transaction = tx;
            consulta.CommandText =
                "SELECT id FROM lugares_esquema WHERE esquema_id=$e AND orden=$o;";
            consulta.Parameters.AddWithValue("$e", esquemaId);
            consulta.Parameters.AddWithValue("$o", lugarOrden);
            lugarId = Convert.ToInt64(
                consulta.ExecuteScalar()
                    ?? throw new InvalidOperationException("No existe el lugar del esquema.")
            );
        }
        long? modoId = null;
        if (nombreModo is not null)
        {
            using var consulta = conexion.CreateCommand();
            consulta.Transaction = tx;
            consulta.CommandText =
                "SELECT id FROM modos_esquema WHERE esquema_id=$e AND nombre=$n;";
            consulta.Parameters.AddWithValue("$e", esquemaId);
            consulta.Parameters.AddWithValue("$n", nombreModo.Trim());
            modoId = Convert.ToInt64(
                consulta.ExecuteScalar()
                    ?? throw new InvalidOperationException("No existe el modo del esquema.")
            );
        }
        using var comando = conexion.CreateCommand();
        comando.Transaction = tx;
        comando.CommandText =
            "INSERT INTO reglas_alerta_esquema(esquema_id,lugar_id,modo_id,tipo,parametros_json) VALUES($e,$l,$m,$t,$p) RETURNING id;";
        comando.Parameters.AddWithValue("$e", esquemaId);
        comando.Parameters.AddWithValue("$l", lugarId);
        comando.Parameters.AddWithValue("$m", (object?)modoId ?? DBNull.Value);
        comando.Parameters.AddWithValue("$t", tipo.Trim());
        comando.Parameters.AddWithValue("$p", parametrosJson);
        long id = Convert.ToInt64(comando.ExecuteScalar());
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "guardar_regla_alerta_esquema",
            $"regla:{id}",
            $"esquema:{esquemaId}"
        );
        tx.Commit();
        return id;
    }

    private static void ValidarReglaAlerta(string tipo, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipo);
        using var documento = System.Text.Json.JsonDocument.Parse(json);
        var raiz = documento.RootElement;
        if (raiz.ValueKind != System.Text.Json.JsonValueKind.Object)
            throw new ArgumentException(
                "Los parámetros de la regla deben ser un objeto JSON.",
                nameof(json)
            );
        string Texto(string nombre)
        {
            if (
                !raiz.TryGetProperty(nombre, out var valor)
                || valor.ValueKind != System.Text.Json.JsonValueKind.String
                || string.IsNullOrWhiteSpace(valor.GetString())
            )
                throw new ArgumentException(
                    $"La regla requiere el texto «{nombre}».",
                    nameof(json)
                );
            return valor.GetString()!;
        }
        long Entero(string nombre)
        {
            if (
                !raiz.TryGetProperty(nombre, out var valor)
                || !valor.TryGetInt64(out long numero)
                || numero <= 0
            )
                throw new ArgumentException(
                    $"La regla requiere el identificador «{nombre}».",
                    nameof(json)
                );
            return numero;
        }
        switch (tipo)
        {
            case "falta_dato":
                Texto("dato");
                Texto("texto");
                break;
            case "plazo":
                bool lugar =
                    raiz.TryGetProperty("desdeLugarId", out var origen)
                    && origen.TryGetInt64(out long origenId)
                    && origenId > 0;
                bool dato =
                    raiz.TryGetProperty("desdeDato", out var desdeDato)
                    && desdeDato.ValueKind == System.Text.Json.JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(desdeDato.GetString());
                if (lugar == dato)
                    throw new ArgumentException(
                        "Indique un solo origen: desdeLugarId o desdeDato.",
                        nameof(json)
                    );
                Entero("hastaLugarId");
                Texto("texto");
                if (
                    !raiz.TryGetProperty("dias", out var dias)
                    || !dias.TryGetInt32(out int cantidad)
                    || cantidad
                        is < 0
                            or > Hormiguero.Nucleo.Utilidades.CalculoFechas.CantidadMaxima
                )
                    throw new ArgumentException("La cantidad de días no es válida.", nameof(json));
                if (
                    !raiz.TryGetProperty("tipoDias", out var tipoDias)
                    || tipoDias.GetString() is not ("habiles" or "corridos")
                )
                    throw new ArgumentException(
                        "tipoDias debe ser «habiles» o «corridos».",
                        nameof(json)
                    );
                if (
                    raiz.TryGetProperty("porLinea", out var porLinea)
                    && porLinea.ValueKind
                        is not (
                            System.Text.Json.JsonValueKind.True
                            or System.Text.Json.JsonValueKind.False
                        )
                )
                    throw new ArgumentException("porLinea debe ser booleano.", nameof(json));
                break;
            case "listo_para":
                Entero("cuandoLugarId");
                Entero("hastaLugarId");
                Texto("lista");
                break;
            default:
                throw new ArgumentException(
                    "El tipo de regla de alerta no es válido.",
                    nameof(tipo)
                );
        }
    }

    private IReadOnlyList<long> LeerIds()
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT id FROM esquemas_cadena ORDER BY proveedor;";
        using var lector = comando.ExecuteReader();
        var ids = new List<long>();
        while (lector.Read())
            ids.Add(lector.GetInt64(0));
        return ids;
    }

    private EsquemaCadena? ObtenerPorId(long id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT proveedor FROM esquemas_cadena WHERE id=$id;";
        comando.Parameters.AddWithValue("$id", id);
        string? proveedor = Convert.ToString(comando.ExecuteScalar());
        return proveedor is null ? null : ObtenerPorProveedor(proveedor);
    }

    private IReadOnlyList<LugarEsquema> LeerLugares(long id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,esquema_id,orden,identificacion_id,inicia_cadena,nombre FROM lugares_esquema WHERE esquema_id=$e ORDER BY orden,id;";
        comando.Parameters.AddWithValue("$e", id);
        using var lector = comando.ExecuteReader();
        var lista = new List<LugarEsquema>();
        while (lector.Read())
            lista.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    lector.GetInt32(2),
                    lector.GetInt64(3),
                    lector.GetInt64(4) != 0,
                    lector.GetString(5)
                )
            );
        return lista;
    }

    private IReadOnlyList<ParejaEsquema> LeerParejas(long id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT p.id,p.lugar_a,p.lugar_b,p.dato_diccionario_id FROM parejas_esquema p JOIN lugares_esquema l ON l.id=p.lugar_a WHERE l.esquema_id=$e ORDER BY p.id;";
        comando.Parameters.AddWithValue("$e", id);
        using var lector = comando.ExecuteReader();
        var lista = new List<ParejaEsquema>();
        while (lector.Read())
            lista.Add(
                new(lector.GetInt64(0), lector.GetInt64(1), lector.GetInt64(2), lector.GetString(3))
            );
        return lista;
    }

    private IReadOnlyList<ModoEsquema> LeerModos(long id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,esquema_id,nombre FROM modos_esquema WHERE esquema_id=$e ORDER BY id;";
        comando.Parameters.AddWithValue("$e", id);
        using var lector = comando.ExecuteReader();
        var lista = new List<ModoEsquema>();
        while (lector.Read())
            lista.Add(new(lector.GetInt64(0), lector.GetInt64(1), lector.GetString(2)));
        return lista;
    }

    private IReadOnlyList<ReglaAlertaEsquema> LeerReglas(long id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,esquema_id,lugar_id,modo_id,tipo,parametros_json FROM reglas_alerta_esquema WHERE esquema_id=$e ORDER BY id;";
        comando.Parameters.AddWithValue("$e", id);
        using var lector = comando.ExecuteReader();
        var lista = new List<ReglaAlertaEsquema>();
        while (lector.Read())
            lista.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    lector.GetInt64(2),
                    lector.IsDBNull(3) ? null : lector.GetInt64(3),
                    lector.GetString(4),
                    lector.GetString(5)
                )
            );
        return lista;
    }
}
