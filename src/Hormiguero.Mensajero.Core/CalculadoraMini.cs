using System.Globalization;

namespace Hormiguero.Mensajero.Core;

public static class CalculadoraMini
{
    private static readonly CultureInfo CulturaChilena = CultureInfo.GetCultureInfo("es-CL");

    public static decimal Evaluar(string expresion)
    {
        ArgumentNullException.ThrowIfNull(expresion);
        var analizador = new Analizador(expresion);
        decimal resultado = analizador.LeerExpresion();
        analizador.IgnorarEspacios();
        if (!analizador.Termino)
            throw new FormatException("La expresión contiene caracteres no válidos.");
        if (analizador.CantidadNumeros < 2)
            throw new FormatException("Escribe una operación con al menos dos números.");
        return resultado;
    }

    public static string FormatearResultado(decimal resultado) =>
        decimal.Round(resultado, 2, MidpointRounding.AwayFromZero)
            .ToString("#,##0.##", CulturaChilena);

    public static string FormatearParaCopiar(decimal resultado) =>
        decimal.Round(resultado, 2, MidpointRounding.AwayFromZero).ToString("0.##", CulturaChilena);

    private sealed class Analizador(string texto)
    {
        private int posicion;

        public int CantidadNumeros { get; private set; }

        public bool Termino => posicion == texto.Length;

        public void IgnorarEspacios()
        {
            while (posicion < texto.Length && char.IsWhiteSpace(texto[posicion]))
                posicion++;
        }

        public decimal LeerExpresion()
        {
            decimal valor = LeerTermino();
            while (true)
            {
                IgnorarEspacios();
                if (Consumir('+'))
                    valor += LeerTermino();
                else if (Consumir('-'))
                    valor -= LeerTermino();
                else
                    return valor;
            }
        }

        private decimal LeerTermino()
        {
            decimal valor = LeerUnario();
            while (true)
            {
                IgnorarEspacios();
                if (Consumir('*') || Consumir('x') || Consumir('X'))
                    valor *= LeerUnario();
                else if (Consumir('/') || Consumir('÷'))
                {
                    decimal divisor = LeerUnario();
                    if (divisor == 0)
                        throw new DivideByZeroException("No se puede dividir por cero.");
                    valor /= divisor;
                }
                else
                    return valor;
            }
        }

        private decimal LeerUnario()
        {
            IgnorarEspacios();
            if (Consumir('+'))
                return LeerUnario();
            if (Consumir('-'))
                return -LeerUnario();
            return LeerPrimario();
        }

        private decimal LeerPrimario()
        {
            IgnorarEspacios();
            if (Consumir('('))
            {
                decimal valor = LeerExpresion();
                IgnorarEspacios();
                if (!Consumir(')'))
                    throw new FormatException("Falta cerrar un paréntesis.");
                return valor;
            }

            string numero = LeerNumero();
            CantidadNumeros++;
            if (
                !decimal.TryParse(
                    numero,
                    NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint,
                    CulturaChilena,
                    out decimal valorNumero
                )
            )
                throw new FormatException("El número no tiene un formato válido.");
            return valorNumero;
        }

        private string LeerNumero()
        {
            IgnorarEspacios();
            int inicio = posicion;
            while (
                posicion < texto.Length
                && (char.IsAsciiDigit(texto[posicion]) || texto[posicion] == '.')
            )
                posicion++;
            if (posicion < texto.Length && texto[posicion] == ',')
            {
                posicion++;
                int inicioDecimal = posicion;
                while (posicion < texto.Length && char.IsAsciiDigit(texto[posicion]))
                    posicion++;
                if (inicioDecimal == posicion)
                    throw new FormatException("Escribe al menos un decimal después de la coma.");
            }

            if (inicio == posicion)
                throw new FormatException("Se esperaba un número.");

            string numero = texto[inicio..posicion];
            if (!AgrupacionValida(numero))
                throw new FormatException(
                    "Los puntos deben separar grupos de miles de tres cifras."
                );
            return numero;
        }

        private static bool AgrupacionValida(string numero)
        {
            int finEntero = numero.IndexOf(',');
            if (finEntero < 0)
                finEntero = numero.Length;
            string entero = numero[..finEntero];
            string[] grupos = entero.Split('.');
            if (grupos.Any(grupo => grupo.Length == 0 || !grupo.All(char.IsAsciiDigit)))
                return false;
            return grupos.Length == 1
                || (
                    grupos[0].Length is >= 1 and <= 3
                    && grupos.Skip(1).All(grupo => grupo.Length == 3)
                );
        }

        private bool Consumir(char caracter)
        {
            if (posicion >= texto.Length || texto[posicion] != caracter)
                return false;
            posicion++;
            return true;
        }
    }
}
