using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Utilidades;

namespace Buscadero.Core.Alertas;

public static class PresentacionAlertas
{
    public static string OrigenDelPlazo(Alerta alerta) =>
        alerta.OrigenFecha switch
        {
            "fecha_documento" when alerta.FechaBase is DateOnly fecha =>
                $"Desde la fecha del documento {fecha.ToString("dd-MM-yyyy", System.Globalization.CultureInfo.InvariantCulture)}",
            "entrada_cadena" => "Desde que entró a la cadena",
            _ => string.Empty,
        };

    public static string ParaCuando(DateOnly objetivo, DateOnly hoy, TipoDias modo)
    {
        if (objetivo == hoy)
            return "vence hoy";
        if (objetivo < hoy)
        {
            int transcurridos = ContarDias(hoy, objetivo, modo);
            return $"venció hace {transcurridos} {NombreDias(transcurridos, modo)}";
        }

        int restantes = ContarDias(objetivo, hoy, modo);
        return $"vence en {restantes} {NombreDias(restantes, modo)}";
    }

    public static string CrearFraseRegla(
        string origen,
        int dias,
        TipoDias modo,
        string destino,
        string aviso
    ) =>
        $"Si llega {origen} y en {dias} días {NombreModo(modo)} no llega {destino}, avisar: {aviso}";

    public static IReadOnlyList<Alerta> Filtrar(IEnumerable<Alerta> alertas, string filtro) =>
        filtro switch
        {
            "Pendientes" => alertas.Where(a => a.Estado is "pendiente" or "vencida").ToList(),
            "Vencidas" => alertas.Where(a => a.Estado == "vencida").ToList(),
            "Resueltas" => alertas.Where(a => a.Estado == "resuelta").ToList(),
            "Todas" => alertas.ToList(),
            _ => throw new ArgumentException("El filtro de alertas no es válido.", nameof(filtro)),
        };

    private static int ContarDias(DateOnly hasta, DateOnly desde, TipoDias modo)
    {
        int dias = 0;
        for (var fecha = desde; fecha < hasta; fecha = fecha.AddDays(1))
            if (modo == TipoDias.Corridos || CalculoFechas.EsHabil(fecha.AddDays(1)))
                dias++;
        return dias;
    }

    private static string NombreDias(int cantidad, TipoDias modo) =>
        $"día{(cantidad == 1 ? string.Empty : "s")} {NombreModo(modo)}";

    private static string NombreModo(TipoDias modo) =>
        modo == TipoDias.Habiles ? "hábiles" : "corridos";
}
