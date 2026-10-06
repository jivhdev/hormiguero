using Archivero.Datos;

namespace Archivero.Servicios;

public static class PeriodoAtajoService
{
    public static DateTime? ResolverFecha(
        AtajoGuardadoRapido atajo,
        string? textoFecha,
        DateTime hoy,
        out string? error
    )
    {
        error = null;
        if (atajo.Periodo == PeriodoAtajo.PreguntarFechaCadaVez)
        {
            if (string.IsNullOrWhiteSpace(textoFecha))
            {
                if (!OrganizacionCarpetaService.FechaEsOpcional(atajo.Formato))
                    error = "Escribir la fecha del documento.";
                return error is null ? hoy : null;
            }

            if (FechaExtraidaService.TryParsear(textoFecha, out var fecha))
                return fecha;

            error = "No se reconoce la fecha escrita (ej. 15/03/2026).";
            return null;
        }

        if (atajo.Periodo == PeriodoAtajo.AnioFijo)
        {
            if (atajo.AnioFijo is null or < 1 or > 9999)
            {
                error = "Elegir un año fijo válido para este acceso rápido.";
                return null;
            }
            return new DateTime(
                atajo.AnioFijo.Value,
                hoy.Month,
                Math.Min(hoy.Day, DateTime.DaysInMonth(atajo.AnioFijo.Value, hoy.Month))
            );
        }

        return hoy;
    }
}
