using Hormiguero.Nucleo.Datos;

namespace Buscadero.Core.Marcas;

public sealed class RepositorioMarcas : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conexion;
    private readonly RepositorioDocumentosDatos _documentos;
    private readonly Dictionary<string, long> _versiones = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, long> _ids = [];

    public RepositorioMarcas(string rutaBaseComun)
    {
        _conexion = BaseComun.Abrir(rutaBaseComun);
        _documentos = new RepositorioDocumentosDatos(_conexion);
    }

    // Solo lee: abrir un PDF no lo registra en la base común (eso pasa recién al guardar una
    // marca). Se busca la versión vigente con la misma huella, prefiriendo la de esta ruta.
    public IReadOnlyList<Marca> ObtenerPorDocumento(string rutaDocumento)
    {
        string ruta = Path.GetFullPath(rutaDocumento);
        if (!File.Exists(ruta))
            return [];
        string huella = Hormiguero.Nucleo.Utilidades.Huella.Calcular(ruta);
        var vigentes = _documentos
            .BuscarVersiones(huella)
            .Where(version => version.Estado == "vigente")
            .ToList();
        var version =
            vigentes.FirstOrDefault(v =>
                string.Equals(v.RutaObservada, ruta, StringComparison.OrdinalIgnoreCase)
            ) ?? vigentes.FirstOrDefault();
        if (version is null)
            return [];
        _versiones[ruta] = version.Id;
        return LeerMarcas(version.Id);
    }

    private IReadOnlyList<Marca> LeerMarcas(long versionId)
    {
        var marcas = _documentos.BuscarMarcas(versionId);
        foreach (var marca in marcas)
            _ids[Guid.NewGuid()] = marca.Id;
        return marcas
            .Select(marca =>
            {
                var id = _ids.First(par => par.Value == marca.Id).Key;
                return Convertir(marca, id);
            })
            .ToList();
    }

    public IReadOnlyList<Marca> ReemplazarDelDocumento(
        string rutaDocumento,
        IEnumerable<Marca> marcas
    )
    {
        string ruta = Path.GetFullPath(rutaDocumento);
        // Siempre se confirma la versión vigente al guardar: el PDF pudo cambiar de contenido
        // desde que se abrió (entonces las marcas van a la versión nueva y las viejas quedan
        // como historial). Guardar marcas es poco frecuente; el costo de la huella no pesa.
        var (_, version) = _documentos.AsegurarDocumentoYVersionVigente(ruta);
        long versionId = version.Id;
        _versiones[ruta] = versionId;
        var recibidas = marcas.ToList();
        var seleccionadas = recibidas;
        var versionadas = seleccionadas
            .Select(marca => new MarcaVersion(
                _ids.GetValueOrDefault(marca.Id),
                versionId,
                marca.Tipo.ToString(),
                marca.Pagina,
                marca.X,
                marca.Y,
                marca.Ancho,
                marca.Alto,
                marca.Texto,
                DateTime.Now,
                "activa"
            ))
            .ToList();
        var guardadas = _documentos.SincronizarMarcas(versionId, versionadas);
        for (int i = 0; i < guardadas.Count; i++)
            _ids[seleccionadas[i].Id] = guardadas[i].Id;
        return guardadas.Select((marca, i) => Convertir(marca, seleccionadas[i].Id)).ToList();
    }

    public void Dispose() => _conexion.Dispose();

    private static Marca Convertir(MarcaVersion marca, Guid id) =>
        new()
        {
            Id = id,
            Pagina = marca.Pagina,
            Tipo = Enum.Parse<TipoMarca>(marca.Tipo),
            X = marca.X,
            Y = marca.Y,
            Ancho = marca.Ancho,
            Alto = marca.Alto,
            Texto = marca.Texto,
        };
}
