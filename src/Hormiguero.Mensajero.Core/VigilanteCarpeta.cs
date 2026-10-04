namespace Hormiguero.Mensajero.Core;

public sealed class VigilanteCarpeta : IDisposable
{
    private readonly object cerrojo = new();
    private readonly Dictionary<string, DateTime> procesados = new(
        StringComparer.OrdinalIgnoreCase
    );
    private FileSystemWatcher? vigilante;
    private bool activo;

    public event Action<string>? NuevoPdf;
    public event Action<string>? Error;

    public bool EstaActivo
    {
        get
        {
            lock (cerrojo)
            {
                return activo;
            }
        }
    }

    public bool Iniciar(string carpeta)
    {
        Detener();
        if (!Directory.Exists(carpeta))
        {
            Error?.Invoke($"La carpeta no existe: {carpeta}");
            return false;
        }

        try
        {
            var nuevo = new FileSystemWatcher(carpeta)
            {
                IncludeSubdirectories = false,
                NotifyFilter =
                    NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
                EnableRaisingEvents = false,
            };
            nuevo.Created += AlCambiar;
            nuevo.Changed += AlCambiar;
            nuevo.Error += AlError;
            nuevo.EnableRaisingEvents = true;
            lock (cerrojo)
            {
                vigilante = nuevo;
                activo = true;
            }
            return true;
        }
        catch (Exception excepcion)
        {
            Error?.Invoke($"No se pudo iniciar vigilancia: {excepcion.Message}");
            return false;
        }
    }

    public void Detener()
    {
        FileSystemWatcher? anterior;
        lock (cerrojo)
        {
            anterior = vigilante;
            vigilante = null;
            activo = false;
        }
        if (anterior is not null)
        {
            anterior.EnableRaisingEvents = false;
            anterior.Created -= AlCambiar;
            anterior.Changed -= AlCambiar;
            anterior.Error -= AlError;
            anterior.Dispose();
        }
    }

    public void Dispose() => Detener();

    private void AlCambiar(object sender, FileSystemEventArgs e)
    {
        if (
            !string.Equals(
                Path.GetExtension(e.FullPath),
                ".pdf",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        lock (cerrojo)
        {
            DateTime ahora = DateTime.UtcNow;
            foreach (
                string ruta in procesados
                    .Where(par => ahora - par.Value >= TimeSpan.FromSeconds(2))
                    .Select(par => par.Key)
                    .ToArray()
            )
            {
                procesados.Remove(ruta);
            }
            if (
                procesados.TryGetValue(e.FullPath, out DateTime ultimo)
                && ahora - ultimo < TimeSpan.FromSeconds(2)
            )
            {
                return;
            }
            procesados[e.FullPath] = ahora;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(300).ConfigureAwait(false);
            try
            {
                var info = new FileInfo(e.FullPath);
                if (info.Exists && info.Length > 0)
                {
                    NuevoPdf?.Invoke(e.FullPath);
                }
                else
                {
                    Error?.Invoke($"Archivo inválido o vacío: {Path.GetFileName(e.FullPath)}");
                }
            }
            catch (Exception excepcion)
            {
                Error?.Invoke(
                    $"Error al procesar {Path.GetFileName(e.FullPath)}: {excepcion.Message}"
                );
            }
        });
    }

    private void AlError(object sender, ErrorEventArgs e) =>
        Error?.Invoke($"Error de vigilancia: {e.GetException().Message}");
}
