using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Utilidades;
using Microsoft.Data.Sqlite;

namespace Buscadero.Core.Lineas;

public sealed class RepositorioLineas : IDisposable
{
    private readonly SqliteConnection _conexion;
    private readonly RepositorioCadenas _cadenas;
    private readonly RepositorioDocumentosDatos _documentos;
    private readonly RepositorioReglasYEnlaces _enlaces;
    private readonly string _rutaBaseComun;
    private Task? _revisionMotor;

    public RepositorioLineas(string rutaBaseComun)
    {
        _rutaBaseComun = rutaBaseComun;
        _conexion = BaseComun.Abrir(rutaBaseComun);
        _cadenas = new RepositorioCadenas(_conexion);
        _documentos = new RepositorioDocumentosDatos(_conexion);
        _enlaces = new RepositorioReglasYEnlaces(_conexion);
    }

    public PlantillaLinea AgregarPlantilla(string nombre, DateTime fechaCreacion, bool esModeloHijo)
    {
        var modelo = new PlantillaLinea
        {
            Id = _cadenas.CrearModelo(nombre, fechaCreacion, esModeloHijo),
            Nombre = nombre,
            FechaCreacion = fechaCreacion,
            EsModeloHijo = esModeloHijo,
        };
        return modelo;
    }

    public IReadOnlyList<PlantillaLinea> ObtenerPlantillas()
    {
        using var cmd = _conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id,nombre,fecha_creacion,es_modelo_hijo,preferencia_nombre,vagon_nombre_id FROM modelos_cadena ORDER BY nombre,id;";
        using var r = cmd.ExecuteReader();
        var lista = new List<PlantillaLinea>();
        while (r.Read())
            lista.Add(
                new PlantillaLinea
                {
                    Id = r.GetInt64(0),
                    Nombre = r.GetString(1),
                    FechaCreacion = DateTime.Parse(r.GetString(2)),
                    EsModeloHijo = r.GetInt32(3) != 0,
                    PreferenciaNombre = (PreferenciaNombreCadena)r.GetInt32(4),
                    VagonNombreId = r.IsDBNull(5) ? null : r.GetInt64(5),
                }
            );
        return lista;
    }

    public PlantillaLinea? ObtenerPlantilla(long id) =>
        ObtenerPlantillas().FirstOrDefault(p => p.Id == id);

