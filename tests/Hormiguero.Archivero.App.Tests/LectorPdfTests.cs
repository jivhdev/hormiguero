using Archivero.Servicios.Pdf;

namespace Archivero.Tests;

public class LectorPdfTests : IDisposable
{
    private readonly string _carpetaTemporal;

    public LectorPdfTests()
    {
        _carpetaTemporal = Path.Combine(Path.GetTempPath(), "archivero-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpetaTemporal);
    }

    [Fact]
    public void ExtraerTexto_ConRectanguloSobreTodaLaPagina_DevuelveAmbasLineas()
    {
        var ruta = CreadorPdfDePrueba.Crear(_carpetaTemporal, "EMISOR DE PRUEBA", "TIPO DE PRUEBA");

        var texto = LectorPdf.ExtraerTexto(ruta, 0, new RectanguloFraccion(0, 0, 1, 1));

        Assert.Contains("EMISORDEPRUEBA", texto.Replace(" ", string.Empty));
        Assert.Contains("TIPODEPRUEBA", texto.Replace(" ", string.Empty));
    }

    [Fact]
    public void ExtraerTexto_ConRectanguloSoloSobreElBordeSuperior_NoIncluyeLaLineaInferior()
    {
        var ruta = CreadorPdfDePrueba.Crear(_carpetaTemporal, "EMISOR DE PRUEBA", "TIPO DE PRUEBA");

        // La primera linea se escribio cerca del borde superior de la pagina (y=750 de 842 puntos);
        // la segunda cerca del borde inferior (y=100). Un rectangulo sobre el 30% superior de la
        // imagen deberia capturar solo la primera.
        var texto = LectorPdf.ExtraerTexto(ruta, 0, new RectanguloFraccion(0, 0, 1, 0.3));

        Assert.Contains("EMISOR", texto);
        Assert.DoesNotContain("TIPO", texto);
    }

    [Fact]
    public void ExtraerTexto_ConRectanguloSoloSobreElBordeInferior_NoIncluyeLaLineaSuperior()
    {
        var ruta = CreadorPdfDePrueba.Crear(_carpetaTemporal, "EMISOR DE PRUEBA", "TIPO DE PRUEBA");

        var texto = LectorPdf.ExtraerTexto(ruta, 0, new RectanguloFraccion(0, 0.8, 1, 0.2));

        Assert.Contains("TIPO", texto);
        Assert.DoesNotContain("EMISOR", texto);
    }

    [Fact]
    public void ExtraerTexto_ConRectanguloFueraDeCualquierTexto_DevuelveVacio()
    {
        var ruta = CreadorPdfDePrueba.Crear(_carpetaTemporal, "EMISOR DE PRUEBA", "TIPO DE PRUEBA");

        var texto = LectorPdf.ExtraerTexto(ruta, 0, new RectanguloFraccion(0, 0.45, 1, 0.1));

        Assert.Equal(string.Empty, texto);
    }

    [Fact]
    public void TieneTextoExtraible_ConPdfDeTexto_DevuelveTrue()
    {
        var ruta = CreadorPdfDePrueba.Crear(_carpetaTemporal, "EMISOR DE PRUEBA", "TIPO DE PRUEBA");

        Assert.True(LectorPdf.TieneTextoExtraible(ruta));
    }

    public void Dispose()
    {
        Directory.Delete(_carpetaTemporal, recursive: true);
    }
}
