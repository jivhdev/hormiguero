using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Hormiguero.Mensajero.Core;

public sealed record RectanguloOcc(double X0, double Y0, double X1, double Y1);

public static class ZonasOcc
{
    public static readonly RectanguloOcc Occ = new(498.60, 105.53, 558.56, 119.47);
    public static readonly RectanguloOcc Nvv = new(481.56, 240.05, 565.52, 253.99);
    public static readonly RectanguloOcc Ocl = new(495.72, 252.05, 580.00, 265.99);
    public static readonly RectanguloOcc Despacho = new(34.32, 665.09, 560.00, 800.00);
    public static readonly RectanguloOcc Proveedor = new(14.16, 201.53, 350.00, 215.47);

    public static readonly IReadOnlyList<KeyValuePair<string, string>> MapeoProveedores =
    [
        new("HOFFENS", "HOFFENS"),
        new("COBELCAR", "COBELCAR"),
        new("VELIZ BELTRAN LIMITADA", "COBELCAR"),
        new("VELIZ BELTRAN", "COBELCAR"),
        new("SENSUS", "SENSUS"),
        new("CHILE HDPE", "CHILE HDPE"),
        new("HDPE", "CHILE HDPE"),
        new("FLUCHILE", "FLUCHILE"),
    ];
}

public static class ExtractorOcc
{
    public static (string Occ, string Nvv, string Ocl) ExtraerOccNvvOcl(string rutaPdf)
    {
        using var documento = AbrirPdf(rutaPdf);
        Page pagina = documento.GetPage(1);
        return (
            LimpiarOcc(ExtraerTexto(pagina, ZonasOcc.Occ)),
            LimpiarNvv(ExtraerTexto(pagina, ZonasOcc.Nvv)),
            ExtraerTexto(pagina, ZonasOcc.Ocl)
        );
    }

    public static string ExtraerDespacho(string rutaPdf)
    {
        using var documento = AbrirPdf(rutaPdf);
        Page pagina = documento.GetPage(1);
        var zona = new RectanguloOcc(
            24.0,
            ZonasOcc.Despacho.Y0,
            ZonasOcc.Despacho.X1,
            pagina.Height
        );
        string texto = ExtraerTexto(pagina, zona).Trim();
        string[] lineas = texto.Split('\n');
        if (lineas.Length == 0 || texto.Length == 0)
        {
            return "";
        }

        string titulo = lineas[0].Trim();
        string contenido = string.Join(
            "\n",
            lineas
                .Skip(1)
                .Where(linea => !string.IsNullOrWhiteSpace(linea))
                .Select(linea => linea.Trim())
        );
        return $"{titulo}\n{contenido}";
    }

    public static string ExtraerProveedor(string rutaPdf)
    {
        using var documento = AbrirPdf(rutaPdf);
        string texto = ExtraerTexto(documento.GetPage(1), ZonasOcc.Proveedor);
        if (texto.Length == 0)
        {
            return "DESCONOCIDO";
        }

        texto = Regex
            .Replace(texto, @"^(Señores/as|Señor(es)?):\s*", "", RegexOptions.IgnoreCase)
            .Trim()
            .ToUpperInvariant();
        texto = Regex.Replace(texto, @"\s+S\.?A\.?$", "");
        texto = Regex.Replace(texto, @"\s+LTDA\.?$", "");
        texto = Regex.Replace(texto, @"\s+LIMITADA$", "");
        texto = Regex.Replace(texto, @"\s+LTD\.?$", "");
        foreach (KeyValuePair<string, string> par in ZonasOcc.MapeoProveedores)
        {
            if (texto.Contains(par.Key, StringComparison.Ordinal))
            {
                return par.Value;
            }
        }

        return texto;
    }

    public static string LimpiarOcc(string textoBruto)
    {
        if (string.IsNullOrEmpty(textoBruto))
        {
            return "";
        }

        string texto = textoBruto.Trim();
        return BigInteger.TryParse(
            texto,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out BigInteger numero
        )
            ? numero.ToString(CultureInfo.InvariantCulture)
            : texto;
    }

    public static string LimpiarNvv(string textoBruto)
    {
        if (string.IsNullOrEmpty(textoBruto))
        {
            return "";
        }

        string sinPrefijo = Regex.Replace(textoBruto, @"^[Nn][Vv][Vv]\s*\-?\s*", "").Trim();
        if (sinPrefijo.Length == 0)
        {
            return "";
        }

        return BigInteger.TryParse(
            sinPrefijo,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out BigInteger numero
        )
            ? numero.ToString(CultureInfo.InvariantCulture)
            : sinPrefijo;
    }

