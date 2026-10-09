using Archivero;
using Hormiguero.Nucleo.Datos;

namespace Archivero.Tests;

public class DecisionPendienteFilaTests
{
    [Fact]
    public void DecisionModo_MuestraPreguntaYOpcionesYNoMuestraAccionesDeOrigen()
    {
        var decision = new DecisionPendienteCadena(
            12,
            "NVV 4512",
            "Nota de venta",
            "Proveedor X",
            "",
            ""
        );
        var modo = new DecisionModoPendiente(12, 34, "Proveedor X", ["Retiro", "Despacho"]);

        var fila = new DecisionPendienteFila(decision, modo);

        Assert.Equal("Modo", fila.Etiqueta);
        Assert.Equal("NVV 4512 · Proveedor X · ¿Qué modo tiene este proceso?", fila.Pregunta);
        Assert.Equal(["Retiro", "Despacho"], fila.OpcionesModo);
        Assert.False(fila.MostrarAccionesOrigen);
        Assert.True(fila.MostrarOpcionesModo);
    }

    [Fact]
    public void DecisionSinOrigen_ConservaSuEtiquetaYExplicacion()
    {
        var decision = new DecisionPendienteCadena(
            13,
            "Factura 10",
            "Factura",
            "Proveedor X",
            "N° OC propia",
            "123"
        );

        var fila = new DecisionPendienteFila(decision, null);

        Assert.Equal("Sin origen", fila.Etiqueta);
        Assert.Contains("N° OC propia 123", fila.Explicacion);
        Assert.True(fila.MostrarAccionesOrigen);
        Assert.Empty(fila.OpcionesModo);
    }

    [Fact]
    public void ElegirModo_MantieneLaFilaParaElDatoPendienteYOcultaOpciones()
    {
        var decision = new DecisionPendienteCadena(
            12,
            "NVV 4512",
            "Nota de venta",
            "Proveedor X",
            "",
            ""
        );
        var modo = new DecisionModoPendiente(12, 34, "Proveedor X", ["Retiro", "Despacho"]);
        var fila = new DecisionPendienteFila(decision, modo);

        fila.MarcarModoElegido(["Fecha de retiro"]);

        Assert.True(fila.ModoElegido);
        Assert.False(fila.MostrarOpcionesModo);
        Assert.True(fila.MostrarCapturaDato);
        Assert.Equal("Fecha de retiro", fila.DatoPendiente);
    }

    [Fact]
    public void FilaMuestraYAvanzaPorCadaDatoPendienteDelModo()
    {
        var decision = new DecisionPendienteCadena(
            12,
            "NVV 4512",
            "Nota de venta",
            "Proveedor X",
            "",
            ""
        );
        var modo = new DecisionModoPendiente(12, 34, "Proveedor X", ["Retiro"]);
        var fila = new DecisionPendienteFila(decision, modo);

        fila.MarcarModoElegido(["Fecha de retiro", "Fecha de entrega"]);
        fila.FechaDato = new DateTime(2026, 10, 8);
        fila.AvanzarDatoPendiente();

        Assert.Equal("Fecha de entrega", fila.DatoPendiente);
        Assert.Null(fila.FechaDato);
        Assert.True(fila.MostrarCapturaDato);
        fila.AvanzarDatoPendiente();
        Assert.Null(fila.DatoPendiente);
        Assert.False(fila.MostrarCapturaDato);
    }
}
