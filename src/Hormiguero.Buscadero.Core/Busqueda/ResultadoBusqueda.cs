namespace Buscadero.Core.Busqueda;

public sealed class ResultadoBusqueda
{
    public required string Ruta { get; init; }
    public required string Nombre { get; init; }
    public required string Carpeta { get; init; }
    public required DateTime FechaModificacion { get; init; }
}
