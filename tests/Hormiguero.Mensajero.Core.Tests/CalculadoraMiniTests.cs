using Hormiguero.Mensajero.Core;

namespace Hormiguero.Mensajero.Core.Tests;

public sealed class CalculadoraMiniTests
{
    [Fact]
    public void Evalua_miles_chilenos()
    {
        Assert.Equal(52_230, CalculadoraMini.Evaluar("1.250.430 - 1.198.200"));
    }

    [Fact]
    public void Evalua_decimales_con_coma_y_numeros_sin_miles()
    {
        Assert.Equal(3.75m, CalculadoraMini.Evaluar("1,5 + 2,25"));
        Assert.Equal(1_250_430.5m, CalculadoraMini.Evaluar("1250430,5 + 0"));
    }

    [Fact]
    public void Evalua_varios_operadores_con_precedencia()
    {
        Assert.Equal(14, CalculadoraMini.Evaluar("2 + 3 * 4"));
        Assert.Equal(12, CalculadoraMini.Evaluar("2 + 3 * 4 - 4 / 2"));
    }

    [Fact]
    public void Evalua_parentesis_y_negativos()
    {
        Assert.Equal(10, CalculadoraMini.Evaluar("(2 + 3) * 2"));
        Assert.Equal(-7, CalculadoraMini.Evaluar("-10 + 3"));
        Assert.Equal(6, CalculadoraMini.Evaluar("2 * (-5 + 8)"));
    }

    [Fact]
    public void Acepta_los_operadores_de_multiplicacion_y_division()
    {
        Assert.Equal(12, CalculadoraMini.Evaluar("3 x 4"));
        Assert.Equal(3, CalculadoraMini.Evaluar("9 ÷ 3"));
    }

    [Fact]
    public void Falla_al_dividir_por_cero()
    {
        Assert.Throws<DivideByZeroException>(() => CalculadoraMini.Evaluar("12 / (3 - 3)"));
    }

    [Theory]
    [InlineData("texto + 2")]
    [InlineData("1 +")]
    [InlineData("1.25 + 1")]
    [InlineData("5")]
    [InlineData("(1 + 2")]
    public void Rechaza_expresiones_invalidas(string expresion)
    {
        Assert.Throws<FormatException>(() => CalculadoraMini.Evaluar(expresion));
    }

    [Fact]
    public void Formatea_resultado_y_valor_para_copiar()
    {
        Assert.Equal("1.250.430,5", CalculadoraMini.FormatearResultado(1_250_430.5m));
        Assert.Equal("1250430,5", CalculadoraMini.FormatearParaCopiar(1_250_430.5m));
    }
}
