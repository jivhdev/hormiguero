namespace Buscadero.Core.Pdf;

public enum OpcionImpresion
{
    PrimeraPagina,
    PrimerasDosPaginas,
    DocumentoCompleto,
}

public static class ImpresionDocumento
{
    public static int CuantasPaginas(OpcionImpresion opcion) =>
        opcion switch
        {
            OpcionImpresion.PrimeraPagina => 1,
            OpcionImpresion.PrimerasDosPaginas => 2,
            OpcionImpresion.DocumentoCompleto => int.MaxValue,
            _ => throw new ArgumentOutOfRangeException(nameof(opcion)),
        };

    public static async Task ImprimirAsync(
        string ruta,
        OpcionImpresion opcion,
        Action<byte[], int, string?> imprimir
    )
    {
        ArgumentNullException.ThrowIfNull(imprimir);
        await Task.Run(() =>
        {
            using var archivo = new FileStream(
                ruta,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete
            );
            var bytes = new byte[checked((int)archivo.Length)];
            archivo.ReadExactly(bytes);
            imprimir(bytes, CuantasPaginas(opcion), null);
        });
    }
}
