using Buscadero.Core.Pdf;

namespace Buscadero.Core.Tests;

public sealed class ImpresionDocumentoTests
{
    [Theory]
    [InlineData(OpcionImpresion.PrimeraPagina, 1)]
    [InlineData(OpcionImpresion.PrimerasDosPaginas, 2)]
    [InlineData(OpcionImpresion.DocumentoCompleto, int.MaxValue)]
    public void CuantasPaginas_DevuelveLaCantidadDeLaOpcion(
        OpcionImpresion opcion,
        int esperadas
    ) => Assert.Equal(esperadas, ImpresionDocumento.CuantasPaginas(opcion));
}
