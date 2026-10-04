using System.Diagnostics;

namespace Hormiguero.Buscadero.Logica;

// Marca el ritmo del índice para ser un vecino silencioso (RNF-2: máximo
// 5 carpetas por segundo) y lo pausa mientras el usuario busca (REQ-002).
public sealed class ControlIndice
{
    private readonly TimeSpan intervalo;
    private readonly Stopwatch reloj = Stopwatch.StartNew();
    private readonly ManualResetEventSlim sinPausa = new(true);
    private readonly object candado = new();
    private TimeSpan proximoTurno = TimeSpan.Zero;
    private int archivosRevisados;
    private int carpetasRevisadas;

    public ControlIndice(int carpetasPorSegundo = 5)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(carpetasPorSegundo, 0);
        intervalo = TimeSpan.FromSeconds(1.0 / carpetasPorSegundo);
    }

    public event Action? Cambio;

    public bool EnPausa => !sinPausa.IsSet;

    public int ArchivosRevisados => Volatile.Read(ref archivosRevisados);

    public int CarpetasRevisadas => Volatile.Read(ref carpetasRevisadas);

    public void EsperarTurno(CancellationToken cancelar)
    {
        sinPausa.Wait(cancelar);

        TimeSpan espera;
        lock (candado)
        {
            TimeSpan ahora = reloj.Elapsed;
            if (proximoTurno < ahora)
            {
                proximoTurno = ahora;
            }

            espera = proximoTurno - ahora;
            proximoTurno += intervalo;
        }

        if (espera > TimeSpan.Zero && cancelar.WaitHandle.WaitOne(espera))
        {
            cancelar.ThrowIfCancellationRequested();
        }

        // Si el usuario empezó a buscar durante la espera, se respeta la pausa.
        sinPausa.Wait(cancelar);
    }

    public void Pausar() => sinPausa.Reset();

    public void Reanudar() => sinPausa.Set();

    public void ContarArchivo()
    {
        Interlocked.Increment(ref archivosRevisados);
        Cambio?.Invoke();
    }

    public void ContarCarpeta()
    {
        Interlocked.Increment(ref carpetasRevisadas);
        Cambio?.Invoke();
    }

    public void ReiniciarCuenta()
    {
        Interlocked.Exchange(ref archivosRevisados, 0);
        Interlocked.Exchange(ref carpetasRevisadas, 0);
        Cambio?.Invoke();
    }
}
