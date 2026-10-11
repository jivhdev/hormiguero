using System.IO;
using System.Text.Json;

namespace Archivero.Datos;

public static class PeriodosCarpetaObservada
{
    public static (string RutaBase, string Formato)? Detectar(string ruta)
    {
        string[] partes = Path.GetFullPath(ruta)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries
            );
        if (partes.Length >= 2 && partes[^2].Length == 4 && partes[^2].All(char.IsAsciiDigit))
        {
            if (
                partes[^1].Length == 6
                && partes[^1].StartsWith(partes[^2], StringComparison.Ordinal)
            )
                return (QuitarSegmentos(ruta, 2), "AAAA_AAAAMM");
            if (partes[^1].Length == 2 && partes[^1].All(char.IsAsciiDigit))
                return (QuitarSegmentos(ruta, 2), "AAAA_MM");
        }
        if (partes[^1].Length == 6 && partes[^1].All(char.IsAsciiDigit))
            return (QuitarSegmentos(ruta, 1), "AAAAMM");
        if (partes[^1].Length == 4 && partes[^1].All(char.IsAsciiDigit))
            return (QuitarSegmentos(ruta, 1), "AAAA");
        return null;
    }

    public static IReadOnlyList<string> Rutas(CarpetaObservadaExterna carpeta, DateTime ahora)
    {
        if (!carpeta.SeguirPeriodo)
            return [carpeta.Ruta];
        DateTime actual = new(ahora.Year, ahora.Month, 1);
        DateTime anterior =
            carpeta.FormatoPeriodo == "AAAA"
                ? new DateTime(ahora.Year - 1, 1, 1)
                : actual.AddMonths(-1);
        var periodos = ahora.Day <= 5 ? new[] { actual, anterior } : [actual];
        return periodos
            .Select(mes => Path.Combine(carpeta.Ruta, Formatear(mes, carpeta.FormatoPeriodo)))
            .Where(Directory.Exists)
            .ToArray();
    }

    private static string Formatear(DateTime fecha, string formato) =>
        formato switch
        {
            "AAAA_MM" => Path.Combine(fecha.ToString("yyyy"), fecha.ToString("MM")),
            "AAAAMM" => fecha.ToString("yyyyMM"),
            "AAAA" => fecha.ToString("yyyy"),
            _ => Path.Combine(fecha.ToString("yyyy"), fecha.ToString("yyyyMM")),
        };

    private static string QuitarSegmentos(string ruta, int quitar)
    {
        ruta = Path.GetFullPath(ruta);
        for (int i = 0; i < quitar; i++)
            ruta = Path.GetDirectoryName(ruta)!;
        return ruta;
    }
}

public sealed record CarpetaObservadaExterna(
    Guid Id,
    string Nombre,
    string Ruta,
    bool IncluirSubcarpetas,
    bool Activa,
    // Solo se imprimen automáticamente los PDF que llegan después de agregar la carpeta,
    // para no imprimir de golpe todo lo que ya estaba ahí.
    DateTime? Agregada = null,
    string ModoReconocimiento = "Configuraciones",
    string? DatoIdentificador = null,
    string Emisor = "",
    bool SeguirPeriodo = false,
    string FormatoPeriodo = "AAAA_AAAAMM",
    string AccionAlLlegar = "Configuracion",
    ZonaControlCarpeta? ZonaIdentificacion = null,
    string? IdentificacionEsperada = null,
    int? EntidadIdentificacionId = null,
    bool TieneCedibles = false,
    ZonaControlCarpeta? ZonaCedible = null,
    string? CedibleEsperado = null,
    IReadOnlyList<int>? ConfiguracionesDocumentoIds = null,
    string? Impresora = null
)
{
    public string Resumen
    {
        get
        {
            string tipo =
                Hormiguero
                    .Nucleo.Datos.DiccionarioDatosEnlazantes.Todos.FirstOrDefault(d =>
                        d.Id == DatoIdentificador
                    )
                    ?.EtiquetaTipo
                ?? "configuración";
            string accion = AccionAlLlegar switch
            {
                "ImprimirPrimeraPagina" => "imprime 1.ª página",
                "ImprimirTodo" => "imprime todo",
                "Avisar" => "avisa",
                "AvisarImprimirPrimeraPagina" => "avisa e imprime 1.ª página",
                "SoloRegistrar" => "solo registra",
                _ => "según el tipo",
            };
            return ModoReconocimiento == "TipoPorCarpeta"
                ? $"{Nombre} · {tipo} · {accion}{(SeguirPeriodo ? " · sigue el mes" : "")}"
                : Nombre;
        }
    }
}

public sealed record ZonaControlCarpeta(
    int Pagina,
    double X,
    double Y,
    double Ancho,
    double Alto,
    string TextoEsperado
);

public sealed record DocumentoPorAtender(
    Guid Id,
    DateTime Llegada,
    string Ruta,
    string Tipo,
    string Numero,
    DateTime? Listo = null
)
{
    public string Resumen =>
        $"{Llegada:dd-MM HH:mm} · {Tipo} N° {Hormiguero.Nucleo.Datos.DiccionarioDatosEnlazantes.ValorComoSeLee(Numero)} · {Path.GetFileName(Ruta)}";
}

