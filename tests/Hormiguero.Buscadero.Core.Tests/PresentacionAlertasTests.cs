using Buscadero.Core.Alertas;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Utilidades;

namespace Hormiguero.Buscadero.Core.Tests;

public sealed class PresentacionAlertasTests
{
    [Fact]
    public void ParaCuando_MuestraHoyYDiasHabiles()
    {
        var hoy = new DateOnly(2026, 10, 5);
        Assert.Equal("vence hoy", PresentacionAlertas.ParaCuando(hoy, hoy, TipoDias.Habiles));
        Assert.Equal(
            "vence en 3 días hábiles",
            PresentacionAlertas.ParaCuando(new(2026, 10, 8), hoy, TipoDias.Habiles)
        );
        Assert.Equal(
            "venció hace 2 días hábiles",
            PresentacionAlertas.ParaCuando(new(2026, 10, 1), hoy, TipoDias.Habiles)
        );
    }

    [Fact]
    public void CrearFraseRegla_UsaTextoAmigable()
    {
        Assert.Equal(
            "Si llega Guía y en 5 días hábiles no llega Factura, avisar: Guía esperando factura",
            PresentacionAlertas.CrearFraseRegla(
                "Guía",
                5,
                TipoDias.Habiles,
                "Factura",
                "Guía esperando factura"
            )
        );
    }

    [Fact]
    public void Filtrar_SeparaEstadosSolicitados()
    {
        var alertas = new[]
        {
            Crear("pendiente"),
            Crear("vencida"),
            Crear("resuelta"),
            Crear("descartada"),
        };
        Assert.Equal(2, PresentacionAlertas.Filtrar(alertas, "Pendientes").Count);
        Assert.Single(PresentacionAlertas.Filtrar(alertas, "Vencidas"));
        Assert.Single(PresentacionAlertas.Filtrar(alertas, "Resueltas"));
        Assert.Equal(4, PresentacionAlertas.Filtrar(alertas, "Todas").Count);
    }

    private static Alerta Crear(string estado) =>
        new(1, null, null, null, null, "Aviso", estado, null, new(2026, 10, 5), DateTime.Now);
}
