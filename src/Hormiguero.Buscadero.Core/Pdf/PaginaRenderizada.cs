namespace Buscadero.Core.Pdf;

public sealed class PaginaRenderizada
{
    public required byte[] Png { get; init; }
    public required int Ancho { get; init; }
    public required int Alto { get; init; }
}
