using Archivero.Datos;
using Archivero.Servicios;

namespace Archivero.Tests;

public class LimpiezaNombreServiceTests
{
    // ----- Caso-11, punto 4: regla de nombre de un atajo (los botones usados, en orden) -----

    [Fact]
    public void AplicarRegla_SinOperaciones_DejaElNombreOriginal()
    {
        Assert.Equal("Guia 00123", LimpiezaNombreService.AplicarRegla("Guia 00123", []));
    }

    [Fact]
    public void AplicarRegla_SoloNumerosYQuitarCeros_LasAplicaEnOrden()
    {
        var resultado = LimpiezaNombreService.AplicarRegla("Guia N° 000456",
            [OperacionNombre.DejarSoloNumeros, OperacionNombre.QuitarCerosIzquierda]);

        Assert.Equal("456", resultado);
    }

    [Fact]
    public void AplicarRegla_ConBorrar_DejaElNombreVacioParaEscribirlo()
    {
        Assert.Equal(string.Empty, LimpiezaNombreService.AplicarRegla("Guia 123", [OperacionNombre.Borrar]));
    }

    [Theory]
    [InlineData(@"C:\Documentos\Guías firmadas", FormatoCarpeta.Directo, "Guías firmadas")]
    [InlineData(@"C:\Documentos\Guías firmadas\", FormatoCarpeta.AnioMes, "Guías firmadas (Por año y mes)")]
    public void SugerirNombreAtajo_UsaElNombreDeLaCarpetaYElTipo(string carpeta, FormatoCarpeta formato, string esperado)
    {
        Assert.Equal(esperado, LimpiezaNombreService.SugerirNombreAtajo(carpeta, formato));
    }

    [Theory]
    [InlineData("00123", "123")]
    [InlineData("0001200", "1200")]
    [InlineData("0000000042", "42")]
    public void QuitarCerosIzquierda_ConCerosDeRelleno_LosQuita(string nombre, string esperado)
    {
        Assert.Equal(esperado, LimpiezaNombreService.QuitarCerosIzquierda(nombre));
    }

    [Theory]
    [InlineData("1200")]
    [InlineData("123")]
    [InlineData("100")]
    public void QuitarCerosIzquierda_SinCerosALaIzquierda_NoTocaLosCerosDelValor(string nombre)
    {
        Assert.Equal(nombre, LimpiezaNombreService.QuitarCerosIzquierda(nombre));
    }

    [Fact]
    public void QuitarCerosIzquierda_SoloCeros_DejaUnCero()
    {
        Assert.Equal("0", LimpiezaNombreService.QuitarCerosIzquierda("0000"));
    }

    [Theory]
    [InlineData("Guia 00123")]
    [InlineData("00123-A")]
    [InlineData("")]
    public void QuitarCerosIzquierda_NombreNoPuramenteNumerico_LoDejaTalCual(string nombre)
    {
        Assert.Equal(nombre, LimpiezaNombreService.QuitarCerosIzquierda(nombre));
    }

    [Theory]
    [InlineData("Guia N° 00123-2026", "001232026")]
    [InlineData("sin numeros", "")]
    public void DejarSoloNumeros_ConservaSoloLosDigitos(string nombre, string esperado)
    {
        Assert.Equal(esperado, LimpiezaNombreService.DejarSoloNumeros(nombre));
    }
}