    public static string FormatearAsunto(string occ, string nvv, string ocl)
    {
        var partes = new List<string>();
        if (!string.IsNullOrEmpty(occ))
            partes.Add($"OCC {occ}");
        if (!string.IsNullOrEmpty(nvv))
            partes.Add($"NVV {nvv}");
        if (!string.IsNullOrEmpty(ocl))
            partes.Add($"OCL {ocl}");
        return string.Join(" ", partes);
    }

    private static PdfDocument AbrirPdf(string rutaPdf)
    {
        var flujo = new FileStream(
            rutaPdf,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete
        );
        try
        {
            return PdfDocument.Open(flujo);
        }
        catch
        {
            flujo.Dispose();
            throw;
        }
    }

    // Reproduce page.get_text("text", clip=rect).strip() de PyMuPDF (lo que usa Ofisuiza).
    // MuPDF deja una letra si su TINTA toca la zona (aunque sea apenas), recorre las letras en
    // el orden del PDF y arma los renglones según la distancia entre cada letra y la anterior
    // que quedó: poca distancia (o un retroceso corto) = misma palabra, algo más hacia adelante =
    // espacio, mucha = renglón nuevo.
    private const double DistanciaEspacio = 0.15;
    private const double DistanciaMaxima = 0.8;

    private static string ExtraerTexto(Page pagina, RectanguloOcc zona)
    {
        double alto = pagina.Height;
        var texto = new StringBuilder();
        (double X, double Y)? pluma = null;
        (double X, double Y) direccion = (1, 0);
        foreach (Letter letra in pagina.Letters)
        {
            if (!TintaTocaLaZona(letra, zona, alto))
                continue;

            (double X, double Y) origen = (letra.StartBaseLine.X, alto - letra.StartBaseLine.Y);
            (double X, double Y) fin = (letra.EndBaseLine.X, alto - letra.EndBaseLine.Y);
            double tamano = letra.PointSize > 0 ? letra.PointSize : 1;
            if (pluma is { } anterior)
            {
                double dx = origen.X - anterior.X;
                double dy = origen.Y - anterior.Y;
                double avance = (direccion.X * dx + direccion.Y * dy) / tamano;
                double desvio = (-direccion.Y * dx + direccion.X * dy) / tamano;
                if (Math.Abs(desvio) >= DistanciaMaxima || Math.Abs(avance) >= DistanciaMaxima)
                    texto.Append('\n');
                else if (avance >= DistanciaEspacio && texto.Length > 0 && texto[^1] != ' ')
                    texto.Append(' ');
            }

            texto.Append(letra.Value);
            pluma = fin;
            double largo = Math.Sqrt(
                (fin.X - origen.X) * (fin.X - origen.X) + (fin.Y - origen.Y) * (fin.Y - origen.Y)
            );
            if (largo > 0)
                direccion = ((fin.X - origen.X) / largo, (fin.Y - origen.Y) / largo);
        }

        return texto.ToString().Trim();
    }

    // Caja de tinta de la letra en coordenadas de PyMuPDF (origen arriba). Una letra sin tinta
    // (espacio) cuenta como el punto donde empieza, igual que en MuPDF. Si la fuente es una de
    // las estándar sin incrustar, la caja sale de la tabla de MuPDF y no de la de PdfPig.
    private static bool TintaTocaLaZona(Letter letra, RectanguloOcc zona, double alto)
    {
        var caja = letra.BoundingBox;
        double x0,
            x1,
            y0,
            y1;
        if (TintaBase14.Buscar(letra.FontName ?? "", letra.Value, out var tinta))
        {
            double escala = letra.PointSize / 1000;
            x0 = letra.StartBaseLine.X + tinta.X0 * escala;
            x1 = letra.StartBaseLine.X + tinta.X1 * escala;
            y0 = alto - (letra.StartBaseLine.Y + tinta.Y1 * escala);
            y1 = alto - (letra.StartBaseLine.Y + tinta.Y0 * escala);
        }
        else if (!string.IsNullOrWhiteSpace(letra.Value) && (caja.Width > 0 || caja.Height > 0))
        {
            x0 = caja.Left;
            x1 = caja.Right;
            y0 = alto - caja.Top;
            y1 = alto - caja.Bottom;
        }
        else
        {
            x0 = x1 = letra.StartBaseLine.X;
            y0 = y1 = alto - letra.StartBaseLine.Y;
        }

        return !(x1 < zona.X0 || y1 < zona.Y0 || x0 > zona.X1 || y0 > zona.Y1);
    }
}

public static class MensajesRetiro
{
    public static readonly IReadOnlyList<string> BloquesHoffens =
    [
        "09:00 - 11:00",
        "11:00 - 13:00",
        "Manual",
    ];

