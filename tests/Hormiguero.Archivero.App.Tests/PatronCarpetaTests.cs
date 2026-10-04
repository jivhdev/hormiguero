using Archivero.Datos;
using Archivero.Servicios;

namespace Archivero.Tests;

/// <summary>
/// Motor de patrones de carpeta extendido por Caso-3: tokens nuevos (semestre, trimestre,
/// quincena, semana ISO, semana del mes, día), hasta tres niveles, próximo período por tipo,
/// y compatibilidad hacia atrás con los patrones que ya se guardaban antes del rediseño.
/// </summary>
public class PatronCarpetaTests : IDisposable
{
    private readonly string _carpetaTemporal;

    public PatronCarpetaTests()
    {
        _carpetaTemporal = Path.Combine(Path.GetTempPath(), "archivero-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpetaTemporal);
    }

    [Theory]
    [InlineData("yyyy", "2026-03-15", "2026")]
    [InlineData("yy", "2026-03-15", "26")]
    [InlineData("'S'S", "2026-03-15", "S1")]
    [InlineData("'S'S", "2026-09-15", "S2")]
    [InlineData("'Semestre 'S", "2026-09-15", "Semestre 2")]
    [InlineData("'T'T", "2026-03-15", "T1")]
    [InlineData("'T'T", "2026-11-15", "T4")]
    [InlineData("'Q'T", "2026-03-15", "Q1")]
    [InlineData("MM", "2026-03-15", "03")]
    [InlineData("MMMM", "2026-03-15", "Marzo")]
    [InlineData("yyyyMM", "2026-03-15", "202603")]
    [InlineData("MM-yyyy", "2026-03-15", "03-2026")]
    [InlineData("'Q'H", "2026-03-15", "Q1")]
    [InlineData("'Q'H", "2026-03-16", "Q2")]
    [InlineData("MMMM' - 'O' quincena'", "2026-03-15", "Marzo - 1ra quincena")]
    [InlineData("MMMM' - 'O' quincena'", "2026-03-16", "Marzo - 2da quincena")]
    [InlineData("'Semana 'WW", "2026-03-15", "Semana 11")]
    [InlineData("'Semana 'WW", "2026-01-01", "Semana 01")]
    [InlineData("'W'WW", "2026-03-15", "W11")]
    [InlineData("dd", "2026-03-15", "15")]
    [InlineData("yyyyMMdd", "2026-03-15", "20260315")]
    [InlineData("'Mes 'MM", "2026-03-15", "Mes 03")]
    public void FormatearNivel_ConTokensDelCatalogo_GeneraElNombreEsperado(string nivel, string fechaTexto, string esperado)
    {
        var resultado = FormatoCarpetaService.FormatearNivel(nivel, DateTime.Parse(fechaTexto));

        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData("2026-03-01", 1)] // marzo 2026 empieza domingo: el día 1 está en la Semana 1
    [InlineData("2026-03-07", 2)]
    [InlineData("2026-03-15", 3)]
    [InlineData("2026-03-30", 6)] // los meses de 31 días que empiezan domingo llegan a Semana 6
    [InlineData("2026-06-01", 1)] // junio 2026 empieza lunes: semana 1 = 1 al 7
    [InlineData("2026-06-07", 1)]
    [InlineData("2026-06-08", 2)]
    public void FormatearNivel_SemanaDelMes_CuentaSemanasLunesADomingoTocandoElMes(string fechaTexto, int semanaEsperada)
    {
        var resultado = FormatoCarpetaService.FormatearNivel("'Semana 'N", DateTime.Parse(fechaTexto));

        Assert.Equal($"Semana {semanaEsperada}", resultado);
    }

    [Fact]
    public void FormatearNivel_ConTokenDesconocido_UsaElFormateoNetDeSiempreComoRespaldo()
    {
        // "MMM" no es un token del motor propio: el nivel se formatea con DateTime.ToString como
        // antes de Caso-3, para que cualquier patrón viejo o exótico siga funcionando igual.
        var fecha = new DateTime(2026, 3, 15);
        var esperado = fecha.ToString("MMM", System.Globalization.CultureInfo.GetCultureInfo("es-ES"));

        Assert.Equal(esperado, FormatoCarpetaService.FormatearNivel("MMM", fecha));
    }

    [Fact]
    public void ConstruirSubcarpeta_ConTresNiveles_CombinaLasPartes()
    {
        var resultado = FormatoCarpetaService.ConstruirSubcarpeta(
            FormatoCarpeta.AnioMesDia, @"yyyy\MM\dd", new DateTime(2026, 3, 15));

        Assert.Equal(Path.Combine("2026", "03", "15"), resultado);
    }

    [Fact]
    public void ConstruirSubcarpeta_ConQuincenaDeTresNiveles_CombinaLasPartes()
    {
        var resultado = FormatoCarpetaService.ConstruirSubcarpeta(
            FormatoCarpeta.AnioQuincena, @"yyyy\MM\'Q'H", new DateTime(2026, 3, 20));

        Assert.Equal(Path.Combine("2026", "03", "Q2"), resultado);
    }

    [Theory]
    [InlineData("yyyy", "2026")]
    [InlineData("yy", "26")]
    [InlineData(@"yyyy\MM", @"2026\03")]
    [InlineData(@"yyyy\yyyyMM", @"2026\202603")]
    public void ConstruirSubcarpeta_ConPatronesAnterioresACaso3_SigueFuncionandoIgual(string patron, string esperado)
    {
        var resultado = FormatoCarpetaService.ConstruirSubcarpeta(
            patron.Contains('\\') ? FormatoCarpeta.AnioMes : FormatoCarpeta.Anio,
            patron,
            new DateTime(2026, 3, 15));

        Assert.Equal(esperado.Replace('\\', Path.DirectorySeparatorChar), resultado);
    }

    [Fact]
    public void ConstruirSubcarpeta_ConPatronDetectadoPorEvidenciaAntesDeCaso3_SigueFuncionando()
    {
        var resultado = FormatoCarpetaService.ConstruirSubcarpeta(
            FormatoCarpeta.Anio, "'Año 'yyyy", new DateTime(2026, 3, 15));

        Assert.Equal("Año 2026", resultado);
    }

    [Theory]
    [InlineData(FormatoCarpeta.Anio, null, "2026-03-15", "2027-03-15")]
    [InlineData(FormatoCarpeta.AnioSemestre, null, "2026-03-15", "2026-09-15")]
    [InlineData(FormatoCarpeta.AnioTrimestre, null, "2026-03-15", "2026-06-15")]
    [InlineData(FormatoCarpeta.AnioMes, null, "2026-03-15", "2026-04-15")]
    [InlineData(FormatoCarpeta.AnioQuincena, null, "2026-03-10", "2026-03-16")]
    [InlineData(FormatoCarpeta.AnioQuincena, null, "2026-03-20", "2026-04-01")]
    [InlineData(FormatoCarpeta.AnioSemana, null, "2026-03-15", "2026-03-22")]
    [InlineData(FormatoCarpeta.AnioMesDia, null, "2026-03-15", "2026-03-16")]
    [InlineData(FormatoCarpeta.MesSinAnio, null, "2026-12-15", "2027-01-15")]
    [InlineData(FormatoCarpeta.SemanaDelMes, null, "2026-03-15", "2026-03-22")]
    [InlineData(FormatoCarpeta.Personalizado, @"yyyy\MM", "2026-03-15", "2026-04-15")]
    [InlineData(FormatoCarpeta.Personalizado, "'Semestre 'S", "2026-03-15", "2026-09-15")]
    [InlineData(FormatoCarpeta.Personalizado, "yyyy-'W'WW", "2026-03-15", "2026-03-22")]
    [InlineData(FormatoCarpeta.Personalizado, "yyyy-MM-dd", "2026-03-15", "2026-03-16")]
    public void SiguientePeriodo_AvanzaUnPeriodoSegunElTipo(FormatoCarpeta formato, string? patron, string fechaTexto, string esperado)
    {
        var resultado = FormatoCarpetaService.SiguientePeriodo(formato, DateTime.Parse(fechaTexto), patron);

        Assert.Equal(DateTime.Parse(esperado), resultado);
    }

    [Fact]
    public void SiguientePeriodo_PersonalizadoSinTokensDeFecha_AvanzaUnAnio()
    {
        var resultado = FormatoCarpetaService.SiguientePeriodo(
            FormatoCarpeta.Personalizado, new DateTime(2026, 3, 15), "'Solo texto'");

        Assert.Equal(new DateTime(2027, 3, 15), resultado);
    }

    [Fact]
    public void BuscarCarpetaAnteriorReal_ConTresNiveles_DevuelveLaMasRecienteExistente()
    {
        // Fechas relativas a hoy: la ventana de candidatos del servicio es acotada y estos
        // tests no deben romperse con el paso de los años.
        var anterior = DateTime.Today.AddMonths(-1);
        var siguiente = anterior.AddDays(1);
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, FormatoCarpetaService.ConstruirSubcarpeta(FormatoCarpeta.AnioMesDia, @"yyyy\MM\dd", anterior)));
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, FormatoCarpetaService.ConstruirSubcarpeta(FormatoCarpeta.AnioMesDia, @"yyyy\MM\dd", siguiente)));

        var resultado = FormatoCarpetaService.BuscarCarpetaAnteriorReal(
            _carpetaTemporal, FormatoCarpeta.AnioMesDia, @"yyyy\MM\dd");

        Assert.Equal(
            Path.Combine(_carpetaTemporal, FormatoCarpetaService.ConstruirSubcarpeta(FormatoCarpeta.AnioMesDia, @"yyyy\MM\dd", siguiente)),
            resultado);
    }

    [Fact]
    public void BuscarCarpetaAnteriorReal_OrdenaCronologicamenteNoAlfabeticamente()
    {
        // Con nombres de mes, el orden alfabético ("Abril" < "Marzo") no es el cronológico:
        // la carpeta anterior tiene que ser la del período más reciente, no la que ordena última.
        var anio = DateTime.Today.AddYears(-1).Year.ToString();
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, anio, "Abril"));
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, anio, "Marzo"));

        var resultado = FormatoCarpetaService.BuscarCarpetaAnteriorReal(
            _carpetaTemporal, FormatoCarpeta.AnioMes, @"yyyy\MMMM");

        Assert.Equal(Path.Combine(_carpetaTemporal, anio, "Abril"), resultado);
    }

    [Fact]
    public void BuscarCarpetaAnteriorReal_CoincideSinDistinguirMayusculas()
    {
        // Las carpetas reales creadas antes podían tener el mes en minúscula ("marzo"): en
        // Windows las carpetas no distinguen mayúsculas, la búsqueda tampoco debe hacerlo.
        var anio = DateTime.Today.AddYears(-1).Year.ToString();
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, anio, "marzo"));

        var resultado = FormatoCarpetaService.BuscarCarpetaAnteriorReal(
            _carpetaTemporal, FormatoCarpeta.AnioMes, @"yyyy\MMMM");

        Assert.Equal(Path.Combine(_carpetaTemporal, anio, "marzo"), resultado);
    }

    [Fact]
    public void BuscarCarpetaAnteriorReal_SinCoincidencias_DevuelveNull()
    {
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, "Facturas"));

        var resultado = FormatoCarpetaService.BuscarCarpetaAnteriorReal(
            _carpetaTemporal, FormatoCarpeta.AnioSemana, @"yyyy\'Semana 'WW");

        Assert.Null(resultado);
    }

    [Fact]
    public void BuscarCarpetaAnteriorReal_SiElPeriodoMasNuevoEstaVacio_BusaEnLosAnteriores()
    {
        var anioActual = DateTime.Today.Year.ToString();
        var anioAnterior = (DateTime.Today.Year - 1).ToString();
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, anioAnterior, "12"));
        Directory.CreateDirectory(Path.Combine(_carpetaTemporal, anioActual));

        var resultado = FormatoCarpetaService.BuscarCarpetaAnteriorReal(
            _carpetaTemporal, FormatoCarpeta.AnioMes, @"yyyy\MM");

        Assert.Equal(Path.Combine(_carpetaTemporal, anioAnterior, "12"), resultado);
    }

    public void Dispose()
    {
        Directory.Delete(_carpetaTemporal, recursive: true);
    }
}
