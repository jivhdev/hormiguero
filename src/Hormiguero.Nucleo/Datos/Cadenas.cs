using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed record ModeloCadena(
    long Id,
    string Nombre,
    DateTime FechaCreacion,
    bool EsModeloHijo,
    int PreferenciaNombre,
    long? VagonNombreId
);

public sealed record VagonModelo(
    long Id,
    long ModeloId,
    long? PadreId,
    int Orden,
    string Nombre,
    bool EsMultiple,
    bool EsAnexo,
    long? ModeloCadenaHijaId
);

public sealed record NodoVagonModelo(VagonModelo Vagon, IReadOnlyList<NodoVagonModelo> Hijos);

public sealed record Cadena(
    long Id,
    long? ModeloId,
    string? NombreModeloOrigen,
    string Nombre,
    DateTime FechaCreacion,
    string EstructuraJson,
    long? CadenaMadreId,
    long? VagonPadreId,
    string Estado
);

public sealed record VagonCadena(
    long Id,
    long CadenaId,
    long? PadreId,
    long? VagonModeloId,
    int Orden,
    string Nombre,
    bool EsMultiple,
    bool EsAnexo,
    long? VersionId,
    string Estado
);

public sealed record NodoVagonCadena(VagonCadena Vagon, IReadOnlyList<NodoVagonCadena> Hijos);

public sealed record VersionDocumentoCadena(
    long VersionId,
    string Ruta,
    string Nombre,
    string Tipo,
    string Emisor,
    DateTime Fecha,
    string Numero
);

