namespace Buscadero.Core.Indexado;

public sealed class ProgresoIndexado
{
    public required string CarpetaActual { get; init; }
    public required int CarpetasVisitadas { get; init; }
    public required int ArchivosIndexados { get; init; }
    public required bool Omitida { get; init; }
}

public sealed class ResumenIndexado
{
    public required int CarpetasVisitadas { get; init; }
    public required int ArchivosIndexados { get; init; }
}
