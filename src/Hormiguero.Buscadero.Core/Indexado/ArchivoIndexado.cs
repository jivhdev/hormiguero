namespace Buscadero.Core.Indexado;

public sealed class ArchivoIndexado
{
    public long Id { get; init; }
    public required string Ruta { get; init; }
    public required string Nombre { get; init; }
    public required string CarpetaContenedora { get; init; }
    public required long FechaModificacion { get; init; }
}
