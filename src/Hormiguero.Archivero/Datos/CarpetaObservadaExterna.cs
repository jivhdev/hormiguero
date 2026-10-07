using System.IO;
using System.Text.Json;

namespace Archivero.Datos;

public sealed record CarpetaObservadaExterna(
    Guid Id,
    string Nombre,
    string Ruta,
    bool IncluirSubcarpetas,
    bool Activa,
    // Solo se imprimen automáticamente los PDF que llegan después de agregar la carpeta,
    // para no imprimir de golpe todo lo que ya estaba ahí.
    DateTime? Agregada = null
);

public sealed record DocumentoObservadoReciente(
    DateTime Fecha,
    string Ruta,
    string? Tipo,
    string? Emisor,
    string? Cadena,
    string? Motivo
)
{
    public string Resumen =>
        Motivo is not null
            ? $"{Fecha:HH:mm:ss} — {Path.GetFileName(Ruta)} — {Motivo}"
            : $"{Fecha:HH:mm:ss} — {Path.GetFileName(Ruta)} — {Tipo} · {Emisor}{(Cadena is null ? "" : $" — {Cadena}")}";
}

public sealed class CarpetasObservadasRepository(ConfiguracionRepository? configuracion = null)
{
    private readonly ConfiguracionRepository _configuracion = configuracion ?? new();
    private const string ClaveCarpetas = "carpetas.observadas";
    private const string ClaveActividad = "carpetas.observadas.actividad";
    private const string ClaveHuellas = "carpetas.observadas.huellas";

    public IReadOnlyList<CarpetaObservadaExterna> Leer()
    {
        try
        {
            return JsonSerializer.Deserialize<List<CarpetaObservadaExterna>>(
                    _configuracion.Obtener(ClaveCarpetas) ?? "[]"
                ) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public void Guardar(IReadOnlyList<CarpetaObservadaExterna> carpetas) =>
        _configuracion.Guardar(ClaveCarpetas, JsonSerializer.Serialize(carpetas));

    public IReadOnlyList<DocumentoObservadoReciente> LeerActividad()
    {
        try
        {
            return JsonSerializer.Deserialize<List<DocumentoObservadoReciente>>(
                    _configuracion.Obtener(ClaveActividad) ?? "[]"
                ) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public void Registrar(DocumentoObservadoReciente documento)
    {
        var actividad = LeerActividad()
            .Where(a => a.Ruta != documento.Ruta)
            .Prepend(documento)
            .Take(30)
            .ToList();
        _configuracion.Guardar(ClaveActividad, JsonSerializer.Serialize(actividad));
    }

    public string? LeerHuella(string ruta)
    {
        try
        {
            return (
                JsonSerializer.Deserialize<Dictionary<string, string>>(
                    _configuracion.Obtener(ClaveHuellas) ?? "{}"
                ) ?? []
            ).GetValueOrDefault(Path.GetFullPath(ruta));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void GuardarHuella(string ruta, string huella)
    {
        Dictionary<string, string> huellas;
        try
        {
            huellas =
                JsonSerializer.Deserialize<Dictionary<string, string>>(
                    _configuracion.Obtener(ClaveHuellas) ?? "{}"
                ) ?? [];
        }
        catch (JsonException)
        {
            huellas = [];
        }
        huellas[Path.GetFullPath(ruta)] = huella;
        _configuracion.Guardar(ClaveHuellas, JsonSerializer.Serialize(huellas));
    }
}
