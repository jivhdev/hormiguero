using Buscadero.Core.Carpetas;
using Buscadero.Core.Indexado;

namespace Buscadero.Core.Busqueda;

public sealed class ServicioBusqueda
{
    private readonly ServicioCarpetas _servicioCarpetas;
    private readonly Indexador _indexador;
    private readonly RepositorioIndice _repositorio;
    private readonly IndexadoEnSegundoPlano? _enSegundoPlano;

    public ServicioBusqueda(
        ServicioCarpetas servicioCarpetas,
        Indexador indexador,
        RepositorioIndice repositorio,
        IndexadoEnSegundoPlano? enSegundoPlano = null
    )
    {
        _servicioCarpetas = servicioCarpetas;
        _indexador = indexador;
        _repositorio = repositorio;
        _enSegundoPlano = enSegundoPlano;
    }

    public IReadOnlyList<ResultadoBusqueda> Buscar(
        string consulta,
        string? filtroCarpeta = null,
        ModoBusqueda modo = ModoBusqueda.Todos,
        AlcanceBusqueda alcance = AlcanceBusqueda.PrimeraCoincidencia,
        CancellationToken cancellationToken = default,
        IProgress<ProgresoIndexado>? progreso = null
    )
    {
        var carpetasMadre = _servicioCarpetas.ObtenerTodas().Select(c => c.Ruta).ToList();
        if (!string.IsNullOrWhiteSpace(filtroCarpeta))
        {
            _repositorio.RegistrarUsoFiltro(filtroCarpeta.Trim());
        }

        // Fase B-2a (D-66): antes se recorrían todas las carpetas antes de cada búsqueda
        // (con pausa de 200 ms por subcarpeta: minutos con carpetas grandes). Ahora se busca
        // primero en el índice y, solo si no aparece nada, se actualiza y se busca de nuevo,
        // así un documento recién llegado igual se encuentra.
        var resultado = BuscarEnIndice(
            carpetasMadre,
            consulta,
            filtroCarpeta,
            modo,
            alcance,
            cancellationToken
        );
        if (resultado.Count > 0)
        {
            _enSegundoPlano?.Pedir();
            return resultado;
        }

        if (_enSegundoPlano is { EnCurso: true })
        {
            // Ya se está actualizando: basta esperar esa pasada.
            _enSegundoPlano.EsperarPasadaActual(cancellationToken);
        }
        else
        {
            _indexador.Indexar(carpetasMadre, cancellationToken, progreso);
        }

        return BuscarEnIndice(
            carpetasMadre,
            consulta,
            filtroCarpeta,
            modo,
            alcance,
            cancellationToken
        );
    }

    private IReadOnlyList<ResultadoBusqueda> BuscarEnIndice(
        List<string> carpetasMadre,
        string consulta,
        string? filtroCarpeta,
        ModoBusqueda modo,
        AlcanceBusqueda alcance,
        CancellationToken cancellationToken
    )
    {
        // El filtro (una carpeta o subcarpeta puntual, elegida por el usuario) siempre
        // restringe la busqueda a esa ubicacion, sin importar el alcance activo - es la
        // implementacion de "Carpeta especifica" (Caso-11, punto 4.3).
        if (!string.IsNullOrWhiteSpace(filtroCarpeta))
        {
            return BuscarEnArchivos(
                _repositorio.ObtenerArchivos(filtroCarpeta),
                consulta,
                modo,
                cancellationToken
            );
        }

        // Sin filtro elegido: el alcance decide como se recorren las carpetas madre configuradas.
        if (alcance == AlcanceBusqueda.TodasLasCarpetas || carpetasMadre.Count <= 1)
        {
            return BuscarEnArchivos(
                _repositorio.ObtenerArchivos(null),
                consulta,
                modo,
                cancellationToken
            );
        }

        // PrimeraCoincidencia (y CarpetaEspecifica sin una carpeta elegida, por seguridad):
        // revisar carpeta madre por carpeta madre, deteniendose en la primera con resultado.
        foreach (var carpetaMadre in carpetasMadre)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resultado = BuscarEnArchivos(
                _repositorio.ObtenerArchivos(carpetaMadre),
                consulta,
                modo,
                cancellationToken
            );
            if (resultado.Count > 0)
            {
                return resultado;
            }
        }

