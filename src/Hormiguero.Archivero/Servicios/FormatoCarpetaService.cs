using System.Globalization;
using System.IO;
using Archivero.Datos;

namespace Archivero.Servicios;

public record DeteccionFormatoCarpeta(FormatoCarpeta Formato, string? PatronCarpeta);

public static class FormatoCarpetaService
{
    private static readonly string[] TokensAnio = ["yyyy", "yy"];
    // "MMM" (abreviado, ej. "ene.") queda afuera: en es-ES el nombre abreviado termina en punto,
    // y Windows recorta el punto final de cualquier nombre de carpeta -- nunca podria coincidir
    // con una carpeta real.
    private static readonly string[] TokensMes = ["MM", "MMMM", "yyyyMM"];
    private static readonly string[] TodosLosTokens = TokensAnio.Concat(TokensMes).ToArray();
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-ES");

    public static DeteccionFormatoCarpeta Detectar(string carpetaDestino)
    {
        if (!Directory.Exists(carpetaDestino))
        {
            return new DeteccionFormatoCarpeta(FormatoCarpeta.Directo, null);
        }

        var carpetasNivel1 = Directory.GetDirectories(carpetaDestino);
        var nombresNivel1 = carpetasNivel1.Select(Path.GetFileName).OfType<string>().ToList();

        var patronNivel1 = DetectarPatronPorEvidencia(nombresNivel1, TokensAnio);
        if (patronNivel1 is null)
        {
            // Sin evidencia clara y consistente en las subcarpetas ya existentes: nunca se
            // inventa una subdivision que nadie pidio. Ver Caso-1 (preguntas/Caso-1.md), punto 2.
            return new DeteccionFormatoCarpeta(FormatoCarpeta.Directo, null);
        }

        // La evidencia del segundo nivel se junta de TODAS las carpetas de primer nivel que ya
        // coinciden con el patron (no solo la primera): con mas de un ejemplo real, se puede
        // distinguir un texto realmente fijo de un numero que solo parece fijo porque por ahora
        // hay una sola carpeta de primer nivel (ver DetectarPatronPorEvidencia).
        var nombresNivel2 = carpetasNivel1
            .Where(d => CoincideConPatron(Path.GetFileName(d), patronNivel1))
            .SelectMany(Directory.GetDirectories)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Distinct()
            .ToList();

        var patronNivel2 = DetectarPatronPorEvidencia(nombresNivel2, TokensMes);

        return patronNivel2 is not null
            ? new DeteccionFormatoCarpeta(FormatoCarpeta.AnioMes, $"{patronNivel1}\\{patronNivel2}")
            : new DeteccionFormatoCarpeta(FormatoCarpeta.Anio, patronNivel1);
    }

    /// <summary>
    /// Busca un patron real comparando los nombres entre si (evidencia), en vez de compararlos
    /// contra una lista fija de formatos completos. Separa, para cada nombre, una parte literal
    /// fija (igual en todos los ejemplos) de una parte variable, y prueba si esa parte variable
    /// es consistente con alguno de los tokens de fecha candidatos. Si no hay ningun nombre, o
    /// ninguna combinacion es consistente en TODOS los ejemplos, no hay evidencia: devuelve null
    /// (nunca se inventa un patron sin evidencia clara).
    /// </summary>
    private static string? DetectarPatronPorEvidencia(List<string> nombres, string[] tokensCandidatos)
    {
        if (nombres.Count == 0)
        {
            return null;
        }

        var largoMinimo = nombres.Min(n => n.Length);
        var prefijoMaximo = LargoPrefijoComun(nombres);
        var sufijoMaximo = LargoSufijoComun(nombres);

        foreach (var token in tokensCandidatos)
        {
            // Se prueba de mayor a menor cuanto texto literal se "come" el prefijo/sufijo: el
            // primer corte que da una parte variable consistente con el token, en los tres
            // (prefijo, variable, sufijo), es el patron real. Empezar por el corte mas grande
            // evita que un numero coincidencialmente compartido entre los ejemplos (ej. "202" en
            // "2023"/"2024"/"2025") se coma de mas por casualidad -- si eso pasara, el ancho de
            // la parte variable resultante no encajaria con el token y el corte se descarta.
            for (var prefijo = prefijoMaximo; prefijo >= 0; prefijo--)
            {
                for (var sufijo = sufijoMaximo; sufijo >= 0; sufijo--)
                {
                    if (prefijo + sufijo >= largoMinimo)
                    {
                        continue;
                    }

                    if (EncajaCorte(nombres, prefijo, sufijo, token))
                    {
                        var textoPrefijo = nombres[0][..prefijo];
                        var textoSufijo = sufijo == 0 ? string.Empty : nombres[0][^sufijo..];
                        return EnvolverLiteral(textoPrefijo) + token + EnvolverLiteral(textoSufijo);
                    }
                }
            }
        }

        return null;
    }

