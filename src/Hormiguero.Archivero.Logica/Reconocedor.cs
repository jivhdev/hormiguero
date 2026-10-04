using System.Globalization;
using System.Text.Json;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Pdf;

namespace Hormiguero.Archivero.Logica;

// Lo que el asistente (A2) guarda en Identificacion.Datos (ADR-001 de Archivero).
// TextoTipo y TextoEmisor son lo que dice el PDF en esas zonas; ZonaFecha null
// significa que el mes sale de hoy (REQ-003 paso 4).
public record ConfiguracionArchivo(
    Zona ZonaTipo,
    string TextoTipo,
    Zona ZonaEmisor,
    string TextoEmisor,
    Zona ZonaNumero,
    Zona? ZonaFecha,
    ReglaDestino Destino
)
{
    public const int Version = 1;

    private static readonly JsonSerializerOptions Opciones = new()
    {
        TypeInfoResolver =
            new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
    };

    public string AJson() => JsonSerializer.Serialize(new Sobre(Version, this), Opciones);

    public static ConfiguracionArchivo DeJson(string json) =>
        JsonSerializer.Deserialize<Sobre>(json, Opciones)?.Configuracion
        ?? throw new InvalidOperationException("Configuración vacía.");

    private sealed record Sobre(int Version, ConfiguracionArchivo Configuracion);
}

public enum ResultadoReconocimiento
{
    Reconocido,
    PorReconocer,
    SinTexto,
}

public record Reconocimiento(
    ResultadoReconocimiento Resultado,
    Identificacion? Identificacion,
    DatosDocumento? Datos,
    string? Detalle
);

public static class Reconocedor
{
    public static Reconocimiento Reconocer(
        InfoPdf info,
        string nombreOriginal,
        IReadOnlyList<Identificacion> identificaciones,
        DateOnly hoy
    )
    {
        if (info.Estado != EstadoPdf.Correcto || !info.TieneTexto)
        {
            // Escaneados y dañados quedan a la vista para guardarlos a mano (REQ-002).
            return new(ResultadoReconocimiento.SinTexto, null, null, Detalle(info));
        }

        var coincidencias = new List<(Identificacion, ConfiguracionArchivo)>();
        foreach (Identificacion identificacion in identificaciones)
        {
            ConfiguracionArchivo configuracion = ConfiguracionArchivo.DeJson(identificacion.Datos);
            if (
                Igual(ZonaPdf.Texto(info, configuracion.ZonaTipo), configuracion.TextoTipo)
                && Igual(ZonaPdf.Texto(info, configuracion.ZonaEmisor), configuracion.TextoEmisor)
            )
            {
                coincidencias.Add((identificacion, configuracion));
            }
        }

        if (coincidencias.Count == 0)
        {
            return new(ResultadoReconocimiento.PorReconocer, null, null, null);
        }

        if (coincidencias.Count > 1)
        {
            return new(
                ResultadoReconocimiento.PorReconocer,
                null,
                null,
                "Coincide con dos configuraciones"
            );
        }

        var (elegida, config) = coincidencias[0];

        string numero = new(ZonaPdf.Texto(info, config.ZonaNumero).Where(char.IsDigit).ToArray());
        if (numero.Length == 0)
        {
            // Nada de clasificaciones a medias: sin número no se archiva.
            return new(
                ResultadoReconocimiento.PorReconocer,
                elegida,
                null,
                "No se encontró el número"
            );
        }

        DateOnly fecha = hoy;
        if (config.ZonaFecha is Zona zonaFecha)
        {
            if (LeerFecha(ZonaPdf.Texto(info, zonaFecha)) is not DateOnly leida)
            {
                return new(
                    ResultadoReconocimiento.PorReconocer,
                    elegida,
                    null,
                    "No se pudo leer la fecha"
                );
            }
            fecha = leida;
        }

        bool esCedible =
            nombreOriginal.Contains("CEDIBLE", StringComparison.OrdinalIgnoreCase)
            || info.Palabras.Any(palabra =>
                palabra.Texto.Equals("CEDIBLE", StringComparison.OrdinalIgnoreCase)
            );

        return new(
            ResultadoReconocimiento.Reconocido,
            elegida,
            new DatosDocumento(
                elegida.Tipo,
                elegida.Emisor,
                numero,
                nombreOriginal,
                fecha,
                esCedible
            ),
            null
        );
    }

    // Coincidencia exacta salvo espacios repetidos y mayúsculas (REQ-002).
    public static bool Igual(string leido, string esperado) =>
        string.Equals(Compactar(leido), Compactar(esperado), StringComparison.OrdinalIgnoreCase);

    private static string Compactar(string texto) =>
        string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static readonly string[] FormatosFecha =
    [
        "dd-MM-yyyy",
        "d-M-yyyy",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "dd.MM.yyyy",
        "yyyy-MM-dd",
        "d 'de' MMMM 'de' yyyy",
        "d 'de' MMMM 'del' yyyy",
    ];

    public static DateOnly? LeerFecha(string texto)
    {
        var cultura = CultureInfo.GetCultureInfo("es-CL");
        foreach (string palabra in Candidatas(texto))
        {
            if (
                DateOnly.TryParseExact(
                    palabra,
                    FormatosFecha,
                    cultura,
                    DateTimeStyles.None,
                    out DateOnly fecha
                )
            )
            {
                return fecha;
            }
        }
        return null;
    }

    // La zona puede traer texto alrededor ("Fecha: 04-10-2026"): se prueba el
    // texto completo y cada palabra por separado.
    private static IEnumerable<string> Candidatas(string texto)
    {
        string limpio = Compactar(texto).Trim(':', ' ');
        yield return limpio;
        if (limpio.IndexOf(':') is int dosPuntos and >= 0)
        {
            yield return limpio[(dosPuntos + 1)..].Trim();
        }
        foreach (string palabra in limpio.Split(' '))
        {
            yield return palabra.Trim(':', ',', '.');
        }
    }

    private static string Detalle(InfoPdf info) =>
        info.Estado switch
        {
            EstadoPdf.Danado => "PDF dañado",
            EstadoPdf.Protegido => "PDF protegido",
            _ => "Sin texto (escaneado)",
        };
}
