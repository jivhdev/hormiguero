using System.Text.Json;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Archivero.Logica;

// Carpetas de entrada vigiladas (REQ-001), guardadas en la configuración común.
public sealed class CarpetasEntrada(SqliteConnection conexion)
{
    private const string Clave = "archivero.entradas";

    private readonly Configuracion configuracion = new(conexion);

    public IReadOnlyList<string> Listar() =>
        configuracion.Leer(Clave) is { Length: > 0 } json
            ? JsonSerializer.Deserialize<string[]>(json) ?? []
            : [];

    public void Guardar(IEnumerable<string> carpetas) =>
        configuracion.Guardar(
            Clave,
            JsonSerializer.Serialize(
                carpetas
                    .Select(c => Path.TrimEndingDirectorySeparator(Path.GetFullPath(c)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            )
        );

    // Un destino dentro de una carpeta vigilada haría que el documento se moviera
    // en círculo (REQ-004, "¿Qué tal si...?").
    public bool ContieneA(string ruta)
    {
        string completa = Path.GetFullPath(ruta);
        return Listar()
            .Any(entrada =>
                string.Equals(
                    Path.TrimEndingDirectorySeparator(completa),
                    entrada,
                    StringComparison.OrdinalIgnoreCase
                )
                || completa.StartsWith(
                    entrada + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase
                )
            );
    }
}