public sealed record DocumentoObservadoReciente(
    DateTime Fecha,
    string Ruta,
    string? Tipo,
    string? Emisor,
    string? Cadena,
    string? Motivo
)
{
    public string Resumen =>
        Motivo is not null
            ? $"{Fecha:HH:mm:ss} — {Path.GetFileName(Ruta)} — {Motivo}"
            : $"{Fecha:HH:mm:ss} — {Path.GetFileName(Ruta)} — {Tipo} · {Emisor}{(Cadena is null ? "" : $" — {Cadena}")}";
}

public sealed class CarpetasObservadasRepository(ConfiguracionRepository? configuracion = null)
{
    private readonly ConfiguracionRepository _configuracion = configuracion ?? new();
    private const string ClaveCarpetas = "carpetas.observadas";
    private const string ClaveActividad = "carpetas.observadas.actividad";
    private const string ClaveHuellas = "carpetas.observadas.huellas";
    private const string ClaveEstados = "carpetas.observadas.estados";
    private const string ClavePorAtender = "carpetas.observadas.atender";

    public IReadOnlyList<CarpetaObservadaExterna> Leer()
    {
        try
        {
            return JsonSerializer.Deserialize<List<CarpetaObservadaExterna>>(
                    _configuracion.Obtener(ClaveCarpetas) ?? "[]"
                ) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public void Guardar(IReadOnlyList<CarpetaObservadaExterna> carpetas) =>
        _configuracion.Guardar(ClaveCarpetas, JsonSerializer.Serialize(carpetas));

    public IReadOnlyList<DocumentoObservadoReciente> LeerActividad()
    {
        try
        {
            return JsonSerializer.Deserialize<List<DocumentoObservadoReciente>>(
                    _configuracion.Obtener(ClaveActividad) ?? "[]"
                ) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public void Registrar(DocumentoObservadoReciente documento)
    {
        var actividad = LeerActividad()
            .Where(a => a.Ruta != documento.Ruta)
            .Prepend(documento)
            .Take(30)
            .ToList();
        _configuracion.Guardar(ClaveActividad, JsonSerializer.Serialize(actividad));
    }

    public string? LeerHuella(string ruta)
    {
        try
        {
            return (
                JsonSerializer.Deserialize<Dictionary<string, string>>(
                    _configuracion.Obtener(ClaveHuellas) ?? "{}"
                ) ?? []
            ).GetValueOrDefault(Path.GetFullPath(ruta));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public bool YaFueRevisado(string ruta, long tamano, DateTime modificado)
    {
        try
        {
            var estados =
                JsonSerializer.Deserialize<Dictionary<string, EstadoArchivoObservado>>(
                    _configuracion.Obtener(ClaveEstados) ?? "{}"
                ) ?? [];
            return estados.TryGetValue(Path.GetFullPath(ruta), out var estado)
                && estado.Tamano == tamano
                && estado.Modificado == modificado;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public string? ResultadoRevisado(string ruta)
    {
        try
        {
            var estados =
                JsonSerializer.Deserialize<Dictionary<string, EstadoArchivoObservado>>(
                    _configuracion.Obtener(ClaveEstados) ?? "{}"
                ) ?? [];
            return estados.TryGetValue(Path.GetFullPath(ruta), out var estado)
                ? estado.Resultado
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void GuardarEstado(string ruta, long tamano, DateTime modificado, string resultado)
    {
        Dictionary<string, EstadoArchivoObservado> estados;
        try
        {
            estados =
                JsonSerializer.Deserialize<Dictionary<string, EstadoArchivoObservado>>(
                    _configuracion.Obtener(ClaveEstados) ?? "{}"
                ) ?? [];
        }
        catch (JsonException)
        {
            estados = [];
        }
        estados[Path.GetFullPath(ruta)] = new(tamano, modificado, resultado);
        _configuracion.Guardar(ClaveEstados, JsonSerializer.Serialize(estados));
    }

    public void GuardarHuella(string ruta, string huella)
    {
        Dictionary<string, string> huellas;
        try
        {
            huellas =
                JsonSerializer.Deserialize<Dictionary<string, string>>(
                    _configuracion.Obtener(ClaveHuellas) ?? "{}"
                ) ?? [];
        }
        catch (JsonException)
        {
            huellas = [];
        }
        huellas[Path.GetFullPath(ruta)] = huella;
        _configuracion.Guardar(ClaveHuellas, JsonSerializer.Serialize(huellas));
    }

    public IReadOnlyList<DocumentoPorAtender> LeerPorAtender()
    {
        try
        {
            return (
                JsonSerializer.Deserialize<List<DocumentoPorAtender>>(
                    _configuracion.Obtener(ClavePorAtender) ?? "[]"
                ) ?? []
            )
                .Where(d => d.Listo is null)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public void AgregarPorAtender(DocumentoPorAtender documento)
    {
        var documentos = LeerPorAtender().Prepend(documento).ToList();
        _configuracion.Guardar(ClavePorAtender, JsonSerializer.Serialize(documentos));
    }

    public void MarcarListo(Guid id)
    {
        List<DocumentoPorAtender> documentos;
        try
        {
            documentos =
                JsonSerializer.Deserialize<List<DocumentoPorAtender>>(
                    _configuracion.Obtener(ClavePorAtender) ?? "[]"
                ) ?? [];
        }
        catch (JsonException)
        {
            documentos = [];
        }
        _configuracion.Guardar(
            ClavePorAtender,
            JsonSerializer.Serialize(
                documentos.Select(d => d.Id == id ? d with { Listo = DateTime.Now } : d)
            )
        );
    }
}

public sealed record EstadoArchivoObservado(long Tamano, DateTime Modificado, string Resultado);
