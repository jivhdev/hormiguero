using Hormiguero.Mensajero.Core;

namespace Hormiguero.Mensajero.Core.Tests;

// Lo nuevo de Mensajero sobre la carpeta de OCC (lo heredado de Ofisuiza está en
// EquivalenciaOfisuizaTests).
public sealed class CarpetaOccTests : IDisposable
{
    private readonly string carpeta = Path.Combine(
        Path.GetTempPath(),
        "HormigueroCarpetaOcc",
        Guid.NewGuid().ToString("N")
    );

    public CarpetaOccTests() => Directory.CreateDirectory(carpeta);

    public void Dispose() => Directory.Delete(carpeta, recursive: true);

    private string Pdf(string nombre, DateTime creado, DateTime modificado)
    {
        string ruta = Path.Combine(carpeta, nombre);
        File.WriteAllText(ruta, "%PDF-1.4");
        File.SetCreationTimeUtc(ruta, creado);
        File.SetLastWriteTimeUtc(ruta, modificado);
        return ruta;
    }

    [Fact]
    public void Ultimo_agregado_reconoce_un_pdf_copiado_con_fecha_antigua()
    {
        var hoy = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        Pdf("OCC100.pdf", hoy.AddHours(-2), hoy.AddHours(-2));
        // Copiado recién: se creó ahora, pero conserva la modificación de hace un mes.
        string copiado = Pdf("OCC101.pdf", hoy, hoy.AddDays(-30));

        Assert.Equal(copiado, CarpetaOcc.UltimoAgregado(carpeta));
    }

    [Fact]
    public void Ultimo_agregado_reconoce_un_pdf_modificado_despues()
    {
        var hoy = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        string editado = Pdf("OCC100.pdf", hoy.AddDays(-5), hoy);
        Pdf("OCC101.pdf", hoy.AddHours(-1), hoy.AddHours(-1));

        Assert.Equal(editado, CarpetaOcc.UltimoAgregado(carpeta));
    }

    [Fact]
    public void Ultimo_agregado_sin_pdfs_es_nulo()
    {
        File.WriteAllText(Path.Combine(carpeta, "nota.txt"), "x");

        Assert.Null(CarpetaOcc.UltimoAgregado(carpeta));
        Assert.Null(CarpetaOcc.UltimoAgregado(Path.Combine(carpeta, "no-existe")));
    }
}
