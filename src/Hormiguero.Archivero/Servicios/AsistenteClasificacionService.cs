using Hormiguero.Nucleo.Datos;

namespace Archivero.Servicios;

public static class AsistenteClasificacionService
{
    public static string TextoBotonOtrosDatos(bool hayDatosMarcados) =>
        hayDatosMarcados ? "Siguiente" : "Saltar";

    public static IReadOnlyList<string> Categorias =>
        DiccionarioDatosEnlazantes
            .Todos.Select(d => d.Grupo)
            .Distinct(StringComparer.CurrentCulture)
            .ToList();

    public static IReadOnlyList<DatoEnlazante> DocumentosDeCategoria(string categoria) =>
        DiccionarioDatosEnlazantes
            .Todos.Where(d => d.Grupo == categoria && !DiccionarioDatosEnlazantes.EsParte(d.Id))
            .ToList();

    public static string NombreDocumento(string datoId) =>
        DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == datoId).EtiquetaTipo;

    public static string NombreEstandar(string datoId, string? emisor) =>
        string.IsNullOrWhiteSpace(emisor)
            ? string.Empty
            : DiccionarioDatosEnlazantes.NombreEstandar(datoId, emisor);

    public static string? ValidarDocumento(string? tipo) =>
        string.IsNullOrWhiteSpace(tipo) ? "Elige el documento." : null;

    public static string? ValidarEmisor(string? emisor, bool emisorMarcado) =>
        string.IsNullOrWhiteSpace(emisor) ? "Escribe o elige quién emitió el documento."
        : !emisorMarcado ? "Marca en el PDF dónde aparece el emisor."
        : null;

    public static string? ValidarNumero(string? valor, bool zonaMarcada, bool sinNumero) =>
        zonaMarcada && (sinNumero || !string.IsNullOrWhiteSpace(valor)) ? null
        : sinNumero
            ? "Marca en el PDF la zona donde aparecería el número, aunque este documento no lo traiga."
        : "Marca el número en el PDF o indica que este documento no trae número.";

    public static string GrupoDocumento(string categoria) =>
        categoria.Contains("propia", StringComparison.CurrentCultureIgnoreCase)
        || categoria.Contains("propias", StringComparison.CurrentCultureIgnoreCase)
            ? "Emitido"
        : categoria.Contains("cliente", StringComparison.CurrentCultureIgnoreCase)
        || categoria.Contains("proveedor", StringComparison.CurrentCultureIgnoreCase)
            ? "Recibido"
        : string.Empty;
}
