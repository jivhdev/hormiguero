using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed class MotorCadenasDiccionario(SqliteConnection conexion)
{
    private readonly HashSet<long> procesando = [];

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
        if (!procesando.Add(versionId))
            return new("ya_procesado");
        try
        {
            return ProcesarVersionInterno(versionId);
        }
        finally
        {
            procesando.Remove(versionId);
        }
    }

    private ResultadoMotorCadena ProcesarVersionInterno(long versionId)
    {
        using (var ya = conexion.CreateCommand())
        {
            ya.CommandText =
                "SELECT EXISTS(SELECT 1 FROM ubicaciones_cadena_diccionario WHERE version_id=$v) OR EXISTS(SELECT 1 FROM decisiones_esquema_diccionario WHERE version_id=$v AND tipo='rechazado' AND estado='resuelta');";
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
        if (!lugar.IniciaCadena && cadenas.Length > 1 && exactas.Length == elegidas.Length)
        {
            foreach (long cadenaId in cadenas)
            {
                Coincidencia coincidencia = elegidas.First(c => c.Cadena == cadenaId);
                GuardarUbicacion(versionId, lugar, cadenaId, coincidencia, "activo", null);
                CompletarCadena(cadenaId, esquema);
            }
            ResolverSinInicioMencionados(versionId);
            return new("ubicado", cadenas[0]);
        }
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
            RegistrarDecisionModo(versionId, esquema, lugar, cadenas[0]);
            CompletarCadena(cadenas[0], esquema);
            ResolverSinInicioMencionados(versionId);
            return new("ubicado", cadenas[0]);
        }
        if (!lugar.IniciaCadena)
        {
            if (lugar.Orden < esquema.Lugares.Single(l => l.IniciaCadena).Orden)
                return new("esperando_inicio");
            RegistrarSinInicio(versionId, proveedor.Clave, esquema, lugar);
            return new(
                "sin_inicio",
                Motivo: MotivoSinInicio(esquema, lugar, LeerValores(versionId))
            );
        }
        long cadena = CrearCadena(versionId, esquema, proveedor.Clave, lugar);
        RegistrarDecisionModo(versionId, esquema, lugar, cadena);
        CompletarCadena(cadena, esquema);
        ResolverSinInicioMencionados(versionId);
        return new("cadena_creada", cadena);
    }

    private void CompletarCadena(long cadenaId, EsquemaDiccionario esquema)
    {
        bool huboCambios;
        do
        {
            huboCambios = false;
            using var comando = conexion.CreateCommand();
            comando.CommandText =
                "SELECT u.version_id FROM ubicaciones_cadena_diccionario u WHERE u.cadena_id=$c AND u.estado='activo' ORDER BY u.orden;";
            comando.Parameters.AddWithValue("$c", cadenaId);
            var versiones = new List<long>();
            using (var lector = comando.ExecuteReader())
                while (lector.Read())
                    versiones.Add(lector.GetInt64(0));
            foreach (long version in versiones)
            {
                foreach (long candidato in BuscarVersionesEnlazadas(version, esquema, cadenaId))
                {
                    if (procesando.Contains(candidato))
                        continue;
                    int antes = ContarUbicaciones(cadenaId);
                    ProcesarVersion(candidato);
                    huboCambios |= ContarUbicaciones(cadenaId) > antes;
                }
            }
        } while (huboCambios);
    }

    private IEnumerable<long> BuscarVersionesEnlazadas(
        long versionId,
        EsquemaDiccionario esquema,
        long cadenaId
    )
    {
        string? tipo = LeerTipo(versionId);
        var lugar = tipo is null
            ? null
            : esquema.Lugares.SingleOrDefault(l => l.DatoTipoId == tipo);
        if (lugar is null)
            yield break;
        var vistos = new HashSet<long>();
        foreach (Valor valor in LeerValores(versionId))
        {
            using var consulta = conexion.CreateCommand();
            consulta.CommandText =
                "SELECT DISTINCT x.version_id FROM enlaces_esquema_diccionario e JOIN lugares_esquema_diccionario otro ON otro.id=CASE WHEN e.lugar_a=$l THEN e.lugar_b ELSE e.lugar_a END JOIN diccionario_datos de ON de.id=e.dato_diccionario_id JOIN valores_documento x ON x.valor_clave=$k AND x.estado='vigente' JOIN diccionario_datos dx ON dx.id=x.dato_diccionario_id AND COALESCE(dx.enlaza_como,dx.id)=COALESCE(de.enlaza_como,de.id) JOIN versiones_documento v ON v.id=x.version_id AND v.estado='vigente' WHERE (e.lugar_a=$l OR e.lugar_b=$l) AND otro.esquema_id=$e AND x.version_id<>$v AND EXISTS(SELECT 1 FROM valores_documento xv JOIN diccionario_datos dv ON dv.id=xv.dato_diccionario_id WHERE xv.version_id=$v AND xv.estado='vigente' AND xv.valor_clave=$k AND COALESCE(dv.enlaza_como,dv.id)=COALESCE(de.enlaza_como,de.id));";
            consulta.Parameters.AddWithValue("$l", lugar.Id);
            consulta.Parameters.AddWithValue("$e", esquema.Id);
            consulta.Parameters.AddWithValue("$v", versionId);
            consulta.Parameters.AddWithValue("$k", valor.Clave);
            var encontrados = new List<long>();
            using (var lector = consulta.ExecuteReader())
                while (lector.Read())
                    encontrados.Add(lector.GetInt64(0));
            foreach (long candidato in encontrados)
                if (vistos.Add(candidato) && !EstaEnCadena(candidato, cadenaId))
                    yield return candidato;
        }
    }

    private void RegistrarSinInicio(
        long versionId,
        string proveedor,
        EsquemaDiccionario esquema,
        LugarDiccionario lugar
    )
    {
        string motivo = MotivoSinInicio(esquema, lugar, LeerValores(versionId));
        using var q = conexion.CreateCommand();
        q.CommandText =
            "INSERT INTO decisiones_esquema_diccionario(version_id,proveedor,motivo,creada_en,estado,tipo,opciones_json) VALUES($v,$p,$m,$f,'pendiente','sin_inicio','[\"Buscar otra vez\",\"Crear cadena igual\",\"Solo archivar\"]') ON CONFLICT(version_id) DO UPDATE SET proveedor=excluded.proveedor,motivo=excluded.motivo,tipo='sin_inicio',estado='pendiente',opciones_json=excluded.opciones_json WHERE decisiones_esquema_diccionario.estado='pendiente';";
        q.Parameters.AddWithValue("$v", versionId);
        q.Parameters.AddWithValue("$p", proveedor);
        q.Parameters.AddWithValue("$m", motivo);
        q.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        q.ExecuteNonQuery();
    }

    private static string MotivoSinInicio(
        EsquemaDiccionario esquema,
        LugarDiccionario lugar,
        IReadOnlyList<Valor> valores
    )
    {
        var inicio = esquema.Lugares.Single(l => l.IniciaCadena);
        EnlaceDeclarado? enlace = esquema.Enlaces.FirstOrDefault(e =>
            e.LugarA == lugar.Id || e.LugarB == lugar.Id
        );
        string dato = enlace?.DatoDiccionarioId ?? inicio.DatoTipoId;
        string numero = valores.FirstOrDefault(v => v.Dato == dato)?.Original ?? "desconocido";
        string tipoFaltante = inicio.Nombre;
        if (enlace is not null)
        {
            long otroId = enlace.LugarA == lugar.Id ? enlace.LugarB : enlace.LugarA;
            tipoFaltante = esquema.Lugares.Single(l => l.Id == otroId).Nombre;
        }
        return $"Falta la {tipoFaltante} {numero}: no está en la carpeta.";
    }

    private void ResolverSinInicioMencionados(long versionId)
    {
        string? tipoLlegado = LeerTipo(versionId);
        if (tipoLlegado is null)
            return;
        var esquemaLlegado = new RepositorioEsquemasDiccionario(conexion)
            .Listar()
            .FirstOrDefault(e => e.Lugares.Any(l => l.DatoTipoId == tipoLlegado));
        if (esquemaLlegado is null)
            return;
        LugarDiccionario? lugarLlegado = esquemaLlegado.Lugares.SingleOrDefault(l =>
            l.DatoTipoId == tipoLlegado
        );
        if (lugarLlegado is null)
            return;
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT version_id,motivo FROM decisiones_esquema_diccionario WHERE tipo='sin_inicio' AND estado='pendiente';";
        var pendientes = new List<(long Version, string Motivo)>();
        using (var r = q.ExecuteReader())
            while (r.Read())
                pendientes.Add((r.GetInt64(0), r.GetString(1)));
        foreach (var (pendiente, motivo) in pendientes)
        {
            string? tipoFaltante = motivo
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Skip(2)
                .FirstOrDefault();
            if (
                tipoFaltante is null
                || !string.Equals(
                    tipoFaltante,
                    lugarLlegado.Nombre,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                continue;
            if (!EstaEnAlgunaCadena(pendiente))
                continue;
            using var marcar = conexion.CreateCommand();
            marcar.CommandText =
                "UPDATE decisiones_esquema_diccionario SET estado='resuelta' WHERE version_id=$v AND tipo='sin_inicio' AND estado='pendiente';";
            marcar.Parameters.AddWithValue("$v", pendiente);
            marcar.ExecuteNonQuery();
        }
    }

    private bool EstaEnAlgunaCadena(long version)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT EXISTS(SELECT 1 FROM ubicaciones_cadena_diccionario WHERE version_id=$v AND estado='activo');";
        q.Parameters.AddWithValue("$v", version);
        return Convert.ToInt64(q.ExecuteScalar()) != 0;
    }

    private bool EstaEnCadena(long version, long cadena)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT EXISTS(SELECT 1 FROM ubicaciones_cadena_diccionario WHERE version_id=$v AND cadena_id=$c AND estado='activo');";
        q.Parameters.AddWithValue("$v", version);
        q.Parameters.AddWithValue("$c", cadena);
        return Convert.ToInt64(q.ExecuteScalar()) != 0;
    }

    private int ContarUbicaciones(long cadena)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT COUNT(*) FROM ubicaciones_cadena_diccionario WHERE cadena_id=$c AND estado='activo';";
        q.Parameters.AddWithValue("$c", cadena);
        return Convert.ToInt32(q.ExecuteScalar());
    }

    public ResultadoMotorCadena CrearCadenaManual(long versionId)
    {
        ProveedorDocumento? proveedor = ProveedorDeDocumento.Determinar(conexion, versionId);
        if (proveedor is null)
            return new("sin_esquema", Motivo: "No se pudo determinar el proveedor.");
        var esquema = new RepositorioEsquemasDiccionario(conexion).ObtenerPorProveedor(
            proveedor.Clave
        );
        string? tipo = LeerTipo(versionId);
        var lugar = tipo is null
            ? null
            : esquema?.Lugares.SingleOrDefault(l => l.DatoTipoId == tipo);
        if (esquema is null || lugar is null)
            return new("tipo_fuera_esquema");
        long cadena = CrearCadena(versionId, esquema, proveedor.Clave, lugar);
        using var q = conexion.CreateCommand();
        q.CommandText =
            "UPDATE decisiones_esquema_diccionario SET estado='resuelta' WHERE version_id=$v AND tipo IN ('sin_piso','sin_inicio') AND estado='pendiente';";
        q.Parameters.AddWithValue("$v", versionId);
        q.ExecuteNonQuery();
        RegistrarDecisionModo(versionId, esquema, lugar, cadena);
        CompletarCadena(cadena, esquema);
        return new("cadena_creada", cadena);
    }

    private void RegistrarSinPiso(long versionId, string proveedor)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "INSERT INTO decisiones_esquema_diccionario(version_id,proveedor,motivo,creada_en,estado,tipo,opciones_json) VALUES($v,$p,'No se encontró un origen en los enlaces declarados.',$f,'pendiente','sin_piso','[]') ON CONFLICT(version_id) DO NOTHING;";
        q.Parameters.AddWithValue("$v", versionId);
        q.Parameters.AddWithValue("$p", proveedor);
        q.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        q.ExecuteNonQuery();
    }

    private void RegistrarDecisionModo(
        long versionId,
        EsquemaDiccionario esquema,
        LugarDiccionario lugar,
        long cadenaId
    )
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT e.lugar_decide_modo_id,(SELECT json_group_array(nombre) FROM (SELECT nombre FROM modos_esquema_diccionario WHERE esquema_id=e.id ORDER BY id)) FROM esquemas_diccionario e WHERE e.id=$e AND NOT EXISTS(SELECT 1 FROM cadenas_diccionario c WHERE c.id=$c AND c.modo_id IS NOT NULL);";
        q.Parameters.AddWithValue("$e", esquema.Id);
        q.Parameters.AddWithValue("$c", cadenaId);
        using var r = q.ExecuteReader();
        if (!r.Read() || r.IsDBNull(0) || r.GetInt64(0) != lugar.Id)
            return;
        string opciones = r.IsDBNull(1) ? "[]" : r.GetString(1);
        r.Close();
        if (opciones == "[]")
            return;
        using var guardar = conexion.CreateCommand();
        guardar.CommandText =
            "INSERT INTO decisiones_esquema_diccionario(version_id,proveedor,motivo,creada_en,estado,tipo,cadena_id,opciones_json) VALUES($v,$p,'Modo pendiente',$f,'pendiente','modo',$c,$o) ON CONFLICT(version_id) DO NOTHING;";
        guardar.Parameters.AddWithValue("$v", versionId);
        guardar.Parameters.AddWithValue("$p", esquema.Proveedor);
        guardar.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        guardar.Parameters.AddWithValue("$c", cadenaId);
        guardar.Parameters.AddWithValue("$o", opciones);
        guardar.ExecuteNonQuery();
    }

    private List<Coincidencia> Buscar(
        EsquemaDiccionario esquema,
        LugarDiccionario actual,
        Valor valor
    )
    {
        using var equivalencia = conexion.CreateCommand();
        equivalencia.CommandText =
            "SELECT COALESCE(enlaza_como,id) FROM diccionario_datos WHERE id=$d;";
        equivalencia.Parameters.AddWithValue("$d", valor.Dato);
        string datoComparable = Convert.ToString(equivalencia.ExecuteScalar()) ?? valor.Dato;
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT c.id,u.id,u.lugar_id,u.linea_id,x.valor_original,e.cardinalidad,CASE WHEN e.lugar_b=$l THEN 1 ELSE 0 END FROM enlaces_esquema_diccionario e JOIN diccionario_datos de ON de.id=e.dato_diccionario_id JOIN lugares_esquema_diccionario la ON la.id=e.lugar_a JOIN ubicaciones_cadena_diccionario u ON u.lugar_id=CASE WHEN e.lugar_a=$l THEN e.lugar_b ELSE e.lugar_a END AND u.estado='activo' JOIN cadenas_diccionario c ON c.id=u.cadena_id AND c.esquema_id=la.esquema_id AND c.estado='activa' JOIN valores_documento x ON x.version_id=u.version_id AND x.estado='vigente' JOIN diccionario_datos dx ON dx.id=x.dato_diccionario_id AND COALESCE(dx.enlaza_como,dx.id)=COALESCE(de.enlaza_como,de.id) WHERE la.esquema_id=$e AND (e.lugar_a=$l OR e.lugar_b=$l) AND COALESCE(de.enlaza_como,de.id)=$d AND x.valor_clave=$k;";
        comando.Parameters.AddWithValue("$l", actual.Id);
        comando.Parameters.AddWithValue("$e", esquema.Id);
        comando.Parameters.AddWithValue("$d", datoComparable);
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
