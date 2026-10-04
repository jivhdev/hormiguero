namespace Buscadero.Core.Carpetas;

public enum ResultadoAgregarCarpeta
{
    Agregada,
    YaConfigurada,
    RutaInvalida,
}

public sealed class ServicioCarpetas
{
    private readonly RepositorioCarpetas _repositorio;

    public ServicioCarpetas(RepositorioCarpetas repositorio)
    {
        _repositorio = repositorio;
    }

    public IReadOnlyList<Carpeta> ObtenerTodas() => _repositorio.ObtenerTodas();

    public ResultadoAgregarCarpeta Agregar(string ruta)
    {
        var rutaNormalizada = NormalizarRuta(ruta);
        if (string.IsNullOrWhiteSpace(rutaNormalizada))
        {
            return ResultadoAgregarCarpeta.RutaInvalida;
        }

        var yaExiste = _repositorio
            .ObtenerTodas()
            .Any(c =>
                string.Equals(
                    NormalizarRuta(c.Ruta),
                    rutaNormalizada,
                    StringComparison.OrdinalIgnoreCase
                )
            );
        if (yaExiste)
        {
            return ResultadoAgregarCarpeta.YaConfigurada;
        }

        _repositorio.Agregar(rutaNormalizada);
        return ResultadoAgregarCarpeta.Agregada;
    }

    public void Quitar(int id) => _repositorio.Quitar(id);

    public void ActualizarRuta(int id, string nuevaRuta) =>
        _repositorio.ActualizarRuta(id, NormalizarRuta(nuevaRuta));

    public bool EsAccesible(string ruta) => Directory.Exists(ruta);

    public static string NormalizarRuta(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            return string.Empty;
        }

        return ruta.Trim().TrimEnd('\\', '/');
    }
}
