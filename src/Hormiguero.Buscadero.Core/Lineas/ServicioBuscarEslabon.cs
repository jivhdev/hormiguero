using System.Text.RegularExpressions;

namespace Buscadero.Core.Lineas;

public sealed class ServicioBuscarEslabon
{
    public string? BuscarOCargar(
        string carpeta,
        string? numeroEsperado,
        Func<string, bool> confirmarCoincidencia,
        Func<string, string?> seleccionarArchivo
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carpeta);
        ArgumentNullException.ThrowIfNull(confirmarCoincidencia);
        ArgumentNullException.ThrowIfNull(seleccionarArchivo);
        string? encontrado = Directory.Exists(carpeta)
            ? ArchivosPorPeriodo(carpeta)
                .FirstOrDefault(r =>
                    numeroEsperado is null || NombreContieneNumero(r, numeroEsperado)
                )
            : null;
        if (encontrado is not null && confirmarCoincidencia(encontrado))
            return encontrado;
        return seleccionarArchivo(carpeta);
    }

    public static IEnumerable<string> ArchivosPorPeriodo(string carpeta)
    {
        var directorios = new[] { carpeta }.Concat(
            Directory.EnumerateDirectories(carpeta, "*", SearchOption.AllDirectories)
        );
        foreach (
            var directorio in directorios
                .Select(d => new DirectoryInfo(d))
                .OrderByDescending(d => Periodo(d.Name))
                .ThenByDescending(d => d.LastWriteTimeUtc)
        )
        {
            IEnumerable<string> archivos;
            try
            {
                archivos = Directory
                    .EnumerateFiles(directorio.FullName, "*.pdf", SearchOption.TopDirectoryOnly)
                    .ToArray();
            }
            catch (IOException)
            {
                continue;
            }
            foreach (
                string archivo in archivos.OrderBy(
                    Path.GetFileName,
                    StringComparer.OrdinalIgnoreCase
                )
            )
                yield return archivo;
        }
    }

    public static bool NombreContieneNumero(string ruta, string numero)
    {
        string clave = string.Concat(numero.Where(char.IsAsciiDigit)).TrimStart('0');
        if (clave.Length == 0)
            return false;
        string digitos = string.Concat(
                Path.GetFileNameWithoutExtension(ruta).Where(char.IsAsciiDigit)
            )
            .TrimStart('0');
        return digitos.Contains(clave, StringComparison.Ordinal);
    }

    private static long Periodo(string nombre)
    {
        string digitos = string.Concat(nombre.Where(char.IsAsciiDigit));
        return
            Regex.IsMatch(digitos, @"^20\d{2}(0[1-9]|1[0-2])?$")
            && long.TryParse(digitos, out long periodo)
            ? periodo
            : 0;
    }
}
