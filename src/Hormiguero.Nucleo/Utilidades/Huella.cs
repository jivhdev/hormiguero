using System.Security.Cryptography;

namespace Hormiguero.Nucleo.Utilidades;

public static class Huella
{
    public static string Calcular(string ruta)
    {
        using var flujo = new FileStream(
            ruta,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan
        );

        return Convert.ToHexString(SHA256.HashData(flujo)).ToLowerInvariant();
    }

    public static string DeContenido(ReadOnlySpan<byte> contenido) =>
        Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();
}
