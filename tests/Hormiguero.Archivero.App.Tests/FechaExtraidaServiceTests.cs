using Archivero.Servicios;

namespace Archivero.Tests;

public class FechaExtraidaServiceTests
{
    [Theory]
    [InlineData("30/04/2026", 2026, 4, 30)]
    [InlineData("30-04-2026", 2026, 4, 30)]
    [InlineData("30.04.2026", 2026, 4, 30)]
    [InlineData("2026-04-30", 2026, 4, 30)]
    [InlineData("2026/04/30", 2026, 4, 30)]
    [InlineData("30/04/26", 2026, 4, 30)]
    [InlineData("30-ABR-2026", 2026, 4, 30)]
    [InlineData("30-abr-2026", 2026, 4, 30)]
    [InlineData("30/ABR/26", 2026, 4, 30)]
    [InlineData("30 ABR 2026", 2026, 4, 30)]
    [InlineData("30 de abril de 2026", 2026, 4, 30)]
    [InlineData("1 de enero de 2026", 2026, 1, 1)]
    [InlineData("15-SEP-2026", 2026, 9, 15)]
    [InlineData("15-SET-2026", 2026, 9, 15)]
    public void TryParsear_ConFormatosSoportados_InterpretaLaFechaCorrecta(string texto, int anio, int mes, int dia)
    {
        var pudo = FechaExtraidaService.TryParsear(texto, out var fecha);

        Assert.True(pudo, $"Se esperaba poder interpretar \"{texto}\"");
        Assert.Equal(new DateTime(anio, mes, dia), fecha);
    }

    [Theory]
    [InlineData("esto no es una fecha")]
    [InlineData("")]
    [InlineData("32-13-2026")]
    public void TryParsear_ConTextoInvalido_DevuelveFalse(string texto)
    {
        Assert.False(FechaExtraidaService.TryParsear(texto, out _));
    }
}
