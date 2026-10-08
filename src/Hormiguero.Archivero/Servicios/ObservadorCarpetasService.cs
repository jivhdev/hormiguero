using System.IO;
using System.Text.RegularExpressions;
using Archivero.Datos;
using Archivero.Servicios.Pdf;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Utilidades;

namespace Archivero.Servicios;

public sealed class ObservadorCarpetasService : IDisposable
{
    public static string? NumeroDesdeNombre(string ruta)
    {
        string digitos = string.Concat(
            Path.GetFileNameWithoutExtension(ruta).Where(char.IsAsciiDigit)
        );
        return digitos.Length == 0 ? null : digitos;
    }

    private readonly CarpetasObservadasRepository _repositorio;
    private readonly ConfiguracionDocumentoRepository _configuraciones = new();
    private readonly SemaphoreSlim _procesamiento = new(1, 1);
    private readonly List<FileSystemWatcher> _vigilantes = [];
    private readonly System.Threading.Timer _revision;
    private readonly ImpresionAlArchivarService? _impresion;
    private readonly Func<TimeSpan, Task> _pausa;

    // Tamaño y fecha de lo ya revisado en esta sesión: evita leer y calcular la huella de cada
    // PDF cada 30 segundos. "Revisar ahora" lo vacía (p. ej. tras crear una configuración nueva).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        string,
        (long Tamano, DateTime Modificado)
    > _revisados = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _avisados = [];
    private bool _dispuesto;

    public event Action<DocumentoObservadoReciente>? DocumentoActualizado;
    public event Action<string>? ErrorVisible;

    /// <summary>Punto de enganche posterior a identificar, publicar y revisar el enlace automático.</summary>
    public event Action<string, ConfiguracionDocumento>? DocumentoIdentificadoYPublicado;
    public event Action<DocumentoPorAtender>? DocumentoRequiereAtencion;
    public event Action<string, int, int>? ProgresoActualizado;
    private readonly Func<DateTime> _reloj;

    public ObservadorCarpetasService(
        CarpetasObservadasRepository? repositorio = null,
        ImpresionAlArchivarService? impresion = null,
        Func<DateTime>? reloj = null,
        Func<TimeSpan, Task>? pausa = null
    )
    {
        _repositorio = repositorio ?? new();
        _impresion = impresion;
        _reloj = reloj ?? (() => DateTime.Now);
        _pausa = pausa ?? Task.Delay;
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
                    IncludeSubdirectories = carpeta.IncluirSubcarpetas || carpeta.SeguirPeriodo,
                    NotifyFilter =
                        NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true,
                };
                vigilante.Created += (_, e) => Encolar(e.FullPath, carpeta);
                vigilante.Changed += (_, e) => Encolar(e.FullPath, carpeta);
                vigilante.Renamed += (_, e) => Encolar(e.FullPath, carpeta);
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

