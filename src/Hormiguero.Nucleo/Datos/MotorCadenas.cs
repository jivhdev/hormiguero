using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed record ResultadoMotorCadena(
    string Estado,
    long? CadenaId = null,
    string? Motivo = null
);

public sealed class MotorCadenas(SqliteConnection conexion)
{
    private readonly HashSet<long> _reprocesando = [];

    public IReadOnlyList<ResultadoMotorCadena> Procesar(long? versionId = null)
    {
        var versiones = LeerVersiones(versionId);
        var resultados = new List<ResultadoMotorCadena>();
        foreach (long version in versiones)
            resultados.Add(ProcesarVersion(version));
        return resultados;
    }

    public ResultadoMotorCadena ProcesarVersion(long versionId) =>
        ProcesarVersion(versionId, false);

    private ResultadoMotorCadena ProcesarVersion(long versionId, bool reprocesarPendiente)
    {
        if (TieneEnlace(versionId) || (!reprocesarPendiente && TienePendiente(versionId)))
            return new("ya_procesado");
        ProveedorDocumento? proveedor = ProveedorDeDocumento.Determinar(conexion, versionId);
        if (proveedor is null)
            return new("sin_esquema", Motivo: "El documento no tiene proveedor determinado.");
        EsquemaCadena? esquema = new RepositorioEsquemas(conexion).ObtenerPorProveedor(
            proveedor.Clave
        );
        if (esquema is null || !esquema.Activo)
            return new("sin_esquema", Motivo: "El proveedor no tiene esquema activo.");
        long? identificacion = IdentificacionVersion(versionId);
        LugarEsquema? lugar = identificacion is null
            ? null
            : esquema.Lugares.FirstOrDefault(l => l.IdentificacionId == identificacion);
        if (lugar is null)
            return new(
                "tipo_fuera_esquema",
                Motivo: "El tipo de documento no pertenece al esquema."
            );

        var valores = LeerValoresEnlazables(versionId);
        var coincidenciasExactas = new Dictionary<long, string>();
        var coincidenciasLimpias =
            new List<(long CadenaId, string Dato, string Nuevo, string Anterior)>();
        foreach (var valor in valores)
        {
            foreach (var anterior in BuscarCoincidencias(esquema.Id, valor.Dato, valor.Clave))
            {
                if (
                    DiccionarioDatosEnlazantes.ValorLiteralNormalizado(anterior.Original)
                    == DiccionarioDatosEnlazantes.ValorLiteralNormalizado(valor.Original)
                )
                    coincidenciasExactas[anterior.CadenaId] = valor.Dato;
                else
                    coincidenciasLimpias.Add(
                        (anterior.CadenaId, valor.Dato, valor.Original, anterior.Original)
                    );
            }
        }
        coincidenciasLimpias.RemoveAll(c => coincidenciasExactas.ContainsKey(c.CadenaId));
        var cadenas = coincidenciasExactas
            .Keys.Concat(coincidenciasLimpias.Select(c => c.CadenaId))
            .Distinct()
            .ToArray();
        if (coincidenciasLimpias.Count > 0 || cadenas.Length > 1)
        {
            string motivo =
                coincidenciasLimpias.Count > 0
                    ? $"Calza solo al limpiar: «{coincidenciasLimpias[0].Nuevo}» ↔ «{coincidenciasLimpias[0].Anterior}»"
                    : $"Coincide con {cadenas.Length} cadenas por más de un dato del documento.";
            long destino = cadenas.Min();
            Agregar(versionId, destino, esquema, lugar, "dudoso", motivo, proveedor.Clave);
            return new("dudoso", destino, motivo);
        }
        if (cadenas.Length == 1)
        {
            Agregar(versionId, cadenas[0], esquema, lugar, "activo", null, proveedor.Clave);
            ResolverDecision(versionId);
            ReprocesarDecisionesMencionadas(versionId);
            return new("ubicado", cadenas[0]);
        }
        if (lugar.IniciaCadena)
        {
            long cadena = CrearCadena(versionId, proveedor, esquema, lugar);
            ResolverDecision(versionId);
            ReprocesarDecisionesMencionadas(versionId);
            return new("cadena_creada", cadena);
        }
        GuardarSinPiso(versionId, proveedor.Clave);
        return new(
            "sin_piso",
            Motivo: "El documento no coincide con una cadena y su lugar no inicia cadenas."
        );
    }

