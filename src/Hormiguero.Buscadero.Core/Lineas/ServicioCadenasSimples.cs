using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Buscadero.Core.Lineas;

public sealed record DocumentoDisponibleCadena(
    long VersionId,
    string Ruta,
    string Nombre,
    string Tipo,
    string Emisor,
    DateTime Fecha,
    string Numero
);

public sealed record DudosoCadenaSimple(
    long EnlaceId,
    string Motivo,
    long VersionId,
    string Ruta,
    string Nombre
);

public sealed class ServicioCadenasSimples(SqliteConnection conexion)
{
    private readonly RepositorioCadenas _cadenas = new(conexion);
    private readonly RepositorioReglasYEnlaces _enlaces = new(conexion);
    private readonly RepositorioDatosEnlazantes _datos = new(conexion);

    public Task? UltimaRevisionEnlaces { get; private set; }
    public event Action<string>? RevisionEnlacesFallida;

    public long CrearCadena(string nombre)
    {
        long id = _cadenas.CrearCadenaSimple(nombre, DateTime.Now);
        ProgramarRevision();
        return id;
    }

    public IReadOnlyList<Cadena> ListarCadenas() => _cadenas.ListarCadenasSimples();

    public IReadOnlyList<Cadena> BuscarCadenas(string consulta)
    {
        if (string.IsNullOrWhiteSpace(consulta))
            return ListarCadenas();
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT DISTINCT c.id FROM cadenas c LEFT JOIN vagones_cadena v ON v.cadena_id=c.id AND v.estado='activo' LEFT JOIN enlaces_cadena e ON e.vagon_cadena_id=v.id AND e.estado='activo' LEFT JOIN versiones_documento ver ON ver.id=e.version_id LEFT JOIN documentos d ON d.id=ver.documento_id LEFT JOIN numeros_documento n ON n.documento_id=d.id WHERE c.modelo_id IS NULL AND c.estado='activa' AND (c.nombre LIKE $q OR n.numero LIKE $q) ORDER BY c.id;";
        cmd.Parameters.AddWithValue("$q", $"%{consulta.Trim()}%");
        using var r = cmd.ExecuteReader();
        var ids = new List<long>();
        while (r.Read())
            ids.Add(r.GetInt64(0));
        return ListarCadenas().Where(c => ids.Contains(c.Id)).ToArray();
    }

