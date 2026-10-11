using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed record DefinicionReglaAlertaDiccionario(
    int OrdenLugar,
    long? ModoId,
    string Tipo,
    string ParametrosJson,
    bool IdsSonOrdenes = true
);

public sealed class RepositorioConfiguracionEsquemaDiccionario(SqliteConnection conexion)
{
    public void GuardarModos(long esquemaId, int? ordenDecideModo, IReadOnlyList<string> modos)
    {
        using var tx = conexion.BeginTransaction();
        foreach (
            string nombre in modos
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
        )
        {
            using var q = conexion.CreateCommand();
            q.Transaction = tx;
            q.CommandText =
                "INSERT INTO modos_esquema_diccionario(esquema_id,nombre) SELECT $e,$n WHERE NOT EXISTS(SELECT 1 FROM modos_esquema_diccionario WHERE esquema_id=$e AND nombre=$n COLLATE NOCASE);";
            q.Parameters.AddWithValue("$e", esquemaId);
            q.Parameters.AddWithValue("$n", nombre);
            q.ExecuteNonQuery();
        }
        using (var q = conexion.CreateCommand())
        {
            q.Transaction = tx;
            q.CommandText =
                "UPDATE esquemas_diccionario SET lugar_decide_modo_id=(SELECT id FROM lugares_esquema_diccionario WHERE esquema_id=$e AND orden=$o AND activo=1) WHERE id=$e;";
            q.Parameters.AddWithValue("$e", esquemaId);
            q.Parameters.AddWithValue("$o", (object?)ordenDecideModo ?? DBNull.Value);
            q.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void GuardarReglas(
        long esquemaId,
        IReadOnlyList<DefinicionReglaAlertaDiccionario> reglas
    )
    {
        using var tx = conexion.BeginTransaction();
        using (var borrar = conexion.CreateCommand())
        {
            borrar.Transaction = tx;
            borrar.CommandText =
                "DELETE FROM reglas_alerta_esquema_diccionario WHERE esquema_id=$e;";
            borrar.Parameters.AddWithValue("$e", esquemaId);
            borrar.ExecuteNonQuery();
        }
        foreach (var regla in reglas)
        {
            string json = regla.IdsSonOrdenes
                ? ConvertirOrdenesAIds(tx, esquemaId, regla.ParametrosJson)
                : regla.ParametrosJson;
            using var q = conexion.CreateCommand();
            q.Transaction = tx;
            q.CommandText =
                "INSERT INTO reglas_alerta_esquema_diccionario(esquema_id,lugar_id,modo_id,tipo,parametros_json) SELECT $e,id,$m,$t,$j FROM lugares_esquema_diccionario WHERE esquema_id=$e AND orden=$o AND activo=1;";
            q.Parameters.AddWithValue("$e", esquemaId);
            q.Parameters.AddWithValue("$o", regla.OrdenLugar);
            q.Parameters.AddWithValue("$m", (object?)regla.ModoId ?? DBNull.Value);
            q.Parameters.AddWithValue("$t", regla.Tipo);
            q.Parameters.AddWithValue("$j", json);
            q.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void Guardar(
        long esquemaId,
        int? ordenDecideModo,
        IReadOnlyList<string> modos,
        IReadOnlyList<DefinicionReglaAlertaDiccionario> reglas
    )
    {
        using var tx = conexion.BeginTransaction();
        using (var q = conexion.CreateCommand())
        {
            q.Transaction = tx;
            q.CommandText =
                "UPDATE esquemas_diccionario SET lugar_decide_modo_id=(SELECT id FROM lugares_esquema_diccionario WHERE esquema_id=$e AND orden=$o AND activo=1) WHERE id=$e;";
            q.Parameters.AddWithValue("$e", esquemaId);
            q.Parameters.AddWithValue("$o", (object?)ordenDecideModo ?? DBNull.Value);
            q.ExecuteNonQuery();
        }
        using (var q = conexion.CreateCommand())
        {
            q.Transaction = tx;
            q.CommandText = "DELETE FROM reglas_alerta_esquema_diccionario WHERE esquema_id=$e;";
            q.Parameters.AddWithValue("$e", esquemaId);
            q.ExecuteNonQuery();
        }
        foreach (
            string nombre in modos
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
        )
        {
            using var q = conexion.CreateCommand();
            q.Transaction = tx;
            q.CommandText =
                "INSERT INTO modos_esquema_diccionario(esquema_id,nombre) SELECT $e,$n WHERE NOT EXISTS(SELECT 1 FROM modos_esquema_diccionario WHERE esquema_id=$e AND nombre=$n COLLATE NOCASE);";
            q.Parameters.AddWithValue("$e", esquemaId);
            q.Parameters.AddWithValue("$n", nombre);
            q.ExecuteNonQuery();
        }
        foreach (var regla in reglas)
        {
            string json = regla.IdsSonOrdenes
                ? ConvertirOrdenesAIds(tx, esquemaId, regla.ParametrosJson)
                : regla.ParametrosJson;
            using var q = conexion.CreateCommand();
            q.Transaction = tx;
            q.CommandText =
                "INSERT INTO reglas_alerta_esquema_diccionario(esquema_id,lugar_id,modo_id,tipo,parametros_json) SELECT $e,id,$m,$t,$j FROM lugares_esquema_diccionario WHERE esquema_id=$e AND orden=$o AND activo=1;";
            q.Parameters.AddWithValue("$e", esquemaId);
            q.Parameters.AddWithValue("$o", regla.OrdenLugar);
            q.Parameters.AddWithValue("$m", (object?)regla.ModoId ?? DBNull.Value);
            q.Parameters.AddWithValue("$t", regla.Tipo);
            q.Parameters.AddWithValue("$j", json);
            q.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private string ConvertirOrdenesAIds(SqliteTransaction tx, long esquemaId, string json)
    {
        using var documento = System.Text.Json.JsonDocument.Parse(json);
        var raiz = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        foreach (
            string propiedad in new[]
            {
                "desdeLugarId",
                "hastaLugarId",
                "cuandoLugarId",
                "condicionLugarId",
            }
        )
        {
            if (
                !documento.RootElement.TryGetProperty(propiedad, out var valor)
                || !valor.TryGetInt64(out long orden)
            )
                continue;
            using var q = conexion.CreateCommand();
            q.Transaction = tx;
            q.CommandText =
                "SELECT id FROM lugares_esquema_diccionario WHERE esquema_id=$e AND orden=$o AND activo=1;";
            q.Parameters.AddWithValue("$e", esquemaId);
            q.Parameters.AddWithValue("$o", orden);
            if (q.ExecuteScalar() is long id)
                raiz[propiedad] = id;
        }
        return raiz.ToJsonString();
    }
}

public sealed class RepositorioDatosCadenaDiccionario(SqliteConnection conexion)
{
    public void Guardar(long cadenaId, string nombre, string valor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(valor);
        using var q = conexion.CreateCommand();
        q.CommandText =
            "INSERT INTO datos_cadena_diccionario(cadena_id,nombre,valor,actualizado_en) SELECT $c,$n,$v,$f WHERE EXISTS(SELECT 1 FROM reglas_alerta_esquema_diccionario r JOIN cadenas_diccionario c ON c.esquema_id=r.esquema_id WHERE c.id=$c AND r.tipo IN ('falta_dato','plazo')) ON CONFLICT(cadena_id,nombre) DO UPDATE SET valor=excluded.valor,actualizado_en=excluded.actualizado_en;";
        q.Parameters.AddWithValue("$c", cadenaId);
        q.Parameters.AddWithValue("$n", nombre.Trim());
        q.Parameters.AddWithValue("$v", valor.Trim());
        q.Parameters.AddWithValue("$f", DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
        if (q.ExecuteNonQuery() == 0)
            throw new ArgumentException(
                "El dato no está configurado para esta cadena.",
                nameof(nombre)
            );
        new EvaluadorAlertasEsquemaDiccionario(conexion).Evaluar();
    }

    public IReadOnlyList<DatoCadena> Listar(long cadenaId)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT nombre,valor,actualizado_en FROM datos_cadena_diccionario WHERE cadena_id=$c ORDER BY nombre COLLATE NOCASE;";
        q.Parameters.AddWithValue("$c", cadenaId);
        using var r = q.ExecuteReader();
        var filas = new List<DatoCadena>();
        while (r.Read())
            filas.Add(
                new(
                    r.GetString(0),
                    r.GetString(1),
                    DateTime.Parse(r.GetString(2), CultureInfo.InvariantCulture)
                )
            );
        return filas;
    }
}