public sealed class RepositorioCadenas(SqliteConnection conexion)
{
    public long CrearCadenaSimple(string nombre, DateTime fechaCreacion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        long id = CrearCadena(null, nombre.Trim(), fechaCreacion);
        using var tx = conexion.BeginTransaction();
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "crear_cadena_simple",
            $"cadena:{id}",
            nombre.Trim(),
            app: "Buscadero"
        );
        tx.Commit();
        return id;
    }

    public IReadOnlyList<Cadena> ListarCadenasSimples()
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,modelo_id,nombre_modelo_origen,nombre,fecha_creacion,estructura_json,cadena_madre_id,vagon_padre_id,estado FROM cadenas WHERE modelo_id IS NULL AND estado='activa' ORDER BY id;";
        using var lector = comando.ExecuteReader();
        var resultado = new List<Cadena>();
        while (lector.Read())
            resultado.Add(LeerCadena(lector));
        return resultado;
    }

    public IReadOnlyList<Cadena> BuscarCadenasSimples(string consulta)
    {
        if (string.IsNullOrWhiteSpace(consulta))
            return ListarCadenasSimples();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT DISTINCT c.id FROM cadenas c LEFT JOIN vagones_cadena v ON v.cadena_id=c.id AND v.estado='activo' LEFT JOIN enlaces_cadena e ON e.vagon_cadena_id=v.id AND e.estado='activo' LEFT JOIN versiones_documento ver ON ver.id=e.version_id LEFT JOIN documentos d ON d.id=ver.documento_id LEFT JOIN numeros_documento n ON n.documento_id=d.id WHERE c.modelo_id IS NULL AND c.estado='activa' AND (c.nombre LIKE $q OR n.numero LIKE $q) ORDER BY c.id;";
        comando.Parameters.AddWithValue("$q", $"%{consulta.Trim()}%");
        using var lector = comando.ExecuteReader();
        var ids = new HashSet<long>();
        while (lector.Read())
            ids.Add(lector.GetInt64(0));
        return ListarCadenasSimples().Where(c => ids.Contains(c.Id)).ToArray();
    }

    public IReadOnlyList<VersionDocumentoCadena> BuscarVersionesDocumento(
        string consulta,
        int maximo = 50
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consulta);
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT DISTINCT ver.id,d.ruta,d.nombre,COALESCE(i.tipo,''),COALESCE(i.emisor,''),ver.registrada_en,COALESCE(n.numero,'') FROM versiones_documento ver JOIN documentos d ON d.id=ver.documento_id LEFT JOIN identificaciones i ON i.id=(SELECT f.identificacion_id FROM valores_documento x JOIN campos_documento f ON f.id=x.campo_id WHERE x.version_id=ver.id AND x.estado='vigente' LIMIT 1) LEFT JOIN numeros_documento n ON n.documento_id=d.id WHERE ver.estado='vigente' AND d.estado_baja='activo' AND (d.nombre LIKE $q OR n.numero LIKE $q OR EXISTS(SELECT 1 FROM valores_documento x WHERE x.version_id=ver.id AND x.estado='vigente' AND x.valor_original LIKE $q)) ORDER BY ver.id DESC LIMIT $limite;";
        comando.Parameters.AddWithValue("$q", $"%{consulta.Trim()}%");
        comando.Parameters.AddWithValue("$limite", Math.Clamp(maximo, 1, 200));
        using var lector = comando.ExecuteReader();
        var resultado = new List<VersionDocumentoCadena>();
        while (lector.Read())
            resultado.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetString(1),
                    lector.GetString(2),
                    lector.GetString(3),
                    lector.GetString(4),
                    DateTime.Parse(lector.GetString(5)),
                    lector.GetString(6)
                )
            );
        return resultado;
    }

    public VersionDocumentoCadena? ObtenerVersionDocumento(long versionId)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT ver.id,d.ruta,d.nombre,COALESCE(i.tipo,''),COALESCE(i.emisor,''),ver.registrada_en,COALESCE((SELECT numero FROM numeros_documento n WHERE n.documento_id=d.id LIMIT 1),'') FROM versiones_documento ver JOIN documentos d ON d.id=ver.documento_id LEFT JOIN identificaciones i ON i.id=(SELECT f.identificacion_id FROM valores_documento x JOIN campos_documento f ON f.id=x.campo_id WHERE x.version_id=ver.id AND x.estado='vigente' LIMIT 1) WHERE ver.id=$id;";
        comando.Parameters.AddWithValue("$id", versionId);
        using var lector = comando.ExecuteReader();
        return lector.Read()
            ? new(
                lector.GetInt64(0),
                lector.GetString(1),
                lector.GetString(2),
                lector.GetString(3),
                lector.GetString(4),
                DateTime.Parse(lector.GetString(5)),
                lector.GetString(6)
            )
            : null;
    }

    public bool TransicionCadenasSimplesAceptada()
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT valor FROM configuracion WHERE clave='buscadero_transicion_cadenas_simples';";
        return comando.ExecuteScalar() is not null;
    }

    public void AceptarTransicionCadenasSimples()
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "INSERT INTO configuracion(clave,valor) VALUES('buscadero_transicion_cadenas_simples','aceptada') ON CONFLICT(clave) DO UPDATE SET valor='aceptada';";
        comando.ExecuteNonQuery();
    }

    public void RenombrarCadena(long cadenaId, string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        using var tx = conexion.BeginTransaction();
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "UPDATE cadenas SET nombre=$n WHERE id=$id AND modelo_id IS NULL AND estado='activa';";
        cmd.Parameters.AddWithValue("$n", nombre.Trim());
        cmd.Parameters.AddWithValue("$id", cadenaId);
        if (cmd.ExecuteNonQuery() == 0)
            throw new InvalidOperationException("No existe la cadena simple.");
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "renombrar_cadena",
            $"cadena:{cadenaId}",
            nombre.Trim(),
            app: "Buscadero"
        );
        tx.Commit();
    }

    public IReadOnlyList<VagonCadena> ListarDocumentosCadena(long cadenaId) =>
        LeerVagonesCadena(cadenaId);

    public long AgregarDocumentoCadena(long cadenaId, long versionId, string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        using var tx = conexion.BeginTransaction();
        using (var validar = conexion.CreateCommand())
        {
            validar.Transaction = tx;
            validar.CommandText =
                "SELECT 1 FROM cadenas WHERE id=$c AND modelo_id IS NULL AND estado='activa';";
            validar.Parameters.AddWithValue("$c", cadenaId);
            if (validar.ExecuteScalar() is null)
                throw new InvalidOperationException("No existe la cadena simple.");
            validar.CommandText =
                "SELECT 1 FROM versiones_documento WHERE id=$v AND estado='vigente';";
            validar.Parameters.Clear();
            validar.Parameters.AddWithValue("$v", versionId);
            if (validar.ExecuteScalar() is null)
                throw new InvalidOperationException("La versión del documento no está vigente.");
            validar.CommandText =
                "SELECT 1 FROM enlaces_cadena e JOIN vagones_cadena v ON v.id=e.vagon_cadena_id WHERE v.cadena_id=$c AND e.version_id=$v AND e.estado='activo';";
            validar.Parameters.Clear();
            validar.Parameters.AddWithValue("$c", cadenaId);
            validar.Parameters.AddWithValue("$v", versionId);
            if (validar.ExecuteScalar() is not null)
                throw new InvalidOperationException("El documento ya está en la cadena.");
        }
        int orden;
        using (var q = conexion.CreateCommand())
        {
            q.Transaction = tx;
            q.CommandText =
                "SELECT COALESCE(MAX(orden),-1)+1 FROM vagones_cadena WHERE cadena_id=$c AND padre_id IS NULL AND estado='activo';";
            q.Parameters.AddWithValue("$c", cadenaId);
            orden = Convert.ToInt32(q.ExecuteScalar());
        }
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO vagones_cadena(cadena_id,orden,nombre) VALUES($c,$o,$n) RETURNING id;";
        cmd.Parameters.AddWithValue("$c", cadenaId);
        cmd.Parameters.AddWithValue("$o", orden);
        cmd.Parameters.AddWithValue("$n", nombre.Trim());
        long id = Convert.ToInt64(cmd.ExecuteScalar());
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "agregar_documento_cadena",
            $"cadena:{cadenaId}",
            $"vagon:{id}",
            app: "Buscadero"
        );
        tx.Commit();
        return id;
    }

    public void QuitarDocumentoCadena(long vagonId)
    {
        using var tx = conexion.BeginTransaction();
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "UPDATE vagones_cadena SET estado='anulado',fecha_anulacion=$f WHERE id=$id AND estado='activo' AND vagon_modelo_id IS NULL;";
        cmd.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        cmd.Parameters.AddWithValue("$id", vagonId);
        if (cmd.ExecuteNonQuery() == 0)
            throw new InvalidOperationException("No existe el documento activo de la cadena.");
        using (var enlaces = conexion.CreateCommand())
        {
            enlaces.Transaction = tx;
            enlaces.CommandText =
                "UPDATE enlaces_cadena SET estado='anulado',cambiada_en=$f WHERE vagon_cadena_id=$id AND estado IN ('activo','dudoso');";
            enlaces.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
            enlaces.Parameters.AddWithValue("$id", vagonId);
            enlaces.ExecuteNonQuery();
        }
        CompactarOrden(tx, vagonId);
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "quitar_documento_cadena",
            $"vagon:{vagonId}",
            app: "Buscadero"
        );
        tx.Commit();
    }

    public void ReordenarDocumentoCadena(long vagonId, int desplazamiento)
    {
        if (desplazamiento is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(desplazamiento));
        using var tx = conexion.BeginTransaction();
        var ids = new List<long>();
        using (var cmd = conexion.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                "SELECT id FROM vagones_cadena WHERE cadena_id=(SELECT cadena_id FROM vagones_cadena WHERE id=$id) AND estado='activo' AND vagon_modelo_id IS NULL AND padre_id IS NULL ORDER BY orden,id;";
            cmd.Parameters.AddWithValue("$id", vagonId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                ids.Add(r.GetInt64(0));
        }
        int indice = ids.IndexOf(vagonId);
        if (indice < 0)
            throw new InvalidOperationException("No existe el documento activo de la cadena.");
        int destino = indice + desplazamiento;
        if (destino >= 0 && destino < ids.Count)
        {
            (ids[indice], ids[destino]) = (ids[destino], ids[indice]);
            for (int i = 0; i < ids.Count; i++)
            {
                using var mover = conexion.CreateCommand();
                mover.Transaction = tx;
                mover.CommandText = "UPDATE vagones_cadena SET orden=$o WHERE id=$id;";
                mover.Parameters.AddWithValue("$o", i);
                mover.Parameters.AddWithValue("$id", ids[i]);
                mover.ExecuteNonQuery();
            }
        }
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "reordenar_documento_cadena",
            $"vagon:{vagonId}",
            destino.ToString(),
            app: "Buscadero"
        );
        tx.Commit();
    }

    public long CrearModelo(string nombre, DateTime fechaCreacion, bool esModeloHijo = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "INSERT INTO modelos_cadena(nombre, fecha_creacion, es_modelo_hijo) VALUES ($n,$f,$h) RETURNING id;";
        comando.Parameters.AddWithValue("$n", nombre.Trim());
        comando.Parameters.AddWithValue("$f", fechaCreacion.ToString("o"));
        comando.Parameters.AddWithValue("$h", esModeloHijo ? 1 : 0);
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    public long AgregarVagonModelo(
        long modeloId,
        long? padreId,
        string nombre,
        bool esMultiple = false,
        bool esAnexo = false,
        long? modeloCadenaHijaId = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        using var transaccion = conexion.BeginTransaction();
        using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText =
            "INSERT INTO vagones_modelo(modelo_id,padre_id,orden,nombre,es_multiple,es_anexo,modelo_cadena_hija_id) VALUES($m,$p,(SELECT COALESCE(MAX(orden),-1)+1 FROM vagones_modelo WHERE modelo_id=$m AND padre_id IS $p),$n,$x,$a,$h) RETURNING id;";
        comando.Parameters.AddWithValue("$m", modeloId);
        comando.Parameters.AddWithValue("$p", (object?)padreId ?? DBNull.Value);
        comando.Parameters.AddWithValue("$n", nombre.Trim());
        comando.Parameters.AddWithValue("$x", esMultiple ? 1 : 0);
        comando.Parameters.AddWithValue("$a", esAnexo ? 1 : 0);
        comando.Parameters.AddWithValue("$h", (object?)modeloCadenaHijaId ?? DBNull.Value);
        long id = Convert.ToInt64(comando.ExecuteScalar());
        transaccion.Commit();
        return id;
    }

    public IReadOnlyList<NodoVagonModelo> ObtenerArbolModelo(long modeloId) =>
        CrearArbol(
            LeerVagones(
                "SELECT id,modelo_id,padre_id,orden,nombre,es_multiple,es_anexo,modelo_cadena_hija_id FROM vagones_modelo WHERE modelo_id=$id ORDER BY orden,id",
                modeloId
            )
        );

    public long CrearCadena(
        long? modeloId,
        string nombre,
        DateTime fechaCreacion,
        long? cadenaMadreId = null,
        long? vagonPadreId = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        using var transaccion = conexion.BeginTransaction();
        List<VagonModelo> vagones = modeloId is null
            ? new()
            : LeerVagones(
                "SELECT id,modelo_id,padre_id,orden,nombre,es_multiple,es_anexo,modelo_cadena_hija_id FROM vagones_modelo WHERE modelo_id=$id ORDER BY orden,id",
                modeloId.Value,
                transaccion
            );
        string nombreModelo = "";
        if (modeloId is not null)
        {
            using var consulta = conexion.CreateCommand();
            consulta.Transaction = transaccion;
            consulta.CommandText = "SELECT nombre FROM modelos_cadena WHERE id=$id;";
            consulta.Parameters.AddWithValue("$id", modeloId.Value);
            nombreModelo =
                (string?)consulta.ExecuteScalar()
                ?? throw new InvalidOperationException("No existe el modelo de cadena.");
        }
        var estructura = vagones
            .Select(v => new
            {
                v.Id,
                v.PadreId,
                v.Orden,
                v.Nombre,
                v.EsMultiple,
                v.EsAnexo,
                v.ModeloCadenaHijaId,
            })
            .ToArray();
        using var crear = conexion.CreateCommand();
        crear.Transaction = transaccion;
        crear.CommandText =
            "INSERT INTO cadenas(modelo_id,nombre_modelo_origen,nombre,fecha_creacion,estructura_json,cadena_madre_id,vagon_padre_id) VALUES($m,$o,$n,$f,$j,$madre,$padre) RETURNING id;";
        crear.Parameters.AddWithValue("$m", (object?)modeloId ?? DBNull.Value);
        crear.Parameters.AddWithValue("$o", (object?)nombreModelo ?? DBNull.Value);
        crear.Parameters.AddWithValue("$n", nombre.Trim());
        crear.Parameters.AddWithValue("$f", fechaCreacion.ToString("o"));
        crear.Parameters.AddWithValue("$j", JsonSerializer.Serialize(estructura));
        crear.Parameters.AddWithValue("$madre", (object?)cadenaMadreId ?? DBNull.Value);
        crear.Parameters.AddWithValue("$padre", (object?)vagonPadreId ?? DBNull.Value);
        long cadenaId = Convert.ToInt64(crear.ExecuteScalar());
        var ids = new Dictionary<long, long>();
        int Profundidad(VagonModelo v) =>
            v.PadreId is null ? 0 : 1 + Profundidad(vagones.Single(p => p.Id == v.PadreId.Value));
        foreach (var v in vagones.OrderBy(Profundidad).ThenBy(v => v.Orden).ThenBy(v => v.Id))
        {
            using var agregar = conexion.CreateCommand();
            agregar.Transaction = transaccion;
            agregar.CommandText =
                "INSERT INTO vagones_cadena(cadena_id,padre_id,vagon_modelo_id,orden,nombre,es_multiple,es_anexo) VALUES($c,$p,$m,$o,$n,$x,$a) RETURNING id;";
            agregar.Parameters.AddWithValue("$c", cadenaId);
            agregar.Parameters.AddWithValue(
                "$p",
                v.PadreId is null ? DBNull.Value : ids[v.PadreId.Value]
            );
            agregar.Parameters.AddWithValue("$m", v.Id);
            agregar.Parameters.AddWithValue("$o", v.Orden);
            agregar.Parameters.AddWithValue("$n", v.Nombre);
            agregar.Parameters.AddWithValue("$x", v.EsMultiple ? 1 : 0);
            agregar.Parameters.AddWithValue("$a", v.EsAnexo ? 1 : 0);
            ids[v.Id] = Convert.ToInt64(agregar.ExecuteScalar());
        }
        transaccion.Commit();
        return cadenaId;
    }

    public IReadOnlyList<NodoVagonCadena> ObtenerArbolCadena(long cadenaId) =>
        CrearArbolCadena(LeerVagonesCadena(cadenaId));

    public Cadena? ObtenerCadena(long id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,modelo_id,nombre_modelo_origen,nombre,fecha_creacion,estructura_json,cadena_madre_id,vagon_padre_id,estado FROM cadenas WHERE id=$id AND estado='activa';";
        comando.Parameters.AddWithValue("$id", id);
        using var lector = comando.ExecuteReader();
        return lector.Read() ? LeerCadena(lector) : null;
    }

    private static Cadena LeerCadena(SqliteDataReader lector) =>
        new(
            lector.GetInt64(0),
            NuloLong(lector, 1),
            NuloTexto(lector, 2),
            lector.GetString(3),
            DateTime.Parse(lector.GetString(4)),
            lector.GetString(5),
            NuloLong(lector, 6),
            NuloLong(lector, 7),
            lector.GetString(8)
        );

    private void CompactarOrden(SqliteTransaction tx, long vagonId)
    {
        var ids = new List<long>();
        using (var cmd = conexion.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                "SELECT id FROM vagones_cadena WHERE cadena_id=(SELECT cadena_id FROM vagones_cadena WHERE id=$id) AND estado='activo' AND padre_id IS NULL ORDER BY orden,id;";
            cmd.Parameters.AddWithValue("$id", vagonId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                ids.Add(r.GetInt64(0));
        }
        for (int i = 0; i < ids.Count; i++)
        {
            using var cmd = conexion.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE vagones_cadena SET orden=$o WHERE id=$id;";
            cmd.Parameters.AddWithValue("$o", i);
            cmd.Parameters.AddWithValue("$id", ids[i]);
            cmd.ExecuteNonQuery();
        }
    }

    private List<VagonModelo> LeerVagones(string sql, long id, SqliteTransaction? tx = null)
    {
        using var comando = conexion.CreateCommand();
        comando.Transaction = tx;
        comando.CommandText = sql;
        comando.Parameters.AddWithValue("$id", id);
        using var lector = comando.ExecuteReader();
        var lista = new List<VagonModelo>();
        while (lector.Read())
            lista.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    NuloLong(lector, 2),
                    lector.GetInt32(3),
                    lector.GetString(4),
                    lector.GetInt32(5) != 0,
                    lector.GetInt32(6) != 0,
                    NuloLong(lector, 7)
                )
            );
        return lista;
    }

    private static IReadOnlyList<NodoVagonModelo> CrearArbol(IReadOnlyList<VagonModelo> vagones)
    {
        IReadOnlyList<NodoVagonModelo> Construir(long? padreId) =>
            vagones
                .Where(v => v.PadreId == padreId)
                .OrderBy(v => v.Orden)
                .ThenBy(v => v.Id)
                .Select(v => new NodoVagonModelo(v, Construir(v.Id)))
                .ToArray();
        return Construir(null);
    }

    private List<VagonCadena> LeerVagonesCadena(long id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,cadena_id,padre_id,vagon_modelo_id,orden,nombre,es_multiple,es_anexo,version_id,estado FROM vagones_cadena WHERE cadena_id=$id AND estado='activo' ORDER BY orden,id;";
        comando.Parameters.AddWithValue("$id", id);
        using var lector = comando.ExecuteReader();
        var lista = new List<VagonCadena>();
        while (lector.Read())
            lista.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    NuloLong(lector, 2),
                    NuloLong(lector, 3),
                    lector.GetInt32(4),
                    lector.GetString(5),
                    lector.GetInt32(6) != 0,
                    lector.GetInt32(7) != 0,
                    NuloLong(lector, 8),
                    lector.GetString(9)
                )
            );
        return lista;
    }

    private static IReadOnlyList<NodoVagonCadena> CrearArbolCadena(
        IReadOnlyList<VagonCadena> vagones
    )
    {
        IReadOnlyList<NodoVagonCadena> Construir(long? padreId) =>
            vagones
                .Where(v => v.PadreId == padreId)
                .OrderBy(v => v.Orden)
                .ThenBy(v => v.Id)
                .Select(v => new NodoVagonCadena(v, Construir(v.Id)))
                .ToArray();
        return Construir(null);
    }

    private static long? NuloLong(SqliteDataReader l, int i) =>
        l.IsDBNull(i) ? null : l.GetInt64(i);

    private static string? NuloTexto(SqliteDataReader l, int i) =>
        l.IsDBNull(i) ? null : l.GetString(i);
}
