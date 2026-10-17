using Hormiguero.Mensajero.Core.ClickFactura;

namespace Hormiguero.Mensajero.Core;

/// <summary>Plantillas configurables de Mensajero. Los marcadores desconocidos se conservan.</summary>
public static class PlantillasMensajero
{
    public static bool PuedeAgregarOCC(
        IReadOnlyList<(string Occ, string Ocl, string Proveedor)> ordenes,
        string occ,
        string proveedor
    ) =>
        !ordenes.Any(orden => orden.Occ == occ)
        && (ordenes.Count == 0 || ordenes[0].Proveedor == proveedor);

    public const string ClaveCobelcar = "plantilla.retiro.cobelcar";
    public const string ClaveHoffens = "plantilla.retiro.hoffens";
    public const string ClaveSensus = "plantilla.retiro.sensus";
    public const string ClaveChileHdpe = "plantilla.retiro.chile_hdpe";
    public const string ClaveGuiaHoffens = "plantilla.guia.hoffens";
    public const string ClaveCuerpoFactura = "plantilla.facturas.cuerpo";

    public const string RetiroCobelcar =
        "RETIRO COBELCAR\n"
        + "Retirar con: OC JCV {OCC}\n"
        + "Decir: \"Retiro JCV\"\n\n"
        + "Fray Camilo Henríquez 889, Santiago\n"
        + "Lunes a viernes, 09:00 a 17:00\n\n"
        + "Ref.: OC cliente {OCL}";

    public const string RetiroHoffens =
        "RETIRO HOFFENS\n"
        + "Decir en bodega: \"Retiro JCV: solicito el armado del pedido de la NVV {NVV_HOFFENS_DICTADA}\"\n"
        + "NVV Hoffens {NVV_HOFFENS} · OC JCV {OCC}\n\n"
        + "Día: {DIA} · Bloque: {BLOQUE}\n"
        + "Llegue a primera hora para evitar largas esperas.\n"
        + "Camino Lonquén 10707, Maipú\n"
        + "https://maps.app.goo.gl/smWJQSwCrq2vfCcx7\n\n"
        + "Ref.: OC cliente {OCL}{OBRA_REF}";

    public const string RetiroSensus =
        "RETIRO SENSUS (Bodega E1 INVAC)\n"
        + "Retirar con: OC JCV {OCC}\n"
        + "Decir: \"Retiro JCV\"\n\n"
        + "Antes de ir, envíenos nombre, teléfono y patente de quien retira, y el día (Sensus genera un QR para entrar).\n\n"
        + "Camino del Cerro 290, Quilicura\n"
        + "https://maps.app.goo.gl/Eg8LzVUWXCSym7Yp7\n"
        + "Lun a jue 08:00-13:00 y 14:00-16:00 · Vie 08:00-13:00\n\n"
        + "Ref.: OC cliente {OCL}";

    public const string RetiroChileHdpe =
        "RETIRO CHILE HDPE\n"
        + "Retirar con: OC JCV {OCC}\n"
        + "Decir: \"Retiro JCV\"\n\n"
        + "Cacique Colín 11950, Lampa\n"
        + "https://maps.app.goo.gl/HwHhbufKS8cBGboG9\n"
        + "Lunes a viernes, 08:30 a 17:30\n\n"
        + "Ref.: OC cliente {OCL}";

    public const string GuiaHoffens =
        "Buenos días:\n\n"
        + "Envío la guía emitida {FECHA_GUIA} para la obra {OBRA}.\n"
        + "Si llegan más guías hoy, se las envío. Hoffens lo contactará para coordinar la entrega.\n\n"
        + "Saludos.";

    public const string CuerpoFactura =
        "Estimados:\n\n"
        + "Adjunto las {TIPO_DOCUMENTOS} de la {SEMANA} de {RAZON_SOCIAL}.\n\n"
        + "Saludos cordiales.";

