using System.Text;
using Hormiguero.Nucleo.Datos;

namespace Hormiguero.Mensajero.Core;

public static class MensajeroLog
{
    public const long TamanioMaximoBytes = 1024 * 1024;
    public const int MaximoArchivosRotados = 3;

    private static readonly object Candado = new();

    public static string RutaLog { get; set; } = Path.Combine(DatosDeApp.Carpeta, "mensajero.log");

    public static void Registrar(string evento, string detalle)
    {
        try
        {
            lock (Candado)
            {
                string ruta = RutaLog;
                Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
                RotarSiHaceFalta(ruta);
                string linea =
                    $"[{DateTime.Now:yyyy-MM-ddTHH:mm:ss}] {Sanear(evento)} — {Sanear(detalle)}{Environment.NewLine}";
                File.AppendAllText(ruta, linea, Encoding.UTF8);
            }
        }
        catch
        {
            // Un problema al escribir el registro no debe interrumpir Mensajero.
        }
    }

    public static void RegistrarError(string operacion, Exception excepcion) =>
        Registrar("ERROR", $"{operacion} ({excepcion.GetType().Name})");

    public static string Sanear(string texto)
    {
        string sinCrLf = texto.Replace("\r\n", " ", StringComparison.Ordinal);
        var resultado = new StringBuilder(sinCrLf.Length);
        foreach (char caracter in sinCrLf)
            resultado.Append(
                caracter is '\n' or '\r' || caracter <= 31 || caracter == 127 ? ' ' : caracter
            );
        return resultado.ToString();
    }

    private static void RotarSiHaceFalta(string ruta)
    {
        if (!File.Exists(ruta) || new FileInfo(ruta).Length < TamanioMaximoBytes)
            return;

        string masViejo = $"{ruta}.{MaximoArchivosRotados}";
        if (File.Exists(masViejo))
            File.Delete(masViejo);
        for (int indice = MaximoArchivosRotados - 1; indice >= 1; indice--)
        {
            string origen = $"{ruta}.{indice}";
            if (File.Exists(origen))
                MoverConVerificacion(origen, $"{ruta}.{indice + 1}");
        }
        MoverConVerificacion(ruta, $"{ruta}.1");
    }

    private static void MoverConVerificacion(string origen, string destino)
    {
        File.Copy(origen, destino, overwrite: true);
        if (!File.ReadAllBytes(origen).AsSpan().SequenceEqual(File.ReadAllBytes(destino)))
            throw new IOException("No se pudo verificar la copia del registro.");
        File.Delete(origen);
    }
}
