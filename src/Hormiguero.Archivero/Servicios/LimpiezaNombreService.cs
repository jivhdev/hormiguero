using System.IO;
using Archivero.Datos;

namespace Archivero.Servicios;

/// <summary>
/// Atajos de limpieza del nombre de archivo que el usuario revisa antes de guardar (Caso-4
/// punto 4, Caso-11 punto 6). Funciones puras: no validan seguridad (eso lo hace siempre
/// <see cref="ValidadorRutaService"/> al guardar), solo transforman el texto.
/// </summary>
public static class LimpiezaNombreService
{
    public static string DejarSoloNumeros(string nombre) => new(nombre.Where(char.IsDigit).ToArray());

    /// <summary>
    /// Quita los ceros de relleno a la izquierda de un nombre puramente numérico ("00123" → "123").
    /// Los ceros que son parte del valor no se tocan ("1200" queda igual), y un nombre que no es
    /// puramente numérico se devuelve tal cual. Un nombre hecho solo de ceros queda en "0", nunca vacío.
    /// </summary>
    public static string QuitarCerosIzquierda(string nombre)
    {
        if (nombre.Length == 0 || !nombre.All(char.IsAsciiDigit))
        {
            return nombre;
        }

        var sinCeros = nombre.TrimStart('0');
        return sinCeros.Length == 0 ? "0" : sinCeros;
    }

    /// <summary>Repite sobre un nombre nuevo, en el mismo orden, los botones de limpieza guardados en un atajo (Caso-11, punto 4).</summary>
    public static string AplicarRegla(string nombreOriginal, IEnumerable<OperacionNombre> regla) =>
        regla.Aggregate(nombreOriginal, (nombre, operacion) => operacion switch
        {
            OperacionNombre.Borrar => string.Empty,
            OperacionNombre.DejarSoloNumeros => DejarSoloNumeros(nombre),
            OperacionNombre.QuitarCerosIzquierda => QuitarCerosIzquierda(nombre),
            _ => nombre
        });

    /// <summary>Nombre sugerido para un atajo nuevo: la carpeta madre y, si organiza por fecha, el tipo de organización.</summary>
    public static string SugerirNombreAtajo(string carpetaMadre, FormatoCarpeta formato)
    {
        var nombreCarpeta = Path.GetFileName(carpetaMadre.TrimEnd('\\', '/'));
        if (nombreCarpeta.Length == 0)
        {
            nombreCarpeta = carpetaMadre;
        }

        return formato == FormatoCarpeta.Directo
            ? nombreCarpeta
            : $"{nombreCarpeta} ({OrganizacionCarpetaService.NombreDe(formato)})";
    }
}
