namespace Buscadero.Core.Datos;

public static class RutasDeDatos
{
    public static string ObtenerRutaBaseDeDatos(string directorioBase)
    {
        var carpetaDatos = Path.Combine(directorioBase, "Datos");
        Directory.CreateDirectory(carpetaDatos);
        return Path.Combine(carpetaDatos, "buscadero.db");
    }
}
