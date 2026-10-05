using Hormiguero.Mensajero.Core;

namespace Hormiguero.Mensajero.App;

public enum EstadoImportacionClientesNvv
{
    ListaVacia,
    ClientesDeEjemplo,
    ListaExistente,
}

public static class PoliticaImportacionClientesNvv
{
    public const string MensajeListaExistente = "Solo se importa en una lista vacía.";
    public const string MensajeReemplazoEjemplos =
        "¿Reemplazar los clientes de ejemplo por los de Ofisuiza?";

    public static EstadoImportacionClientesNvv Evaluar(IReadOnlyList<string> clientes) =>
        clientes.Count == 0 ? EstadoImportacionClientesNvv.ListaVacia
        : clientes.Count == ClientesNvv.PorDefecto.Count
        && ClientesNvv.PorDefecto.All(cliente => clientes.Contains(cliente, StringComparer.Ordinal))
            ? EstadoImportacionClientesNvv.ClientesDeEjemplo
        : EstadoImportacionClientesNvv.ListaExistente;
}
