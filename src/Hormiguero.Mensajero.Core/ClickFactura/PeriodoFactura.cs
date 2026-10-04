namespace Hormiguero.Mensajero.Core.ClickFactura;

public static class PeriodoFactura
{
    private static readonly string[] Meses =
    [
        "Enero",
        "Febrero",
        "Marzo",
        "Abril",
        "Mayo",
        "Junio",
        "Julio",
        "Agosto",
        "Septiembre",
        "Octubre",
        "Noviembre",
        "Diciembre",
    ];

    public static string ConstruirDescripcion(
        int semanaUno,
        int mesUno,
        int anioUno,
        bool incluyeSegundaSemana,
        int semanaDos = 1,
        int mesDos = 1,
        int anioDos = 2026
    )
    {
        string baseDescripcion = $"{semanaUno}° semana de {Mes(mesUno).ToLowerInvariant()}";
        if (!incluyeSegundaSemana)
            return $"{baseDescripcion} {anioUno}";
        string anios = anioUno == anioDos ? anioUno.ToString() : $"{anioUno}-{anioDos}";
        return $"{baseDescripcion} y {semanaDos}° semana de {Mes(mesDos).ToLowerInvariant()} {anios}";
    }

    public static IReadOnlyList<(int Anio, int Mes)>? ObtenerPeriodos(
        int anioUno,
        int mesUno,
        bool busquedaAmpliada,
        bool incluyeSegundaSemana = false,
        int anioDos = 0,
        int mesDos = 0
    ) =>
        busquedaAmpliada ? null
        : incluyeSegundaSemana ? [(anioUno, mesUno), (anioDos, mesDos)]
        : [(anioUno, mesUno)];

    private static string Mes(int numero)
    {
        if (numero is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(numero));
        return Meses[numero - 1];
    }
}
