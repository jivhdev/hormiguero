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
        return _cadenas.BuscarCadenasSimples(consulta);
    }

    public IReadOnlyList<DocumentoDisponibleCadena> BuscarDocumentos(
        string consulta,
        int maximo = 50
    )
    {
        return _cadenas
            .BuscarVersionesDocumento(consulta, maximo)
            .Select(v => new DocumentoDisponibleCadena(
                v.VersionId,
                v.Ruta,
                v.Nombre,
                v.Tipo,
                v.Emisor,
                v.Fecha,
                v.Numero
            ))
            .ToArray();
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
        return _enlaces
            .ListarDudososCadenasSimples()
            .Select(e => new DudosoCadenaSimple(
                e.EnlaceId,
                e.Motivo,
                e.VersionId,
                e.Ruta,
                e.Nombre
            ))
            .ToArray();
    }

    public void VincularDudosoA(long enlaceId, long cadenaId)
    {
        var dudoso = _enlaces.ObtenerDocumentoDudosoCadenaSimple(enlaceId);
        if (dudoso is null)
            throw new InvalidOperationException("La sugerencia ya no está disponible.");
        if (!_enlaces.RechazarDudoso(enlaceId))
            throw new InvalidOperationException("La sugerencia ya fue atendida.");
        _cadenas.QuitarDocumentoCadena(dudoso.VagonId);
        long vagonNuevo = _cadenas.AgregarDocumentoCadena(
            cadenaId,
            dudoso.VersionId,
            dudoso.Nombre
        );
        _enlaces.CrearEnlace(vagonNuevo, dudoso.VersionId, "manual");
    }

    public bool NoCorrespondeDudoso(long enlaceId)
    {
        long? vagonId = _enlaces.ObtenerVagonDudosoCadenaSimple(enlaceId);
        if (vagonId is null || !_enlaces.RechazarDudoso(enlaceId))
            return false;
        _cadenas.QuitarDocumentoCadena(vagonId.Value);
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
