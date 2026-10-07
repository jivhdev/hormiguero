namespace Buscadero.Core.Indexado;

public sealed class Indexador
{
    private readonly RepositorioIndice _repositorio;
    private readonly TimeSpan _intervaloEntreSubcarpetas;
    private readonly Action<TimeSpan> _pausar;

    // Fase B-2a: el índice puede actualizarse en segundo plano mientras se busca;
    // dos pasadas a la vez se pisarían, así que se hacen de a una.
    private readonly object _unaPasadaALaVez = new();

    public Indexador(
        RepositorioIndice repositorio,
        TimeSpan? intervaloEntreSubcarpetas = null,
        Action<TimeSpan>? pausar = null
    )
    {
        _repositorio = repositorio;
        _intervaloEntreSubcarpetas = intervaloEntreSubcarpetas ?? TimeSpan.FromMilliseconds(200);
        _pausar =
            pausar
            ?? (
                espera =>
                {
                    if (espera > TimeSpan.Zero)
                    {
                        Thread.Sleep(espera);
                    }
                }
            );
    }

    public ResumenIndexado Indexar(
        IEnumerable<string> carpetasRaiz,
        CancellationToken cancellationToken = default,
        IProgress<ProgresoIndexado>? progreso = null
    )
    {
        lock (_unaPasadaALaVez)
        {
            return IndexarSinCompetir(carpetasRaiz, cancellationToken, progreso);
        }
    }

    private ResumenIndexado IndexarSinCompetir(
        IEnumerable<string> carpetasRaiz,
        CancellationToken cancellationToken,
        IProgress<ProgresoIndexado>? progreso
    )
    {
        var fechas = new Dictionary<string, long>(
            _repositorio.ObtenerFechasCarpetas(),
            StringComparer.OrdinalIgnoreCase
        );
        var estado = new EstadoIndexado();

        foreach (var raiz in carpetasRaiz)
        {
            Recorrer(raiz, null, fechas, estado, cancellationToken, progreso);
        }

        return new ResumenIndexado
        {
            CarpetasVisitadas = estado.CarpetasVisitadas,
            ArchivosIndexados = estado.ArchivosIndexados,
        };
    }

    public void QuitarDelIndice(string carpeta) =>
        _repositorio.EliminarSubarbol(RepositorioIndice.ClaveRuta(carpeta));

    private void Recorrer(
        string carpeta,
        string? padreClave,
        Dictionary<string, long> fechas,
        EstadoIndexado estado,
        CancellationToken cancellationToken,
        IProgress<ProgresoIndexado>? progreso
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(carpeta))
        {
            return;
        }

        var clave = RepositorioIndice.ClaveRuta(carpeta);
        long fechaModificacion;
        try
        {
            fechaModificacion = Directory.GetLastWriteTimeUtc(carpeta).Ticks;
        }
        catch (Exception excepcion) when (EsErrorDeAcceso(excepcion))
        {
            return;
        }

        var noCambio =
            fechas.TryGetValue(clave, out var fechaPrevia) && fechaPrevia == fechaModificacion;

        if (noCambio)
        {
            progreso?.Report(
                new ProgresoIndexado
                {
                    CarpetaActual = carpeta,
                    CarpetasVisitadas = estado.CarpetasVisitadas,
                    ArchivosIndexados = estado.ArchivosIndexados,
                    Omitida = true,
                }
            );
        }
        else
        {
            estado.CarpetasVisitadas++;

            var archivos = LeerArchivosPdf(carpeta);
            _repositorio.ReemplazarArchivos(clave, archivos);
            estado.ArchivosIndexados += archivos.Count;

            // Una fecha de hace menos de 2 s no es confiable: Windows puede actualizar la fecha
            // de la carpeta con retraso y un PDF recién creado quedaría sin indexar. Se guarda 0
            // para que la próxima pasada la revise de nuevo.
            long fechaGuardada =
                DateTime.UtcNow.Ticks - fechaModificacion < TimeSpan.FromSeconds(2).Ticks
                    ? 0
                    : fechaModificacion;
            _repositorio.GuardarCarpeta(carpeta, clave, padreClave, fechaGuardada);

            progreso?.Report(
                new ProgresoIndexado
                {
                    CarpetaActual = carpeta,
                    CarpetasVisitadas = estado.CarpetasVisitadas,
                    ArchivosIndexados = estado.ArchivosIndexados,
                    Omitida = false,
                }
            );
        }

