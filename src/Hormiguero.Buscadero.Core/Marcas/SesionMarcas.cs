namespace Buscadero.Core.Marcas;

public sealed class SesionMarcas
{
    private readonly RepositorioMarcas _repositorio;
    private readonly string _rutaDocumento;
    private readonly Stack<List<Marca>> _deshacer = new();
    private readonly Stack<List<Marca>> _rehacer = new();
    private List<Marca> _actuales;

    public SesionMarcas(RepositorioMarcas repositorio, string rutaDocumento)
    {
        _repositorio = repositorio;
        _rutaDocumento = rutaDocumento;
        _actuales = repositorio.ObtenerPorDocumento(rutaDocumento).ToList();
    }

    public IReadOnlyList<Marca> Marcas => _actuales;

    public bool PuedeDeshacer => _deshacer.Count > 0;

    public bool PuedeRehacer => _rehacer.Count > 0;

    public Marca Agregar(
        TipoMarca tipo,
        int pagina,
        double x,
        double y,
        double ancho,
        double alto,
        string? texto = null
    )
    {
        var marca = new Marca
        {
            Id = Guid.NewGuid(),
            Tipo = tipo,
            Pagina = pagina,
            X = x,
            Y = y,
            Ancho = ancho,
            Alto = alto,
            Texto = texto,
        };

        Mutar(marcas => marcas.Add(marca));
        return marca;
    }

    public void Mover(Guid id, double x, double y)
    {
        Mutar(marcas =>
        {
            var indice = marcas.FindIndex(m => m.Id == id);
            if (indice < 0)
            {
                return;
            }

            marcas[indice] = new Marca
            {
                Id = marcas[indice].Id,
                Tipo = marcas[indice].Tipo,
                Pagina = marcas[indice].Pagina,
                X = x,
                Y = y,
                Ancho = marcas[indice].Ancho,
                Alto = marcas[indice].Alto,
                Texto = marcas[indice].Texto,
            };
        });
    }

    public void Quitar(Guid id) => Mutar(marcas => marcas.RemoveAll(m => m.Id == id));

    public void BorrarTodas() => Mutar(marcas => marcas.Clear());

    public bool Deshacer()
    {
        if (_deshacer.Count == 0)
        {
            return false;
        }

        _rehacer.Push(new List<Marca>(_actuales));
        _actuales = _deshacer.Pop();
        Persistir();
        return true;
    }

    public bool Rehacer()
    {
        if (_rehacer.Count == 0)
        {
            return false;
        }

        _deshacer.Push(new List<Marca>(_actuales));
        _actuales = _rehacer.Pop();
        Persistir();
        return true;
    }

    private void Mutar(Action<List<Marca>> accion)
    {
        _deshacer.Push(new List<Marca>(_actuales));
        _rehacer.Clear();
        accion(_actuales);
        Persistir();
    }

    private void Persistir() =>
        _actuales = _repositorio.ReemplazarDelDocumento(_rutaDocumento, _actuales).ToList();
}
