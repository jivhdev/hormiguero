using Hormiguero.Mensajero.Core;

namespace Hormiguero.Mensajero.Core.Tests;

// Comparte la variable HORMIGUERO_DATOS (registro de Mensajero) con CausasX1Tests: no en paralelo.
[Collection("RegistroMensajero")]
public sealed class MensajeroLogTests : IDisposable
{
    private readonly string raiz = Path.Combine(
        Path.GetTempPath(),
        "MensajeroLogTests",
        Guid.NewGuid().ToString("N")
    );
    private readonly string rutaOriginal = MensajeroLog.RutaLog;

    public MensajeroLogTests()
    {
        Directory.CreateDirectory(raiz);
        MensajeroLog.RutaLog = Path.Combine(raiz, "mensajero.log");
    }

    public void Dispose()
    {
        MensajeroLog.RutaLog = rutaOriginal;
        Directory.Delete(raiz, recursive: true);
    }

    [Fact]
    public void Rota_el_registro_y_conserva_tres_archivos_anteriores()
    {
        string ruta = MensajeroLog.RutaLog;
        File.WriteAllText(ruta, new string('a', (int)MensajeroLog.TamanioMaximoBytes));
        for (int indice = 1; indice <= MensajeroLog.MaximoArchivosRotados; indice++)
            File.WriteAllText($"{ruta}.{indice}", $"anterior {indice}");

        MensajeroLog.Registrar("ERROR", "prueba de rotación");

        Assert.True(File.Exists(ruta));
        Assert.True(File.Exists($"{ruta}.1"));
        Assert.True(File.Exists($"{ruta}.2"));
        Assert.True(File.Exists($"{ruta}.3"));
        Assert.Contains("prueba de rotación", File.ReadAllText(ruta));
        Assert.Equal(new string('a', 20), File.ReadAllText($"{ruta}.1")[..20]);
    }
}
