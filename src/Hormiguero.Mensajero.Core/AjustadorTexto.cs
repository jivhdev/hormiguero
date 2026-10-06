namespace Hormiguero.Mensajero.Core;

public sealed record ResultadoAjusteTexto(
    IReadOnlyList<string> Lineas,
    string TextoFuera,
    int CaracteresSobran,
    bool TienePalabraLarga
)
{
    public bool Cabe => CaracteresSobran == 0 && !TienePalabraLarga;

    public string ConSaltosDeLinea => string.Join(Environment.NewLine, Lineas);

    public string Relleno(int ancho, int maximoLineas) =>
        string.Concat(
            Enumerable
                .Range(0, maximoLineas)
                .Select(indice =>
                    indice < Lineas.Count ? Lineas[indice].PadRight(ancho) : new string(' ', ancho)
                )
        );
}

public static class AjustadorTexto
{
    public const int LineasPredeterminadas = 4;
    public const int AnchoPredeterminado = 57;

    public static ResultadoAjusteTexto Ajustar(string texto, int maximoLineas, int ancho)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximoLineas);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ancho);

        string[][] palabrasPorLinea = texto
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(linea => linea.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Where(palabras => palabras.Length > 0)
            .ToArray();
        bool tienePalabraLarga = palabrasPorLinea
            .SelectMany(palabras => palabras)
            .Any(palabra => palabra.Length > ancho);

        List<string> conservandoSaltos = [];
        List<string> fueraConSaltos = [];
        foreach (string[] palabras in palabrasPorLinea)
        {
            var resultadoLinea = Distribuir(palabras, ancho, int.MaxValue);
            conservandoSaltos.AddRange(resultadoLinea.Lineas);
            fueraConSaltos.AddRange(resultadoLinea.Fuera);
        }

        List<string> lineas;
        string textoFuera = string.Empty;
        if (conservandoSaltos.Count <= maximoLineas && fueraConSaltos.Count == 0)
        {
            lineas = conservandoSaltos;
        }
        else
        {
            string[] palabras = palabrasPorLinea.SelectMany(palabras => palabras).ToArray();
            var resultadoUnido = Distribuir(palabras, ancho, maximoLineas);
            lineas = resultadoUnido.Lineas;
            textoFuera = string.Join(' ', resultadoUnido.Fuera);
        }

        return new(lineas, textoFuera, textoFuera.Length, tienePalabraLarga);
    }

    private static (List<string> Lineas, List<string> Fuera) Distribuir(
        IEnumerable<string> palabras,
        int ancho,
        int maximoLineas
    )
    {
        List<string> lineas = [];
        List<string> fuera = [];
        string actual = string.Empty;
        string[] listaPalabras = palabras.ToArray();
        for (int indice = 0; indice < listaPalabras.Length; indice++)
        {
            string palabra = listaPalabras[indice];
            if (palabra.Length > ancho)
            {
                fuera.Add(palabra);
                continue;
            }

            string candidata = actual.Length == 0 ? palabra : actual + " " + palabra;
            if (candidata.Length > ancho)
            {
                lineas.Add(actual);
                if (lineas.Count == maximoLineas)
                {
                    fuera.AddRange(listaPalabras[indice..]);
                    break;
                }
                actual = palabra;
            }
            else
            {
                actual = candidata;
            }
        }

        if (actual.Length > 0 && lineas.Count < maximoLineas)
        {
            lineas.Add(actual);
        }
        return (lineas, fuera);
    }
}
