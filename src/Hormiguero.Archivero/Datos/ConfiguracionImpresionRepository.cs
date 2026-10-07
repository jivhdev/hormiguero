using System.Text.Json;

namespace Archivero.Datos;

public sealed class ConfiguracionImpresionRepository
{
    private readonly ConfiguracionRepository _configuracion = new();

    public (ModoImpresion Modo, string? Impresora) Leer(int configuracionId)
    {
        try
        {
            string? json = _configuracion.Obtener(Clave(configuracionId));
            if (json is null)
                return (ModoImpresion.No, null);
            var opcion = JsonSerializer.Deserialize<OpcionImpresion>(json);
            return (
                opcion is not null && Enum.IsDefined(opcion.Modo) ? opcion.Modo : ModoImpresion.No,
                opcion?.Impresora
            );
        }
        catch (JsonException)
        {
            return (ModoImpresion.No, null);
        }
    }

    public void Guardar(int configuracionId, ModoImpresion modo, string? impresora)
    {
        _configuracion.Guardar(
            Clave(configuracionId),
            JsonSerializer.Serialize(new OpcionImpresion(modo, impresora))
        );
    }

    private static string Clave(int id) => $"impresion.tipo.{id}";

    private sealed record OpcionImpresion(ModoImpresion Modo, string? Impresora);
}