    private static bool EncajaCorte(List<string> nombres, int prefijo, int sufijo, string token)
    {
        foreach (var nombre in nombres)
        {
            if (nombre.Length < prefijo + sufijo)
            {
                return false;
            }

            var variable = nombre[prefijo..(nombre.Length - sufijo)];
            if (variable.Length == 0 || !CoincideConPatron(variable, token))
            {
                return false;
            }
        }

        // El prefijo y el sufijo deben ser iguales en TODOS los nombres para contar como texto
        // literal fijo -- si llegamos hasta aca es porque LargoPrefijoComun/LargoSufijoComun ya
        // lo garantiza para estos anchos, pero además ninguno de los dos puede "parecer" en si
        // mismo un valor de fecha valido: si lo pareciera, no hay forma de distinguir con la
        // evidencia actual si es texto fijo de verdad o un componente de fecha que se repite
        // (ej. el mismo año en todas las subcarpetas de mes de una sola carpeta de año todavia).
        var textoPrefijo = nombres[0][..prefijo];
        var textoSufijo = sufijo == 0 ? string.Empty : nombres[0][^sufijo..];
        return !PareceValorDeFecha(textoPrefijo) && !PareceValorDeFecha(textoSufijo);
    }

    private static bool PareceValorDeFecha(string texto) =>
        texto.Length > 0 && TodosLosTokens.Any(token => CoincideConPatron(texto, token));

    private static int LargoPrefijoComun(List<string> nombres)
    {
        if (nombres.Count == 1)
        {
            return 0;
        }

        var largoMinimo = nombres.Min(n => n.Length);
        var largo = 0;
        while (largo < largoMinimo && nombres.All(n => n[largo] == nombres[0][largo]))
        {
            largo++;
        }

        return largo;
    }

    private static int LargoSufijoComun(List<string> nombres)
    {
        if (nombres.Count == 1)
        {
            return 0;
        }

        var largoMinimo = nombres.Min(n => n.Length);
        var largo = 0;
        while (largo < largoMinimo && nombres.All(n => n[^(largo + 1)] == nombres[0][^(largo + 1)]))
        {
            largo++;
        }

        return largo;
    }

    /// <summary>
    /// Envuelve texto literal entre comillas simples para que DateTime.ToString/TryParseExact lo
    /// trate como texto fijo -- ej. una "M" o una "y" dentro del texto literal no debe
    /// interpretarse como parte del formato de fecha.
    /// </summary>
    private static string EnvolverLiteral(string texto) =>
        texto.Length == 0 ? string.Empty : "'" + texto.Replace("'", "\\'") + "'";

    public static bool CoincideConPatron(string? nombre, string patron) =>
        !string.IsNullOrEmpty(nombre) && DateTime.TryParseExact(nombre, patron, Cultura, DateTimeStyles.None, out _);

