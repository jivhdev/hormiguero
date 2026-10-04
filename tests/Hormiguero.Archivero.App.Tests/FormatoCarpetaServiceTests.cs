using System.Globalization;
using Archivero.Datos;
using Archivero.Servicios;

namespace Archivero.Tests;

public class FormatoCarpetaServiceTests : IDisposable
{
    private readonly string _carpetaTemporal;

    public FormatoCarpetaServiceTests()
    {
        _carpetaTemporal = Path.Combine(Path.GetTempPath(), "archivero-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpetaTemporal);
    }

    [Fact]
    public void Detectar_ConCarpetaVacia_DevuelveDirecto()
    {
        var resultado = FormatoCarpetaService.Detectar(_carpetaTemporal);

        Assert.Equal(FormatoCarpeta.Directo, resultado.Formato);
        Assert.Null(resultado.PatronCarpeta);
    }

    [Fact]
    public void Detectar_ConSubcarpetasDeAnioDeCuatroDigitos_DevuelvePatronYyyy()
    {
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, "2023"));
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, "2024"));
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, "2025"));

        var resultado = FormatoCarpetaService.Detectar(_carpetaTemporal);

        Assert.Equal(FormatoCarpeta.Anio, resultado.Formato);
        Assert.Equal("yyyy", resultado.PatronCarpeta);
    }

    [Fact]
    public void Detectar_ConSubcarpetasDeAnioDeDosDigitos_DevuelvePatronYy()
    {
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, "23"));
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, "24"));

        var resultado = FormatoCarpetaService.Detectar(_carpetaTemporal);

        Assert.Equal(FormatoCarpeta.Anio, resultado.Formato);
        Assert.Equal("yy", resultado.PatronCarpeta);
    }

    [Fact]
    public void Detectar_ConSubcarpetasDeAnioYMesNumerico_DevuelveAnioMesConPatronCombinado()
    {
        var carpetaAnio = Path.Combine(_carpetaTemporal, "2024");
        Directory.CreateDirectory(Path.Combine(carpetaAnio, "01"));
        Directory.CreateDirectory(Path.Combine(carpetaAnio, "02"));

        var resultado = FormatoCarpetaService.Detectar(_carpetaTemporal);

        Assert.Equal(FormatoCarpeta.AnioMes, resultado.Formato);
        Assert.Equal("yyyy\\MM", resultado.PatronCarpeta);
    }

    [Fact]
    public void Detectar_ConSubcarpetasDeMesEnFormatoAnioMesConcatenado_DevuelvePatronYyyyMM()
    {
        var carpetaAnio = Path.Combine(_carpetaTemporal, "2026");
        Directory.CreateDirectory(Path.Combine(carpetaAnio, "202601"));
        Directory.CreateDirectory(Path.Combine(carpetaAnio, "202602"));

        var resultado = FormatoCarpetaService.Detectar(_carpetaTemporal);

        Assert.Equal(FormatoCarpeta.AnioMes, resultado.Formato);
        Assert.Equal("yyyy\\yyyyMM", resultado.PatronCarpeta);
    }

    [Fact]
    public void Detectar_ConSubcarpetasQueNoSonFechas_DevuelveDirecto()
    {
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, "Facturas"));
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, "Recibos"));

        var resultado = FormatoCarpetaService.Detectar(_carpetaTemporal);

        Assert.Equal(FormatoCarpeta.Directo, resultado.Formato);
    }

    [Fact]
    public void Detectar_ConCarpetaSinSubcarpetas_NuncaInventaUnaSubdivision()
    {
        // Caso-1, punto 2: si el usuario elige la carpeta exacta y no hay ninguna subcarpeta
        // todavia, Archivero nunca debe asumir que hace falta una subdivision por fecha.
        var resultado = FormatoCarpetaService.Detectar(_carpetaTemporal);

        Assert.Equal(FormatoCarpeta.Directo, resultado.Formato);
        Assert.Null(resultado.PatronCarpeta);
    }

    [Fact]
    public void Detectar_ConTextoLiteralAlrededorDelAnio_DevuelvePatronConLiteralYAnio()
    {
        // Caso-1, punto 2: la deteccion tiene que generalizar a partir de la evidencia real, no
        // limitarse a que el nombre completo de la subcarpeta sea exactamente el token de fecha.
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, "Año 2024"));
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, "Año 2025"));

        var resultado = FormatoCarpetaService.Detectar(_carpetaTemporal);

        Assert.Equal(FormatoCarpeta.Anio, resultado.Formato);
        Assert.Equal("Año 2026", new DateTime(2026, 1, 1).ToString(resultado.PatronCarpeta!, CultureInfo.GetCultureInfo("es-ES")));
    }

    [Fact]
    public void Detectar_ConSoloUnaCarpetaDeAnioYSubcarpetasDeMes_NuncaConfundeElAnioConTextoFijo()
    {
        // Con una sola carpeta de año todavia, el "2026" que se repite en cada subcarpeta de mes
        // (si el mes viene concatenado, ej. "202601") no debe tratarse como si fuera texto
        // literal fijo -- no hay evidencia suficiente para saber si en verdad es el año o no.
        var carpetaAnio = Path.Combine(_carpetaTemporal, "2026");
        Directory.CreateDirectory(Path.Combine(carpetaAnio, "202601"));
        Directory.CreateDirectory(Path.Combine(carpetaAnio, "202602"));

        var resultado = FormatoCarpetaService.Detectar(_carpetaTemporal);

        Assert.Equal(FormatoCarpeta.AnioMes, resultado.Formato);
        Assert.Equal("yyyy\\yyyyMM", resultado.PatronCarpeta);
    }

    [Fact]
    public void Detectar_ConSubcarpetasDeMesConNombreCompletoYTextoLiteral_DevuelvePatronConLiteralYMmmm()
    {
        var carpetaAnio = Path.Combine(_carpetaTemporal, "2026");
        Directory.CreateDirectory(Path.Combine(carpetaAnio, "Mes de Enero"));
        Directory.CreateDirectory(Path.Combine(carpetaAnio, "Mes de Febrero"));

        var resultado = FormatoCarpetaService.Detectar(_carpetaTemporal);

        Assert.Equal(FormatoCarpeta.AnioMes, resultado.Formato);
        Assert.Equal(
            "Mes de septiembre",
            new DateTime(2026, 9, 1).ToString(resultado.PatronCarpeta!.Split('\\')[1], CultureInfo.GetCultureInfo("es-ES")));
    }

    [Theory]
    [InlineData("yyyy", "2026")]
    [InlineData("yy", "26")]
    public void ConstruirSubcarpeta_ConFormatoAnio_DevuelveElAnioSegunElPatron(string patron, string esperado)
    {
        var resultado = FormatoCarpetaService.ConstruirSubcarpeta(FormatoCarpeta.Anio, patron, new DateTime(2026, 9, 12));

        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void ConstruirSubcarpeta_ConFormatoAnioMes_DevuelveRutaConAnioYMes()
    {
        var resultado = FormatoCarpetaService.ConstruirSubcarpeta(FormatoCarpeta.AnioMes, "yyyy\\MM", new DateTime(2026, 9, 12));

        Assert.Equal("2026\\09", resultado);
    }

    [Fact]
    public void ConstruirSubcarpeta_ConFormatoAnioMesConcatenado_DevuelveAnioYAnioMesJuntos()
    {
        var resultado = FormatoCarpetaService.ConstruirSubcarpeta(FormatoCarpeta.AnioMes, "yyyy\\yyyyMM", new DateTime(2026, 9, 12));

        Assert.Equal("2026\\202609", resultado);
    }

    [Fact]
    public void ConstruirSubcarpeta_ConFormatoDirecto_DevuelveVacio()
    {
        var resultado = FormatoCarpetaService.ConstruirSubcarpeta(FormatoCarpeta.Directo, null, DateTime.Now);

        Assert.Equal(string.Empty, resultado);
    }

    public void Dispose()
    {
        Directory.Delete(_carpetaTemporal, recursive: true);
    }
}
