using Hormiguero.Mensajero.App;
using Hormiguero.Mensajero.Core;

namespace Hormiguero.Mensajero.Core.Tests;

public sealed class PoliticaImportacionClientesNvvTests
{
    [Fact]
    public void ListaVaciaPermiteImportarSinReemplazo()
    {
        Assert.Equal(
            EstadoImportacionClientesNvv.ListaVacia,
            PoliticaImportacionClientesNvv.Evaluar([])
        );
    }

    [Fact]
    public void SoloLaListaCompletaDeEjemploSePuedeReemplazarConConfirmacion()
    {
        Assert.Equal(
            EstadoImportacionClientesNvv.ClientesDeEjemplo,
            PoliticaImportacionClientesNvv.Evaluar(ClientesNvv.PorDefecto)
        );
        Assert.Equal(
            EstadoImportacionClientesNvv.ClientesDeEjemplo,
            PoliticaImportacionClientesNvv.Evaluar(ClientesNvv.PorDefecto.Reverse().ToArray())
        );
        Assert.Equal(
            "¿Reemplazar los clientes de ejemplo por los de Ofisuiza?",
            PoliticaImportacionClientesNvv.MensajeReemplazoEjemplos
        );
    }

    [Fact]
    public void ListaPersonalizadaSeConservaYExplicaLaRestriccion()
    {
        Assert.Equal(
            EstadoImportacionClientesNvv.ListaExistente,
            PoliticaImportacionClientesNvv.Evaluar([.. ClientesNvv.PorDefecto, "OTRO CLIENTE"])
        );
        Assert.Equal(
            "Solo se importa en una lista vacía.",
            PoliticaImportacionClientesNvv.MensajeListaExistente
        );
    }
}
