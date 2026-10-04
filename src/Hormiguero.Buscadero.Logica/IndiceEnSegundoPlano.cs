using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Buscadero.Logica;

public enum EstadoIndice
{
    Detenido,
    Armandose,
    AlDia,
}

// Arma y mantiene el índice de las carpetas configuradas en un hilo aparte.
// No vigila con consultas periódicas (ADR-001 de Buscadero): revisa cuando se
// le pide (al abrir la app o al cambiar las carpetas) y solo reintenta, cada
// cierto tiempo, las carpetas que no respondieron (REQ-002).
public sealed class IndiceEnSegundoPlano : IDisposable
{
    private readonly string rutaBase;
    private readonly ControlIndice control;
    private readonly TimeSpan esperaReintento;
    private readonly CancellationTokenSource cancelar = new();
    private readonly SemaphoreSlim pedidos = new(0);
    private readonly object candado = new();
    private readonly Task trabajo;

    private HashSet<string> noDisponibles = new(StringComparer.OrdinalIgnoreCase);
    private EstadoIndice estado = EstadoIndice.Detenido;
    private int documentosIndexados;
    private int totalAnterior;
    private Exception? ultimoError;

    public IndiceEnSegundoPlano(
        string rutaBase,
        ControlIndice control,
        TimeSpan? esperaReintento = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaBase);
        this.rutaBase = rutaBase;
        this.control = control;
        this.esperaReintento = esperaReintento ?? TimeSpan.FromMinutes(1);
        control.Cambio += () => Cambio?.Invoke();
        trabajo = Task.Factory.StartNew(
            Trabajar,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
    }

    public event Action? Cambio;

    public EstadoIndice Estado
    {
        get
        {
            lock (candado)
            {
                return estado;
            }
        }
    }

    public int DocumentosIndexados
    {
        get
        {
            lock (candado)
            {
                return documentosIndexados;
            }
        }
    }

    // Sin un total anterior no se puede calcular el avance sin recorrer antes
    // todas las carpetas, lo que cargaría la red: en ese caso es null.
    public int? Porcentaje
    {
        get
        {
            lock (candado)
            {
                if (estado != EstadoIndice.Armandose || totalAnterior == 0)
                {
                    return null;
                }

                return Math.Min(99, control.ArchivosRevisados * 100 / totalAnterior);
            }
        }
    }

    public IReadOnlyCollection<string> NoDisponibles
    {
        get
        {
            lock (candado)
            {
                return [.. noDisponibles];
            }
        }
    }

    public Exception? UltimoError
    {
        get
        {
            lock (candado)
            {
                return ultimoError;
            }
        }
    }

    public void Revisar() => pedidos.Release();

    public void Dispose()
    {
        cancelar.Cancel();
        try
        {
            trabajo.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException) { }

        cancelar.Dispose();
        pedidos.Dispose();
    }

    private void Trabajar()
    {
        CancellationToken token = cancelar.Token;
        try
        {
            using SqliteConnection conexion = BaseComun.Abrir(rutaBase);

            while (true)
            {
                bool completa;
                if (NoDisponibles.Count > 0)
                {
                    completa = pedidos.Wait(esperaReintento, token);
                }
                else
                {
                    pedidos.Wait(token);
                    completa = true;
                }

                // Varios pedidos juntos se resuelven con una sola vuelta.
                while (pedidos.Wait(0)) { }

                Vuelta(conexion, completa, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            lock (candado)
            {
                ultimoError = error;
                estado = EstadoIndice.Detenido;
            }
            Cambio?.Invoke();
        }
    }

    private void Vuelta(SqliteConnection conexion, bool completa, CancellationToken token)
    {
        IReadOnlyList<string> configuradas = new CarpetasConfiguradas(conexion).Listar();
        var documentos = new Documentos(conexion);
        var indexador = new Indexador(conexion);

        HashSet<string> pendientes;
        lock (candado)
        {
            pendientes = completa
                ? new(configuradas, StringComparer.OrdinalIgnoreCase)
                : new(configuradas.Where(noDisponibles.Contains), StringComparer.OrdinalIgnoreCase);
            totalAnterior = pendientes.Sum(carpeta => documentos.Firmas(carpeta).Count);
            estado = EstadoIndice.Armandose;
        }
        control.ReiniciarCuenta();

        var fallidas = new List<string>();
        foreach (string carpeta in pendientes)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (!Directory.Exists(carpeta))
                {
                    throw new DirectoryNotFoundException(carpeta);
                }

                indexador.Revisar(carpeta, token, control);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                fallidas.Add(carpeta);
            }
        }

        int total = configuradas.Sum(carpeta => documentos.Firmas(carpeta).Count);
        lock (candado)
        {
            // Las que se quitaron de la configuración ya no cuentan como no disponibles.
            noDisponibles = new(
                noDisponibles.Where(configuradas.Contains).Except(pendientes).Concat(fallidas),
                StringComparer.OrdinalIgnoreCase
            );
            documentosIndexados = total;
            estado = EstadoIndice.AlDia;
        }
        Cambio?.Invoke();
    }
}
