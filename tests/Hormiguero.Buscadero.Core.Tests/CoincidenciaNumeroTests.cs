using Buscadero.Core.Busqueda;

namespace Buscadero.Core.Tests;

public sealed class CoincidenciaNumeroTests
{
    [Theory]
    [InlineData("12345.pdf")]
    [InlineData("Factura 12345.pdf")]
    [InlineData("12345 - Proveedor.pdf")]
    [InlineData("(12345).pdf")]
    [InlineData("12345_anexo.pdf")]
    [InlineData(@"C:\Documentos\2026\Marzo\12345.pdf")]
    public void EsCoincidenciaExacta_NumeroDelimitado_Coincide(string nombre)
    {
        Assert.True(CoincidenciaNumero.EsCoincidenciaExacta(nombre, "12345"));
    }

    [Theory]
    [InlineData("123456.pdf")]
    [InlineData("12345F.pdf")]
    [InlineData("F12345.pdf")]
    [InlineData("1234.pdf")]
    [InlineData("112345.pdf")]
    public void EsCoincidenciaExacta_NumeroPegadoAOtroCaracter_NoCoincide(string nombre)
    {
        Assert.False(CoincidenciaNumero.EsCoincidenciaExacta(nombre, "12345"));
    }

    [Theory]
    [InlineData("ABC123 - Factura.pdf", "abc123")]
    [InlineData("abc123.pdf", "ABC123")]
    public void EsCoincidenciaExacta_IgnoraMayusculas(string nombre, string consulta)
    {
        Assert.True(CoincidenciaNumero.EsCoincidenciaExacta(nombre, consulta));
    }

    [Fact]
    public void EsCoincidenciaExacta_ConsultaVacia_NoCoincide()
    {
        Assert.False(CoincidenciaNumero.EsCoincidenciaExacta("12345.pdf", ""));
    }

    [Theory]
    [InlineData("OCC20339.pdf", "occ 20339")]
    [InlineData("OCC 20339.pdf", "occ20339")]
    [InlineData("OCC-20339.pdf", "occ20339")]
    [InlineData("OCC_20339.pdf", "occ 20339")]
    public void EsCoincidenciaAlfanumerica_IgnoraSeparadores(string nombre, string consulta)
    {
        Assert.True(CoincidenciaNumero.EsCoincidenciaAlfanumerica(nombre, consulta));
    }

    [Theory]
    [InlineData("OCC20339.pdf", "occ20340")]
    [InlineData("OCC20339.pdf", "occ203390")]
    public void EsCoincidenciaAlfanumerica_TextoDistinto_NoCoincide(string nombre, string consulta)
    {
        Assert.False(CoincidenciaNumero.EsCoincidenciaAlfanumerica(nombre, consulta));
    }

    [Theory]
    [InlineData("OCC0000020339.pdf", "20339")]
    [InlineData("OCC0000020339.pdf", "020339")]
    [InlineData("OCC0000020339.pdf", "0000020339")]
    [InlineData("12345F.pdf", "12345")]
    [InlineData("Factura 20339.pdf", "020339")]
    public void EsCoincidenciaSoloNumero_IgnoraLetrasYCerosIzquierda(string nombre, string consulta)
    {
        Assert.True(CoincidenciaNumero.EsCoincidenciaSoloNumero(nombre, consulta));
    }

    [Theory]
    [InlineData("OCC0000020339.pdf", "20340")]
    [InlineData("OCC.pdf", "20339")]
    [InlineData("12345.pdf", "abc")]
    public void EsCoincidenciaSoloNumero_NoCoincide(string nombre, string consulta)
    {
        Assert.False(CoincidenciaNumero.EsCoincidenciaSoloNumero(nombre, consulta));
    }

    [Fact]
    public void EsCoincidenciaSoloNumero_CeroUnico_CoincideConVariosCeros()
    {
        Assert.True(CoincidenciaNumero.EsCoincidenciaSoloNumero("0.pdf", "000"));
    }

    [Theory]
    [InlineData("OCC123.pdf", "occ")]
    [InlineData("OCC123.pdf", "OCC")]
    [InlineData("ABC-123.pdf", "abc")]
    public void EsCoincidenciaSoloLetras_IgnoraNumeros(string nombre, string consulta)
    {
        Assert.True(CoincidenciaNumero.EsCoincidenciaSoloLetras(nombre, consulta));
    }

    [Theory]
    [InlineData("OCC123.pdf", "abd")]
    [InlineData("12345.pdf", "occ")]
    [InlineData("OCC.pdf", "123")]
    public void EsCoincidenciaSoloLetras_NoCoincide(string nombre, string consulta)
    {
        Assert.False(CoincidenciaNumero.EsCoincidenciaSoloLetras(nombre, consulta));
    }

    [Fact]
    public void ModosEnOrden_EsElOrdenDeFallbackEsperado()
    {
        Assert.Equal(
            new[]
            {
                ModoBusqueda.Exacto,
                ModoBusqueda.Alfanumerico,
                ModoBusqueda.SoloNumero,
                ModoBusqueda.SoloLetras,
            },
            CoincidenciaNumero.ModosEnOrden
        );
    }
}
