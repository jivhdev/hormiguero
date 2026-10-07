using System.IO;
using Archivero.Datos;
using Archivero.Servicios.Pdf;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Utilidades;

namespace Archivero.Servicios;

public sealed class ObservadorCarpetasService : IDisposable
{
    private readonly CarpetasObservadasRepository _repositorio;
    private readonly ConfiguracionDocumentoRepository _configuraciones = new();
    private readonly SemaphoreSlim _procesamiento = new(1, 1);
    private readonly List<FileSystemWatcher> _vigilantes = [];
    private readonly System.Threading.Timer _revision;
    private bool _dispuesto;

    public event Action<DocumentoObservadoReciente>? DocumentoActualizado;
    public event Action<string>? ErrorVisible;

    /// <summary>Punto de enganche posterior a identificar, publicar y revisar el enlace automático.</summary>
    public event Action<string, ConfiguracionDocumento>? DocumentoIdentificadoYPublicado;

    public ObservadorCarpetasService(CarpetasObservadasRepository? repositorio = null)
    {
        _repositorio = repositorio ?? new();
        _revision = new System.Threading.Timer(
            _ => _ = RevisarAsync(),
            null,
            Timeout.Infinite,
            Timeout.Infinite
        );
    }

    public void Iniciar()
    {
        foreach (var vigilanteAnterior in _vigilantes)
            vigilanteAnterior.Dispose();
        _vigilantes.Clear();
        foreach (var carpeta in _repositorio.Leer().Where(c => c.Activa))
        {
            if (!Directory.Exists(carpeta.Ruta))
            {
                Avisar(
                    $"La carpeta observada «{carpeta.Nombre}» no existe o no está disponible: {carpeta.Ruta}"
                );
                continue;
            }

            try
            {
                var vigilante = new FileSystemWatcher(carpeta.Ruta, "*.pdf")
                {
                    IncludeSubdirectories = carpeta.IncluirSubcarpetas,
                    NotifyFilter =
                        NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true,
                };
                vigilante.Created += (_, e) => Encolar(e.FullPath);
                vigilante.Changed += (_, e) => Encolar(e.FullPath);
                vigilante.Renamed += (_, e) => Encolar(e.FullPath);
                vigilante.Error += (_, _) =>
                    Avisar(
                        $"Se perdió la vigilancia de «{carpeta.Nombre}». Se seguirá revisando periódicamente."
                    );
                _vigilantes.Add(vigilante);
            }
            catch (Exception ex)
                when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Avisar($"No se pudo vigilar «{carpeta.Nombre}»: {ex.Message}");
            }
        }

