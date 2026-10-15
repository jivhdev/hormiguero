using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed record LugarDiccionario(
    long Id,
    long EsquemaId,
    int Orden,
    string DatoTipoId,
    bool IniciaCadena,
    string Nombre
);

public sealed record EnlaceDeclarado(
    long Id,
    long LugarA,
    long LugarB,
    string DatoDiccionarioId,
    string Cardinalidad
);

public sealed record DefinicionLugarDiccionario(
    int Orden,
    string DatoTipoId,
    bool IniciaCadena,
    string Nombre
);

public sealed record DefinicionEnlace(
    int OrdenA,
    int OrdenB,
    string DatoDiccionarioId,
    string Cardinalidad
);

public sealed record ReconocimientoProveedor(string Tipo, string ValorNormalizado);

public sealed record EsquemaDiccionario(
    long Id,
    string Proveedor,
    string Nombre,
    bool Activo,
    IReadOnlyList<LugarDiccionario> Lugares,
    IReadOnlyList<EnlaceDeclarado> Enlaces
);

public sealed class RepositorioEsquemasDiccionario(SqliteConnection conexion)
{
    public IReadOnlyList<ReconocimientoProveedor> ListarReconocimientos(long esquemaId)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT tipo,valor_normalizado FROM reconocimientos_esquema_diccionario WHERE esquema_id=$e ORDER BY tipo,valor_normalizado;";
        q.Parameters.AddWithValue("$e", esquemaId);
        using var r = q.ExecuteReader();
        var filas = new List<ReconocimientoProveedor>();
        while (r.Read())
            filas.Add(new(r.GetString(0), r.GetString(1)));
        return filas;
    }

    public void GuardarReconocimientos(
        long esquemaId,
        IReadOnlyList<ReconocimientoProveedor> reconocimientos
    )
    {
        using var tx = conexion.BeginTransaction();
        using (var borrar = conexion.CreateCommand())
        {
            borrar.Transaction = tx;
            borrar.CommandText =
                "DELETE FROM reconocimientos_esquema_diccionario WHERE esquema_id=$e;";
            borrar.Parameters.AddWithValue("$e", esquemaId);
            borrar.ExecuteNonQuery();
        }
        foreach (var reconocimiento in reconocimientos)
        {
            if (reconocimiento.Tipo is not ("rut" or "nombre"))
                throw new ArgumentException("Tipo de reconocimiento inválido.");
            string valor =
                reconocimiento.Tipo == "rut"
                    ? ProveedorDeDocumento.NormalizarRut(reconocimiento.ValorNormalizado)
                    : ProveedorDeDocumento.NormalizarNombre(reconocimiento.ValorNormalizado);
            if (valor.Length == 0)
                continue;
            using var q = conexion.CreateCommand();
            q.Transaction = tx;
            q.CommandText =
                "INSERT INTO reconocimientos_esquema_diccionario(esquema_id,tipo,valor_normalizado) VALUES($e,$t,$v);";
            q.Parameters.AddWithValue("$e", esquemaId);
            q.Parameters.AddWithValue("$t", reconocimiento.Tipo);
            q.Parameters.AddWithValue("$v", valor);
            q.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public static EsquemaDiccionario? ObtenerPorReconocimiento(
        SqliteConnection conexion,
        string tipo,
        string valor
    )
    {
        string normalizado =
            tipo == "rut"
                ? ProveedorDeDocumento.NormalizarRut(valor)
                : ProveedorDeDocumento.NormalizarNombre(valor);
        if (normalizado.Length == 0)
            return null;
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT e.id FROM reconocimientos_esquema_diccionario r JOIN esquemas_diccionario e ON e.id=r.esquema_id WHERE r.tipo=$t AND r.valor_normalizado=$v AND e.activo=1;";
        q.Parameters.AddWithValue("$t", tipo);
        q.Parameters.AddWithValue("$v", normalizado);
        object? id = q.ExecuteScalar();
        if (id is null)
            return null;
        var repositorio = new RepositorioEsquemasDiccionario(conexion);
        return repositorio.LeerPorId(Convert.ToInt64(id));
    }

    public IReadOnlyList<EsquemaDiccionario> Listar()
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT id FROM esquemas_diccionario ORDER BY proveedor;";
        using var lector = comando.ExecuteReader();
        var ids = new List<long>();
        while (lector.Read())
            ids.Add(lector.GetInt64(0));
        lector.Close();
        return ids.Select(LeerPorId).Where(e => e is not null).Select(e => e!).ToArray();
    }

    public long Guardar(
        string proveedor,
        string nombre,
        IReadOnlyList<DefinicionLugarDiccionario> lugares,
        IReadOnlyList<DefinicionEnlace> enlaces,
        bool activo = true
    )
    {
        string clave = ProveedorDeDocumento.NormalizarProveedor(proveedor);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        if (
            lugares.Count == 0
            || lugares.Any(l =>
                l.Orden < 0
                || string.IsNullOrWhiteSpace(l.DatoTipoId)
                || string.IsNullOrWhiteSpace(l.Nombre)
            )
        )
            throw new ArgumentException("El esquema debe tener lugares válidos.", nameof(lugares));
        if (
            lugares.Select(l => l.DatoTipoId).Distinct(StringComparer.Ordinal).Count()
            != lugares.Count
        )
            throw new ArgumentException(
                "Cada tipo del diccionario debe aparecer una sola vez.",
                nameof(lugares)
            );
        using var tx = conexion.BeginTransaction();
        using var guardar = conexion.CreateCommand();
        guardar.Transaction = tx;
        guardar.CommandText =
            "INSERT INTO esquemas_diccionario(proveedor,nombre,activo,creada_en,actualizada_en) VALUES($p,$n,$a,$f,$f) ON CONFLICT(proveedor) DO UPDATE SET nombre=excluded.nombre,activo=excluded.activo,actualizada_en=excluded.actualizada_en RETURNING id;";
        guardar.Parameters.AddWithValue("$p", clave);
        guardar.Parameters.AddWithValue("$n", nombre.Trim());
        guardar.Parameters.AddWithValue("$a", activo ? 1 : 0);
        guardar.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        long esquemaId = Convert.ToInt64(guardar.ExecuteScalar());
        using (var limpiar = conexion.CreateCommand())
        {
            limpiar.Transaction = tx;
            limpiar.CommandText =
                "DELETE FROM enlaces_esquema_diccionario WHERE lugar_a IN (SELECT id FROM lugares_esquema_diccionario WHERE esquema_id=$e) OR lugar_b IN (SELECT id FROM lugares_esquema_diccionario WHERE esquema_id=$e); UPDATE lugares_esquema_diccionario SET activo=0,orden=-id WHERE esquema_id=$e;";
            limpiar.Parameters.AddWithValue("$e", esquemaId);
            limpiar.ExecuteNonQuery();
        }
        var existentes = new Dictionary<string, long>(StringComparer.Ordinal);
        using (var consulta = conexion.CreateCommand())
        {
            consulta.Transaction = tx;
            consulta.CommandText =
                "SELECT dato_tipo_id,id FROM lugares_esquema_diccionario WHERE esquema_id=$e;";
            consulta.Parameters.AddWithValue("$e", esquemaId);
            using var lector = consulta.ExecuteReader();
            while (lector.Read())
                existentes.Add(lector.GetString(0), lector.GetInt64(1));
        }
        var ids = new Dictionary<int, long>();
        foreach (var lugar in lugares.OrderBy(l => l.Orden))
        {
            using var comando = conexion.CreateCommand();
            comando.Transaction = tx;
            if (existentes.TryGetValue(lugar.DatoTipoId, out long id))
            {
                comando.CommandText =
                    "UPDATE lugares_esquema_diccionario SET orden=$o,inicia_cadena=$i,nombre=$n,activo=1 WHERE id=$id;";
                comando.Parameters.AddWithValue("$id", id);
                comando.Parameters.AddWithValue("$o", lugar.Orden);
                comando.Parameters.AddWithValue("$i", lugar.IniciaCadena ? 1 : 0);
                comando.Parameters.AddWithValue("$n", lugar.Nombre.Trim());
                comando.ExecuteNonQuery();
                ids.Add(lugar.Orden, id);
            }
            else
            {
                comando.CommandText =
                    "INSERT INTO lugares_esquema_diccionario(esquema_id,orden,dato_tipo_id,inicia_cadena,nombre,activo) VALUES($e,$o,$d,$i,$n,1) RETURNING id;";
                comando.Parameters.AddWithValue("$e", esquemaId);
                comando.Parameters.AddWithValue("$o", lugar.Orden);
                comando.Parameters.AddWithValue("$d", lugar.DatoTipoId);
                comando.Parameters.AddWithValue("$i", lugar.IniciaCadena ? 1 : 0);
                comando.Parameters.AddWithValue("$n", lugar.Nombre.Trim());
                ids.Add(lugar.Orden, Convert.ToInt64(comando.ExecuteScalar()));
            }
        }
        foreach (var enlace in enlaces)
        {
            if (
                !ids.TryGetValue(enlace.OrdenA, out long a)
                || !ids.TryGetValue(enlace.OrdenB, out long b)
                || a == b
                || enlace.Cardinalidad is not ("uno_a_uno" or "uno_a_varios")
            )
                throw new ArgumentException(
                    "Cada enlace debe conectar dos lugares distintos y tener cardinalidad válida.",
                    nameof(enlaces)
                );
            using var comando = conexion.CreateCommand();
            comando.Transaction = tx;
            comando.CommandText =
                "INSERT INTO enlaces_esquema_diccionario(lugar_a,lugar_b,dato_diccionario_id,cardinalidad) VALUES($a,$b,$d,$c);";
            comando.Parameters.AddWithValue("$a", a);
            comando.Parameters.AddWithValue("$b", b);
            comando.Parameters.AddWithValue("$d", enlace.DatoDiccionarioId);
            comando.Parameters.AddWithValue("$c", enlace.Cardinalidad);
            comando.ExecuteNonQuery();
        }
        tx.Commit();
        return esquemaId;
    }

    public EsquemaDiccionario? ObtenerPorProveedor(string proveedor)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,proveedor,nombre,activo FROM esquemas_diccionario WHERE proveedor=$p;";
        comando.Parameters.AddWithValue("$p", ProveedorDeDocumento.NormalizarProveedor(proveedor));
        using var lector = comando.ExecuteReader();
        if (!lector.Read())
            return null;
        long id = lector.GetInt64(0);
        var resultado = new EsquemaDiccionario(
            id,
            lector.GetString(1),
            lector.GetString(2),
            lector.GetInt64(3) != 0,
            LeerLugares(id),
            LeerEnlaces(id)
        );
        return resultado;
    }

    public bool Desactivar(long id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "UPDATE esquemas_diccionario SET activo=0,actualizada_en=$f WHERE id=$id;";
        comando.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        comando.Parameters.AddWithValue("$id", id);
        return comando.ExecuteNonQuery() > 0;
    }

    public static IReadOnlyList<(string Id, string Nombre, string Grupo)> ListarTiposDocumento(
        SqliteConnection conexion
    )
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,nombre,grupo FROM diccionario_datos WHERE grupo IN ('Ventas propias','Del cliente','Compras propias','Del proveedor') OR id='comprobante_pago' ORDER BY grupo,orden;";
        using var lector = comando.ExecuteReader();
        var filas = new List<(string, string, string)>();
        while (lector.Read())
            filas.Add((lector.GetString(0), lector.GetString(1), lector.GetString(2)));
        return filas;
    }

    public static IReadOnlyList<(
        string Id,
        string Nombre,
        string Grupo,
        bool Sugerido
    )> ListarDatos(SqliteConnection conexion, string tipoA, string tipoB)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT d.id,d.nombre,d.grupo,EXISTS(SELECT 1 FROM tipos_documento_datos ta JOIN tipos_documento_datos tb ON tb.dato_diccionario_id=$b AND tb.define_tipo=1 AND tb.activo=1 JOIN tipos_documento_datos da ON da.identificacion_id=ta.identificacion_id AND da.dato_diccionario_id=d.id AND da.activo=1 AND da.enlazable=1 JOIN tipos_documento_datos db ON db.identificacion_id=tb.identificacion_id AND db.dato_diccionario_id=d.id AND db.activo=1 AND db.enlazable=1 WHERE ta.dato_diccionario_id=$a AND ta.define_tipo=1 AND ta.activo=1 AND da.enlazable=1 AND db.enlazable=1) AS sugerido FROM diccionario_datos d ORDER BY sugerido DESC,d.grupo,d.orden;";
        comando.Parameters.AddWithValue("$a", tipoA);
        comando.Parameters.AddWithValue("$b", tipoB);
        using var lector = comando.ExecuteReader();
        var filas = new List<(string, string, string, bool)>();
        while (lector.Read())
            filas.Add(
                (
                    lector.GetString(0),
                    lector.GetString(1),
                    lector.GetString(2),
                    lector.GetInt64(3) != 0
                )
            );
        return filas;
    }

    private IReadOnlyList<LugarDiccionario> LeerLugares(long id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,esquema_id,orden,dato_tipo_id,inicia_cadena,nombre FROM lugares_esquema_diccionario WHERE esquema_id=$e AND activo=1 ORDER BY orden;";
        comando.Parameters.AddWithValue("$e", id);
        using var lector = comando.ExecuteReader();
        var filas = new List<LugarDiccionario>();
        while (lector.Read())
            filas.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    lector.GetInt32(2),
                    lector.GetString(3),
                    lector.GetInt64(4) != 0,
                    lector.GetString(5)
                )
            );
        return filas;
    }

    public static string NombreDiccionario(SqliteConnection conexion, string id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT nombre FROM diccionario_datos WHERE id=$id;";
        comando.Parameters.AddWithValue("$id", id);
        return Convert.ToString(comando.ExecuteScalar()) ?? id;
    }

    private EsquemaDiccionario? LeerPorId(long id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,proveedor,nombre,activo FROM esquemas_diccionario WHERE id=$e;";
        comando.Parameters.AddWithValue("$e", id);
        using var lector = comando.ExecuteReader();
        if (!lector.Read())
            return null;
        return new(
            lector.GetInt64(0),
            lector.GetString(1),
            lector.GetString(2),
            lector.GetInt64(3) != 0,
            LeerLugares(id),
            LeerEnlaces(id)
        );
    }

    private IReadOnlyList<EnlaceDeclarado> LeerEnlaces(long id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT e.id,e.lugar_a,e.lugar_b,e.dato_diccionario_id,e.cardinalidad FROM enlaces_esquema_diccionario e JOIN lugares_esquema_diccionario l ON l.id=e.lugar_a WHERE l.esquema_id=$e ORDER BY e.id;";
        comando.Parameters.AddWithValue("$e", id);
        using var lector = comando.ExecuteReader();
        var filas = new List<EnlaceDeclarado>();
        while (lector.Read())
            filas.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    lector.GetInt64(2),
                    lector.GetString(3),
                    lector.GetString(4)
                )
            );
        return filas;
    }
}
