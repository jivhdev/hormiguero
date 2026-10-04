namespace Hormiguero.Archivero.Logica;

public enum FormaCarpeta
{
    AnioYMes,
    SoloAnio,
    Directo,
}

public enum ParteNombre
{
    Tipo,
    Emisor,
    Numero,
    NombreOriginal,
    Fecha,
}

public record ReglaDestino(
    string CarpetaMadre,
    FormaCarpeta Forma,
    IReadOnlyList<ParteNombre> Partes,
    string Separador
);

public record DatosDocumento(
    string Tipo,
    string Emisor,
    string Numero,
    string NombreOriginal,
    DateOnly Fecha,
    bool EsCedible
);

public static class DestinoArchivo
{
    public static string Carpeta(ReglaDestino regla, DateOnly fecha)
    {
        return regla.Forma switch
        {
            FormaCarpeta.AnioYMes => Path.Combine(
                regla.CarpetaMadre,
                fecha.Year.ToString(),
                $"{fecha.Year}{fecha.Month:00}"
            ),
            FormaCarpeta.SoloAnio => Path.Combine(regla.CarpetaMadre, fecha.Year.ToString()),
            FormaCarpeta.Directo => regla.CarpetaMadre,
            _ => throw new InvalidOperationException("Forma de carpeta no válida"),
        };
    }

    public static string Nombre(ReglaDestino regla, DatosDocumento datos)
    {
        var partes = new List<string>();

        foreach (var parte in regla.Partes)
        {
            string valor = parte switch
            {
                ParteNombre.Tipo => datos.Tipo.Trim(),
                ParteNombre.Emisor => datos.Emisor.Trim(),
                ParteNombre.Numero => QuitarCerosIzquierda(datos.Numero),
                ParteNombre.NombreOriginal => Path.GetFileNameWithoutExtension(
                    datos.NombreOriginal
                ),
                ParteNombre.Fecha => datos.Fecha.ToString("yyyy-MM-dd"),
                _ => string.Empty,
            };

            if (!string.IsNullOrEmpty(valor))
            {
                partes.Add(valor);
            }
        }

        if (partes.Count == 0)
        {
            throw new InvalidOperationException("No hay partes para formar el nombre");
        }

        var nombre = string.Join(regla.Separador, partes);

        nombre = ReemplazarCaracteresInvalidos(nombre);

        if (datos.EsCedible && !nombre.EndsWith("CEDIBLE", StringComparison.OrdinalIgnoreCase))
        {
            nombre += "_CEDIBLE";
        }

        return nombre + ".pdf";
    }

    public static string Ruta(ReglaDestino regla, DatosDocumento datos)
    {
        return Path.Combine(Carpeta(regla, datos.Fecha), Nombre(regla, datos));
    }

    private static string QuitarCerosIzquierda(string numero)
    {
        numero = numero.Trim();
        if (numero.Length == 0)
        {
            return "";
        }

        var sinCeros = numero.TrimStart('0');
        return string.IsNullOrEmpty(sinCeros) ? "0" : sinCeros;
    }

    private static string ReemplazarCaracteresInvalidos(string nombre)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(nombre.Length);
        foreach (var c in nombre)
        {
            sb.Append(invalidos.Contains(c) ? '-' : c);
        }
        return sb.ToString();
    }
}
