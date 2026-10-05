using Hormiguero.Nucleo.Utilidades;

namespace Hormiguero.Nucleo.Tests;

public class CalculoFechasTests
{
    [Theory]
    [InlineData(10, "2026-03-05", "2026-03-15")]
    [InlineData(-10, "2026-03-05", "2026-02-23")]
    [InlineData(1, "2026-03-31", "2026-04-01")]
    [InlineData(-1, "2026-01-01", "2025-12-31")]
    [InlineData(3650, "2026-03-05", "2036-03-02")]
    [InlineData(-3650, "2026-03-05", "2016-03-07")]
    public void Suma_corridos(int cantidad, string baseTexto, string esperadoTexto)
    {
        var baseFecha = DateOnly.Parse(baseTexto);
        var esperado = DateOnly.Parse(esperadoTexto);

        DateOnly resultado = CalculoFechas.Sumar(baseFecha, cantidad, TipoDias.Corridos);

        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void Un_habil_desde_viernes_cae_lunes()
    {
        var viernes = new DateOnly(2026, 3, 6);

        DateOnly lunes = CalculoFechas.Sumar(viernes, 1, TipoDias.Habiles);

        Assert.Equal(new DateOnly(2026, 3, 9), lunes);
    }

    [Fact]
    public void Un_habil_desde_sabado_cae_lunes()
    {
        var sabado = new DateOnly(2026, 3, 7);

        DateOnly lunes = CalculoFechas.Sumar(sabado, 1, TipoDias.Habiles);

        Assert.Equal(new DateOnly(2026, 3, 9), lunes);
    }

    [Fact]
    public void Menos_un_habil_desde_lunes_cae_viernes_anterior()
    {
        var lunes = new DateOnly(2026, 3, 9);

        DateOnly viernes = CalculoFechas.Sumar(lunes, -1, TipoDias.Habiles);

        Assert.Equal(new DateOnly(2026, 3, 6), viernes);
    }

    [Fact]
    public void Un_habil_desde_domingo_cae_lunes()
    {
        var domingo = new DateOnly(2026, 3, 8);

        DateOnly lunes = CalculoFechas.Sumar(domingo, 1, TipoDias.Habiles);

        Assert.Equal(new DateOnly(2026, 3, 9), lunes);
    }

    [Fact]
    public void Feriado_en_medio_se_salta()
    {
        // 2026-03-12 es jueves y 2026-03-13 es viernes; 2026-03-11 es miércoles.
        var miercoles = new DateOnly(2026, 3, 11);
        var feriados = new HashSet<DateOnly> { new(2026, 3, 12) };

        DateOnly viernes = CalculoFechas.Sumar(miercoles, 2, TipoDias.Habiles, feriados);

        // 1 hábil: jueves feriado se salta → viernes 13. 2 hábiles: lunes 16.
        Assert.Equal(new DateOnly(2026, 3, 16), viernes);
    }

    [Fact]
    public void Feriado_en_sabado_no_cuenta_doble()
    {
        // 2026-03-06 es viernes, 2026-03-07 es sábado.
        var viernes = new DateOnly(2026, 3, 6);
        var feriados = new HashSet<DateOnly> { new(2026, 3, 7), new(2026, 3, 8) };

        DateOnly resultado = CalculoFechas.Sumar(viernes, 1, TipoDias.Habiles, feriados);

        Assert.Equal(new DateOnly(2026, 3, 9), resultado);
    }

    [Fact]
    public void Feriados_repetidos_no_alteran_el_resultado()
    {
        var miercoles = new DateOnly(2026, 3, 11);
        var feriados = new HashSet<DateOnly> { new(2026, 3, 12), new(2026, 3, 12) };

        DateOnly viernes = CalculoFechas.Sumar(miercoles, 1, TipoDias.Habiles, feriados);

        Assert.Equal(new DateOnly(2026, 3, 13), viernes);
    }

    [Fact]
    public void Cantidad_cero_devuelve_la_misma_fecha_aunque_sea_domingo()
    {
        var domingo = new DateOnly(2026, 3, 8);

        DateOnly resultado = CalculoFechas.Sumar(domingo, 0, TipoDias.Habiles);

        Assert.Equal(domingo, resultado);
    }

    [Fact]
    public void Cinco_habiles_cruzando_fin_de_anio_con_feriados()
    {
        // 2026-12-25 es viernes (feriado) y 2027-01-01 es viernes (feriado).
        var lunes = new DateOnly(2026, 12, 28);
        var feriados = new HashSet<DateOnly> { new(2026, 12, 25), new(2027, 1, 1) };

        DateOnly resultado = CalculoFechas.Sumar(lunes, 5, TipoDias.Habiles, feriados);

        // Hábiles contados: 29(1), 30(2), 31(3), 1 feriado, fin de semana, 4(4), 5(5).
        Assert.Equal(new DateOnly(2027, 1, 5), resultado);
    }

    [Fact]
    public void Fuera_de_rango_lanza_error()
    {
        var fecha = new DateOnly(2026, 3, 5);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalculoFechas.Sumar(fecha, 3651, TipoDias.Corridos)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalculoFechas.Sumar(fecha, -3651, TipoDias.Habiles)
        );
    }
}
