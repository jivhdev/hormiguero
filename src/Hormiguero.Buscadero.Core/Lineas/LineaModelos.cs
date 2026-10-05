namespace Buscadero.Core.Lineas;

/// <summary>
/// Como se nombran las cadenas creadas con un modelo (Caso-12). "Personalizado" existe
/// en la interfaz pero todavia no tiene mecanismo propio - se comporta como Generico
/// hasta que se defina en un Caso futuro.
/// </summary>
public enum PreferenciaNombreCadena
{
    Generico,
    PorDocumento,
    Personalizado,
}

public sealed class PlantillaLinea
{
    public long Id { get; init; }
    public required string Nombre { get; init; }
    public required DateTime FechaCreacion { get; init; }
    public bool EsModeloHijo { get; init; }
    public PreferenciaNombreCadena PreferenciaNombre { get; init; } =
        PreferenciaNombreCadena.Generico;
    public long? VagonNombreId { get; init; }
}

public sealed class PlantillaVagon
{
    public long Id { get; init; }
    public required long PlantillaId { get; init; }
    public long? PadreId { get; init; }
    public required int Orden { get; init; }
    public required string Nombre { get; init; }
    public required bool EsMultiple { get; init; }
    public required bool EsAnexo { get; init; }
    public long? ModeloCadenaHijaId { get; init; }
}

public sealed class NodoPlantilla
{
    public required PlantillaVagon Vagon { get; init; }
    public required IReadOnlyList<NodoPlantilla> Hijos { get; init; }
}

public sealed class InstanciaLinea
{
    public long Id { get; init; }
    public long? PlantillaIdOrigen { get; init; }
    public string? NombrePlantillaOrigen { get; init; }
    public required string Nombre { get; init; }
    public required DateTime FechaCreacion { get; init; }
    public required string EstructuraJson { get; init; }
    public long? CadenaMadreId { get; init; }
    public long? InstanciaVagonPadreId { get; init; }
}

public sealed class InstanciaVagon
{
    public long Id { get; init; }
    public required long InstanciaId { get; init; }
    public long? PadreId { get; init; }
    public long? PlantillaVagonId { get; init; }
    public required int Orden { get; init; }
    public required string Nombre { get; init; }
    public required bool EsMultiple { get; init; }
    public required bool EsAnexo { get; init; }
    public string? RutaDocumento { get; init; }
    public string? NombreDocumento { get; init; }
    public bool AvisoDocumentoModificado { get; init; }
}

public sealed class NodoInstancia
{
    public required InstanciaVagon Vagon { get; init; }
    public required IReadOnlyList<NodoInstancia> Hijos { get; init; }
}

public sealed class CoincidenciaCadena
{
    public required InstanciaLinea CadenaRaiz { get; init; }
    public required InstanciaVagon Documento { get; init; }
}

public sealed record OpcionReglaVagon(
    long Id,
    string Nombre,
    IReadOnlyList<OpcionCampoRegla> Campos
);

public sealed record OpcionCampoRegla(long Id, string Nombre, string? Configuracion = null)
{
    public string Etiqueta => Configuracion is null ? Nombre : $"{Nombre} ({Configuracion})";
}

public sealed record ReglaVagonConfigurada(
    long VagonModeloId,
    long IdentificacionId,
    long CampoOrigenId,
    long VagonComparacionId,
    long CampoComparacionId,
    bool IgnorarEspacios = true,
    bool IgnorarGuiones = false,
    bool IgnorarCerosIniciales = false,
    int LargoMinimo = 6
);

public sealed record DudosoVagon(
    long Id,
    string NombreDocumento,
    string NombreVagon,
    string? ValorPropuesto,
    string? ValorComparado,
    string Motivo,
    string? RutaDocumento,
    string? NombreDocumentoComparado
);

public static class TextoReglaVagon
{
    public static string CrearFrase(
        string nombreVagon,
        string configuracion,
        string nombreCampo,
        string nombreVagonComparacion,
        string nombreCampoComparacion
    ) =>
        $"Completar {nombreVagon} con documentos de {configuracion} cuando {nombreCampo} sea igual a {nombreCampoComparacion} de {nombreVagonComparacion}";
}

public sealed class NodoEstructura
{
    public long PlantillaVagonId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool EsMultiple { get; set; }
    public bool EsAnexo { get; set; }
    public int Orden { get; set; }
    public long? ModeloCadenaHijaId { get; set; }
    public string? NombreModeloCadenaHija { get; set; }
    public List<NodoEstructura> Hijos { get; set; } = new();
}
