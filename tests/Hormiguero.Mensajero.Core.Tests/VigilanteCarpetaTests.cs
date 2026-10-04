using Hormiguero.Mensajero.Core;

namespace Hormiguero.Mensajero.Core.Tests;

public sealed class VigilanteCarpetaTests : IDisposable
{
    private readonly string carpeta = Path.Combine(
        Path.GetTempPath(),
        $"mensajero-{Guid.NewGuid():N}"
    );

    public VigilanteCarpetaTests() => Directory.CreateDirectory(carpeta);

    [Fact]
    public async Task Iniciar_AvisaPdfCreadoYModificadoUnaSolaVezDuranteDosSegundos()
    {
        using var vigilante = new VigilanteCarpeta();
        var recibido = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        int cantidadAvisos = 0;
        vigilante.NuevoPdf += ruta =>
        {
            Interlocked.Increment(ref cantidadAvisos);
            recibido.TrySetResult(ruta);
        };

        Assert.True(vigilante.Iniciar(carpeta));
        Assert.True(vigilante.EstaActivo);
        string ruta = Path.Combine(carpeta, "OCC 123.pdf");
        await File.WriteAllTextAsync(ruta, "pdf");

        Assert.Equal(ruta, await recibido.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await Task.Delay(500);
        Assert.Equal(1, Volatile.Read(ref cantidadAvisos));
    }

    [Fact]
    public void Iniciar_CarpetaInexistenteDevuelveFalseYAvisaError()
    {
        using var vigilante = new VigilanteCarpeta();
        string? mensaje = null;
        vigilante.Error += valor => mensaje = valor;

        Assert.False(vigilante.Iniciar(Path.Combine(carpeta, "inexistente")));
        Assert.False(vigilante.EstaActivo);
        Assert.Contains("La carpeta no existe", mensaje);
    }

    [Fact]
    public void Detener_DejaDeEstarActivo()
    {
        using var vigilante = new VigilanteCarpeta();

        Assert.True(vigilante.Iniciar(carpeta));
        vigilante.Detener();

        Assert.False(vigilante.EstaActivo);
    }

    public void Dispose() => Directory.Delete(carpeta, recursive: true);
}
