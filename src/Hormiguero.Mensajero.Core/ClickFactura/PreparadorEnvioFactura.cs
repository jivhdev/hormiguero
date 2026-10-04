using System.Globalization;

namespace Hormiguero.Mensajero.Core.ClickFactura;

public sealed class ErrorPreparacionEnvio(string mensaje, Exception? inner = null)
    : Exception(mensaje, inner);

public static class PreparadorEnvioFactura
{
    public static string CrearCarpetaTemporal(string baseTemporal, string rut, DateTime ahoraLocal)
    {
        string timestamp = ahoraLocal.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string nombre =
            $"{rut.Replace(".", "", StringComparison.Ordinal).Replace("-", "_", StringComparison.Ordinal)}_{timestamp}";
        string destino = Path.Combine(baseTemporal, nombre);
        try
        {
            Directory.CreateDirectory(destino);
            return destino;
        }
        catch (Exception error)
        {
            throw new ErrorPreparacionEnvio(
                $"No se pudo crear la carpeta temporal: {error.Message}",
                error
            );
        }
    }

    public static IReadOnlyList<string> CopiarPdfs(
        IReadOnlyList<string> rutasPdf,
        string carpetaDestino
    )
    {
        if (rutasPdf.Count == 0)
            throw new ErrorPreparacionEnvio("No hay archivos para copiar.");

        var copiados = new List<string>();
        int cantidadErrores = 0;
        foreach (string ruta in rutasPdf)
        {
            if (!File.Exists(ruta))
            {
                cantidadErrores++;
                continue;
            }

            string destino = Path.Combine(carpetaDestino, Path.GetFileName(ruta));
            try
            {
                File.Copy(ruta, destino, overwrite: true);
                File.SetLastWriteTimeUtc(destino, File.GetLastWriteTimeUtc(ruta));
                copiados.Add(destino);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                cantidadErrores++;
            }
        }

        if (cantidadErrores > 0)
            throw new ErrorPreparacionEnvio(
                $"Se encontraron {cantidadErrores} errores al copiar archivos."
            );
        return copiados;
    }

    public static void LimpiarCarpetaTemporal(string carpeta)
    {
        try
        {
            if (Directory.Exists(carpeta))
                Directory.Delete(carpeta, recursive: true);
        }
        catch (IOException)
        {
            // La rutina original registra el fallo y permite continuar con el siguiente cliente.
        }
        catch (UnauthorizedAccessException)
        {
            // La rutina original registra el fallo y permite continuar con el siguiente cliente.
        }
    }
}
