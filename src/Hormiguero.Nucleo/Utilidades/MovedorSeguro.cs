using System.Runtime.InteropServices;

namespace Hormiguero.Nucleo.Utilidades;

public enum ResultadoTraslado
{
    Movido,
    YaEstabaIgual,
    DestinoConOtroContenido,
    OrigenEnUso,
    Fallo,
}

public record Traslado(ResultadoTraslado Resultado, string? Huella, string? Detalle);

// Mover = copiar, verificar la huella y recién entonces borrar (Archivero REQ-004).
// Nunca sobrescribe ni borra el original si algo no salió perfecto.
public static class MovedorSeguro
{
    private const string Temporal = ".hormiguero-tmp";

    public static Traslado Mover(string origen, string destino)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(origen);
        ArgumentException.ThrowIfNullOrWhiteSpace(destino);

        string huella;
        try
        {
            // Sin compartir la escritura: si otro programa todavía lo está
            // copiando o escribiendo, falla aquí y se reintenta después.
            using var flujo = new FileStream(
                origen,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );
            huella = Huella.DeFlujo(flujo);
        }
        catch (IOException error)
        {
            return new Traslado(ResultadoTraslado.OrigenEnUso, null, error.Message);
        }

        string temporal = destino + Temporal;
        try
        {
            if (File.Exists(destino))
            {
                return DestinoOcupado(destino, huella);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destino))!);
            File.Copy(origen, temporal, overwrite: true);

            if (Huella.Calcular(temporal) != huella)
            {
                File.Delete(temporal);
                return new Traslado(
                    ResultadoTraslado.Fallo,
                    huella,
                    "La copia no quedó igual al original."
                );
            }

            try
            {
                File.Move(temporal, destino, overwrite: false);
            }
            catch (IOException) when (File.Exists(destino))
            {
                // Otro proceso dejó un archivo con ese nombre mientras se copiaba.
                File.Delete(temporal);
                return DestinoOcupado(destino, huella);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            BorrarSinError(temporal);
            return new Traslado(ResultadoTraslado.Fallo, huella, error.Message);
        }

        try
        {
            File.Delete(origen);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // La copia ya está verificada en su destino; el original queda y la
            // próxima revisión lo verá como "ya estaba igual".
            return new Traslado(
                ResultadoTraslado.Movido,
                huella,
                "No se pudo borrar el original: " + error.Message
            );
        }

        return new Traslado(ResultadoTraslado.Movido, huella, null);
    }

    // A la Papelera de reciclaje de Windows: se puede recuperar (D-62).
    public static void APapelera(string ruta)
    {
        string completa = Path.GetFullPath(ruta);
        if (!File.Exists(completa))
        {
            throw new FileNotFoundException("No existe el archivo.", completa);
        }

        var operacion = new OperacionArchivo
        {
            Funcion = FoDelete,
            Desde = completa + "\0\0",
            Opciones = FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi,
        };
        int codigo = SHFileOperation(ref operacion);
        if (codigo != 0 || operacion.Cancelada || File.Exists(completa))
        {
            throw new IOException($"No se pudo enviar a la Papelera (código {codigo}).");
        }
    }

    private static Traslado DestinoOcupado(string destino, string huella) =>
        Huella.Calcular(destino) == huella
            ? new Traslado(ResultadoTraslado.YaEstabaIgual, huella, null)
            : new Traslado(ResultadoTraslado.DestinoConOtroContenido, huella, null);

    private static void BorrarSinError(string ruta)
    {
        try
        {
            File.Delete(ruta);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    private const uint FoDelete = 3;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OperacionArchivo
    {
        public IntPtr Ventana;
        public uint Funcion;
        public string Desde;
        public string? Hacia;
        public ushort Opciones;

        [MarshalAs(UnmanagedType.Bool)]
        public bool Cancelada;
        public IntPtr Nombres;
        public string? Titulo;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref OperacionArchivo operacion);
}
