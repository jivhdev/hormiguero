using Buscadero.Core.Alertas;
using Hormiguero.Nucleo.Datos;

namespace Hormiguero.Buscadero.Core.Tests;

public sealed class EditorReglasEsquemaTests
{
    [Fact]
    public void ArmaJsonConElFormatoDeLosTresTipos()
    {
        Assert.Equal(
            "{\"dato\":\"Fecha de retiro\",\"texto\":\"Ingresar fecha\"}",
            EditorReglasEsquema.CrearJson(
                "falta_dato",
                new ParametrosFaltaDato("Fecha de retiro", "Ingresar fecha")
            )
        );
        Assert.Contains(
            "\"desdeLugarId\":12",
            EditorReglasEsquema.CrearJson(
                "plazo",
                new ParametrosPlazo(12, null, 15, 7, "corridos", null, null, "Guía pendiente", true)
            )
        );
        Assert.Contains(
            "\"cuandoLugarId\":12",
            EditorReglasEsquema.CrearJson(
                "listo_para",
                new ParametrosListoPara(12, null, 15, "Listos para facturar")
            )
        );
    }

    [Fact]
    public void ValidaReglasYGeneraFrasesAmigables()
    {
        Assert.NotNull(
            EditorReglasEsquema.Validar("falta_dato", new ParametrosFaltaDato("", "Aviso"))
        );
        Assert.Null(
            EditorReglasEsquema.Validar(
                "plazo",
                new ParametrosPlazo(
                    null,
                    "Fecha de retiro",
                    15,
                    7,
                    "corridos",
                    null,
                    null,
                    "Guía pendiente",
                    false
                )
            )
        );
        Assert.Equal(
            "Si llega Factura y en 7 días corridos no llega Guía, avisar: «Guía pendiente».",
            EditorReglasEsquema.CrearFrase(
                "plazo",
                new ParametrosPlazo(
                    12,
                    null,
                    15,
                    7,
                    "corridos",
                    null,
                    null,
                    "Guía pendiente",
                    false
                ),
                new Dictionary<long, string> { [12] = "Factura", [15] = "Guía" }
            )
        );
    }

    [Fact]
    public void OrdenaUrgenciasDeMayorAPrioridad()
    {
        Assert.Equal(
            new[] { 0, 1, 2 },
            new[] { "vencido", "por_vencer", "normal" }.Select(EditorReglasEsquema.OrdenUrgencia)
        );
    }
}