    public ResultadoMotorCadena CrearCadenaIgual(long versionId)
    {
        var decision = LeerDecision(versionId);
        if (decision is null)
            return new("sin_decision");
        var proveedor = ProveedorDeDocumento.Determinar(conexion, versionId);
        if (proveedor is null)
            return new("sin_esquema", Motivo: "El documento no tiene proveedor determinado.");
        var esquema = new RepositorioEsquemas(conexion).ObtenerPorProveedor(proveedor.Clave);
        if (esquema is null || !esquema.Activo)
            return new("sin_esquema", Motivo: "El proveedor no tiene esquema activo.");
        long? identificacion = IdentificacionVersion(versionId);
        var lugar = identificacion is null
            ? null
            : esquema.Lugares.FirstOrDefault(l => l.IdentificacionId == identificacion);
        if (lugar is null)
            return new("tipo_fuera_esquema");

        long cadena = CrearCadena(versionId, proveedor, esquema, lugar);
        ResolverDecision(versionId);
        ReprocesarDecisionesMencionadas(versionId);
        return new("cadena_creada", cadena);
    }

    public bool ArchivarSinCadena(long versionId)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "UPDATE decisiones_pendientes_cadena SET estado='resuelta' WHERE version_id=$v AND estado='pendiente' AND tipo='sin_piso';";
        comando.Parameters.AddWithValue("$v", versionId);
        return comando.ExecuteNonQuery() > 0;
    }

    public void RegistrarError(Exception error, long? versionId)
    {
        using var tx = conexion.BeginTransaction();
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "error_enlace_cadena",
            $"version:{versionId?.ToString() ?? "pendiente"}",
            error.Message,
            app: "Archivero"
        );
        tx.Commit();
    }

    private long CrearCadena(
        long versionId,
        ProveedorDocumento proveedor,
        EsquemaCadena esquema,
        LugarEsquema lugar
    )
    {
        using var tx = conexion.BeginTransaction();
        string nombreDocumento = NombreVersion(versionId, tx);
        string cliente = ClienteVersion(versionId, tx) ?? "";
        using var comando = conexion.CreateCommand();
        comando.Transaction = tx;
        comando.CommandText =
            "INSERT INTO cadenas(modelo_id,nombre_modelo_origen,nombre,fecha_creacion,estructura_json,proveedor,cliente,esquema_id) VALUES(NULL,NULL,$n,$f,'[]',$p,$c,$e) RETURNING id;";
        comando.Parameters.AddWithValue("$n", nombreDocumento);
        comando.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        comando.Parameters.AddWithValue("$p", proveedor.Clave);
        comando.Parameters.AddWithValue("$c", cliente);
        comando.Parameters.AddWithValue("$e", esquema.Id);
        long id = Convert.ToInt64(comando.ExecuteScalar());
        CrearUbicacion(tx, versionId, id, lugar, "activo", null, null, null);
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "crear_cadena_esquema",
            $"cadena:{id}",
            $"esquema:{esquema.Id}",
            app: "Archivero"
        );
        tx.Commit();
        RepositorioModosEsquema.RegistrarDecision(conexion, versionId, id, lugar.Id);
        return id;
    }

    private void Agregar(
        long versionId,
        long cadenaId,
        EsquemaCadena esquema,
        LugarEsquema lugar,
        string estado,
        string? motivo,
        string proveedor
    )
    {
        using var tx = conexion.BeginTransaction();
        ActualizarCliente(tx, cadenaId, versionId);
        long? linea = null;
        long? parejaVersion = null;
        ParejaEsquema? pareja = esquema.Parejas.FirstOrDefault(p =>
            p.LugarA == lugar.Id || p.LugarB == lugar.Id
        );
        if (pareja is not null)
        {
            long otroLugar = pareja.LugarA == lugar.Id ? pareja.LugarB : pareja.LugarA;
            string? clave = LeerClave(versionId, pareja.DatoDiccionarioId, tx);
            if (clave is not null)
                parejaVersion = BuscarPar(
                    tx,
                    cadenaId,
                    otroLugar,
                    pareja.DatoDiccionarioId,
                    clave,
                    out linea
                );
            if (linea is null)
                linea = SiguienteLinea(tx, cadenaId);
        }
        CrearUbicacion(tx, versionId, cadenaId, lugar, estado, motivo, linea, parejaVersion);
        if (parejaVersion is not null && linea is not null)
            ActualizarPar(tx, cadenaId, parejaVersion.Value, linea.Value, versionId);
        using (var actualizar = conexion.CreateCommand())
        {
            actualizar.Transaction = tx;
            actualizar.CommandText =
                "UPDATE cadenas SET proveedor=COALESCE(proveedor,$p) WHERE id=$c;";
            actualizar.Parameters.AddWithValue("$p", proveedor);
            actualizar.Parameters.AddWithValue("$c", cadenaId);
            actualizar.ExecuteNonQuery();
        }
        tx.Commit();
        RepositorioModosEsquema.RegistrarDecision(conexion, versionId, cadenaId, lugar.Id);
    }

    private void CrearUbicacion(
        SqliteTransaction tx,
        long versionId,
        long cadenaId,
        LugarEsquema lugar,
        string estado,
        string? motivo,
        long? linea,
        long? parejaVersion
    )
    {
        string nombre = NombreVersion(versionId, tx);
        int orden;
        using (var q = conexion.CreateCommand())
        {
            q.Transaction = tx;
            q.CommandText =
                "SELECT COALESCE(MAX(orden),-1)+1 FROM vagones_cadena WHERE cadena_id=$c AND lugar_esquema_id=$l AND estado='activo';";
            q.Parameters.AddWithValue("$c", cadenaId);
            q.Parameters.AddWithValue("$l", lugar.Id);
            orden = Convert.ToInt32(q.ExecuteScalar());
        }
        using var insertar = conexion.CreateCommand();
        insertar.Transaction = tx;
        insertar.CommandText =
            "INSERT INTO vagones_cadena(cadena_id,orden,nombre,lugar_esquema_id,linea_id,pareja_version_id) VALUES($c,$o,$n,$l,$linea,$par) RETURNING id;";
        insertar.Parameters.AddWithValue("$c", cadenaId);
        insertar.Parameters.AddWithValue("$o", orden);
        insertar.Parameters.AddWithValue("$n", nombre);
        insertar.Parameters.AddWithValue("$l", lugar.Id);
        insertar.Parameters.AddWithValue("$linea", (object?)linea ?? DBNull.Value);
        insertar.Parameters.AddWithValue("$par", (object?)parejaVersion ?? DBNull.Value);
        long vagon = Convert.ToInt64(insertar.ExecuteScalar());
        using var enlace = conexion.CreateCommand();
        enlace.Transaction = tx;
        enlace.CommandText =
            "INSERT INTO enlaces_cadena(vagon_cadena_id,version_id,origen,estado,creada_en,motivo) VALUES($v,$d,'automatico',$e,$f,$m);";
        enlace.Parameters.AddWithValue("$v", vagon);
        enlace.Parameters.AddWithValue("$d", versionId);
        enlace.Parameters.AddWithValue("$e", estado);
        enlace.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        enlace.Parameters.AddWithValue("$m", (object?)motivo ?? DBNull.Value);
        enlace.ExecuteNonQuery();
        if (estado == "activo")
        {
            using var actualizar = conexion.CreateCommand();
            actualizar.Transaction = tx;
            actualizar.CommandText = "UPDATE vagones_cadena SET version_id=$v WHERE id=$id;";
            actualizar.Parameters.AddWithValue("$v", versionId);
            actualizar.Parameters.AddWithValue("$id", vagon);
            actualizar.ExecuteNonQuery();
        }
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            estado == "dudoso" ? "proponer_enlace_dudoso" : "ubicar_documento_cadena",
            $"version:{versionId}",
            $"cadena:{cadenaId};lugar:{lugar.Id}",
            app: "Archivero"
        );
    }

    private IReadOnlyList<(long CadenaId, string Original)> BuscarCoincidencias(
        long esquemaId,
        string dato,
        string clave
    )
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT DISTINCT c.id,x.valor_original FROM cadenas c JOIN vagones_cadena v ON v.cadena_id=c.id JOIN enlaces_cadena e ON e.vagon_cadena_id=v.id AND e.estado='activo' JOIN versiones_documento ver ON ver.id=e.version_id JOIN valores_documento x ON x.version_id=e.version_id JOIN campos_documento f ON f.id=x.campo_id WHERE c.esquema_id=$e AND c.estado='activa' AND v.estado='activo' AND ver.estado='vigente' AND x.estado='vigente' AND x.dato_diccionario_id=$d AND x.valor_clave=$k AND EXISTS(SELECT 1 FROM tipos_documento_datos t WHERE t.identificacion_id=f.identificacion_id AND t.dato_diccionario_id=x.dato_diccionario_id AND t.activo=1 AND t.enlazable=1);";
        comando.Parameters.AddWithValue("$e", esquemaId);
        comando.Parameters.AddWithValue("$d", dato);
        comando.Parameters.AddWithValue("$k", clave);
        using var lector = comando.ExecuteReader();
        var lista = new List<(long, string)>();
        while (lector.Read())
            lista.Add((lector.GetInt64(0), lector.GetString(1)));
        return lista;
    }

    private IReadOnlyList<(string Dato, string Original, string Clave)> LeerValoresEnlazables(
        long version
    )
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT DISTINCT x.dato_diccionario_id,x.valor_original,x.valor_clave FROM valores_documento x JOIN campos_documento f ON f.id=x.campo_id WHERE x.version_id=$v AND x.estado='vigente' AND x.dato_diccionario_id IS NOT NULL AND x.valor_clave<>'' AND EXISTS(SELECT 1 FROM tipos_documento_datos t WHERE t.identificacion_id=f.identificacion_id AND t.dato_diccionario_id=x.dato_diccionario_id AND t.activo=1 AND t.enlazable=1);";
        comando.Parameters.AddWithValue("$v", version);
        using var lector = comando.ExecuteReader();
        var lista = new List<(string, string, string)>();
        while (lector.Read())
            lista.Add((lector.GetString(0), lector.GetString(1), lector.GetString(2)));
        return lista;
    }

    private long? IdentificacionVersion(long version)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT identificacion_id FROM campos_documento f JOIN valores_documento x ON x.campo_id=f.id WHERE x.version_id=$v AND x.estado='vigente' ORDER BY x.id LIMIT 1;";
        comando.Parameters.AddWithValue("$v", version);
        object? valor = comando.ExecuteScalar();
        return valor is null ? null : Convert.ToInt64(valor);
    }

    private List<long> LeerVersiones(long? id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id FROM versiones_documento WHERE estado='vigente'"
            + (id is null ? " ORDER BY id;" : " AND id=$id;");
        comando.Parameters.AddWithValue("$id", (object?)id ?? DBNull.Value);
        using var lector = comando.ExecuteReader();
        var lista = new List<long>();
        while (lector.Read())
            lista.Add(lector.GetInt64(0));
        return lista;
    }

    private bool TieneEnlace(long id) =>
        Existe("SELECT EXISTS(SELECT 1 FROM enlaces_cadena WHERE version_id=$v);", id);

    private bool TienePendiente(long id) =>
        Existe(
            "SELECT EXISTS(SELECT 1 FROM decisiones_pendientes_cadena WHERE version_id=$v);",
            id
        );

    private bool Existe(string sql, long id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        comando.Parameters.AddWithValue("$v", id);
        return Convert.ToInt64(comando.ExecuteScalar()) != 0;
    }

    private string NombreVersion(long version, SqliteTransaction? tx = null)
    {
        using var comando = conexion.CreateCommand();
        comando.Transaction = tx;
        comando.CommandText =
            "SELECT d.nombre FROM versiones_documento v JOIN documentos d ON d.id=v.documento_id WHERE v.id=$v;";
        comando.Parameters.AddWithValue("$v", version);
        return Convert.ToString(comando.ExecuteScalar()) ?? "Documento";
    }

    private string? ClienteVersion(long version, SqliteTransaction? tx = null)
    {
        using var comando = conexion.CreateCommand();
        comando.Transaction = tx;
        comando.CommandText =
            "SELECT COALESCE((SELECT valor_original FROM valores_documento WHERE version_id=$v AND estado='vigente' AND dato_diccionario_id IN ('rut_cliente','nombre_cliente') ORDER BY CASE dato_diccionario_id WHEN 'rut_cliente' THEN 0 ELSE 1 END,id LIMIT 1),(SELECT valor FROM valores_informativos_documento WHERE version_id=$v AND dato='nombre_cliente' AND trim(valor)<>'' LIMIT 1));";
        comando.Parameters.AddWithValue("$v", version);
        return Convert.ToString(comando.ExecuteScalar()) is { Length: > 0 } cliente
            ? cliente
            : null;
    }

    private void ActualizarCliente(SqliteTransaction tx, long cadena, long version)
    {
        string? cliente = ClienteVersion(version, tx);
        if (cliente is null)
            return;
        using var comando = conexion.CreateCommand();
        comando.Transaction = tx;
        comando.CommandText =
            "UPDATE cadenas SET cliente=$cl WHERE id=$c AND (cliente IS NULL OR cliente='');";
        comando.Parameters.AddWithValue("$cl", cliente);
        comando.Parameters.AddWithValue("$c", cadena);
        comando.ExecuteNonQuery();
    }

    private void GuardarSinPiso(long version, string proveedor)
    {
        string numeros = LeerNumeros(version);
        using var tx = conexion.BeginTransaction();
        using var comando = conexion.CreateCommand();
        comando.Transaction = tx;
        comando.CommandText =
            "INSERT OR IGNORE INTO decisiones_pendientes_cadena(version_id,proveedor,numeros_json,motivo,creada_en) VALUES($v,$p,$n,'sin piso',$f);";
        comando.Parameters.AddWithValue("$v", version);
        comando.Parameters.AddWithValue("$p", proveedor);
        comando.Parameters.AddWithValue("$n", numeros);
        comando.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        comando.ExecuteNonQuery();
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "registrar_sin_piso",
            $"version:{version}",
            proveedor,
            app: "Archivero"
        );
        tx.Commit();
    }

    private (string Proveedor, string Numeros)? LeerDecision(long version)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT proveedor,numeros_json FROM decisiones_pendientes_cadena WHERE version_id=$v AND estado='pendiente';";
        comando.Parameters.AddWithValue("$v", version);
        using var lector = comando.ExecuteReader();
        return lector.Read() ? (lector.GetString(0), lector.GetString(1)) : null;
    }

    private void ResolverDecision(long version)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "UPDATE decisiones_pendientes_cadena SET estado='resuelta' WHERE version_id=$v AND estado='pendiente' AND tipo='sin_piso';";
        comando.Parameters.AddWithValue("$v", version);
        comando.ExecuteNonQuery();
    }

    private void ReprocesarDecisionesMencionadas(long origenVersion)
    {
        if (!_reprocesando.Add(origenVersion))
            return;
        try
        {
            var valores = LeerValoresEnlazables(origenVersion)
                .Select(v => (v.Dato, Clave: v.Clave))
                .ToHashSet();
            if (valores.Count == 0)
                return;
            using var comando = conexion.CreateCommand();
            comando.CommandText =
                "SELECT version_id,numeros_json FROM decisiones_pendientes_cadena WHERE estado='pendiente' ORDER BY creada_en,version_id;";
            using var lector = comando.ExecuteReader();
            var candidatos = new List<(long Version, string Numeros)>();
            while (lector.Read())
                candidatos.Add((lector.GetInt64(0), lector.GetString(1)));
            foreach (var candidato in candidatos)
            {
                if (candidato.Version == origenVersion || _reprocesando.Contains(candidato.Version))
                    continue;
                var numeros =
                    System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                        candidato.Numeros
                    ) ?? [];
                bool mencionaOrigen = numeros.Any(par =>
                    valores.Contains(
                        (par.Key, DiccionarioDatosEnlazantes.ClaveDeEnlace(par.Key, par.Value))
                    )
                );
                if (!mencionaOrigen)
                    continue;
                _reprocesando.Add(candidato.Version);
                var resultado = ProcesarVersion(candidato.Version, true);
                if (resultado.Estado is "ubicado" or "cadena_creada")
                    ResolverDecision(candidato.Version);
                _reprocesando.Remove(candidato.Version);
            }
        }
        finally
        {
            _reprocesando.Remove(origenVersion);
        }
    }

    private string LeerNumeros(long version)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT dato_diccionario_id,valor_original FROM valores_documento WHERE version_id=$v AND estado='vigente' AND dato_diccionario_id IS NOT NULL ORDER BY id;";
        comando.Parameters.AddWithValue("$v", version);
        using var lector = comando.ExecuteReader();
        var valores = new Dictionary<string, string>();
        while (lector.Read())
            valores.TryAdd(lector.GetString(0), lector.GetString(1));
        return System.Text.Json.JsonSerializer.Serialize(valores);
    }

    private string? LeerClave(long version, string dato, SqliteTransaction tx)
    {
        using var comando = conexion.CreateCommand();
        comando.Transaction = tx;
        comando.CommandText =
            "SELECT valor_clave FROM valores_documento WHERE version_id=$v AND dato_diccionario_id=$d AND estado='vigente' ORDER BY id LIMIT 1;";
        comando.Parameters.AddWithValue("$v", version);
        comando.Parameters.AddWithValue("$d", dato);
        return Convert.ToString(comando.ExecuteScalar());
    }

    private long? BuscarPar(
        SqliteTransaction tx,
        long cadena,
        long lugar,
        string dato,
        string clave,
        out long? linea
    )
    {
        using var comando = conexion.CreateCommand();
        comando.Transaction = tx;
        comando.CommandText =
            "SELECT e.version_id,v.linea_id FROM vagones_cadena v JOIN enlaces_cadena e ON e.vagon_cadena_id=v.id AND e.estado='activo' JOIN valores_documento x ON x.version_id=e.version_id AND x.dato_diccionario_id=$d AND x.estado='vigente' WHERE v.cadena_id=$c AND v.lugar_esquema_id=$l AND v.estado='activo' AND x.valor_clave=$k AND v.pareja_version_id IS NULL ORDER BY v.id LIMIT 1;";
        comando.Parameters.AddWithValue("$d", dato);
        comando.Parameters.AddWithValue("$c", cadena);
        comando.Parameters.AddWithValue("$l", lugar);
        comando.Parameters.AddWithValue("$k", clave);
        using var lector = comando.ExecuteReader();
        if (!lector.Read())
        {
            linea = null;
            return null;
        }
        long version = lector.GetInt64(0);
        linea = lector.IsDBNull(1) ? null : lector.GetInt64(1);
        return version;
    }

    private long SiguienteLinea(SqliteTransaction tx, long cadena)
    {
        using var comando = conexion.CreateCommand();
        comando.Transaction = tx;
        comando.CommandText =
            "SELECT COALESCE(MAX(linea_id),0)+1 FROM vagones_cadena WHERE cadena_id=$c;";
        comando.Parameters.AddWithValue("$c", cadena);
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private void ActualizarPar(
        SqliteTransaction tx,
        long cadena,
        long versionPar,
        long linea,
        long versionNueva
    )
    {
        using var comando = conexion.CreateCommand();
        comando.Transaction = tx;
        comando.CommandText =
            "UPDATE vagones_cadena SET linea_id=$l,pareja_version_id=$v WHERE cadena_id=$c AND id=(SELECT v.id FROM vagones_cadena v JOIN enlaces_cadena e ON e.vagon_cadena_id=v.id WHERE e.version_id=$v AND e.estado='activo' LIMIT 1);";
        comando.Parameters.AddWithValue("$l", linea);
        comando.Parameters.AddWithValue("$v", versionNueva);
        comando.Parameters.AddWithValue("$c", cadena);
        comando.ExecuteNonQuery();
        using var inverso = conexion.CreateCommand();
        inverso.Transaction = tx;
        inverso.CommandText =
            "UPDATE vagones_cadena SET pareja_version_id=$v WHERE cadena_id=$c AND id=(SELECT v.id FROM vagones_cadena v JOIN enlaces_cadena e ON e.vagon_cadena_id=v.id WHERE e.version_id=$p AND e.estado='activo' LIMIT 1);";
        inverso.Parameters.AddWithValue("$v", versionPar);
        inverso.Parameters.AddWithValue("$p", versionNueva);
        inverso.Parameters.AddWithValue("$c", cadena);
        inverso.ExecuteNonQuery();
    }
}
