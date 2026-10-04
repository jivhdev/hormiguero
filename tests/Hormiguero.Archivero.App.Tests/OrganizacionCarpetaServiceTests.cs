using Archivero.Datos;
using Archivero.Servicios;

namespace Archivero.Tests;

/// <summary>
/// Caso-3: el catálogo de tipos de organización y sus ejemplos concretos. La fecha de referencia
/// de los asserts es la del documento de Caso-3 (15 de marzo de 2026), salvo donde se indica otra.
/// </summary>
public class OrganizacionCarpetaServiceTests
{
    private static readonly DateTime FechaDoc = new(2026, 3, 15);

    [Fact]
    public void TodosLosTipos_TieneElOrdenExactoDeLaListaCompletaDeCaso3()
    {
        var nombres = OrganizacionCarpetaService.TodosLosTipos.Select(t => t.Nombre).ToList();

        Assert.Equal(
            [
                "Directo en la carpeta",
                "Por año",
                "Por año y semestre",
                "Por año y trimestre",
                "Por año y mes",
                "Por año y quincena",
                "Por año y semana",
                "Por año, mes y día",
                "Por mes (sin año)",
                "Por semana del mes",
            ],
            nombres);
    }

    [Fact]
    public void TodosLosTipos_OrdenadosPorGranularidadCreciente()
    {
        var formatos = OrganizacionCarpetaService.TodosLosTipos.Select(t => t.Formato).ToList();

        Assert.Equal(
            [
                FormatoCarpeta.Directo,
                FormatoCarpeta.Anio,
                FormatoCarpeta.AnioSemestre,
                FormatoCarpeta.AnioTrimestre,
                FormatoCarpeta.AnioMes,
                FormatoCarpeta.AnioQuincena,
                FormatoCarpeta.AnioSemana,
                FormatoCarpeta.AnioMesDia,
                FormatoCarpeta.MesSinAnio,
                FormatoCarpeta.SemanaDelMes,
            ],
            formatos);
    }

    [Fact]
    public void ObtenerEjemplos_PorAnio_MuestraSoloElAnioConcreto()
    {
        var textos = OrganizacionCarpetaService.ObtenerEjemplos(FormatoCarpeta.Anio, FechaDoc).Select(e => e.Texto).ToList();

        Assert.Equal(["2026"], textos);
    }

    [Fact]
    public void ObtenerEjemplos_PorAnioYSemestre_CoincideConLaTablaDeCaso3()
    {
        var textos = OrganizacionCarpetaService.ObtenerEjemplos(FormatoCarpeta.AnioSemestre, FechaDoc).Select(e => e.Texto).ToList();

        Assert.Equal(["2026/S1", "2026-S1", "2026/Semestre 1"], textos);
    }

    [Fact]
    public void ObtenerEjemplos_PorAnioYTrimestre_CoincideConLaTablaDeCaso3()
    {
        var textos = OrganizacionCarpetaService.ObtenerEjemplos(FormatoCarpeta.AnioTrimestre, FechaDoc).Select(e => e.Texto).ToList();

        Assert.Equal(["2026/T1", "2026-T1", "2026/Q1"], textos);
    }

    [Fact]
    public void ObtenerEjemplos_PorAnioYMes_CoincideConLaTablaDeCaso3MasLaVariantePedidaPorJavier()
    {
        var textos = OrganizacionCarpetaService.ObtenerEjemplos(FormatoCarpeta.AnioMes, FechaDoc).Select(e => e.Texto).ToList();

        // El último (2026/202603) es la variante yyyy\yyyyMM que Javier pidió explícitamente
        // en una sesión anterior y confirmó conservar para Caso-3.
        Assert.Equal(["2026/03", "202603", "2026-03", "03-2026", "2026/Marzo", "2026/202603"], textos);
    }

    [Fact]
    public void ObtenerEjemplos_PorAnioYQuincena_CoincideConLaTablaDeCaso3()
    {
        var textos = OrganizacionCarpetaService.ObtenerEjemplos(FormatoCarpeta.AnioQuincena, FechaDoc).Select(e => e.Texto).ToList();

        Assert.Equal(["2026/03/Q1", "2026-03-Q1", "2026/Marzo - 1ra quincena"], textos);
    }

