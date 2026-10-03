using Hormiguero.Nucleo.Utilidades;

namespace Hormiguero.Nucleo.Tests;

public class NombreArchivoTests
{
    public static IEnumerable<object[]> Casos()
    {
        yield return new object[] { "OCC104523.pdf", new[] { ("104523", "OCC", "") } };
        yield return new object[]
        {
            "FCV0000025001_CEDIBLE.pdf",
            new[] { ("25001", "FCV", "CEDIBLE") },
        };
        yield return new object[] { "104523 recibida.pdf", new[] { ("104523", "", "recibida") } };
        yield return new object[] { "F-123.pdf", new[] { ("123", "F", "") } };
        yield return new object[] { "informe.pdf", Array.Empty<(string, string, string)>() };
        yield return new object[]
        {
            "OCC 104523 NVV 33120.pdf",
            new[] { ("104523", "OCC", "NVV"), ("33120", "NVV", "") },
        };
        yield return new object[] { "", Array.Empty<(string, string, string)>() };
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void Extrae_numeros_prefijos_y_sufijos(
        string nombre,
        (string Numero, string Prefijo, string Sufijo)[] esperados
    )
    {
        IReadOnlyList<NumeroEnNombre> numeros = NombreArchivo.Extraer(nombre);

        Assert.Equal(esperados.Length, numeros.Count);
        for (int indice = 0; indice < esperados.Length; indice++)
        {
            Assert.Equal(esperados[indice].Numero, numeros[indice].Numero);
            Assert.Equal(esperados[indice].Prefijo, numeros[indice].Prefijo);
            Assert.Equal(esperados[indice].Sufijo, numeros[indice].Sufijo);
        }
    }
}