    public static string Generar(
        string proveedor,
        string occ,
        string ocl,
        string? dia = null,
        string? bloque = null
    ) =>
        proveedor switch
        {
            "COBELCAR" =>
                $"Retiro en BODEGA FRAY CAMILO 889:\nImportante: Al presentarse en el lugar de retiro, favor identificarse como: \"Retiro JCV\". \nOrden de retiro JCV: OCC {occ}\nDirección: Fray Camilo Henríquez 889, Santiago\nHorario: Lunes a viernes de 09:00 a 17:00 hrs\nOrden de compra: OCL {ocl}",
            "HOFFENS" =>
                $"Retiro en HOFFENS:\nA continuación, detallo los datos necesarios para realizar el retiro:\nImportante: Al presentarse en el lugar de retiro, favor identificarse como: \"Retiro JCV\". \nNota de Venta para retiro Hoffens: \nOrden de compra: OCL {ocl}\nOrden de JCV: OCC {occ}\n\nBodega HOFFENS\nDirección: Camino Lonquen 10707, Maipú\nhttps://maps.app.goo.gl/smWJQSwCrq2vfCcx7\nDia: {dia ?? "_______________"}\nBloque: {bloque ?? "_______________"}\n\nEn caso de no presentarse el día indicado, el retiro queda armado y disponible los proximos 3 dias hábiles en el mismo horario.",
            "SENSUS" =>
                $"Retiro en SENSUS:\nImportante: Al presentarse en el lugar de retiro, favor identificarse como: \"Retiro JCV\".\nDetallo retiro de la OC OCL {ocl}\nOrden JCV: OCC {occ}\n\nBodega SENSUS \nDirección: Camino del cerro 290 Quilicura Bodega E1 INVAC\nhttps://maps.app.goo.gl/Eg8LzVUWXCSym7Yp7\nHorarios:\nLunes a Jueves \n08:00 a 13:00 y 14:00 a 16:00 \nViernes solo AM 08:00 a 13:00\n\nImportante: Indicar nombre y teléfono de quien retira. Es importante estos datos, pues se emite un código QR para el ingreso a las instalaciones. Avisar con anticipación el día para notificar a bodega.",
            "CHILE HDPE" =>
                $"Retiro en CHILE HDPE:\nImportante: Al presentarse en el lugar de retiro, favor identificarse como: \"Retiro JCV\".\nDetallo retiro de la OC OCL {ocl}\nOrden de retiro: OCC {occ}\n\nBodega HDPE\nDirección: Cacique Colín 11950, Lampa\nhttps://maps.app.goo.gl/HwHhbufKS8cBGboG9\nHorario: 08:30 - 17:30",
            _ => $"Proveedor no reconocido: {proveedor}\nOCC {occ}\nOCL {ocl}",
        };
}

public enum OpcionDia
{
    Hoy,
    Ayer,
    Otro,
}

public static class MensajeGuia
{
    private static readonly string[] Comunas =
    [
        "Santiago",
        "Maipú",
        "Quilicura",
        "Lampa",
        "Puente Alto",
        "Las Condes",
        "Providencia",
        "Ñuñoa",
        "La Florida",
        "San Bernardo",
        "Renca",
        "Conchalí",
        "Huechuraba",
        "Vitacura",
        "Lo Barnechea",
        "Colina",
        "Pudahuel",
        "Estación Central",
        "Cerrillos",
        "Peñalolén",
    ];

    public static (string Obra, string Comuna) ExtraerObraYComuna(string despacho)
    {
        string obra = "",
            comuna = "";
        string[] lineas = despacho.Split('\n');
        for (int i = 0; i < lineas.Length; i++)
        {
            string linea = lineas[i].Trim();
            if (
                linea.Contains("Obra:", StringComparison.Ordinal)
                || linea.Contains("obra:", StringComparison.Ordinal)
            )
            {
                obra = linea[(linea.IndexOf(':') + 1)..].Trim();
                break;
            }
            if (
                i == 1
                && linea.Length > 0
                && !linea.ToUpperInvariant().Contains("INFORMACIÓN", StringComparison.Ordinal)
            )
                obra = linea;
        }
        foreach (string original in lineas)
        {
            string linea = original.Trim();
            if (
                linea.Contains("Comuna:", StringComparison.Ordinal)
                || linea.Contains("comuna:", StringComparison.Ordinal)
            )
            {
                comuna = linea[(linea.IndexOf(':') + 1)..].Trim();
                break;
            }
            string? coincidencia = Comunas.FirstOrDefault(c =>
                linea.ToUpperInvariant().Contains(c.ToUpperInvariant(), StringComparison.Ordinal)
            );
            if (coincidencia is not null)
            {
                comuna = coincidencia;
                break;
            }
        }
        return (obra, comuna);
    }

