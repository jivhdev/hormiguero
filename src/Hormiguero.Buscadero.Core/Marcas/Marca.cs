namespace Buscadero.Core.Marcas;

public enum TipoMarca
{
    Tick,
    Equis,
    Raya,
    Circulo,
    Texto,
}

public sealed class Marca
{
    public required Guid Id { get; init; }
    public required TipoMarca Tipo { get; init; }
    public required int Pagina { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Ancho { get; init; }
    public required double Alto { get; init; }
    public string? Texto { get; init; }
}
