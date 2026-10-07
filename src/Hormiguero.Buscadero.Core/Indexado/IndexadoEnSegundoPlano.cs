namespace Buscadero.Core.Indexado;

/// <summary>
/// Fase B-2a (D-66): mantiene el índice al día en un hilo aparte, para que buscar ya no
/// tenga que recorrer todas las carpetas antes de cada búsqueda. Respeta la misma pausa
/// entre subcarpetas del <see cref="Indexador"/> (vecino silencioso). Varios pedidos
/// seguidos se juntan en una sola pasada.
/// </summary>
public sealed class IndexadoEnSegundoPlano : IDisposable
{
    private readonly Indexador _indexador;
    private readonly Func<IReadOnlyList<string>> _carpetas;
    private readonly SemaphoreSlim _pedidos = new(0);
    private readonly CancellationTokenSource _cancelar = new();
    private readonly ManualResetEventSlim _libre = new(true);
    private readonly Thread _hilo;

    public IndexadoEnSegundoPlano(Indexador indexador, Func<IReadOnlyList<string>> carpetas)
    {
        _indexador = indexador;
        _carpetas = carpetas;
        _hilo = new Thread(Trabajar) { IsBackground = true, Name = "Indexado en segundo plano" };
        _hilo.Start();
    }

    /// <summary>Avisa el avance de la pasada en curso (desde el hilo del índice).</summary>
    public event Action<ProgresoIndexado>? Avance;

    /// <summary>Se dispara al terminar cada pasada (desde el hilo del índice).</summary>
    public event Action? Terminado;

    public bool EnCurso => !_libre.IsSet;

    /// <summary>Pide una pasada. Si ya hay una en curso, se hace otra al terminar.</summary>
    public void Pedir()
    {
        if (!_cancelar.IsCancellationRequested)
        {
            _pedidos.Release();
        }
    }

    /// <summary>Espera a que termine la pasada en curso, si la hay.</summary>
    public void EsperarPasadaActual(CancellationToken cancellationToken) =>
        _libre.Wait(cancellationToken);

    public void Dispose()
    {
        _cancelar.Cancel();
        // No se puede devolver mientras el hilo todavía usa el repositorio: el dueño
        // puede liberar sus recursos inmediatamente después de Dispose.
        // ponytail: espera sin límite; si una unidad de red se cuelga leyendo un PDF, cerrar
        // Buscadero puede tardar. Si molesta: límite + no liberar el repositorio hasta que termine.
        _hilo.Join();
    }

    private void Trabajar()
    {
        var token = _cancelar.Token;
        var progreso = new ProgresoDirecto(p => Avance?.Invoke(p));
        try
        {
            while (true)
            {
                _pedidos.Wait(token);
                while (_pedidos.Wait(0)) { }

                _libre.Reset();
                try
                {
                    _indexador.Indexar(_carpetas(), token, progreso);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    // Una carpeta caída o sin permiso no detiene el índice: se reintenta
                    // en la próxima pasada pedida.
                }
                finally
                {
                    _libre.Set();
                }
                Terminado?.Invoke();
            }
        }
        catch (OperationCanceledException) { }
    }

    // Progress<T> pasaría por el hilo de la pantalla; aquí se avisa directo.
    private sealed class ProgresoDirecto(Action<ProgresoIndexado> avisar)
        : IProgress<ProgresoIndexado>
    {
        public void Report(ProgresoIndexado value) => avisar(value);
    }
}
