using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed class MotorCadenasDiccionario(SqliteConnection conexion)
{
    private sealed record Valor(string Dato, string Original, string Clave);

    private sealed record Coincidencia(
        long Cadena,
        long Ubicacion,
        long Lugar,
        long? Linea,
        string Original,
        string Nuevo,
        string Cardinalidad,
        bool ActualEsB
    );

    public ResultadoMotorCadena ProcesarVersion(long versionId)
    {
        using (var ya = conexion.CreateCommand())
        {
            ya.CommandText =
                "SELECT EXISTS(SELECT 1 FROM ubicaciones_cadena_diccionario WHERE version_id=$v);";
            ya.Parameters.AddWithValue("$v", versionId);
            if (Convert.ToInt64(ya.ExecuteScalar()) != 0)
                return new("ya_procesado");
        }
        ProveedorDocumento? proveedor = ProveedorDeDocumento.Determinar(conexion, versionId);
        if (proveedor is null)
            return new("sin_esquema", Motivo: "El documento no tiene proveedor determinado.");
        EsquemaDiccionario? esquema = new RepositorioEsquemasDiccionario(
            conexion
        ).ObtenerPorProveedor(proveedor.Clave);
        if (esquema is null || !esquema.Activo)
            return new("sin_esquema", Motivo: "El proveedor no tiene esquema activo.");
        string? tipo = LeerTipo(versionId);
        LugarDiccionario? lugar = tipo is null
            ? null
            : esquema.Lugares.SingleOrDefault(l => l.DatoTipoId == tipo);
        if (lugar is null)
            return new("tipo_fuera_esquema");
        var coincidencias = new List<Coincidencia>();
        foreach (Valor valor in LeerValores(versionId))
            coincidencias.AddRange(Buscar(esquema, lugar, valor));
        var exactas = coincidencias
            .Where(c =>
                DiccionarioDatosEnlazantes.ValorLiteralNormalizado(c.Original)
                == DiccionarioDatosEnlazantes.ValorLiteralNormalizado(c.Nuevo)
            )
            .ToArray();
        var elegidas = exactas.Length > 0 ? exactas : coincidencias.ToArray();
        var cadenas = elegidas.Select(c => c.Cadena).Distinct().ToArray();
        if (elegidas.Any(c => !exactas.Contains(c)) || cadenas.Length > 1)
        {
            long destino = cadenas.Min();
            GuardarUbicacion(
                versionId,
                lugar,
                destino,
                elegidas.FirstOrDefault(),
                "dudoso",
                "Coincidencia dudosa con un enlace declarado."
            );
            return new("dudoso", destino, "Coincidencia dudosa con un enlace declarado.");
        }
        if (cadenas.Length == 1)
        {
            Coincidencia coincidencia = elegidas[0];
            GuardarUbicacion(versionId, lugar, cadenas[0], coincidencia, "activo", null);
            return new("ubicado", cadenas[0]);
        }
        if (!lugar.IniciaCadena)
            return new(
                "sin_piso",
                Motivo: "No coincide con un enlace declarado y el lugar no inicia cadenas."
            );
        long cadena = CrearCadena(versionId, esquema, proveedor.Clave, lugar);
        return new("cadena_creada", cadena);
    }

    private List<Coincidencia> Buscar(
        EsquemaDiccionario esquema,
        LugarDiccionario actual,
        Valor valor
    )
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT c.id,u.id,u.lugar_id,u.linea_id,x.valor_original,e.cardinalidad,CASE WHEN e.lugar_b=$l THEN 1 ELSE 0 END FROM enlaces_esquema_diccionario e JOIN lugares_esquema_diccionario la ON la.id=e.lugar_a JOIN ubicaciones_cadena_diccionario u ON u.lugar_id=CASE WHEN e.lugar_a=$l THEN e.lugar_b ELSE e.lugar_a END AND u.estado='activo' JOIN cadenas_diccionario c ON c.id=u.cadena_id AND c.esquema_id=la.esquema_id AND c.estado='activa' JOIN valores_documento x ON x.version_id=u.version_id AND x.dato_diccionario_id=e.dato_diccionario_id AND x.estado='vigente' WHERE la.esquema_id=$e AND (e.lugar_a=$l OR e.lugar_b=$l) AND e.dato_diccionario_id=$d AND x.valor_clave=$k;";
        comando.Parameters.AddWithValue("$l", actual.Id);
        comando.Parameters.AddWithValue("$e", esquema.Id);
        comando.Parameters.AddWithValue("$d", valor.Dato);
        comando.Parameters.AddWithValue("$k", valor.Clave);
        using var lector = comando.ExecuteReader();
        var resultados = new List<Coincidencia>();
        while (lector.Read())
            resultados.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    lector.GetInt64(2),
                    lector.IsDBNull(3) ? null : lector.GetInt64(3),
                    lector.GetString(4),
                    valor.Original,
                    lector.GetString(5),
                    lector.GetInt64(6) != 0
                )
            );
        return resultados;
    }

    private long CrearCadena(
        long versionId,
        EsquemaDiccionario esquema,
        string proveedor,
        LugarDiccionario lugar
    )
    {
        using var tx = conexion.BeginTransaction();
        using var comando = conexion.CreateCommand();
        comando.Transaction = tx;
        comando.CommandText =
            "INSERT INTO cadenas_diccionario(esquema_id,nombre,cliente) VALUES($e,(SELECT d.nombre FROM versiones_documento v JOIN documentos d ON d.id=v.documento_id WHERE v.id=$v),(SELECT valor_original FROM valores_documento WHERE version_id=$v AND estado='vigente' AND dato_diccionario_id='nombre_cliente' ORDER BY id LIMIT 1)) RETURNING id;";
        comando.Parameters.AddWithValue("$e", esquema.Id);
        comando.Parameters.AddWithValue("$v", versionId);
        long id = Convert.ToInt64(comando.ExecuteScalar());
        InsertarUbicacion(tx, versionId, lugar, id, null, null, "activo", null);
        tx.Commit();
        return id;
    }

    private void GuardarUbicacion(
        long versionId,
        LugarDiccionario lugar,
        long cadena,
        Coincidencia? coincidencia,
        string estado,
        string? motivo
    )
    {
        using var tx = conexion.BeginTransaction();
        long? padre = coincidencia is null ? null : coincidencia.Ubicacion;
        long? linea = coincidencia?.Linea;
        if (coincidencia is not null && linea is null)
        {
            using var buscarLinea = conexion.CreateCommand();
            buscarLinea.Transaction = tx;
            buscarLinea.CommandText =
                "SELECT COALESCE(MAX(linea_id),0)+1 FROM ubicaciones_cadena_diccionario WHERE cadena_id=$c;";
            buscarLinea.Parameters.AddWithValue("$c", cadena);
            linea = Convert.ToInt64(buscarLinea.ExecuteScalar());
        }
        InsertarUbicacion(tx, versionId, lugar, cadena, padre, linea, estado, motivo);
        tx.Commit();
    }

    private void InsertarUbicacion(
        SqliteTransaction tx,
        long versionId,
        LugarDiccionario lugar,
        long cadena,
        long? padre,
        long? linea,
        string estado,
        string? motivo
    )
    {
        using var comando = conexion.CreateCommand();
        comando.Transaction = tx;
        comando.CommandText =
            "INSERT INTO ubicaciones_cadena_diccionario(cadena_id,version_id,lugar_id,padre_id,linea_id,estado,motivo,orden) VALUES($c,$v,$l,$p,$linea,$e,$m,(SELECT COALESCE(MAX(orden),-1)+1 FROM ubicaciones_cadena_diccionario WHERE cadena_id=$c));";
        comando.Parameters.AddWithValue("$c", cadena);
        comando.Parameters.AddWithValue("$v", versionId);
        comando.Parameters.AddWithValue("$l", lugar.Id);
        comando.Parameters.AddWithValue("$p", (object?)padre ?? DBNull.Value);
        comando.Parameters.AddWithValue("$linea", (object?)linea ?? DBNull.Value);
        comando.Parameters.AddWithValue("$e", estado);
        comando.Parameters.AddWithValue("$m", (object?)motivo ?? DBNull.Value);
        comando.ExecuteNonQuery();
    }

    private string? LeerTipo(long versionId)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT t.dato_diccionario_id FROM valores_documento x JOIN campos_documento f ON f.id=x.campo_id JOIN tipos_documento_datos t ON t.identificacion_id=f.identificacion_id AND t.define_tipo=1 AND t.activo=1 AND t.dato_diccionario_id=x.dato_diccionario_id WHERE x.version_id=$v AND x.estado='vigente' ORDER BY x.id LIMIT 1;";
        comando.Parameters.AddWithValue("$v", versionId);
        return comando.ExecuteScalar() as string;
    }

    private List<Valor> LeerValores(long versionId)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT DISTINCT dato_diccionario_id,valor_original,valor_clave FROM valores_documento WHERE version_id=$v AND estado='vigente' AND dato_diccionario_id IS NOT NULL AND valor_clave<>'';";
        comando.Parameters.AddWithValue("$v", versionId);
        using var lector = comando.ExecuteReader();
        var resultados = new List<Valor>();
        while (lector.Read())
            resultados.Add(new(lector.GetString(0), lector.GetString(1), lector.GetString(2)));
        return resultados;
    }
}
