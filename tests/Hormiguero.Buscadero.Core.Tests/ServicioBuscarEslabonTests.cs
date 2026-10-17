using Buscadero.Core.Lineas;

namespace Hormiguero.Buscadero.Core.Tests;

public sealed class ServicioBuscarEslabonTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(
        Path.GetTempPath(),
        Guid.NewGuid().ToString("N")
    );

    public void Dispose()
    {
        if (Directory.Exists(_carpeta))
            Directory.Delete(_carpeta, true);
    }

    [Fact]
    public void BuscarOCargar_EncuentraEnCarpetaYConfirmaArchivo()
    {
        Directory.CreateDirectory(Path.Combine(_carpeta, "2025"));
        string archivo = Path.Combine(_carpeta, "2025", "GRC 001234.pdf");
        File.WriteAllText(archivo, "prueba");
        var selectorLlamado = false;

        string? resultado = new ServicioBuscarEslabon().BuscarOCargar(
            _carpeta,
            "1234",
            ruta => ruta == archivo,
            _ =>
            {
                selectorLlamado = true;
                return null;
            }
        );

        Assert.Equal(archivo, resultado);
        Assert.False(selectorLlamado);
    }

    [Fact]
    public void BuscarOCargar_SinCoincidenciaAbreSelectorEnCarpetaIndicada()
    {
        Directory.CreateDirectory(_carpeta);
        string elegido = Path.Combine(_carpeta, "STA manual.pdf");
        string? carpetaSelector = null;
        string? resultado = new ServicioBuscarEslabon().BuscarOCargar(
            _carpeta,
            "88",
            _ => false,
            carpeta =>
            {
                carpetaSelector = carpeta;
                return elegido;
            }
        );

        Assert.Equal(elegido, resultado);
        Assert.Equal(_carpeta, carpetaSelector);
    }
}
