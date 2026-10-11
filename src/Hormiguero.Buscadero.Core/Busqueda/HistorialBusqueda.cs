namespace Buscadero.Core.Busqueda;

public sealed record EntradaHistorialBusqueda(
    string Numero,
    AlcanceBusqueda Alcance,
    string Carpeta,
    ModoBusqueda Modo,
    IReadOnlyList<ResultadoBusqueda> Resultados,
    string? Documento,
    int Pagina,
    double Zoom
);

public sealed class HistorialBusqueda
{
    public const int MaximoEntradas = 30;

    private readonly List<EntradaHistorialBusqueda> _entradas = [];
    private int _indiceActual = -1;

    public bool PuedeIrAtras => _indiceActual > 0;
    public bool PuedeIrAdelante => _indiceActual >= 0 && _indiceActual < _entradas.Count - 1;
    public EntradaHistorialBusqueda? Actual => _indiceActual >= 0 ? _entradas[_indiceActual] : null;

    public void Agregar(EntradaHistorialBusqueda entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        if (_indiceActual + 1 < _entradas.Count)
        {
            _entradas.RemoveRange(_indiceActual + 1, _entradas.Count - _indiceActual - 1);
        }

        _entradas.Add(entrada);
        if (_entradas.Count > MaximoEntradas)
        {
            _entradas.RemoveAt(0);
        }

        _indiceActual = _entradas.Count - 1;
    }

    public void ActualizarActual(EntradaHistorialBusqueda entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        if (_indiceActual >= 0)
        {
            _entradas[_indiceActual] = entrada;
        }
    }

    public EntradaHistorialBusqueda? Atras()
    {
        if (!PuedeIrAtras)
        {
            return null;
        }

        return _entradas[--_indiceActual];
    }

    public EntradaHistorialBusqueda? Adelante()
    {
        if (!PuedeIrAdelante)
        {
            return null;
        }

        return _entradas[++_indiceActual];
    }
}
