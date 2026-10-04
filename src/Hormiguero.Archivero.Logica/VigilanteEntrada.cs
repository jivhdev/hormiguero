namespace Hormiguero.Archivero.Logica;

public enum EstadoCarpeta
{
    Vigilando,
    SinAvisos,
    NoDisponible,
}

// Vigila las carpetas de entrada como vecino silencioso (D-52, REQ-001): usa los
// avisos de cambio de Windows; si fallan, revisa solo los archivos nuevos cada
// cierto tiempo; si la carpeta no responde, la reintenta sin detener las demás.
// Avisa cada PDF que ve con el evento Llego (puede avisar el mismo dos veces:
// quien escucha descarta los repetidos).
public sealed class VigilanteEntrada : IDisposable
{
    private readonly TimeSpan intervalo;
    private readonly object candado = new();
    private readonly Dictionary<string, Vigilada> carpetas = new(StringComparer.OrdinalIgnoreCase);
    private bool cerrado;

    public VigilanteEntrada(IEnumerable<string> rutas, TimeSpan? intervalo = null)
    {
        this.intervalo = intervalo ?? TimeSpan.FromSeconds(30);
        foreach (string ruta in rutas)
        {
            string completa = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ruta));
            carpetas[completa] = new Vigilada(completa);
        }
    }

    public event Action<string>? Llego;

    public event Action? CambioEstado;

    public IReadOnlyDictionary<string, EstadoCarpeta> Estados
    {
        get
        {
            lock (candado)
            {
                return carpetas.ToDictionary(
                    par => par.Key,
                    par => par.Value.Estado,
                    StringComparer.OrdinalIgnoreCase
                );
            }
        }
    }

    // Empieza a vigilar y revisa lo que llegó mientras la app estaba cerrada.
    public void Iniciar()
    {
        foreach (Vigilada carpeta in Copia())
        {
            _ = Task.Run(() => Preparar(carpeta));
        }
    }

    public void RevisarAhora()
    {
        foreach (Vigilada carpeta in Copia())
        {
            _ = Task.Run(() => Revisar(carpeta, soloNuevos: false));
        }
    }

    public void Dispose()
    {
        lock (candado)
        {
            cerrado = true;
            foreach (Vigilada carpeta in carpetas.Values)
            {
                carpeta.Detener();
            }
        }
    }

    private List<Vigilada> Copia()
    {
        lock (candado)
        {
            return [.. carpetas.Values];
        }
    }

    private void Preparar(Vigilada carpeta)
    {
        if (!Directory.Exists(carpeta.Ruta))
        {
            CambiarEstado(carpeta, EstadoCarpeta.NoDisponible);
            Reintentar(carpeta);
            return;
        }

        try
        {
            var vigia = new FileSystemWatcher(carpeta.Ruta, "*.*")
            {
                IncludeSubdirectories = false,
                NotifyFilter =
                    NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                InternalBufferSize = 64 * 1024,
            };
            vigia.Created += (_, e) => Avisar(e.FullPath);
            vigia.Changed += (_, e) => Avisar(e.FullPath);
            vigia.Renamed += (_, e) => Avisar(e.FullPath);
            vigia.Error += (_, _) => PasarARespaldo(carpeta);

            lock (candado)
            {
                if (cerrado)
                {
                    vigia.Dispose();
                    return;
                }
                carpeta.Detener();
                carpeta.Vigia = vigia;
            }
            vigia.EnableRaisingEvents = true;
            CambiarEstado(carpeta, EstadoCarpeta.Vigilando);
        }
        catch (Exception error)
            when (error is IOException or ArgumentException or PlatformNotSupportedException)
        {
            // Algunas carpetas de red no aceptan avisos de cambio.
            PasarARespaldo(carpeta);
        }

        Revisar(carpeta, soloNuevos: false);
    }

    private void PasarARespaldo(Vigilada carpeta)
    {
        lock (candado)
        {
            if (cerrado)
            {
                return;
            }
            carpeta.Vigia?.Dispose();
            carpeta.Vigia = null;
            carpeta.Reloj?.Dispose();
            // Sin avisos: se revisan solo los archivos nuevos cada cierto tiempo.
            carpeta.Reloj = new Timer(
                _ => Revisar(carpeta, soloNuevos: true),
                null,
                intervalo,
                intervalo
            );
        }
        CambiarEstado(carpeta, EstadoCarpeta.SinAvisos);
    }

    private void Reintentar(Vigilada carpeta)
    {
        lock (candado)
        {
            if (cerrado)
            {
                return;
            }
            carpeta.Reloj?.Dispose();
            carpeta.Reloj = new Timer(
                _ =>
                {
                    if (Directory.Exists(carpeta.Ruta))
                    {
                        lock (candado)
                        {
                            carpeta.Reloj?.Dispose();
                            carpeta.Reloj = null;
                        }
                        Preparar(carpeta);
                    }
                },
                null,
                intervalo,
                intervalo
            );
        }
    }

    private void Revisar(Vigilada carpeta, bool soloNuevos)
    {
        string[] archivos;
        try
        {
            archivos = Directory.GetFiles(carpeta.Ruta, "*.pdf");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            CambiarEstado(carpeta, EstadoCarpeta.NoDisponible);
            Reintentar(carpeta);
            return;
        }

        if (carpeta.Estado == EstadoCarpeta.NoDisponible)
        {
            CambiarEstado(
                carpeta,
                carpeta.Vigia is null ? EstadoCarpeta.SinAvisos : EstadoCarpeta.Vigilando
            );
        }

        foreach (string archivo in archivos)
        {
            bool nuevo;
            lock (candado)
            {
                nuevo = carpeta.Vistos.Add(archivo);
            }
            if (nuevo || !soloNuevos)
            {
                Avisar(archivo);
            }
        }
    }

    private void Avisar(string ruta)
    {
        if (
            !string.Equals(Path.GetExtension(ruta), ".pdf", StringComparison.OrdinalIgnoreCase)
            || cerrado
        )
        {
            return;
        }
        Llego?.Invoke(ruta);
    }

    private void CambiarEstado(Vigilada carpeta, EstadoCarpeta estado)
    {
        lock (candado)
        {
            if (carpeta.Estado == estado)
            {
                return;
            }
            carpeta.Estado = estado;
        }
        CambioEstado?.Invoke();
    }

    private sealed class Vigilada(string ruta)
    {
        public string Ruta { get; } = ruta;
        public EstadoCarpeta Estado { get; set; } = EstadoCarpeta.NoDisponible;
        public FileSystemWatcher? Vigia { get; set; }
        public Timer? Reloj { get; set; }
        public HashSet<string> Vistos { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void Detener()
        {
            Vigia?.Dispose();
            Vigia = null;
            Reloj?.Dispose();
            Reloj = null;
        }
    }
}
