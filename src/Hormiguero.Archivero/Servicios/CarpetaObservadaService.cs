using System.IO;
using Archivero.Datos;

namespace Archivero.Servicios;

public class CarpetaYaExisteException : Exception
{
    public CarpetaYaExisteException(string ruta)
        : base($"Ya existe una carpeta en \"{ruta}\". Elegir otro nombre u otra ubicación.")
    {
    }
}

public class CarpetaObservadaService
{
    private const string ClaveConfiguracion = "CarpetaObservada";

    private readonly ConfiguracionRepository _configuracion;

    public CarpetaObservadaService(ConfiguracionRepository configuracion)
    {
        _configuracion = configuracion;
    }

    public string NombrePorDefecto => "Archivero";

    public string UbicacionPorDefecto =>
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public string? ObtenerCarpetaConfigurada() => _configuracion.Obtener(ClaveConfiguracion);

    public bool ExisteCarpeta(string ubicacion, string nombre) =>
        Directory.Exists(Path.Combine(ubicacion, nombre));

    public string CrearYMarcarComoObservada(string ubicacion, string nombre)
    {
        var ruta = Path.Combine(ubicacion, nombre);
        if (Directory.Exists(ruta))
        {
            throw new CarpetaYaExisteException(ruta);
        }

        Directory.CreateDirectory(ruta);
        _configuracion.Guardar(ClaveConfiguracion, ruta);
        return ruta;
    }
}