    [Fact]
    public void ObtenerEjemplos_PorAnioYSemana_CoincideConLaTablaDeCaso3()
    {
        var textos = OrganizacionCarpetaService.ObtenerEjemplos(FormatoCarpeta.AnioSemana, FechaDoc).Select(e => e.Texto).ToList();

        Assert.Equal(["2026/Semana 11", "2026-W11"], textos);
    }

    [Fact]
    public void ObtenerEjemplos_PorAnioMesYDia_CoincideConLaTablaDeCaso3()
    {
        var textos = OrganizacionCarpetaService.ObtenerEjemplos(FormatoCarpeta.AnioMesDia, FechaDoc).Select(e => e.Texto).ToList();

        Assert.Equal(["2026/03/15", "20260315", "2026-03-15"], textos);
    }

    [Fact]
    public void ObtenerEjemplos_PorMesSinAnio_CoincideConLaTablaDeCaso3()
    {
        var textos = OrganizacionCarpetaService.ObtenerEjemplos(FormatoCarpeta.MesSinAnio, FechaDoc).Select(e => e.Texto).ToList();

        Assert.Equal(["03", "Marzo", "Mes 03"], textos);
    }

    [Fact]
    public void ObtenerEjemplos_PorSemanaDelMes_UsaSemanasLunesADomingoConDia1EnSemana1()
    {
        // 15-mar-2026 cae en la Semana 3 con la regla que eligió Javier (semanas de calendario
        // lunes-domingo que tocan el mes; la Semana 1 es la que contiene al día 1).
        var textos = OrganizacionCarpetaService.ObtenerEjemplos(FormatoCarpeta.SemanaDelMes, FechaDoc).Select(e => e.Texto).ToList();

        Assert.Equal(["03/Semana 3", "Marzo/Semana 3", "Mes 03/Semana 3"], textos);
    }

    [Fact]
    public void ObtenerEjemplos_Directo_NoTienePatron()
    {
        Assert.Empty(OrganizacionCarpetaService.ObtenerEjemplos(FormatoCarpeta.Directo, FechaDoc));
    }

    [Fact]
    public void LosEjemplosDeLaTabla_AlFormatearOtraFecha_CambianLosValoresYSiguenConLaMismaForma()
    {
        var ejemplos = OrganizacionCarpetaService.ObtenerEjemplos(FormatoCarpeta.AnioQuincena, new DateTime(2026, 7, 20));

        Assert.Equal(["2026/07/Q2", "2026-07-Q2", "2026/Julio - 2da quincena"], ejemplos.Select(e => e.Texto).ToList());
    }

    [Fact]
    public void NombreDe_TiposNuevos_DevuelveElNombreDelCatalogo()
    {
        Assert.Equal("Por año y trimestre", OrganizacionCarpetaService.NombreDe(FormatoCarpeta.AnioTrimestre));
        Assert.Equal("Patrón personalizado", OrganizacionCarpetaService.NombreDe(FormatoCarpeta.Personalizado));
    }

    [Fact]
    public void NormalizarPatronPersonalizado_AceptaBarraOBarraInvertidaComoSeparador()
    {
        Assert.Equal(@"yyyy\MM", OrganizacionCarpetaService.NormalizarPatronPersonalizado(" yyyy/MM "));
        Assert.Equal(@"yyyy\MM", OrganizacionCarpetaService.NormalizarPatronPersonalizado(@"yyyy\MM"));
    }

    [Theory]
    [InlineData(@"yyyy\MM", true)]
    [InlineData("'Semestre 'S", true)]
    [InlineData("", false)]
    [InlineData(@"yyyy\MM:dd", false)] // dos puntos: inválido en un nombre de carpeta de Windows
    public void EsPatronValido_ValidaFormaYCaracteresDeCarpeta(string patron, bool esperado)
    {
        var resultado = OrganizacionCarpetaService.EsPatronValido(patron, FechaDoc, out _);

        Assert.Equal(esperado, resultado);
    }
}
