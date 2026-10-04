using System.Text;

namespace Buscadero.Core.Busqueda;

public enum ModoBusqueda
{
    Todos,
    Exacto,
    Alfanumerico,
    SoloNumero,
    SoloLetras,
}

public static class CoincidenciaNumero
{
    public static readonly IReadOnlyList<ModoBusqueda> ModosEnOrden = new[]
    {
        ModoBusqueda.Exacto,
        ModoBusqueda.Alfanumerico,
        ModoBusqueda.SoloNumero,
        ModoBusqueda.SoloLetras,
    };

    public static bool Coincide(string nombreArchivo, string consulta, ModoBusqueda modo) =>
        modo switch
        {
            ModoBusqueda.Exacto => EsCoincidenciaExacta(nombreArchivo, consulta),
            ModoBusqueda.Alfanumerico => EsCoincidenciaAlfanumerica(nombreArchivo, consulta),
            ModoBusqueda.SoloNumero => EsCoincidenciaSoloNumero(nombreArchivo, consulta),
            ModoBusqueda.SoloLetras => EsCoincidenciaSoloLetras(nombreArchivo, consulta),
            _ => false,
        };

    public static bool EsCoincidenciaExacta(string nombreArchivo, string consulta)
    {
        if (string.IsNullOrWhiteSpace(consulta))
        {
            return false;
        }

        var nombre = NombreSinExtension(nombreArchivo);
        var indice = 0;
        while ((indice = nombre.IndexOf(consulta, indice, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var limiteInferior = indice == 0 || !char.IsLetterOrDigit(nombre[indice - 1]);
            var limiteSuperior =
                indice + consulta.Length == nombre.Length
                || !char.IsLetterOrDigit(nombre[indice + consulta.Length]);

            if (limiteInferior && limiteSuperior)
            {
                return true;
            }

            indice++;
        }

        return false;
    }

    public static bool EsCoincidenciaAlfanumerica(string nombreArchivo, string consulta)
    {
        var buscado = NormalizarAlfanumerico(consulta);
        return buscado.Length > 0
            && NormalizarAlfanumerico(NombreSinExtension(nombreArchivo)) == buscado;
    }

    public static bool EsCoincidenciaSoloNumero(string nombreArchivo, string consulta)
    {
        var buscado = NormalizarNumero(consulta);
        return buscado.Length > 0 && NormalizarNumero(NombreSinExtension(nombreArchivo)) == buscado;
    }

    public static bool EsCoincidenciaSoloLetras(string nombreArchivo, string consulta)
    {
        var buscado = NormalizarLetras(consulta);
        return buscado.Length > 0 && NormalizarLetras(NombreSinExtension(nombreArchivo)) == buscado;
    }

    private static string NombreSinExtension(string nombreArchivo) =>
        Path.GetFileNameWithoutExtension(nombreArchivo);

    private static string NormalizarAlfanumerico(string valor)
    {
        var resultado = new StringBuilder(valor.Length);
        foreach (var caracter in valor)
        {
            if (char.IsLetterOrDigit(caracter))
            {
                resultado.Append(char.ToLowerInvariant(caracter));
            }
        }

        return resultado.ToString();
    }

    private static string NormalizarNumero(string valor)
    {
        var soloDigitos = new StringBuilder(valor.Length);
        foreach (var caracter in valor)
        {
            if (char.IsDigit(caracter))
            {
                soloDigitos.Append(caracter);
            }
        }

        if (soloDigitos.Length == 0)
        {
            return string.Empty;
        }

        var sinCerosIzquierda = soloDigitos.ToString().TrimStart('0');
        return sinCerosIzquierda.Length > 0 ? sinCerosIzquierda : "0";
    }

    private static string NormalizarLetras(string valor)
    {
        var resultado = new StringBuilder(valor.Length);
        foreach (var caracter in valor)
        {
            if (char.IsLetter(caracter))
            {
                resultado.Append(char.ToLowerInvariant(caracter));
            }
        }

        return resultado.ToString();
    }
}
