using Buscadero.Core.Pdf;

namespace Buscadero.Core.Tests;

public sealed class RenderizadorPdfTests
{
    private const string PdfValidoBase64 =
        "JVBERi0xLjQKMSAwIG9iago8PCAvVHlwZSAvQ2F0YWxvZyAvUGFnZXMgMiAwIFIgPj4KZW5kb2JqCjIgMCBvYmoKPDwgL1R5cGUgL1BhZ2VzIC9LaWRzIFszIDAgUl0gL0NvdW50IDEgPj4KZW5kb2JqCjMgMCBvYmoKPDwgL1R5cGUgL1BhZ2UgL1BhcmVudCAyIDAgUiAvTWVkaWFCb3ggWzAgMCA2MTIgNzkyXSAvQ29udGVudHMgNCAwIFIgL1Jlc291cmNlcyA8PCAvRm9udCA8PCAvRjEgNSAwIFIgPj4gPj4gPj4KZW5kb2JqCjQgMCBvYmoKPDwgL0xlbmd0aCA0NSA+PgpzdHJlYW0KQlQgL0YxIDI0IFRmIDcyIDcyMCBUZCAoSG9sYSBCdXNjYWRlcm8pIFRqIEVUCmVuZHN0cmVhbQplbmRvYmoKNSAwIG9iago8PCAvVHlwZSAvRm9udCAvU3VidHlwZSAvVHlwZTEgL0Jhc2VGb250IC9IZWx2ZXRpY2EgPj4KZW5kb2JqCnhyZWYKMCA2CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAwOSAwMDAwMCBuIAowMDAwMDAwMDU4IDAwMDAwIG4gCjAwMDAwMDAxMTUgMDAwMDAgbiAKMDAwMDAwMDI0MSAwMDAwMCBuIAowMDAwMDAwMzM2IDAwMDAwIG4gCnRyYWlsZXIKPDwgL1NpemUgNiAvUm9vdCAxIDAgUiA+PgpzdGFydHhyZWYKNDA2CiUlRU9GCg==";

    [Fact]
    public void ObtenerTotalPaginas_PdfValido_DevuelveUnaPagina()
    {
        var ruta = EscribirPdfValido();
        try
        {
            Assert.Equal(1, RenderizadorPdf.ObtenerTotalPaginas(ruta));
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public void RenderizarPagina_PdfValido_DevuelveImagen()
    {
        var ruta = EscribirPdfValido();
        try
        {
            var pagina = RenderizadorPdf.RenderizarPagina(ruta, 0);

            Assert.NotEmpty(pagina.Png);
            Assert.True(pagina.Ancho > 0);
            Assert.True(pagina.Alto > 0);
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public void ObtenerTotalPaginas_ArchivoIlegible_Lanza()
    {
        var ruta = EscribirArchivoIlegible();
        try
        {
            Assert.ThrowsAny<Exception>(() => RenderizadorPdf.ObtenerTotalPaginas(ruta));
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public void RenderizarPagina_ArchivoIlegible_Lanza()
    {
        var ruta = EscribirArchivoIlegible();
        try
        {
            Assert.ThrowsAny<Exception>(() => RenderizadorPdf.RenderizarPagina(ruta, 0));
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    private static string EscribirPdfValido()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"buscadero-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(ruta, Convert.FromBase64String(PdfValidoBase64));
        return ruta;
    }

    private static string EscribirArchivoIlegible()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"buscadero-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(ruta, "esto no es un PDF");
        return ruta;
    }
}
