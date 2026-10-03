using System.Text.Json;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Buscadero.Logica;

public sealed class CarpetasConfiguradas
{
    private const string Clave = "buscadero.carpetas";

    private readonly Configuracion configuracion;

    public CarpetasConfiguradas(SqliteConnection conexion) =>
        configuracion = new Configuracion(conexion);

    public bool HayAlguna => Listar().Count > 0;

    public IReadOnlyList<string> Listar()
    {
        if (configuracion.Leer(Clave) is not string json || json.Length == 0)
        {
            return [];
        }

        return JsonSerializer.Deserialize<string[]>(json) ?? [];
    }

    public bool Agregar(string ruta)
    {
        var carpetas = Listar().ToList();
        string normalizada = Normalizar(ruta);

        if (
            carpetas.Any(otra =>
                string.Equals(otra, normalizada, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return false;
        }

        carpetas.Add(normalizada);
        Guardar(carpetas);
        return true;
    }

    public bool Quitar(string ruta)
    {
        var carpetas = Listar().ToList();
        string normalizada = Normalizar(ruta);

        if (
            carpetas.RemoveAll(otra =>
                string.Equals(otra, normalizada, StringComparison.OrdinalIgnoreCase)
            ) == 0
        )
        {
            return false;
        }

        Guardar(carpetas);
        return true;
    }

    public static async Task<bool> EstaDisponibleAsync(string ruta, TimeSpan limite)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limite, TimeSpan.Zero);

        // Una carpeta de red que no responde deja Directory.Exists bloqueado; se
        // revisa en otro hilo y, si no contesta dentro del límite, se da por no
        // disponible en vez de colgar la app.
        var tarea = Task.Run(() => Directory.Exists(ruta));
        var primera = await Task.WhenAny(tarea, Task.Delay(limite));
        return ReferenceEquals(primera, tarea) && await tarea;
    }

    private static string Normalizar(string ruta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(ruta));
    }

    private void Guardar(IEnumerable<string> carpetas) =>
        configuracion.Guardar(Clave, JsonSerializer.Serialize(carpetas.ToArray()));
}