    public static IReadOnlyList<PlantillaMensaje> Listar() =>
        [
            new(
                ClaveCobelcar,
                "Retiro Cobelcar",
                RetiroCobelcar,
                ["{OCC} = número de la OCC", "{OCL} = número de la OCL"]
            ),
            new(
                ClaveHoffens,
                "Retiro Hoffens",
                RetiroHoffens,
                [
                    "{OCC} = número de la OCC",
                    "{OCL} = número de la OCL",
                    "{DIA} = día del retiro",
                    "{BLOQUE} = horario del retiro",
                    "{NVV_HOFFENS} = nota de venta de Hoffens para retirar",
                    "{NVV_HOFFENS_DICTADA} = NVV de Hoffens separada después de tres cifras",
                    "{OBRA_REF} = referencia opcional a la obra ingresada",
                    "{OBRA} = nombre de la obra ingresado para el retiro",
                ]
            ),
            new(
                ClaveSensus,
                "Retiro Sensus",
                RetiroSensus,
                ["{OCC} = número de la OCC", "{OCL} = número de la OCL"]
            ),
            new(
                ClaveChileHdpe,
                "Retiro Chile HDPE",
                RetiroChileHdpe,
                ["{OCC} = número de la OCC", "{OCL} = número de la OCL"]
            ),
            new(
                ClaveGuiaHoffens,
                "Guía Hoffens",
                GuiaHoffens,
                ["{FECHA_GUIA} = día de emisión de la guía", "{OBRA} = nombre y comuna de la obra"]
            ),
            new(
                ClaveCuerpoFactura,
                "Correo de facturas",
                CuerpoFactura,
                [
                    "{TIPO_DOCUMENTOS} = facturas y notas de crédito",
                    "{SEMANA} = semana informada",
                    "{RAZON_SOCIAL} = nombre de la empresa",
                ]
            ),
        ];

    public static string ObtenerPredeterminada(string clave) =>
        Listar().FirstOrDefault(plantilla => plantilla.Clave == clave)?.TextoPredeterminado
        ?? throw new ArgumentException("La plantilla no existe.", nameof(clave));

    public static string CrearVistaPrevia(string clave) =>
        CrearVistaPrevia(clave, ObtenerPredeterminada(clave));

    public static string CrearVistaPrevia(string clave, string textoPlantilla)
    {
        Dictionary<string, string> valores = clave switch
        {
            ClaveCobelcar or ClaveSensus or ClaveChileHdpe => new()
            {
                ["OCC"] = "104523",
                ["OCL"] = "4500012345",
            },
            ClaveHoffens => new()
            {
                ["OCC"] = "104523",
                ["OCL"] = "4500012345",
                ["DIA"] = "martes 15",
                ["BLOQUE"] = "09:00 a 14:00",
                ["NVV_HOFFENS"] = "1066086",
                ["NVV_HOFFENS_DICTADA"] = "106 6086",
                ["OBRA_REF"] = " · Obra Avanza Park Vespucio 500",
                ["OBRA"] = "Avanza Park Vespucio 500",
            },
            ClaveGuiaHoffens => new() { ["FECHA_GUIA"] = "hoy", ["OBRA"] = "Obra Ejemplo, Maipú" },
            ClaveCuerpoFactura => new()
            {
                ["TIPO_DOCUMENTOS"] = "facturas y notas de crédito",
                ["SEMANA"] = "2° semana",
                ["RAZON_SOCIAL"] = "Empresa Ejemplo",
            },
            _ => throw new ArgumentException("La plantilla no existe.", nameof(clave)),
        };
        string texto = textoPlantilla;
        foreach ((string marcador, string valor) in valores)
            texto = texto.Replace("{" + marcador + "}", valor, StringComparison.Ordinal);
        return texto;
    }

    public static void VolverAlTextoPredeterminado(AlmacenMensajero almacen, string clave) =>
        almacen.GuardarValor(clave, ObtenerPredeterminada(clave));

    public static string GenerarRetiro(
        AlmacenMensajero almacen,
        string proveedor,
        string occ,
        string ocl,
        string? dia = null,
        string? bloque = null,
        string? nvvHoffens = null,
        string? obra = null
    ) => GenerarRetiro(almacen, proveedor, [(occ, ocl)], dia, bloque, nvvHoffens, obra);