    public IReadOnlyList<DocumentoDisponibleCadena> BuscarDocumentos(
        string consulta,
        int maximo = 50
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consulta);
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT DISTINCT ver.id,d.ruta,d.nombre,COALESCE(i.tipo,''),COALESCE(i.emisor,''),ver.registrada_en,COALESCE(n.numero,'') FROM versiones_documento ver JOIN documentos d ON d.id=ver.documento_id LEFT JOIN identificaciones i ON i.id=(SELECT f.identificacion_id FROM valores_documento x JOIN campos_documento f ON f.id=x.campo_id WHERE x.version_id=ver.id AND x.estado='vigente' LIMIT 1) LEFT JOIN numeros_documento n ON n.documento_id=d.id WHERE ver.estado='vigente' AND d.estado_baja='activo' AND (d.nombre LIKE $q OR n.numero LIKE $q OR EXISTS(SELECT 1 FROM valores_documento x WHERE x.version_id=ver.id AND x.estado='vigente' AND x.valor_original LIKE $q)) ORDER BY ver.id DESC LIMIT $limite;";
        cmd.Parameters.AddWithValue("$q", $"%{consulta.Trim()}%");
        cmd.Parameters.AddWithValue("$limite", Math.Clamp(maximo, 1, 200));
        using var r = cmd.ExecuteReader();
        var resultado = new List<DocumentoDisponibleCadena>();
        while (r.Read())
            resultado.Add(
                new(
                    r.GetInt64(0),
                    r.GetString(1),
                    r.GetString(2),
                    r.GetString(3),
                    r.GetString(4),
                    DateTime.Parse(r.GetString(5)),
                    r.GetString(6)
                )
            );
        return resultado;
    }

    public IReadOnlyList<VagonCadena> Documentos(long cadenaId) =>
        _cadenas.ListarDocumentosCadena(cadenaId);

    public void Renombrar(long cadenaId, string nombre)
    {
        _cadenas.RenombrarCadena(cadenaId, nombre);
        ProgramarRevision();
    }

    public long AgregarDocumento(long cadenaId, long versionId, string nombre)
    {
        long vagon = _cadenas.AgregarDocumentoCadena(cadenaId, versionId, nombre);
        _enlaces.CrearEnlace(vagon, versionId, "manual");
        ProgramarRevision();
        return vagon;
    }

    public void QuitarDocumento(long vagonId)
    {
        _cadenas.QuitarDocumentoCadena(vagonId);
        ProgramarRevision();
    }

    public void MoverDocumento(long vagonId, int desplazamiento)
    {
        _cadenas.ReordenarDocumentoCadena(vagonId, desplazamiento);
        ProgramarRevision();
    }

    public IReadOnlyList<CoincidenciasPorDato> Sugerir(long versionId) =>
        _datos.SugerirParaDocumento(versionId);

    public IReadOnlyList<CoincidenciasPorDato> Sugerir(string datoId, string valor) =>
        _datos.SugerirPorDato(datoId, valor);

    public IReadOnlyList<EnlaceCadena> Dudosos() => _enlaces.ListarDudosos();

    public IReadOnlyList<DudosoCadenaSimple> DudososCadenasSimples()
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT e.id,e.motivo,e.version_id,d.ruta,d.nombre FROM enlaces_cadena e JOIN vagones_cadena v ON v.id=e.vagon_cadena_id JOIN cadenas c ON c.id=v.cadena_id JOIN versiones_documento ver ON ver.id=e.version_id JOIN documentos d ON d.id=ver.documento_id WHERE e.estado='dudoso' AND c.modelo_id IS NULL ORDER BY e.id;";
        using var r = cmd.ExecuteReader();
        var resultado = new List<DudosoCadenaSimple>();
        while (r.Read())
            resultado.Add(
                new(
                    r.GetInt64(0),
                    r.IsDBNull(1) ? "Revisar coincidencia" : r.GetString(1),
                    r.GetInt64(2),
                    r.GetString(3),
                    r.GetString(4)
                )
            );
        return resultado;
    }

    public void VincularDudosoA(long enlaceId, long cadenaId)
    {
        using var leer = conexion.CreateCommand();
        leer.CommandText =
            "SELECT e.vagon_cadena_id,e.version_id,d.nombre FROM enlaces_cadena e JOIN vagones_cadena v ON v.id=e.vagon_cadena_id JOIN cadenas c ON c.id=v.cadena_id JOIN versiones_documento ver ON ver.id=e.version_id JOIN documentos d ON d.id=ver.documento_id WHERE e.id=$e AND e.estado='dudoso' AND c.modelo_id IS NULL;";
        leer.Parameters.AddWithValue("$e", enlaceId);
        using var r = leer.ExecuteReader();
        if (!r.Read())
            throw new InvalidOperationException("La sugerencia ya no está disponible.");
        long vagonAnterior = r.GetInt64(0),
            version = r.GetInt64(1);
        string nombre = r.GetString(2);
        r.Close();
        if (!_enlaces.RechazarDudoso(enlaceId))
            throw new InvalidOperationException("La sugerencia ya fue atendida.");
        _cadenas.QuitarDocumentoCadena(vagonAnterior);
        long vagonNuevo = _cadenas.AgregarDocumentoCadena(cadenaId, version, nombre);
        _enlaces.CrearEnlace(vagonNuevo, version, "manual");
    }

    public bool NoCorrespondeDudoso(long enlaceId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT vagon_cadena_id FROM enlaces_cadena WHERE id=$id AND estado='dudoso';";
        cmd.Parameters.AddWithValue("$id", enlaceId);
        object? id = cmd.ExecuteScalar();
        if (id is null || !_enlaces.RechazarDudoso(enlaceId))
            return false;
        _cadenas.QuitarDocumentoCadena(Convert.ToInt64(id));
        return true;
    }

    public IReadOnlyList<EnlaceCadena> Historial(long vagonId) =>
        _enlaces.HistorialEnlaces(vagonId);

    public bool Deshacer(long enlaceId) => _enlaces.DeshacerEnlace(enlaceId);

    private void ProgramarRevision()
    {
        if (conexion.DataSource == ":memory:")
            return;
        string ruta = conexion.DataSource;
        UltimaRevisionEnlaces = Task.Run(() =>
        {
            using var baseComun = BaseComun.Abrir(ruta);
            var motor = new MotorCadenasSimples(baseComun);
            try
            {
                motor.Procesar();
            }
            catch (Exception error)
            {
                try
                {
                    motor.RegistrarError(error, null);
                }
                catch { }
                RevisionEnlacesFallida?.Invoke(
                    $"No se pudieron revisar las cadenas: {error.Message}"
                );
            }
        });
    }
}
