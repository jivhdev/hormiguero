using Buscadero.Core.Busqueda;

namespace Buscadero.Core.Tests;

public sealed class HistorialBusquedaTests
{
    [Fact]
    public void Agregar_YMoverseAtrasYAdelante()
    {
        var historial = new HistorialBusqueda();
        var primero = CrearEntrada("primero");
        var segundo = CrearEntrada("segundo");
        historial.Agregar(primero);
        historial.Agregar(segundo);

        Assert.False(historial.PuedeIrAdelante);
        Assert.Equal(primero, historial.Atras());
        Assert.True(historial.PuedeIrAdelante);
        Assert.Equal(segundo, historial.Adelante());
        Assert.Null(historial.Adelante());
    }

    [Fact]
    public void AgregarTrasVolverEliminaLasEntradasAdelante()
    {
        var historial = new HistorialBusqueda();
        historial.Agregar(CrearEntrada("primero"));
        historial.Agregar(CrearEntrada("segundo"));
        historial.Atras();

        historial.Agregar(CrearEntrada("nuevo"));

        Assert.False(historial.PuedeIrAdelante);
        Assert.Equal("primero", historial.Atras()!.Numero);
        Assert.Equal("nuevo", historial.Adelante()!.Numero);
    }

    [Fact]
    public void AgregarConservaComoMaximoTreintaEntradas()
    {
        var historial = new HistorialBusqueda();
        for (var indice = 0; indice < 35; indice++)
        {
            historial.Agregar(CrearEntrada(indice.ToString()));
        }

        for (var indice = 33; indice >= 5; indice--)
        {
            Assert.Equal(indice.ToString(), historial.Atras()!.Numero);
        }

        Assert.False(historial.PuedeIrAtras);
        Assert.Equal("5", historial.Actual!.Numero);
    }

    private static EntradaHistorialBusqueda CrearEntrada(string numero) =>
        new(numero, AlcanceBusqueda.PrimeraCoincidencia, "", ModoBusqueda.Todos, [], null, 0, 1);
}
