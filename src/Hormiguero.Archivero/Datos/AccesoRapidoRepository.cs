using System.Text.Json;

namespace Archivero.Datos;

/// <summary>
/// Accesos rápidos del Paso 3 (Caso-3, punto 3a): los tipos de organización que el usuario
/// dejó enlazados como favoritos, en orden. Sin máximo (decisión de Javier). Se guardan como
/// JSON en la tabla Configuracion (clave/valor).
/// </summary>
public class AccesoRapidoRepository
{
    private const string Clave = "AccesosRapidosOrganizacion";
    private readonly ConfiguracionRepository _configuracion = new();

    public List<FormatoCarpeta> Obtener()
    {
        var json = _configuracion.Obtener(Clave);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)?
                .Select(nombre => Enum.TryParse<FormatoCarpeta>(nombre, out var formato) ? formato : (FormatoCarpeta?)null)
                .Where(formato => formato is not null)
                .Select(formato => formato!.Value)
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public void Guardar(IEnumerable<FormatoCarpeta> accesos) =>
        _configuracion.Guardar(Clave, JsonSerializer.Serialize(accesos.Select(a => a.ToString()).ToList()));
}