    public void ActualizarPreferenciaNombre(
        long plantillaId,
        PreferenciaNombreCadena preferencia,
        long? vagonNombreId
    )
    {
        using var cmd = _conexion.CreateCommand();
        cmd.CommandText =
            "UPDATE modelos_cadena SET preferencia_nombre=$p,vagon_nombre_id=$v WHERE id=$id;";
        cmd.Parameters.AddWithValue("$p", (int)preferencia);
        cmd.Parameters.AddWithValue("$v", (object?)vagonNombreId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", plantillaId);
        cmd.ExecuteNonQuery();
    }

    public void RenombrarPlantilla(long id, string nombre)
    {
        using var cmd = _conexion.CreateCommand();
        cmd.CommandText = "UPDATE modelos_cadena SET nombre=$n WHERE id=$id;";
        cmd.Parameters.AddWithValue("$n", nombre);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void BorrarPlantilla(long id)
    {
        _enlaces.AnularReglasDeVagones(ObtenerVagonesPlantilla(id).Select(v => v.Id));
        using var tx = _conexion.BeginTransaction();
        Ejecutar(tx, "UPDATE cadenas SET modelo_id=NULL WHERE modelo_id=$id", ("$id", id));
        Ejecutar(
            tx,
            "UPDATE vagones_cadena SET vagon_modelo_id=NULL WHERE vagon_modelo_id IN (SELECT id FROM vagones_modelo WHERE modelo_id=$id)",
            ("$id", id)
        );
        Ejecutar(
            tx,
            "UPDATE modelos_cadena SET vagon_nombre_id=NULL WHERE id<>$id AND vagon_nombre_id IN (SELECT id FROM vagones_modelo WHERE modelo_id=$id)",
            ("$id", id)
        );
        Ejecutar(
            tx,
            "UPDATE vagones_modelo SET modelo_cadena_hija_id=NULL WHERE modelo_cadena_hija_id=$id",
            ("$id", id)
        );
        Ejecutar(tx, "UPDATE vagones_modelo SET padre_id=NULL WHERE modelo_id=$id", ("$id", id));
        Ejecutar(tx, "DELETE FROM vagones_modelo WHERE modelo_id=$id", ("$id", id));
        Ejecutar(tx, "DELETE FROM modelos_cadena WHERE id=$id", ("$id", id));
        tx.Commit();
    }

    public PlantillaVagon AgregarVagon(
        long plantillaId,
        long? padreId,
        string nombre,
        bool esMultiple,
        bool esAnexo,
        long? modeloCadenaHijaId
    )
    {
        long id = _cadenas.AgregarVagonModelo(
            plantillaId,
            padreId,
            nombre,
            esMultiple,
            esAnexo,
            modeloCadenaHijaId
        );
        return ObtenerVagon(id)!;
    }

    public void ActualizarVagon(
        long id,
        string nombre,
        bool esMultiple,
        bool esAnexo,
        long? modeloCadenaHijaId
    )
    {
        using var cmd = _conexion.CreateCommand();
        cmd.CommandText =
            "UPDATE vagones_modelo SET nombre=$n,es_multiple=$m,es_anexo=$a,modelo_cadena_hija_id=$h WHERE id=$id;";
        cmd.Parameters.AddWithValue("$n", nombre);
        cmd.Parameters.AddWithValue("$m", esMultiple ? 1 : 0);
        cmd.Parameters.AddWithValue("$a", esAnexo ? 1 : 0);
        cmd.Parameters.AddWithValue("$h", (object?)modeloCadenaHijaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void BorrarVagon(long id)
    {
        var todos = ObtenerVagonesPlantillaPorId(id);
        var borrar = Descendientes(id, todos);
        _enlaces.AnularReglasDeVagones(borrar);
        using var tx = _conexion.BeginTransaction();
        foreach (var vagon in borrar.Reverse())
        {
            Ejecutar(
                tx,
                "UPDATE vagones_cadena SET vagon_modelo_id=NULL WHERE vagon_modelo_id=$id",
                ("$id", vagon)
            );
            Ejecutar(
                tx,
                "UPDATE modelos_cadena SET vagon_nombre_id=NULL WHERE vagon_nombre_id=$id",
                ("$id", vagon)
            );
            Ejecutar(tx, "DELETE FROM vagones_modelo WHERE id=$id", ("$id", vagon));
        }
        tx.Commit();
    }

    public IReadOnlyList<PlantillaVagon> ObtenerVagonesPlantilla(long plantillaId) =>
        LeerVagones(
            "SELECT id,modelo_id,padre_id,orden,nombre,es_multiple,es_anexo,modelo_cadena_hija_id FROM vagones_modelo WHERE modelo_id=$id ORDER BY orden,id",
            plantillaId
        );

    public PlantillaVagon? ObtenerVagon(long id) =>
        ObtenerVagonesPlantillaPorId(id).FirstOrDefault(v => v.Id == id);

    private IReadOnlyList<PlantillaVagon> ObtenerVagonesPlantillaPorId(long id)
    {
        using var cmd = _conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id,modelo_id,padre_id,orden,nombre,es_multiple,es_anexo,modelo_cadena_hija_id FROM vagones_modelo WHERE modelo_id=(SELECT modelo_id FROM vagones_modelo WHERE id=$id) ORDER BY orden,id";
        cmd.Parameters.AddWithValue("$id", id);
        return LeerVagones(cmd);
    }

    public InstanciaLinea AgregarInstancia(
        long? plantillaIdOrigen,
        string? nombrePlantillaOrigen,
        string nombre,
        DateTime fechaCreacion,
        string estructuraJson,
        long? cadenaMadreId,
        long? instanciaVagonPadreId
    )
    {
        using var cmd = _conexion.CreateCommand();
        cmd.CommandText =
            "INSERT INTO cadenas(modelo_id,nombre_modelo_origen,nombre,fecha_creacion,estructura_json,cadena_madre_id,vagon_padre_id) VALUES($m,$o,$n,$f,$j,$madre,$padre) RETURNING id";
        cmd.Parameters.AddWithValue("$m", (object?)plantillaIdOrigen ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$o", (object?)nombrePlantillaOrigen ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$n", nombre);
        cmd.Parameters.AddWithValue("$f", fechaCreacion.ToString("o"));
        cmd.Parameters.AddWithValue("$j", estructuraJson);
        cmd.Parameters.AddWithValue("$madre", (object?)cadenaMadreId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$padre", (object?)instanciaVagonPadreId ?? DBNull.Value);
        long id = Convert.ToInt64(cmd.ExecuteScalar());
        EjecutarMotorEnSegundoPlano();
        return ObtenerInstancia(id)!;
    }

    public IReadOnlyList<InstanciaLinea> ObtenerInstancias() =>
        LeerInstancias(
            "SELECT id,modelo_id,nombre_modelo_origen,nombre,fecha_creacion,estructura_json,cadena_madre_id,vagon_padre_id FROM cadenas WHERE estado='activa' AND cadena_madre_id IS NULL ORDER BY fecha_creacion DESC,id DESC"
        );

    public InstanciaLinea? ObtenerInstancia(long id) =>
        LeerInstancias(
                "SELECT id,modelo_id,nombre_modelo_origen,nombre,fecha_creacion,estructura_json,cadena_madre_id,vagon_padre_id FROM cadenas WHERE id="
                    + id
                    + " AND estado='activa'"
            )
            .FirstOrDefault();

    public IReadOnlyList<InstanciaLinea> ObtenerCadenasHijas(long instanciaVagonPadreId) =>
        LeerInstancias(
            "SELECT c.id,c.modelo_id,c.nombre_modelo_origen,c.nombre,c.fecha_creacion,c.estructura_json,c.cadena_madre_id,c.vagon_padre_id FROM cadenas c WHERE c.vagon_padre_id="
                + instanciaVagonPadreId
                + " AND c.estado='activa' ORDER BY c.fecha_creacion,c.id"
        );

    public void BorrarInstancia(long id)
    {
        using var tx = _conexion.BeginTransaction();
        AnularCadenaRecursiva(tx, id);
        tx.Commit();
    }

    public InstanciaVagon AgregarVagonInstancia(
        long instanciaId,
        long? padreId,
        long? plantillaVagonId,
        string nombre,
        bool esMultiple,
        bool esAnexo
    )
    {
        using var cmd = _conexion.CreateCommand();
        cmd.CommandText =
            "INSERT INTO vagones_cadena(cadena_id,padre_id,vagon_modelo_id,orden,nombre,es_multiple,es_anexo) VALUES($c,$p,$m,(SELECT COALESCE(MAX(orden),-1)+1 FROM vagones_cadena WHERE cadena_id=$c AND padre_id IS $p),$n,$x,$a) RETURNING id";
        cmd.Parameters.AddWithValue("$c", instanciaId);
        cmd.Parameters.AddWithValue("$p", (object?)padreId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$m", (object?)plantillaVagonId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$n", nombre);
        cmd.Parameters.AddWithValue("$x", esMultiple ? 1 : 0);
        cmd.Parameters.AddWithValue("$a", esAnexo ? 1 : 0);
        return ObtenerVagonInstancia(Convert.ToInt64(cmd.ExecuteScalar()))!;
    }

    public IReadOnlyList<InstanciaVagon> ObtenerVagonesInstancia(long instanciaId) =>
        LeerVagonesInstancia(
            "SELECT v.id,v.cadena_id,v.padre_id,v.vagon_modelo_id,v.orden,v.nombre,v.es_multiple,v.es_anexo,COALESCE((SELECT e.version_id FROM enlaces_cadena e WHERE e.vagon_cadena_id=v.id AND e.estado='activo' ORDER BY e.id DESC LIMIT 1),v.version_id),COALESCE((SELECT x.ruta_observada FROM enlaces_cadena e JOIN versiones_documento x ON x.id=e.version_id WHERE e.vagon_cadena_id=v.id AND e.estado='activo' ORDER BY e.id DESC LIMIT 1),(SELECT d.ruta FROM versiones_documento x JOIN documentos d ON d.id=x.documento_id WHERE x.id=v.version_id)),(SELECT x.huella FROM versiones_documento x WHERE x.id=COALESCE((SELECT e.version_id FROM enlaces_cadena e WHERE e.vagon_cadena_id=v.id AND e.estado='activo' ORDER BY e.id DESC LIMIT 1),v.version_id)) FROM vagones_cadena v WHERE v.cadena_id="
                + instanciaId
                + " AND v.estado='activo' ORDER BY v.orden,v.id"
        );

    public InstanciaVagon? ObtenerVagonInstancia(long id) =>
        LeerVagonesInstancia(
                "SELECT v.id,v.cadena_id,v.padre_id,v.vagon_modelo_id,v.orden,v.nombre,v.es_multiple,v.es_anexo,COALESCE((SELECT e.version_id FROM enlaces_cadena e WHERE e.vagon_cadena_id=v.id AND e.estado='activo' ORDER BY e.id DESC LIMIT 1),v.version_id),COALESCE((SELECT x.ruta_observada FROM enlaces_cadena e JOIN versiones_documento x ON x.id=e.version_id WHERE e.vagon_cadena_id=v.id AND e.estado='activo' ORDER BY e.id DESC LIMIT 1),(SELECT d.ruta FROM versiones_documento x JOIN documentos d ON d.id=x.documento_id WHERE x.id=v.version_id)),(SELECT x.huella FROM versiones_documento x WHERE x.id=COALESCE((SELECT e.version_id FROM enlaces_cadena e WHERE e.vagon_cadena_id=v.id AND e.estado='activo' ORDER BY e.id DESC LIMIT 1),v.version_id)) FROM vagones_cadena v WHERE v.id="
                    + id
                    + " AND v.estado='activo'"
            )
            .FirstOrDefault();

    public void ActualizarDocumentoVagon(long id, string? rutaDocumento, string? nombreDocumento)
    {
        var vagon =
            ObtenerVagonInstancia(id)
            ?? throw new InvalidOperationException("No existe el vagón de cadena.");
        var enlaceActivo = _enlaces.HistorialEnlaces(id).LastOrDefault(e => e.Estado == "activo");
        if (rutaDocumento is null)
        {
            if (enlaceActivo is not null)
                _enlaces.DeshacerEnlace(enlaceActivo.Id);
            return;
        }
        var (_, version) = _documentos.AsegurarDocumentoYVersionVigente(rutaDocumento);
        if (enlaceActivo is not null)
            _enlaces.DeshacerEnlace(enlaceActivo.Id);
        _enlaces.CrearEnlace(id, version.Id, "manual");
        EjecutarMotorEnSegundoPlano();
    }

    private void EjecutarMotorEnSegundoPlano()
    {
        string ruta = _rutaBaseComun;
        _revisionMotor = Task.Run(() =>
        {
            using var conexion = BaseComun.Abrir(ruta);
            var motor = new MotorEnlaceAutomatico(conexion);
            try
            {
                motor.Ejecutar();
            }
            catch (Exception error)
            {
                try
                {
                    motor.RegistrarError(error);
                }
                catch { }
            }
        });
    }

    public void BorrarVagonInstancia(long id)
    {
        var todos = ObtenerVagonesInstanciaPorVagon(id);
        var borrar = DescendientesVagon(id, todos);
        using var tx = _conexion.BeginTransaction();
        foreach (long vagon in borrar)
        {
            foreach (long hija in ObtenerCadenasHijasEnTransaccion(tx, vagon))
                AnularCadenaRecursiva(tx, hija);
            Ejecutar(
                tx,
                "UPDATE enlaces_cadena SET estado='anulado',cambiada_en=$f WHERE vagon_cadena_id=$id AND estado='activo'",
                ("$f", DateTime.Now.ToString("o")),
                ("$id", vagon)
            );
            Ejecutar(
                tx,
                "UPDATE vagones_cadena SET estado='anulado',fecha_anulacion=$f WHERE id=$id",
                ("$f", DateTime.Now.ToString("o")),
                ("$id", vagon)
            );
        }
        tx.Commit();
    }

    public IReadOnlyList<EnlaceCadena> HistorialEnlaces(long vagonCadenaId) =>
        _enlaces.HistorialEnlaces(vagonCadenaId);

    private List<InstanciaVagon> ObtenerVagonesInstanciaPorVagon(long id) =>
        ObtenerVagonesInstancia(
                Convert.ToInt64(Escalar("SELECT cadena_id FROM vagones_cadena WHERE id=" + id))
            )
            .ToList();

    private long Escalar(string sql)
    {
        using var cmd = _conexion.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private void AnularCadenaRecursiva(SqliteTransaction tx, long id)
    {
        var ids = new List<long>();
        using (var cmd = _conexion.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT id FROM vagones_cadena WHERE cadena_id=$c";
            cmd.Parameters.AddWithValue("$c", id);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                ids.Add(r.GetInt64(0));
        }
        foreach (long vagon in ids)
        foreach (long hija in ObtenerCadenasHijasEnTransaccion(tx, vagon))
            AnularCadenaRecursiva(tx, hija);
        Ejecutar(
            tx,
            "UPDATE enlaces_cadena SET estado='anulado',cambiada_en=$f WHERE vagon_cadena_id IN (SELECT id FROM vagones_cadena WHERE cadena_id=$c) AND estado='activo'",
            ("$f", DateTime.Now.ToString("o")),
            ("$c", id)
        );
        Ejecutar(
            tx,
            "UPDATE vagones_cadena SET estado='anulado',fecha_anulacion=$f WHERE cadena_id=$c",
            ("$f", DateTime.Now.ToString("o")),
            ("$c", id)
        );
        Ejecutar(
            tx,
            "UPDATE cadenas SET estado='anulada',fecha_anulacion=$f WHERE id=$c",
            ("$f", DateTime.Now.ToString("o")),
            ("$c", id)
        );
    }

    private List<long> ObtenerCadenasHijasEnTransaccion(SqliteTransaction tx, long vagonId)
    {
        using var cmd = _conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM cadenas WHERE vagon_padre_id=$v AND estado='activa'";
        cmd.Parameters.AddWithValue("$v", vagonId);
        using var r = cmd.ExecuteReader();
        var ids = new List<long>();
        while (r.Read())
            ids.Add(r.GetInt64(0));
        return ids;
    }

    private IReadOnlyList<PlantillaVagon> LeerVagones(string sql, long id)
    {
        using var cmd = _conexion.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$id", id);
        return LeerVagones(cmd);
    }

    private static IReadOnlyList<PlantillaVagon> LeerVagones(SqliteCommand cmd)
    {
        using var r = cmd.ExecuteReader();
        var l = new List<PlantillaVagon>();
        while (r.Read())
            l.Add(
                new PlantillaVagon
                {
                    Id = r.GetInt64(0),
                    PlantillaId = r.GetInt64(1),
                    PadreId = NLong(r, 2),
                    Orden = r.GetInt32(3),
                    Nombre = r.GetString(4),
                    EsMultiple = r.GetInt32(5) != 0,
                    EsAnexo = r.GetInt32(6) != 0,
                    ModeloCadenaHijaId = NLong(r, 7),
                }
            );
        return l;
    }

    private IReadOnlyList<InstanciaLinea> LeerInstancias(string sql)
    {
        using var cmd = _conexion.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        var l = new List<InstanciaLinea>();
        while (r.Read())
            l.Add(
                new InstanciaLinea
                {
                    Id = r.GetInt64(0),
                    PlantillaIdOrigen = NLong(r, 1),
                    NombrePlantillaOrigen = NText(r, 2),
                    Nombre = r.GetString(3),
                    FechaCreacion = DateTime.Parse(r.GetString(4)),
                    EstructuraJson = r.GetString(5),
                    CadenaMadreId = NLong(r, 6),
                    InstanciaVagonPadreId = NLong(r, 7),
                }
            );
        return l;
    }

    private IReadOnlyList<InstanciaVagon> LeerVagonesInstancia(string sql)
    {
        using var cmd = _conexion.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        var l = new List<InstanciaVagon>();
        while (r.Read())
        {
            var ruta = NText(r, 9);
            var huella = NText(r, 10);
            var cambiado =
                ruta is not null
                && huella is not null
                && File.Exists(ruta)
                && !string.Equals(
                    Huella.Calcular(ruta),
                    huella,
                    StringComparison.OrdinalIgnoreCase
                );
            l.Add(
                new InstanciaVagon
                {
                    Id = r.GetInt64(0),
                    InstanciaId = r.GetInt64(1),
                    PadreId = NLong(r, 2),
                    PlantillaVagonId = NLong(r, 3),
                    Orden = r.GetInt32(4),
                    Nombre = r.GetString(5),
                    EsMultiple = r.GetInt32(6) != 0,
                    EsAnexo = r.GetInt32(7) != 0,
                    RutaDocumento = ruta,
                    NombreDocumento = ruta is null ? null : Path.GetFileName(ruta),
                    AvisoDocumentoModificado = cambiado,
                }
            );
        }
        return l;
    }

    private static long? NLong(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt64(i);

    private static string? NText(SqliteDataReader r, int i) =>
        r.IsDBNull(i) ? null : r.GetString(i);

    private static IReadOnlyList<long> Descendientes(long raiz, IReadOnlyList<PlantillaVagon> items)
    {
        var l = new List<long> { raiz };
        var q = new Queue<long>();
        q.Enqueue(raiz);
        while (q.Count > 0)
            foreach (var h in items.Where(v => v.PadreId == q.Dequeue()))
            {
                l.Add(h.Id);
                q.Enqueue(h.Id);
            }
        return l;
    }

    private static IReadOnlyList<long> DescendientesVagon(
        long raiz,
        IReadOnlyList<InstanciaVagon> items
    )
    {
        var l = new List<long> { raiz };
        var q = new Queue<long>();
        q.Enqueue(raiz);
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            foreach (var h in items.Where(v => v.PadreId == p))
            {
                l.Add(h.Id);
                q.Enqueue(h.Id);
            }
        }
        return l;
    }

    private void Ejecutar(
        SqliteTransaction tx,
        string sql,
        params (string Nombre, object Valor)[] parametros
    )
    {
        using var cmd = _conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var p in parametros)
            cmd.Parameters.AddWithValue(p.Nombre, p.Valor);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        try
        {
            _revisionMotor?.GetAwaiter().GetResult();
        }
        catch { }
        _conexion.Dispose();
    }
}