        return Array.Empty<ResultadoBusqueda>();
    }

    /// <summary>
    /// Hasta <paramref name="maximo"/> sugerencias para el filtro de carpeta, intercalando
    /// la carpeta mas usada recientemente con una carpeta madre configurada (Fase 2 del
    /// vault) - las carpetas madre deben estar siempre disponibles, no solo cuando todavia
    /// no hay historial de uso (bug corregido en Caso-11).
    /// </summary>
    /// <summary>
    /// Solo las carpetas madre configuradas (sin subcarpetas ni "recientes") - usado por
    /// el selector rápido de "Carpeta específica" (Caso-15), que se filtra en memoria sin
    /// ir a la base de datos para que se sienta instantáneo.
    /// </summary>
    public IReadOnlyList<string> ObtenerCarpetasMadre() =>
        _servicioCarpetas.ObtenerTodas().Select(c => c.Ruta).ToList();

    public IReadOnlyList<string> ObtenerSugerenciasCarpeta(int maximo = 4)
    {
        var recientes = _repositorio.ObtenerCarpetasSugeridas(maximo);
        var madres = _servicioCarpetas.ObtenerTodas().Select(c => c.Ruta).ToList();

        var resultado = new List<string>();
        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var indiceRecientes = 0;
        var indiceMadres = 0;

        while (
            resultado.Count < maximo
            && (indiceRecientes < recientes.Count || indiceMadres < madres.Count)
        )
        {
            if (indiceRecientes < recientes.Count && vistas.Add(recientes[indiceRecientes]))
            {
                resultado.Add(recientes[indiceRecientes]);
            }

            indiceRecientes++;
            if (resultado.Count >= maximo)
            {
                break;
            }

            if (indiceMadres < madres.Count && vistas.Add(madres[indiceMadres]))
            {
                resultado.Add(madres[indiceMadres]);
            }

            indiceMadres++;
        }

        return resultado;
    }

    /// <summary>
    /// Carpetas ya indexadas cuyo nombre (ultimo segmento de la ruta) contiene
    /// <paramref name="texto"/> en cualquier parte - autocompletado del filtro tras 3
    /// letras, que se sigue acotando con cada letra adicional (Caso-11, corregido de
    /// "empieza con" a "contiene" en Caso-14).
    /// </summary>
    public IReadOnlyList<string> BuscarCarpetasPorNombre(string texto, int maximo = 8)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return Array.Empty<string>();
        }

        return _repositorio
            .ObtenerTodasLasCarpetas()
            .Where(ruta =>
                Path.GetFileName(ruta.TrimEnd('\\', '/'))
                    .Contains(texto, StringComparison.OrdinalIgnoreCase)
            )
            .OrderBy(ruta => ruta, StringComparer.OrdinalIgnoreCase)
            .Take(maximo)
            .ToList();
    }

    private static IReadOnlyList<ResultadoBusqueda> BuscarEnArchivos(
        IReadOnlyList<ArchivoIndexado> archivos,
        string consulta,
        ModoBusqueda modo,
        CancellationToken cancellationToken
    )
    {
        var modos = modo == ModoBusqueda.Todos ? CoincidenciaNumero.ModosEnOrden : new[] { modo };

        foreach (var modoActual in modos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var coincidencias = archivos
                .Where(archivo => CoincidenciaNumero.Coincide(archivo.Nombre, consulta, modoActual))
                .Select(Convertir)
                .ToList();

            if (coincidencias.Count > 0)
            {
                return coincidencias;
            }
        }

        return Array.Empty<ResultadoBusqueda>();
    }

    private static ResultadoBusqueda Convertir(ArchivoIndexado archivo) =>
        new()
        {
            Ruta = archivo.Ruta,
            Nombre = archivo.Nombre,
            Carpeta = archivo.CarpetaContenedora,
            FechaModificacion = new DateTime(
                archivo.FechaModificacion,
                DateTimeKind.Utc
            ).ToLocalTime(),
        };
}
