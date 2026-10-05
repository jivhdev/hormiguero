namespace Hormiguero.Nucleo.Utilidades;

public enum TipoDias
{
    Corridos,
    Habiles,
}

public static class CalculoFechas
{
    public const int CantidadMaxima = 3650;

    public static DateOnly Sumar(
        DateOnly fechaBase,
        int cantidad,
        TipoDias tipo,
        IReadOnlySet<DateOnly>? feriados = null
    )
    {
        if (cantidad is < -CantidadMaxima or > CantidadMaxima)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cantidad),
                cantidad,
                $"La cantidad de días debe estar entre -{CantidadMaxima} y {CantidadMaxima}."
            );
        }

        if (cantidad == 0)
        {
            return fechaBase;
        }

        if (tipo == TipoDias.Corridos)
        {
            return fechaBase.AddDays(cantidad);
        }

        int paso = cantidad > 0 ? 1 : -1;
        DateOnly fecha = fechaBase;
        int pendientes = Math.Abs(cantidad);
        while (pendientes > 0)
        {
            fecha = fecha.AddDays(paso);
            if (EsHabil(fecha, feriados))
            {
                pendientes--;
            }
        }

        return fecha;
    }

    public static bool EsHabil(DateOnly fecha, IReadOnlySet<DateOnly>? feriados = null)
    {
        if (fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        return feriados is null || !feriados.Contains(fecha);
    }
}