    public static string Generar(string obra, string comuna, OpcionDia dia, string diaManual)
    {
        if (string.IsNullOrEmpty(obra) || obra == "No detectada")
            obra = "_______________";
        string ubicacion = comuna.Length > 0 ? $"{obra}, {comuna}" : obra;
        string diaTexto = dia switch
        {
            OpcionDia.Hoy => "el día de hoy",
            OpcionDia.Ayer => "el día de ayer",
            _ => string.IsNullOrWhiteSpace(diaManual)
                ? "el día de hoy"
                : $"el día {diaManual.Trim()}",
        };
        return $"Buenos días. Para su conocimiento, envío guía emitida {diaTexto} hacia Obra {ubicacion}. De recibir guías adicionales durante la jornada, se las haré llegar oportunamente. Desde Hoffens se comunicarán con usted para coordinar la entrega. Saludos.";
    }
}

public static class CarpetaOcc
{
    public static string NumeroEnNombre(string nombre)
    {
        Match coincidencia = Regex.Match(nombre, @"\d+");
        if (!coincidencia.Success)
            return "";
        string sinCeros = coincidencia.Value.TrimStart('0');
        return sinCeros.Length == 0 ? "0" : sinCeros;
    }

    public static string? UltimoPdf(string carpeta) =>
        ObtenerPdfs(carpeta)
            .OrderByDescending(ruta => File.GetLastWriteTimeUtc(ruta))
            .FirstOrDefault();

    // Nuevo en Mensajero (pedido de Javier): el PDF que entró último a la carpeta. Un archivo
    // copiado conserva su fecha de modificación antigua y solo estrena la de creación, por eso
    // se toma la más reciente de las dos.
    public static string? UltimoAgregado(string carpeta) =>
        ObtenerPdfs(carpeta)
            .OrderByDescending(ruta =>
            {
                DateTime creado = File.GetCreationTimeUtc(ruta);
                DateTime modificado = File.GetLastWriteTimeUtc(ruta);
                return creado > modificado ? creado : modificado;
            })
            .FirstOrDefault();

    public static IReadOnlyList<string> BuscarPorNumero(string carpeta, string numero)
    {
        string numeroLimpio = Regex.IsMatch(numero, @"^\d+$") ? numero.TrimStart('0') : numero;
        if (numeroLimpio.Length == 0 && Regex.IsMatch(numero, @"^0+$"))
            numeroLimpio = "0";
        return ObtenerPdfs(carpeta)
            .Where(ruta => NumeroEnNombre(Path.GetFileName(ruta)) == numeroLimpio)
            .ToList();
    }

    private static IEnumerable<string> ObtenerPdfs(string carpeta)
    {
        if (string.IsNullOrEmpty(carpeta) || !Directory.Exists(carpeta))
            return [];
        return Directory
            .EnumerateFiles(carpeta)
            .Where(ruta =>
                string.Equals(Path.GetExtension(ruta), ".pdf", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();
    }
}

public static class ClientesNvv
{
    public static readonly IReadOnlyList<string> PorDefecto =
    [
        "76290565-5 FYRA RETIRA CLIENTE",
        "84394000-5 INCA RETIRA CLIENTE",
        "77362545-K SAN ISIDRO RETIRA CLIENTE",
        "76427264-1 HYDRO RETIRA CLIENTE",
        "93343000-6 CONSTRUCTORA BIO BIO RETIRA CLIENTE",
        "76838441-K EPAC RETIRA CLIENTE",
        "83534300-6 CONSTRUCTORA BAQUEDANO RETIRA CLIENTE",
    ];

    public static IReadOnlyList<string> Buscar(string texto, IReadOnlyList<string> lista)
    {
        string busqueda = texto.ToUpperInvariant().Trim();
        return busqueda.Length == 0
            ? lista.ToList()
            : lista
                .Where(cliente =>
                    cliente.ToUpperInvariant().Contains(busqueda, StringComparison.Ordinal)
                )
                .ToList();
    }

    public static IReadOnlyList<string> Leer(string ruta)
    {
        try
        {
            if (!File.Exists(ruta))
                return PorDefecto;
            string[] lineas = File.ReadAllLines(ruta, Encoding.UTF8)
                .Select(linea => linea.Trim())
                .Where(linea => linea.Length > 0)
                .ToArray();
            return lineas.Length == 0 ? PorDefecto : lineas;
        }
        catch (IOException)
        {
            return PorDefecto;
        }
        catch (UnauthorizedAccessException)
        {
            return PorDefecto;
        }
    }

    public static bool Guardar(string ruta, IEnumerable<string> lineas)
    {
        try
        {
            string[] contenido = lineas
                .Select(linea => linea.Trim())
                .Where(linea => linea.Length > 0)
                .ToArray();
            File.WriteAllLines(ruta, contenido, new UTF8Encoding(false));
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
