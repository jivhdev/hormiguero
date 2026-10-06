using Archivero.Datos;
using Archivero.Servicios;

namespace Archivero.Tests;

public class PeriodoAtajoServiceTests
{
    private static readonly DateTime Hoy = new(2026, 3, 15);

    [Fact]
    public void AnioEnCurso_UsaElAnioRecibidoEnCadaResolucion()
    {
        var atajo = Crear(PeriodoAtajo.AnioEnCurso, FormatoCarpeta.Anio);
        Assert.Equal(2026, PeriodoAtajoService.ResolverFecha(atajo, null, Hoy, out _)?.Year);
        Assert.Equal(
            2027,
            PeriodoAtajoService.ResolverFecha(atajo, null, Hoy.AddYears(1), out _)?.Year
        );
    }

    [Fact]
    public void MesEnCurso_UsaLaFechaActual()
    {
        var atajo = Crear(PeriodoAtajo.MesEnCurso, FormatoCarpeta.AnioMes);
        Assert.Equal(Hoy, PeriodoAtajoService.ResolverFecha(atajo, null, Hoy, out _));
    }

    [Fact]
    public void AnioFijo_MantieneMesYDiaDeHoyEnElAnioElegido()
    {
        var atajo = Crear(PeriodoAtajo.AnioFijo, FormatoCarpeta.AnioMesDia) with
        {
            AnioFijo = 2024,
        };
        Assert.Equal(
            new DateTime(2024, 3, 15),
            PeriodoAtajoService.ResolverFecha(atajo, null, Hoy, out _)
        );
    }

    [Fact]
    public void PreguntarFechaCadaVez_RequiereFechaParaFormatoAnual()
    {
        var atajo = Crear(PeriodoAtajo.PreguntarFechaCadaVez, FormatoCarpeta.Anio);
        Assert.Null(PeriodoAtajoService.ResolverFecha(atajo, null, Hoy, out var error));
        Assert.Equal("Escribir la fecha del documento.", error);
        Assert.Equal(Hoy, PeriodoAtajoService.ResolverFecha(atajo, "15/03/2026", Hoy, out _));
    }

    private static AtajoGuardadoRapido Crear(PeriodoAtajo periodo, FormatoCarpeta formato) =>
        new(1, "Prueba", @"C:\Prueba", formato, "yyyy\\MM\\dd", [], periodo);
}