        _revision.Change(TimeSpan.Zero, TimeSpan.FromMinutes(5));
    }

    public void ActualizarCarpetas() => Iniciar();

    public async Task RevisarAhoraAsync()
    {
        _revisados.Clear();
        lock (_avisados)
            _avisados.Clear();
        await RevisarAsync(esperarTurno: true);
    }

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
            foreach (
                var actividad in _repositorio
                    .LeerActividad()
                    .Where(a =>
                        a.Motivo?.Contains("falta el original", StringComparison.OrdinalIgnoreCase)
                            == true
                        && File.Exists(a.Ruta)
                        && ExisteOriginal(a.Ruta)
                    )
            )
                Registrar(
                    new(
                        _reloj(),
                        actividad.Ruta,
                        null,
                        null,
                        null,
                        "Solucionado: apareció el original"
                    )
                );

            foreach (var carpeta in _repositorio.Leer().Where(c => c.Activa))
            {
                if (!Directory.Exists(carpeta.Ruta))
                {
                    Avisar(
                        $"La carpeta observada «{carpeta.Nombre}» no existe o no está disponible: {carpeta.Ruta}"
                    );
                    continue;
                }

                lock (_avisados)
                    _avisados.RemoveWhere(aviso => aviso.Contains(carpeta.Ruta));
                try
                {
                    var rutas = PeriodosCarpetaObservada.Rutas(carpeta, _reloj());
                    var archivos = rutas
                        .SelectMany(rutaCarpeta =>
                        {
                            var opciones =
                                carpeta.SeguirPeriodo || carpeta.IncluirSubcarpetas
                                    ? SearchOption.AllDirectories
                                    : SearchOption.TopDirectoryOnly;
                            return Directory.EnumerateFiles(rutaCarpeta, "*.pdf", opciones);
                        })
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    int pendientes = archivos.Count(ruta =>
                    {
                        var archivo = new FileInfo(ruta);
                        return archivo.Exists
                            && !_repositorio.YaFueRevisado(
                                ruta,
                                archivo.Length,
                                archivo.LastWriteTimeUtc
                            );
                    });
                    int procesados = 0;
                    ProgresoActualizado?.Invoke(carpeta.Nombre, 0, pendientes);
                    foreach (string ruta in archivos)
                    {
                        var archivo = new FileInfo(ruta);
                        bool eraNuevo =
                            archivo.Exists
                            && !_repositorio.YaFueRevisado(
                                ruta,
                                archivo.Length,
                                archivo.LastWriteTimeUtc
                            );
                        await ProcesarAsync(ruta, carpeta);
                        if (eraNuevo)
                        {
                            procesados++;
                            ProgresoActualizado?.Invoke(carpeta.Nombre, procesados, pendientes);
                            if (procesados < pendientes)
                                await _pausa(TimeSpan.FromSeconds(5));
                        }
                    }
                    ProgresoActualizado?.Invoke(carpeta.Nombre, pendientes, pendientes);
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

    private void Encolar(string ruta, CarpetaObservadaExterna carpeta)
    {
        if (carpeta.SeguirPeriodo && !PerteneceAPeriodoActivo(ruta, carpeta))
            return;
        _ = Task.Run(async () =>
        {
            if (!_dispuesto && await _procesamiento.WaitAsync(0))
            {
                try
                {
                    await ProcesarAsync(ruta, carpeta);
                }
                finally
                {
                    _procesamiento.Release();
                }
            }
        });
    }

    private bool PerteneceAPeriodoActivo(string ruta, CarpetaObservadaExterna carpeta) =>
        PeriodosCarpetaObservada
            .Rutas(carpeta, _reloj())
            .Any(periodo =>
                ruta.StartsWith(
                    Path.GetFullPath(periodo).TrimEnd(Path.DirectorySeparatorChar)
                        + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase
                )
            );

    private async Task ProcesarAsync(string ruta, CarpetaObservadaExterna carpeta)
    {
        var info = new FileInfo(ruta);
        if (info.Exists && _repositorio.YaFueRevisado(ruta, info.Length, info.LastWriteTimeUtc))
        {
            _revisados[ruta] = (info.Length, info.LastWriteTimeUtc);
            return;
        }
        if (
            info.Exists
            && _revisados.TryGetValue(ruta, out var revisado)
            && revisado == (info.Length, info.LastWriteTimeUtc)
        )
            return;
        if (!File.Exists(ruta) || !await EsperarEstableAsync(ruta))
            return;
        info.Refresh();
        var estado = (info.Length, info.LastWriteTimeUtc);
        try
        {
            string huella = Huella.Calcular(ruta);
            string? anterior = _repositorio.LeerHuella(ruta);
            bool cedible = false;
            if (carpeta.ZonaIdentificacion is { } zonaIdentificacion)
            {
                string identificacion = LeerZona(ruta, zonaIdentificacion);
                if (!ContieneNormalizado(identificacion, carpeta.IdentificacionEsperada ?? ""))
                {
                    GuardarVisto(ruta, huella, estado, "ajeno");
                    _revisados[ruta] = estado;
                    return;
                }
                if (carpeta.TieneCedibles && carpeta.ZonaCedible is { } zonaCedible)
                    cedible = ContieneNormalizado(
                        LeerZona(ruta, zonaCedible),
                        carpeta.CedibleEsperado ?? "CEDIBLE"
                    );
            }
            else
            {
                // Compatibilidad con carpetas creadas antes del asistente guiado.
                cedible =
                    Path.GetFileName(ruta).Contains("CEDIBLE", StringComparison.OrdinalIgnoreCase)
                    || LectorPdf
                        .ExtraerTextoPrimeraPagina(ruta)
                        .Contains("CEDIBLE", StringComparison.OrdinalIgnoreCase);
            }
            var configuraciones = carpeta.ConfiguracionesDocumentoIds is { Count: > 0 } ids
                ? _configuraciones
                    .ObtenerTodasConPatronesParaObservador()
                    .Where(c => ids.Contains(c.Id))
                    .ToList()
                : _configuraciones.ObtenerTodasConPatrones();
            ConfiguracionDocumento? coincidencia = null;
            IReadOnlyList<Hormiguero.Nucleo.Datos.ValorDocumentoLeido> valores = [];
            DatoEnlazante? datoIdentificador = null;
            string? numeroNombre = null;
            if (carpeta.ModoReconocimiento == "TipoPorCarpeta")
            {
                datoIdentificador = DiccionarioDatosEnlazantes.Todos.SingleOrDefault(d =>
                    d.Id == carpeta.DatoIdentificador
                );
                numeroNombre = NumeroDesdeNombre(ruta);
                if (numeroNombre is null)
                {
                    GuardarVisto(ruta, huella, estado, "sin-numero");
                    _revisados[ruta] = estado;
                    Registrar(new(_reloj(), ruta, null, null, null, "Sin número en el nombre"));
                    return;
                }
                if (datoIdentificador is null || string.IsNullOrWhiteSpace(carpeta.Emisor))
                    throw new InvalidOperationException(
                        "Completa el dato identificador y el emisor de la carpeta."
                    );
                string grupo = datoIdentificador.Grupo is "Ventas propias" or "Compras propias"
                    ? "Emitido"
                    : "Recibido";
                coincidencia = new ConfiguracionDocumento
                {
                    Emisor = carpeta.Emisor,
                    Tipo = datoIdentificador.EtiquetaTipo,
                    CarpetaDestino = carpeta.Ruta,
                    FormatoCarpeta = FormatoCarpeta.Directo,
                    Renombrar = false,
                    Patrones = [],
                    GrupoDocumento = grupo,
                    NombreEstandar = DiccionarioDatosEnlazantes.NombreEstandar(
                        datoIdentificador.Id,
                        carpeta.Emisor
                    ),
                };
            }
            else if (LectorPdf.TieneTextoExtraible(ruta))
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

            bool documentoCedible = cedible;
            if (cedible && carpeta.ZonaIdentificacion is not null)
            {
                if (ExisteOriginal(ruta))
                {
                    GuardarVisto(ruta, huella, estado, "cedible");
                    _revisados[ruta] = estado;
                    return;
                }
                cedible = false;
            }

            if (cedible)
            {
                bool originalExiste = ExisteOriginal(ruta);
                var actividadAnterior = _repositorio
                    .LeerActividad()
                    .FirstOrDefault(a => a.Ruta == ruta);
                if (originalExiste)
                {
                    _revisados[ruta] = estado;
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
                GuardarVisto(ruta, huella, estado, documentoCedible ? "cedible" : "ignorado");
                return;
            }

            if (anterior == huella || coincidencia is null)
            {
                _revisados[ruta] = estado;
                return;
            }

            if (datoIdentificador is null)
                PublicadorDatosDocumentoService.PublicarObservado(ruta, coincidencia, valores);
            else
                PublicadorDatosDocumentoService.PublicarObservado(
                    ruta,
                    coincidencia,
                    datoIdentificador,
                    numeroNombre!
                );
            await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);
            GuardarVisto(ruta, huella, estado, "procesado");
            _revisados[ruta] = estado;
            DocumentoIdentificadoYPublicado?.Invoke(ruta, coincidencia);
            bool posteriorAAgregada =
                carpeta.Agregada is DateTime agregada && info.LastWriteTime >= agregada;
            bool numeroRepetido =
                posteriorAAgregada
                && !string.IsNullOrEmpty(numeroNombre)
                && _repositorio
                    .LeerActividad()
                    .Any(a =>
                        a.Ruta != ruta
                        && a.Tipo == coincidencia.Tipo
                        && a.Emisor == coincidencia.Emisor
                        && NumeroDesdeNombre(a.Ruta) is string otro
                        && DiccionarioDatosEnlazantes.ClaveDeEnlace(otro)
                            == DiccionarioDatosEnlazantes.ClaveDeEnlace(numeroNombre)
                    );
            if (
                posteriorAAgregada
                && !documentoCedible
                && carpeta.AccionAlLlegar is "Avisar" or "AvisarImprimirPrimeraPagina"
            )
            {
                var pendiente = new DocumentoPorAtender(
                    Guid.NewGuid(),
                    _reloj(),
                    ruta,
                    coincidencia.Tipo,
                    numeroNombre ?? NumeroDesdeNombre(ruta) ?? ""
                );
                _repositorio.AgregarPorAtender(pendiente);
                DocumentoRequiereAtencion?.Invoke(pendiente);
            }
            ModoImpresion? modo = carpeta.AccionAlLlegar switch
            {
                "ImprimirPrimeraPagina" or "AvisarImprimirPrimeraPagina" =>
                    ModoImpresion.PrimeraPagina,
                "ImprimirTodo" => ModoImpresion.TodoElDocumento,
                "Configuracion" => coincidencia.ModoImpresion,
                _ => null,
            };
            if (posteriorAAgregada && !documentoCedible && modo is ModoImpresion modoImpresion)
            {
                string? avisoImpresion = _impresion?.Procesar(
                    ruta,
                    coincidencia with
                    {
                        ModoImpresion = modoImpresion,
                    }
                );
                if (avisoImpresion is not null)
                    Avisar($"{Path.GetFileName(ruta)}: {avisoImpresion}");
            }
            Registrar(
                new(
                    DateTime.Now,
                    ruta,
                    coincidencia.Tipo,
                    coincidencia.Emisor,
                    LeerCadena(ruta),
                    numeroRepetido ? "Número repetido" : null
                )
            );
        }
        catch (Exception ex)
        {
            Avisar($"No se pudo revisar {Path.GetFileName(ruta)}: {ex.Message}");
        }
    }

    private void GuardarVisto(
        string ruta,
        string huella,
        (long Tamano, DateTime Modificado) estado,
        string resultado
    )
    {
        _repositorio.GuardarHuella(ruta, huella);
        _repositorio.GuardarEstado(ruta, estado.Tamano, estado.Modificado, resultado);
    }

    private static string LeerZona(string ruta, ZonaControlCarpeta zona) =>
        LectorPdf.ExtraerTexto(ruta, zona.Pagina - 1, new(zona.X, zona.Y, zona.Ancho, zona.Alto));

    private static bool ContieneNormalizado(string texto, string esperado)
    {
        static string Normalizar(string valor) =>
            System.Text.RegularExpressions.Regex.Replace(valor, @"\s+", " ").Trim();
        string valorNormalizado = Normalizar(texto);
        string esperadoNormalizado = Normalizar(esperado);
        return esperadoNormalizado.Length > 0
            && valorNormalizado.Contains(esperadoNormalizado, StringComparison.OrdinalIgnoreCase);
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

    // Cada aviso se muestra una sola vez (la revisión corre cada 30 segundos); vuelve a mostrarse
    // si la carpeta se recupera y falla otra vez, o tras "Revisar ahora".
    private void Avisar(string mensaje)
    {
        lock (_avisados)
            if (!_avisados.Add(mensaje))
                return;
        ErrorVisible?.Invoke(mensaje);
    }

    public void Dispose()
    {
        _dispuesto = true;
        _revision.Dispose();
        foreach (var vigilante in _vigilantes)
            vigilante.Dispose();
    }
}
