namespace Hormiguero.Nucleo.Utilidades;

public record NumeroEnNombre(string Numero, string Prefijo, string Sufijo);

public static class NombreArchivo
{
    public static IReadOnlyList<NumeroEnNombre> Extraer(string nombreArchivo)
    {
        string nombre = Path.GetFileNameWithoutExtension(nombreArchivo);
        var numeros = new List<NumeroEnNombre>();

        int posicion = 0;
        while (posicion < nombre.Length)
        {
            if (!char.IsDigit(nombre[posicion]))
            {
                posicion++;
                continue;
            }

            int inicio = posicion;
            while (posicion < nombre.Length && char.IsDigit(nombre[posicion]))
            {
                posicion++;
            }

            string numero = NumeroSinCeros(nombre, inicio, posicion);
            numeros.Add(
                new NumeroEnNombre(numero, Prefijo(nombre, inicio), Sufijo(nombre, posicion))
            );
        }

        return numeros;
    }

    private static string NumeroSinCeros(string nombre, int inicio, int fin)
    {
        while (inicio < fin - 1 && nombre[inicio] == '0')
        {
            inicio++;
        }

        return nombre[inicio..fin];
    }

    private static string Prefijo(string nombre, int inicioNumero)
    {
        int posicion = inicioNumero;
        while (posicion > 0 && Separador(nombre[posicion - 1]))
        {
            posicion--;
        }

        int fin = posicion;
        while (posicion > 0 && char.IsLetter(nombre[posicion - 1]))
        {
            posicion--;
        }

        return nombre[posicion..fin];
    }

    private static string Sufijo(string nombre, int finNumero)
    {
        int posicion = finNumero;
        while (posicion < nombre.Length && Separador(nombre[posicion]))
        {
            posicion++;
        }

        int inicio = posicion;
        while (posicion < nombre.Length && char.IsLetter(nombre[posicion]))
        {
            posicion++;
        }

        return nombre[inicio..posicion];
    }

    private static bool Separador(char caracter) =>
        char.IsWhiteSpace(caracter) || caracter == '_' || caracter == '-';
}
