using System.Text.Json;
using Hormiguero.Nucleo.Datos;

namespace Buscadero.Core.Alertas;

public static class EditorReglasEsquema
{
    public static string? Validar(string tipo, object parametros)
    {
        try
        {
            var json = JsonSerializer.Serialize(parametros, Opciones);
            if (
                tipo == "falta_dato"
                && JsonSerializer.Deserialize<ParametrosFaltaDato>(json, Opciones) is { } falta
                && !string.IsNullOrWhiteSpace(falta.Dato)
                && !string.IsNullOrWhiteSpace(falta.Texto)
            )
                return null;
            if (
                tipo == "plazo"
                && EsPlazoValido(JsonSerializer.Deserialize<ParametrosPlazo>(json, Opciones))
            )
                return null;
            if (
                tipo == "listo_para"
                && JsonSerializer.Deserialize<ParametrosListoPara>(json, Opciones) is { } listo
                && listo.CuandoLugarId > 0
                && listo.HastaLugarId > 0
                && !string.IsNullOrWhiteSpace(listo.Lista)
            )
                return null;
            return "Complete los campos requeridos del aviso.";
        }
        catch (Exception error) when (error is JsonException or ArgumentException)
        {
            return "Los datos del aviso no son válidos.";
        }
    }

    public static string CrearJson(string tipo, object parametros)
    {
        var error = Validar(tipo, parametros);
        if (error is not null)
            throw new ArgumentException(error, nameof(parametros));
        return JsonSerializer.Serialize(parametros, Opciones);
    }

    public static string CrearFrase(
        string tipo,
        object parametros,
        IReadOnlyDictionary<long, string> lugares
    )
    {
        return tipo switch
        {
            "falta_dato" when parametros is ParametrosFaltaDato p =>
                $"Mientras falte ingresar {p.Dato}, avisar: «{p.Texto}».",
            "plazo" when parametros is ParametrosPlazo p =>
                $"Si llega {Origen(p, lugares)} y en {p.Dias} días {p.TipoDias} no llega {Nombre(lugares, p.HastaLugarId)}{(p.CondicionLugarId is long c ? $" y existe {Nombre(lugares, c)}" : "")}, avisar: «{p.Texto}»."
                    + (p.PorLinea ? " Por cada línea." : ""),
            "listo_para" when parametros is ParametrosListoPara p =>
                $"Cuando llegue {Nombre(lugares, p.CuandoLugarId)}{(p.CondicionLugarId is long c ? $" y exista {Nombre(lugares, c)}" : "")}, hasta que llegue {Nombre(lugares, p.HastaLugarId)}, mostrar en «{p.Lista}».",
            _ => "Complete los datos del aviso.",
        };
    }

    public static int OrdenUrgencia(string? urgencia) =>
        urgencia switch
        {
            "vencido" => 0,
            "por_vencer" => 1,
            _ => 2,
        };

    private static string Origen(ParametrosPlazo p, IReadOnlyDictionary<long, string> lugares) =>
        p.DesdeDato is string dato ? $"se ingresa {dato}" : Nombre(lugares, p.DesdeLugarId!.Value);

    private static string Nombre(IReadOnlyDictionary<long, string> lugares, long id) =>
        lugares.TryGetValue(id, out var nombre) ? nombre : $"lugar {id}";

    private static bool EsPlazoValido(ParametrosPlazo? p) =>
        p is not null
        && (p.DesdeLugarId is > 0) != !string.IsNullOrWhiteSpace(p.DesdeDato)
        && p.HastaLugarId > 0
        && p.Dias >= 0
        && p.TipoDias is "habiles" or "corridos"
        && !string.IsNullOrWhiteSpace(p.Texto);

    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);
}