        _revision.Change(TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    public void ActualizarCarpetas() => Iniciar();

    public async Task RevisarAhoraAsync() => await RevisarAsync(esperarTurno: true);

    private async Task RevisarAsync(bool esperarTurno = false)
    {
        if (_dispuesto)
            return;
        if (esperarTurno)
            await _procesamiento.WaitAsync();
        else if (!await _procesamiento.WaitAsync(0))
            return;
        try
        {
            foreach (var carpeta in _repositorio.Leer().Where(c => c.Activa))
            {
                if (!Directory.Exists(carpeta.Ruta))
                {
                    Avisar(
                        $"La carpeta observada «{carpeta.Nombre}» no existe o no está disponible: {carpeta.Ruta}"
                    );
                    continue;
                }

                try
                {
                    var opciones = carpeta.IncluirSubcarpetas
                        ? SearchOption.AllDirectories
                        : SearchOption.TopDirectoryOnly;
                    foreach (
                        string ruta in Directory.EnumerateFiles(carpeta.Ruta, "*.pdf", opciones)
                    )
                        await ProcesarAsync(ruta);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Avisar($"No se pudo revisar «{carpeta.Nombre}»: {ex.Message}");
                }
            }
        }
        finally
        {
            _procesamiento.Release();
        }
    }

    private void Encolar(string ruta) =>
        _ = Task.Run(async () =>
        {
            if (!_dispuesto && await _procesamiento.WaitAsync(0))
            {
                try
                {
                    await ProcesarAsync(ruta);
                }
                finally
                {
                    _procesamiento.Release();
                }
            }
        });

    private async Task ProcesarAsync(string ruta)
    {
        if (!File.Exists(ruta) || !await EsperarEstableAsync(ruta))
            return;
        try
        {
            string huella = Huella.Calcular(ruta);
            string? anterior = _repositorio.LeerHuella(ruta);
            bool cedible = Path.GetFileName(ruta)
                .Contains("CEDIBLE", StringComparison.OrdinalIgnoreCase);
            string textoPdf = LectorPdf.ExtraerTextoCompleto(ruta);
            cedible |= textoPdf.Contains("CEDIBLE", StringComparison.OrdinalIgnoreCase);
            var configuraciones = _configuraciones.ObtenerTodasConPatrones();
            ConfiguracionDocumento? coincidencia = null;
            IReadOnlyList<Hormiguero.Nucleo.Datos.ValorDocumentoLeido> valores = [];
            if (LectorPdf.TieneTextoExtraible(ruta))
            {
                coincidencia = CoincidenciaAutomaticaService.BuscarConfiguracionQueCoincide(
                    ruta,
                    configuraciones
                );
                if (coincidencia is not null)
                {
                    valores = PublicadorDatosDocumentoService.ExtraerValoresObservados(
                        ruta,
                        coincidencia
                    );
                }
            }

            if (cedible)
            {
                bool originalExiste = ExisteOriginal(ruta);
                var actividadAnterior = _repositorio
                    .LeerActividad()
                    .FirstOrDefault(a => a.Ruta == ruta);
                if (originalExiste)
                {
                    if (
                        actividadAnterior?.Motivo?.Contains(
                            "falta el original",
                            StringComparison.OrdinalIgnoreCase
                        ) == true
                    )
                        Registrar(
                            new(
                                DateTime.Now,
                                ruta,
                                null,
                                null,
                                null,
                                "Solucionado: apareció el original"
                            )
                        );
                }
                else if (anterior != huella)
                {
                    Registrar(
                        new(
                            DateTime.Now,
                            ruta,
                            null,
                            null,
                            null,
                            "Dudoso: falta el original. Acción: Buscar original"
                        )
                    );
                }
                _repositorio.GuardarHuella(ruta, huella);
                return;
            }

            if (anterior == huella || coincidencia is null)
                return;

            PublicadorDatosDocumentoService.PublicarObservado(ruta, coincidencia, valores);
            await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);
            _repositorio.GuardarHuella(ruta, huella);
            DocumentoIdentificadoYPublicado?.Invoke(ruta, coincidencia);
            Registrar(
                new(
                    DateTime.Now,
                    ruta,
                    coincidencia.Tipo,
                    coincidencia.Emisor,
                    LeerCadena(ruta),
                    null
                )
            );
        }
        catch (Exception ex)
        {
            Avisar($"No se pudo revisar {Path.GetFileName(ruta)}: {ex.Message}");
        }
    }

    private static string? LeerCadena(string ruta)
    {
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT c.nombre
            FROM enlaces_cadena e
            JOIN vagones_cadena v ON v.id = e.vagon_cadena_id
            JOIN cadenas c ON c.id = v.cadena_id
            JOIN versiones_documento d ON d.id = e.version_id
            WHERE d.ruta_observada = $ruta AND e.estado = 'activo' AND v.estado = 'activo'
            ORDER BY e.id DESC LIMIT 1;
            """;
        comando.Parameters.AddWithValue("$ruta", Path.GetFullPath(ruta));
        return comando.ExecuteScalar() as string;
    }

    private static async Task<bool> EsperarEstableAsync(string ruta)
    {
        long tamano = -1;
        for (int intento = 0; intento < 6; intento++)
        {
            try
            {
                var info = new FileInfo(ruta);
                info.Refresh();
                if (!info.Exists)
                    return false;
                if (tamano == info.Length)
                {
                    using var stream = new FileStream(
                        ruta,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read
                    );
                    return true;
                }
                tamano = info.Length;
            }
            catch (IOException) { }
            await Task.Delay(500);
        }
        return false;
    }

    private static bool ExisteOriginal(string rutaCedible)
    {
        string nombre = Path.GetFileNameWithoutExtension(rutaCedible);
        string original = System
            .Text.RegularExpressions.Regex.Replace(
                nombre,
                "[_ -]?CEDIBLE",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            )
            .TrimEnd('_', '-', ' ');
        if (original == nombre)
            return false;
        return File.Exists(
            Path.Combine(
                Path.GetDirectoryName(rutaCedible)!,
                original + Path.GetExtension(rutaCedible)
            )
        );
    }

    private void Registrar(DocumentoObservadoReciente documento)
    {
        _repositorio.Registrar(documento);
        DocumentoActualizado?.Invoke(documento);
    }

    private void Avisar(string mensaje) => ErrorVisible?.Invoke(mensaje);

    public void Dispose()
    {
        _dispuesto = true;
        _revision.Dispose();
        foreach (var vigilante in _vigilantes)
            vigilante.Dispose();
    }
}
