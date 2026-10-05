using Hormiguero.Mensajero.Core.ClickFactura;

namespace Hormiguero.Mensajero.Core;

/// <summary>Plantillas configurables de Mensajero. Los marcadores desconocidos se conservan.</summary>
public static class PlantillasMensajero
{
    public const string ClaveCobelcar = "plantilla.retiro.cobelcar";
    public const string ClaveHoffens = "plantilla.retiro.hoffens";
    public const string ClaveSensus = "plantilla.retiro.sensus";
    public const string ClaveChileHdpe = "plantilla.retiro.chile_hdpe";
    public const string ClaveGuiaHoffens = "plantilla.guia.hoffens";
    public const string ClaveCuerpoFactura = "plantilla.facturas.cuerpo";

    public const string RetiroCobelcar =
        "Retiro en BODEGA FRAY CAMILO 889\n"
        + "Identificarse como: \"Retiro JCV\"\n\n"
        + "OCC {OCC}\nOCL {OCL}\n\n"
        + "Dirección: Fray Camilo Henríquez 889, Santiago\n"
        + "Horario: lunes a viernes, 09:00 a 17:00 hrs";

    public const string RetiroHoffens =
        "Retiro en HOFFENS\n"
        + "Identificarse como: \"Retiro JCV\"\n\n"
        + "OCC {OCC}\nOCL {OCL}\nNVV _______________\n\n"
        + "Día: {DIA}\nBloque: {BLOQUE}\n"
        + "Dirección: Camino Lonquen 10707, Maipú\n"
        + "Mapa: https://maps.app.goo.gl/smWJQSwCrq2vfCcx7\n\n"
        + "Si no se retira ese día, queda armado 3 días hábiles más, en el mismo horario.";

    public const string RetiroSensus =
        "Retiro en SENSUS (Bodega E1 INVAC)\n"
        + "Identificarse como: \"Retiro JCV\"\n\n"
        + "OCC {OCC}\nOCL {OCL}\n\n"
        + "Dirección: Camino del Cerro 290, Quilicura\n"
        + "Mapa: https://maps.app.goo.gl/Eg8LzVUWXCSym7Yp7\n"
        + "Horario: lunes a jueves 08:00-13:00 y 14:00-16:00 / viernes 08:00-13:00\n\n"
        + "Antes de ir, enviar nombre y teléfono de quien retira y el día (se genera un QR para entrar).";

    public const string RetiroChileHdpe =
        "Retiro en CHILE HDPE\n"
        + "Identificarse como: \"Retiro JCV\"\n\n"
        + "OCC {OCC}\nOCL {OCL}\n\n"
        + "Dirección: Cacique Colín 11950, Lampa\n"
        + "Mapa: https://maps.app.goo.gl/HwHhbufKS8cBGboG9\n"
        + "Horario: 08:30 a 17:30";

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
                ["BLOQUE"] = "09:00 a 12:00",
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
        string? bloque = null
    )
    {
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
                ["DIA"] = string.IsNullOrWhiteSpace(dia) ? "_______________" : dia,
                ["BLOQUE"] =
                    string.IsNullOrWhiteSpace(bloque) || bloque == "Manual"
                        ? "_______________"
                        : bloque,
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
}

public sealed record PlantillaMensaje(
    string Clave,
    string Nombre,
    string TextoPredeterminado,
    IReadOnlyList<string> Marcadores
);
