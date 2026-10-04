using System.Globalization;

namespace Hormiguero.Mensajero.Core;

// Caja de tinta de las 14 fuentes estándar de PDF tal como la mide MuPDF (URW Nimbus) cuando
// el PDF no las trae incrustadas. Se genera con Recursos/generar_tinta_base14.py.
internal static class TintaBase14
{
    private static readonly Lazy<
        Dictionary<string, Dictionary<int, (int X0, int Y0, int X1, int Y1)>>
    > Tabla = new(Cargar);

    // Caja en milésimas de em (y hacia arriba) de la primera letra de "valor" en "fuente";
    // false si la fuente no es una de las estándar o la letra no está en la tabla.
    public static bool Buscar(
        string fuente,
        string valor,
        out (int X0, int Y0, int X1, int Y1) caja
    )
    {
        caja = default;
        return valor.Length > 0
            && Tabla.Value.TryGetValue(fuente, out var letras)
            && letras.TryGetValue(char.ConvertToUtf32(valor, 0), out caja);
    }

    private static Dictionary<string, Dictionary<int, (int X0, int Y0, int X1, int Y1)>> Cargar()
    {
        var tabla = new Dictionary<string, Dictionary<int, (int, int, int, int)>>(
            StringComparer.Ordinal
        );
        using Stream flujo =
            typeof(TintaBase14).Assembly.GetManifestResourceStream("tinta-base14.txt")
            ?? throw new InvalidOperationException("Falta el recurso tinta-base14.txt");
        using var lector = new StreamReader(flujo);
        Dictionary<int, (int, int, int, int)>? actual = null;
        while (lector.ReadLine() is { } linea)
        {
            if (linea.Length == 0 || linea[0] == '#')
                continue;
            if (linea[0] == '[')
            {
                actual = [];
                tabla[linea[1..^1]] = actual;
                continue;
            }

            string[] partes = linea.Split(' ');
            actual![int.Parse(partes[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture)] = (
                int.Parse(partes[1], CultureInfo.InvariantCulture),
                int.Parse(partes[2], CultureInfo.InvariantCulture),
                int.Parse(partes[3], CultureInfo.InvariantCulture),
                int.Parse(partes[4], CultureInfo.InvariantCulture)
            );
        }

        return tabla;
    }
}
