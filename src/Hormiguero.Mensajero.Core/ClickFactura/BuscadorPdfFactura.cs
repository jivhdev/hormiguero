using System.Text.RegularExpressions;

namespace Hormiguero.Mensajero.Core.ClickFactura;

public static partial class BuscadorPdfFactura
{
    public static string RutaBasePredeterminada =>
        Path.Combine("G:\\", "Mi unidad", "ASIS_JCV", "DOC_FACEL");

    private static readonly IReadOnlyDictionary<string, string> Prefijos = new Dictionary<
        string,
        string
    >
    {
        ["FCV"] = "FCV",
        ["NCV"] = "NCV",
    };

    [GeneratedRegex(@"CEDIBLE", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MarcaCedible();

    public static string RutaDocumentos(string baseDocumentos, string tipo, int anio, int mes) =>
        Path.Combine(
            NormalizarBaseDocumentos(baseDocumentos),
            tipo,
            anio.ToString(),
            $"{anio}{mes:00}"
        );

    public static string NormalizarBaseDocumentos(string baseDocumentos)
    {
        string ruta = Path.GetFullPath(baseDocumentos);
        string nombre = Path.GetFileName(Path.TrimEndingDirectorySeparator(ruta));
        if (Prefijos.ContainsKey(nombre.ToUpperInvariant()))
            return Directory.GetParent(ruta)?.FullName ?? ruta;

        try
        {
            if (
                Directory.Exists(ruta)
                && Directory
                    .EnumerateDirectories(ruta)
                    .Any(carpeta => int.TryParse(Path.GetFileName(carpeta), out _))
            )
                return Directory.GetParent(ruta)?.FullName ?? ruta;
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        return ruta;
    }

    public static bool TieneCarpetasTipos(string baseDocumentos)
    {
        string baseNormalizada = NormalizarBaseDocumentos(baseDocumentos);
        return Prefijos.Keys.All(tipo => Directory.Exists(Path.Combine(baseNormalizada, tipo)));
    }

    public static string? BuscarPdf(
        string baseDocumentos,
        string tipo,
        string numero,
        int anio,
        int mes,
        IDictionary<(string Tipo, string Numero, int Anio, int Mes), string>? cache = null
    )
    {
        string tipoNormalizado = tipo.ToUpperInvariant();
        var clave = (tipoNormalizado, numero, anio, mes);
        if (cache is not null && cache.TryGetValue(clave, out string? guardada))
        {
            if (File.Exists(guardada))
                return guardada;
            cache.Remove(clave);
        }

        string carpeta = RutaDocumentos(baseDocumentos, tipoNormalizado, anio, mes);
        if (!Directory.Exists(carpeta))
            return null;

        Regex patron = CrearPatron(tipoNormalizado, numero);
        try
        {
            foreach (string ruta in Directory.EnumerateFiles(carpeta))
            {
                string nombre = Path.GetFileName(ruta);
                if (tipoNormalizado == "FCV" && MarcaCedible().IsMatch(nombre))
                    continue;
                if (!patron.IsMatch(nombre))
                    continue;
                cache?.TryAdd(clave, ruta);
                return ruta;
            }
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }

        return null;
    }

    public static string? BuscarEnPeriodos(
        string baseDocumentos,
        string tipo,
        string numero,
        IEnumerable<(int Anio, int Mes)> periodos,
        IDictionary<(string Tipo, string Numero, int Anio, int Mes), string>? cache = null
    )
    {
        foreach ((int anio, int mes) in periodos)
        {
            string? encontrado = BuscarPdf(baseDocumentos, tipo, numero, anio, mes, cache);
            if (encontrado is not null)
                return encontrado;
        }
        return null;
    }

    public static string? BuscarEnTodasLasCarpetas(
        string baseDocumentos,
        string tipo,
        string numero,
        IDictionary<(string Tipo, string Numero), string>? cache = null
    )
    {
        string tipoNormalizado = tipo.ToUpperInvariant();
        var clave = (tipoNormalizado, numero);
        if (cache is not null && cache.TryGetValue(clave, out string? guardada))
        {
            if (File.Exists(guardada))
                return guardada;
            cache.Remove(clave);
        }

        string carpetaBase = Path.Combine(
            NormalizarBaseDocumentos(baseDocumentos),
            tipoNormalizado
        );
        if (!Directory.Exists(carpetaBase))
            return null;

        Regex patron = CrearPatron(tipoNormalizado, numero);
        try
        {
            foreach (
                string anio in Directory
                    .EnumerateDirectories(carpetaBase)
                    .OrderDescending(StringComparer.Ordinal)
            )
            foreach (
                string mes in Directory
                    .EnumerateDirectories(anio)
                    .OrderDescending(StringComparer.Ordinal)
            )
            foreach (string ruta in Directory.EnumerateFiles(mes))
            {
                string nombre = Path.GetFileName(ruta);
                if (tipoNormalizado == "FCV" && MarcaCedible().IsMatch(nombre))
                    continue;
                if (!patron.IsMatch(nombre))
                    continue;
                cache?.TryAdd(clave, ruta);
                return ruta;
            }
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        return null;
    }

    public static (
        IReadOnlyDictionary<(string Tipo, string Numero, string Entidad), string> Encontrados,
        IReadOnlyList<DocumentoFactura> NoEncontrados
    ) BuscarLote(
        string baseDocumentos,
        IReadOnlyList<DocumentoFactura> documentos,
        IReadOnlyList<(int Anio, int Mes)> periodos
    )
    {
        var cache = new Dictionary<(string Tipo, string Numero, int Anio, int Mes), string>();
        var encontrados = new Dictionary<(string Tipo, string Numero, string Entidad), string>();
        var faltantes = new List<DocumentoFactura>();
        foreach (DocumentoFactura documento in documentos)
        {
            string? ruta = BuscarEnPeriodos(
                baseDocumentos,
                documento.Tipo,
                documento.Numero,
                periodos,
                cache
            );
            if (ruta is null)
                faltantes.Add(documento);
            else
                encontrados[(documento.Tipo, documento.Numero, documento.Entidad)] = ruta;
        }
        return (encontrados, faltantes);
    }

    private static Regex CrearPatron(string tipo, string numero)
    {
        string prefijo = Prefijos.TryGetValue(tipo, out string? valor) ? valor : tipo;
        string sinCeros = numero.TrimStart('0');
        if (sinCeros.Length == 0)
            sinCeros = "0";
        return new Regex(
            $"^{Regex.Escape(prefijo)}0*{Regex.Escape(sinCeros)}\\.pdf$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );
    }
}