    public static string GenerarRetiro(
        AlmacenMensajero almacen,
        string proveedor,
        IReadOnlyList<(string Occ, string Ocl)> ordenes,
        string? dia = null,
        string? bloque = null,
        string? nvvHoffens = null,
        string? obra = null
    )
    {
        string occ = string.Join(" - ", ordenes.Select(orden => orden.Occ));
        string ocl = string.Join(" - ", ordenes.Select(orden => orden.Ocl));
        string[] nvvs = (nvvHoffens ?? string.Empty).Split(
            [',', ' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries
        );
        string nvv = string.Join(" - ", nvvs);
        string nvvDictada = string.Join(" - ", nvvs.Select(FormatearNvvDictada));
        string obraLimpia = obra?.Trim() ?? string.Empty;
        (string clave, string predeterminada) = proveedor switch
        {
            "COBELCAR" => (ClaveCobelcar, RetiroCobelcar),
            "HOFFENS" => (ClaveHoffens, RetiroHoffens),
            "SENSUS" => (ClaveSensus, RetiroSensus),
            "CHILE HDPE" => (ClaveChileHdpe, RetiroChileHdpe),
            _ => ("", ""),
        };
        if (clave.Length == 0)
            return MensajesRetiro.Generar(proveedor, occ, ocl, dia, bloque);

        return Renderizar(
            almacen,
            clave,
            predeterminada,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["OCC"] = occ,
                ["OCL"] = ocl,
                ["DIA"] = string.IsNullOrWhiteSpace(dia)
                    ? "_______________"
                    : dia.Trim().ToLowerInvariant(),
                ["BLOQUE"] =
                    string.IsNullOrWhiteSpace(bloque) || bloque == "Manual"
                        ? "09:00 a 14:00"
                        : bloque,
                ["NVV_HOFFENS"] = nvv.Length == 0 ? "_______________" : nvv,
                ["NVV_HOFFENS_DICTADA"] = nvvDictada.Length == 0 ? "_______________" : nvvDictada,
                ["OBRA_REF"] = obraLimpia.Length == 0 ? string.Empty : $" · Obra {obraLimpia}",
                ["OBRA"] = obraLimpia,
            }
        );
    }

    public static string GenerarGuia(
        AlmacenMensajero almacen,
        string obra,
        string comuna,
        OpcionDia dia,
        string diaManual
    )
    {
        string fecha = dia switch
        {
            OpcionDia.Ayer => "ayer",
            OpcionDia.Otro when !string.IsNullOrWhiteSpace(diaManual) =>
                $"el día {diaManual.Trim()}",
            _ => "hoy",
        };
        string obraLimpia =
            string.IsNullOrWhiteSpace(obra) || obra == "No detectada" ? "_______________"
            : comuna.Length > 0 ? $"{obra}, {comuna}"
            : obra;
        return Renderizar(
            almacen,
            ClaveGuiaHoffens,
            GuiaHoffens,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["FECHA_GUIA"] = fecha,
                ["OBRA"] = obraLimpia,
            }
        );
    }

    public static string GenerarCuerpoFactura(
        AlmacenMensajero almacen,
        string razonSocial,
        string descripcionSemana,
        IReadOnlyList<DocumentoFactura> documentos
    )
    {
        bool tieneFactura = documentos.Any(documento => documento.Tipo == "FCV");
        bool tieneNota = documentos.Any(documento => documento.Tipo == "NCV");
        string tipo =
            tieneFactura && tieneNota ? "facturas y notas de crédito"
            : tieneNota ? "notas de crédito"
            : "facturas";
        return Renderizar(
            almacen,
            ClaveCuerpoFactura,
            CuerpoFactura,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["TIPO_DOCUMENTOS"] = tipo,
                ["SEMANA"] = descripcionSemana,
                ["RAZON_SOCIAL"] = razonSocial,
            }
        );
    }

    public static string Renderizar(
        AlmacenMensajero almacen,
        string clave,
        string predeterminada,
        IReadOnlyDictionary<string, string> valores
    )
    {
        string plantilla = almacen.LeerValor(clave);
        if (plantilla.Length == 0)
            plantilla = predeterminada;
        foreach ((string marcador, string valor) in valores)
            plantilla = plantilla.Replace("{" + marcador + "}", valor, StringComparison.Ordinal);
        return plantilla;
    }

    private static string FormatearNvvDictada(string nvv) =>
        nvv.Length > 3 ? $"{nvv[..3]} {nvv[3..]}" : nvv;
}

public sealed record PlantillaMensaje(
    string Clave,
    string Nombre,
    string TextoPredeterminado,
    IReadOnlyList<string> Marcadores
);