    /// <summary>
    /// Busca un ejemplo real de subcarpeta ya existente que coincide con el patron confirmado --
    /// para el preview obligatorio de "carpeta anterior" (Caso-1, punto 3): prueba visual de que
    /// el patron se entendio bien, no un calculo teorico.
    ///
    /// Con los tipos de Caso-3 el patron puede tener tokens que DateTime.TryParseExact no conoce
    /// (semestre, quincena, semana...) y hasta tres niveles, asi que en vez de parsear los
    /// nombres de las carpetas se genera hacia atras los nombres que el patron produciria y se
    /// compara con lo que hay en disco (sin distinguir mayusculas, como Windows).
    /// </summary>
    public static string? BuscarCarpetaAnteriorReal(string carpetaDestino, FormatoCarpeta formato, string? patron)
    {
        if (formato == FormatoCarpeta.Directo)
        {
            return Directory.Exists(carpetaDestino) ? carpetaDestino : null;
        }

        if (patron is null || !Directory.Exists(carpetaDestino))
        {
            return null;
        }

        var partes = patron.Split('\\');
        var candidatos = FechasCandidatas(formato, patron, DateTime.Today).ToList();

        var carpetasNivel1 = BuscarCoincidentes(carpetaDestino, partes[0], candidatos, static _ => true);
        if (carpetasNivel1.Count == 0)
        {
            return null;
        }

        if (partes.Length == 1)
        {
            return carpetasNivel1[0].Ruta;
        }

        foreach (var nivel1 in carpetasNivel1)
        {
            var carpetasNivel2 = BuscarCoincidentes(nivel1.Ruta, partes[1], candidatos,
                f => string.Equals(FormatearNivel(partes[0], f), nivel1.Nombre, StringComparison.OrdinalIgnoreCase));
            if (carpetasNivel2.Count == 0)
            {
                continue;
            }

            if (partes.Length == 2)
            {
                return carpetasNivel2[0].Ruta;
            }

            foreach (var nivel2 in carpetasNivel2)
            {
                var carpetasNivel3 = BuscarCoincidentes(nivel2.Ruta, partes[2], candidatos,
                    f => string.Equals(FormatearNivel(partes[0], f), nivel1.Nombre, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(FormatearNivel(partes[1], f), nivel2.Nombre, StringComparison.OrdinalIgnoreCase));
                if (carpetasNivel3.Count > 0)
                {
                    return carpetasNivel3[0].Ruta;
                }
            }

            return carpetasNivel2[0].Ruta;
        }

        return carpetasNivel1[0].Ruta;
    }

    private sealed record CarpetaCoincidente(string Ruta, string Nombre, DateTime Fecha);

    /// <summary>
    /// Subcarpetas de <paramref name="carpetaPadre"/> cuyo nombre coincide con lo que el nivel
    /// del patron generaria para alguna fecha candidata, ordenadas de la mas reciente a la mas
    /// antigua (orden cronologico real, no alfabético -- con "Marzo"/"Abril" no coincide).
    /// </summary>
    private static List<CarpetaCoincidente> BuscarCoincidentes(
        string carpetaPadre, string nivelPatron, List<DateTime> candidatos, Func<DateTime, bool> filtro)
    {
        // Los candidatos vienen en orden descendente: la primera fecha que genera cada nombre
        // es la mas reciente que lo genera (relevante para nombres que se repiten, ej. "03" de
        // "Por mes (sin año)").
        var fechaPorNombre = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        foreach (var fecha in candidatos.Where(f => filtro(f)))
        {
            fechaPorNombre.TryAdd(FormatearNivel(nivelPatron, fecha), fecha);
        }

        return Directory.GetDirectories(carpetaPadre)
            .Select(d => (Ruta: d, Nombre: Path.GetFileName(d)!))
            .Where(x => fechaPorNombre.ContainsKey(x.Nombre))
            .Select(x => new CarpetaCoincidente(x.Ruta, x.Nombre, fechaPorNombre[x.Nombre]))
            .OrderByDescending(c => c.Fecha)
            .ToList();
    }

    /// <summary>
    /// Fechas que cubren los periodos plausibles ya existentes en disco: un poco hacia adelante
    /// (carpetas creadas por adelantado) y bastante hacia atras, en pasos del tamaño del periodo
    /// del patron (ver <see cref="InferirPaso"/>).
    /// </summary>
    private static IEnumerable<DateTime> FechasCandidatas(FormatoCarpeta formato, string patron, DateTime hoy)
    {
        var paso = InferirPaso(formato, patron);

        var fecha = hoy;
        for (var i = 0; i < 60; i++)
        {
            fecha = AvanzarPeriodo(paso, fecha);
        }

        for (var i = 0; i < 2000; i++)
        {
            yield return fecha;
            fecha = RetrocederPeriodo(paso, fecha);
        }
    }

    private enum PasoPeriodo { Anio, Semestre, Trimestre, Mes, Quincena, Semana, Dia }

    /// <summary>Fecha del próximo período, para el preview de "carpeta futura" (Caso-1, punto 3).</summary>
    public static DateTime SiguientePeriodo(FormatoCarpeta formato, DateTime fecha, string? patron = null) =>
        formato == FormatoCarpeta.Directo ? fecha : AvanzarPeriodo(InferirPaso(formato, patron), fecha);

    private static PasoPeriodo InferirPaso(FormatoCarpeta formato, string? patron) => formato switch
    {
        FormatoCarpeta.Anio => PasoPeriodo.Anio,
        FormatoCarpeta.AnioSemestre => PasoPeriodo.Semestre,
        FormatoCarpeta.AnioTrimestre => PasoPeriodo.Trimestre,
        FormatoCarpeta.AnioMes => PasoPeriodo.Mes,
        FormatoCarpeta.AnioQuincena => PasoPeriodo.Quincena,
        FormatoCarpeta.AnioSemana => PasoPeriodo.Semana,
        FormatoCarpeta.AnioMesDia => PasoPeriodo.Dia,
        FormatoCarpeta.MesSinAnio => PasoPeriodo.Mes,
        FormatoCarpeta.SemanaDelMes => PasoPeriodo.Semana,
        FormatoCarpeta.Personalizado => InferirPasoDePatron(patron),
        _ => PasoPeriodo.Anio
    };

    /// <summary>
    /// Para un patrón personalizado, la granularidad del periodo sale del token más fino que
    /// contenga (fuera de los textos literales entre comillas).
    /// </summary>
    private static PasoPeriodo InferirPasoDePatron(string? patron)
    {
        var tokens = EnumerarTokens(patron).ToHashSet();

        if (tokens.Contains("dd")) return PasoPeriodo.Dia;
        if (tokens.Overlaps(new[] { "N", "WW", "W" })) return PasoPeriodo.Semana;
        if (tokens.Contains("H")) return PasoPeriodo.Quincena;
        if (tokens.Overlaps(new[] { "MM", "MMMM" })) return PasoPeriodo.Mes;
        if (tokens.Contains("T")) return PasoPeriodo.Trimestre;
        if (tokens.Contains("S")) return PasoPeriodo.Semestre;
        return PasoPeriodo.Anio;
    }

    private static DateTime AvanzarPeriodo(PasoPeriodo paso, DateTime fecha) => paso switch
    {
        PasoPeriodo.Anio => fecha.AddYears(1),
        PasoPeriodo.Semestre => fecha.AddMonths(6),
        PasoPeriodo.Trimestre => fecha.AddMonths(3),
        PasoPeriodo.Mes => fecha.AddMonths(1),
        PasoPeriodo.Quincena => fecha.Day <= 15 ? new DateTime(fecha.Year, fecha.Month, 16) : new DateTime(fecha.Year, fecha.Month, 1).AddMonths(1),
        PasoPeriodo.Semana => fecha.AddDays(7),
        _ => fecha.AddDays(1)
    };

    private static DateTime RetrocederPeriodo(PasoPeriodo paso, DateTime fecha) => paso switch
    {
        PasoPeriodo.Anio => fecha.AddYears(-1),
        PasoPeriodo.Semestre => fecha.AddMonths(-6),
        PasoPeriodo.Trimestre => fecha.AddMonths(-3),
        PasoPeriodo.Mes => fecha.AddMonths(-1),
        PasoPeriodo.Quincena => fecha.Day > 15 ? new DateTime(fecha.Year, fecha.Month, 15) : new DateTime(fecha.Year, fecha.Month, 1).AddDays(-1),
        PasoPeriodo.Semana => fecha.AddDays(-7),
        _ => fecha.AddDays(-1)
    };

    public static string ConstruirSubcarpeta(FormatoCarpeta formato, string? patron, DateTime fecha)
    {
        if (formato == FormatoCarpeta.Directo)
        {
            return string.Empty;
        }

        if (patron is null)
        {
            throw new InvalidOperationException("Falta el patrón de carpeta.");
        }

        // Cada nivel de carpeta se formatea por separado y se combina con Path.Combine:
        // un "\" dentro de un formato de DateTime.ToString se interpreta como caracter de
        // escape, no como separador de ruta, asi que no se puede pasar "yyyy\MM" de una.
        var partesFormateadas = patron.Split('\\').Select(parte => FormatearNivel(parte, fecha));
        return Path.Combine(partesFormateadas.ToArray());
    }

    private static readonly string[] NombresMes =
    [
        "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    ];

    // Tokens del lenguaje de patrones (los ve el motor, nunca el usuario: en la interfaz solo
    // aparecen ejemplos concretos, ver Caso-3 punto 3c). Ordenados de mas largo a mas corto para
    // el emparejamiento voraz dentro de una corrida de letras.
    private static readonly string[] TokensReconocidos =
    [
        "yyyy", "MMMM", "MM", "yy", "dd", "WW", "W", "S", "T", "H", "O", "N"
    ];

    /// <summary>El token más largo de <see cref="TokensReconocidos"/> que empieza en <paramref name="posicion"/>.</summary>
    private static string? BuscarToken(string corrida, int posicion) =>
        TokensReconocidos.FirstOrDefault(t =>
            corrida.Length >= posicion + t.Length &&
            string.CompareOrdinal(corrida, posicion, t, 0, t.Length) == 0);

    /// <summary>
    /// Formatea un nivel del patron para una fecha: primero con el motor propio (que entiende los
    /// tokens nuevos de Caso-3: semestre, trimestre, quincena, semana, semana del mes...), y si
    /// el nivel contiene algo que el motor no conoce, con DateTime.ToString como antes -- así
    /// cualquier patron viejo o exótico sigue funcionando igual que siempre.
    /// </summary>
    public static string FormatearNivel(string nivel, DateTime fecha) =>
        TryFormatearConMotorPropio(nivel, fecha) ?? fecha.ToString(nivel, Cultura);

    private static string? TryFormatearConMotorPropio(string nivel, DateTime fecha)
    {
        var resultado = new System.Text.StringBuilder();
        var i = 0;

        while (i < nivel.Length)
        {
            if (nivel[i] == '\'')
            {
                i++;
                var cerrada = false;
                while (i < nivel.Length)
                {
                    if (nivel[i] == '\\' && i + 1 < nivel.Length && nivel[i + 1] == '\'')
                    {
                        resultado.Append('\'');
                        i += 2;
                        continue;
                    }

                    if (nivel[i] == '\'')
                    {
                        cerrada = true;
                        i++;
                        break;
                    }

                    resultado.Append(nivel[i]);
                    i++;
                }

                if (!cerrada)
                {
                    return null;
                }
            }
            else if (char.IsAsciiLetter(nivel[i]))
            {
                var inicio = i;
                while (i < nivel.Length && char.IsAsciiLetter(nivel[i]))
                {
                    i++;
                }

                var corrida = nivel[inicio..i];
                var posicion = 0;
                while (posicion < corrida.Length)
                {
                    var token = BuscarToken(corrida, posicion);
                    if (token is null)
                    {
                        return null;
                    }

                    resultado.Append(ValorToken(token, fecha));
                    posicion += token.Length;
                }
            }
            else
            {
                resultado.Append(nivel[i]);
                i++;
            }
        }

        return resultado.ToString();
    }

    private static string ValorToken(string token, DateTime fecha) => token switch
    {
        "yyyy" => fecha.Year.ToString("D4"),
        "yy" => (fecha.Year % 100).ToString("D2"),
        "MMMM" => NombresMes[fecha.Month - 1],
        "MM" => fecha.Month.ToString("D2"),
        "dd" => fecha.Day.ToString("D2"),
        "WW" => System.Globalization.ISOWeek.GetWeekOfYear(fecha).ToString("D2"),
        "W" => System.Globalization.ISOWeek.GetWeekOfYear(fecha).ToString(),
        "S" => ((fecha.Month - 1) / 6 + 1).ToString(),
        "T" => ((fecha.Month - 1) / 3 + 1).ToString(),
        "H" => (fecha.Day <= 15 ? 1 : 2).ToString(),
        "O" => fecha.Day <= 15 ? "1ra" : "2da",
        "N" => SemanaDelMes(fecha).ToString(),
        _ => throw new InvalidOperationException($"Token de patrón desconocido: \"{token}\".")
    };

    /// <summary>
    /// Semana dentro del mes, contando semanas de calendario lunes-domingo que tocan el mes:
    /// la Semana 1 es la que contiene al día 1, aunque empiece el mes anterior (decisión de
    /// Javier para Caso-3).
    /// </summary>
    private static int SemanaDelMes(DateTime fecha)
    {
        var primero = new DateTime(fecha.Year, fecha.Month, 1);
        var desfase = ((int)primero.DayOfWeek + 6) % 7; // lunes=0 ... domingo=6
        return (fecha.Day + desfase - 1) / 7 + 1;
    }

    /// <summary>Tokens de fecha (fuera de literales entre comillas) que aparecen en un patron completo.</summary>
    private static IEnumerable<string> EnumerarTokens(string? patron)
    {
        if (patron is null)
        {
            yield break;
        }

        foreach (var nivel in patron.Split('\\'))
        {
            var i = 0;
            while (i < nivel.Length)
            {
                if (nivel[i] == '\'')
                {
                    i++;
                    while (i < nivel.Length && nivel[i] != '\'')
                    {
                        // \' dentro del literal es una comilla, no cierra ni cuenta como token.
                        i += nivel[i] == '\\' ? 2 : 1;
                    }

                    i++;
                }
                else if (char.IsAsciiLetter(nivel[i]))
                {
                    var inicio = i;
                    while (i < nivel.Length && char.IsAsciiLetter(nivel[i]))
                    {
                        i++;
                    }

                    var corrida = nivel[inicio..i];
                    var posicion = 0;
                    while (posicion < corrida.Length)
                    {
                        var token = BuscarToken(corrida, posicion);
                        if (token is null)
                        {
                            break;
                        }

                        yield return token;
                        posicion += token.Length;
                    }
                }
                else
                {
                    i++;
                }
            }
        }
    }
}