        // Caso-15: las subcarpetas se revisan SIEMPRE, aunque esta carpeta no haya
        // cambiado - en NTFS, agregar/quitar un archivo dentro de una subcarpeta
        // actualiza la fecha de modificacion de esa subcarpeta, pero no la de sus
        // carpetas ancestras. Saltear la recursion cuando el padre esta "sin cambios"
        // dejaba sin detectar archivos nuevos en subcarpetas ya indexadas.
        var subcarpetas = LeerSubcarpetas(carpeta);
        EliminarHijasAusentes(clave, subcarpetas);

        foreach (var subcarpeta in subcarpetas)
        {
            _pausar(_intervaloEntreSubcarpetas);
            Recorrer(subcarpeta, clave, fechas, estado, cancellationToken, progreso);
        }
    }

    private static List<ArchivoIndexado> LeerArchivosPdf(string carpeta)
    {
        var archivos = new List<ArchivoIndexado>();
        IEnumerable<string> rutas;
        try
        {
            rutas = Directory.EnumerateFiles(carpeta);
        }
        catch (Exception excepcion) when (EsErrorDeAcceso(excepcion))
        {
            return archivos;
        }

        foreach (var ruta in rutas)
        {
            if (!EsPdf(ruta))
            {
                continue;
            }

            try
            {
                var informacion = new FileInfo(ruta);
                archivos.Add(
                    new ArchivoIndexado
                    {
                        Ruta = ruta,
                        Nombre = informacion.Name,
                        CarpetaContenedora = carpeta,
                        FechaModificacion = informacion.LastWriteTimeUtc.Ticks,
                    }
                );
            }
            catch (Exception excepcion) when (EsErrorDeAcceso(excepcion)) { }
        }

        return archivos;
    }

    private static string[] LeerSubcarpetas(string carpeta)
    {
        string[] subcarpetas;
        try
        {
            subcarpetas = Directory.GetDirectories(carpeta);
        }
        catch (Exception excepcion) when (EsErrorDeAcceso(excepcion))
        {
            return Array.Empty<string>();
        }

        // Directory.GetDirectories no garantiza ningun orden; SPEC.md exige recorrer
        // siempre de la subcarpeta mas reciente a la mas antigua.
        return subcarpetas.OrderByDescending(ObtenerFechaModificacionSegura).ToArray();
    }

    private static DateTime ObtenerFechaModificacionSegura(string carpeta)
    {
        try
        {
            return Directory.GetLastWriteTimeUtc(carpeta);
        }
        catch (Exception excepcion) when (EsErrorDeAcceso(excepcion))
        {
            return DateTime.MinValue;
        }
    }

    private void EliminarHijasAusentes(string clave, IReadOnlyList<string> subcarpetasPresentes)
    {
        var presentes = subcarpetasPresentes
            .Select(RepositorioIndice.ClaveRuta)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var hija in _repositorio.ObtenerCarpetasHijas(clave))
        {
            if (!presentes.Contains(hija))
            {
                _repositorio.EliminarSubarbol(hija);
            }
        }
    }

    private static bool EsPdf(string ruta) =>
        string.Equals(Path.GetExtension(ruta), ".pdf", StringComparison.OrdinalIgnoreCase);

    private static bool EsErrorDeAcceso(Exception excepcion) =>
        excepcion is UnauthorizedAccessException or IOException or DirectoryNotFoundException;

    private sealed class EstadoIndexado
    {
        public int CarpetasVisitadas { get; set; }
        public int ArchivosIndexados { get; set; }
    }
}
