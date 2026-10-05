namespace Hormiguero.Mensajero.Core.ClickFactura;

public enum FiltroEstadoEnvioFactura
{
    Todos,
    Pendientes,
    Enviados,
}

public static class EstadoEnvioFactura
{
    public static IReadOnlyList<T> Filtrar<T>(
        IEnumerable<T> elementos,
        Func<T, string> obtenerRut,
        IReadOnlySet<string> enviados,
        FiltroEstadoEnvioFactura filtro
    ) => elementos.Where(elemento => Coincide(obtenerRut(elemento), enviados, filtro)).ToArray();

    public static bool Coincide(
        string rut,
        IReadOnlySet<string> enviados,
        FiltroEstadoEnvioFactura filtro
    ) =>
        filtro switch
        {
            FiltroEstadoEnvioFactura.Pendientes => !enviados.Contains(rut),
            FiltroEstadoEnvioFactura.Enviados => enviados.Contains(rut),
            _ => true,
        };
}
