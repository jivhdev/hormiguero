using System.Collections.Generic;
using System.Threading;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Utilidades;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Buscadero.Logica;

public enum AlcanceBusqueda
{
    PrimeraCoincidencia,
    TodasLasCarpetas,
    CarpetaEspecifica,
}

public sealed class Buscador
{
    private readonly Documentos _documentos;
    private readonly CarpetasConfiguradas _carpetasConfiguradas;

    public Buscador(SqliteConnection conexion)
    {
        _documentos = new Documentos(conexion);
        _carpetasConfiguradas = new CarpetasConfiguradas(conexion);
    }

    public static string? Normalizar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        var digitos = new char[texto.Length];
        int contador = 0;
        foreach (char c in texto)
        {
            if (char.IsDigit(c))
            {
                digitos[contador++] = c;
            }
        }

        if (contador == 0)
        {
            return null;
        }

        string soloDigitos = new(digitos, 0, contador);

        int inicio = 0;
        while (inicio < soloDigitos.Length - 1 && soloDigitos[inicio] == '0')
        {
            inicio++;
        }

        return soloDigitos[inicio..];
    }

    public IReadOnlyList<DocumentoIndexado> Buscar(
        string texto,
        AlcanceBusqueda alcance,
        string? carpetaEspecifica,
        CancellationToken cancelar
    )
    {
        string? numero = Normalizar(texto);
        if (numero == null)
        {
            return [];
        }

        var carpetas = _carpetasConfiguradas.Listar();
        if (carpetas.Count == 0)
        {
            return [];
        }

        var resultados = new List<DocumentoIndexado>();

        switch (alcance)
        {
            case AlcanceBusqueda.CarpetaEspecifica:
            {
                if (string.IsNullOrWhiteSpace(carpetaEspecifica))
                {
                    throw new ArgumentException(
                        "La carpeta específica no puede ser nula o vacía.",
                        nameof(carpetaEspecifica)
                    );
                }

                string carpetaNormalizada = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(carpetaEspecifica)
                );
                if (
                    !carpetas.Any(c =>
                        string.Equals(c, carpetaNormalizada, StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    return [];
                }

                resultados.AddRange(_documentos.BuscarPorNumero(numero, carpetaNormalizada));
                break;
            }

            case AlcanceBusqueda.TodasLasCarpetas:
            {
                foreach (string carpeta in carpetas)
                {
                    cancelar.ThrowIfCancellationRequested();
                    resultados.AddRange(_documentos.BuscarPorNumero(numero, carpeta));
                }
                break;
            }

            case AlcanceBusqueda.PrimeraCoincidencia:
            {
                foreach (string carpeta in carpetas)
                {
                    cancelar.ThrowIfCancellationRequested();
                    var encontrados = _documentos.BuscarPorNumero(numero, carpeta);
                    if (encontrados.Count > 0)
                    {
                        resultados.AddRange(encontrados);
                        break;
                    }
                }
                break;
            }
        }

        if (resultados.Count > 0)
        {
            return resultados;
        }

        return BuscarEnVivo(numero, alcance, carpetaEspecifica, carpetas, cancelar);
    }

    private IReadOnlyList<DocumentoIndexado> BuscarEnVivo(
        string numero,
        AlcanceBusqueda alcance,
        string? carpetaEspecifica,
        IReadOnlyList<string> carpetas,
        CancellationToken cancelar
    )
    {
        var resultados = new List<DocumentoIndexado>();
        IEnumerable<string> carpetasABuscar;

        switch (alcance)
        {
            case AlcanceBusqueda.CarpetaEspecifica:
            {
                if (string.IsNullOrWhiteSpace(carpetaEspecifica))
                {
                    return resultados;
                }
                string carpetaNormalizada = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(carpetaEspecifica)
                );
                carpetasABuscar = carpetas.Where(c =>
                    string.Equals(c, carpetaNormalizada, StringComparison.OrdinalIgnoreCase)
                );
                break;
            }

            case AlcanceBusqueda.TodasLasCarpetas:
                carpetasABuscar = carpetas;
                break;

            case AlcanceBusqueda.PrimeraCoincidencia:
                carpetasABuscar = carpetas;
                break;

            default:
                return resultados;
        }

        foreach (string carpeta in carpetasABuscar)
        {
            cancelar.ThrowIfCancellationRequested();

            if (!Directory.Exists(carpeta))
            {
                continue;
            }

            string[] archivos;
            try
            {
                // Los documentos suelen estar en subcarpetas de año y mes: se recorren todas, saltando las sin permiso.
                archivos = Directory
                    .EnumerateFiles(
                        carpeta,
                        "*.pdf",
                        new EnumerationOptions
                        {
                            RecurseSubdirectories = true,
                            IgnoreInaccessible = true,
                            MatchCasing = MatchCasing.CaseInsensitive,
                        }
                    )
                    .ToArray();
            }
            catch
            {
                continue;
            }

            foreach (string archivo in archivos)
            {
                cancelar.ThrowIfCancellationRequested();

                string nombreArchivo = Path.GetFileName(archivo);
                var numerosEnNombre = NombreArchivo.Extraer(nombreArchivo);

                bool coincide = numerosEnNombre.Any(n => n.Numero == numero);
                if (!coincide)
                {
                    continue;
                }

                FileInfo info = new(archivo);
                var doc = new DocumentoIndexado(
                    archivo,
                    carpeta,
                    nombreArchivo,
                    info.Length,
                    info.LastWriteTimeUtc,
                    null,
                    "sin_indexar",
                    false,
                    []
                );
                resultados.Add(doc);
            }

            if (alcance == AlcanceBusqueda.PrimeraCoincidencia && resultados.Count > 0)
            {
                break;
            }
        }

        return resultados;
    }
}
