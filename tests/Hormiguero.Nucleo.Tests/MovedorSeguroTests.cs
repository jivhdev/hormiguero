using Hormiguero.Nucleo.Utilidades;

namespace Hormiguero.Nucleo.Tests;

public sealed class MovedorSeguroTests : IDisposable
{
    private readonly string raiz = Path.Combine(
        Path.GetTempPath(),
        "HormigueroMovedorTests",
        Guid.NewGuid().ToString("N")
    );

    public MovedorSeguroTests() => Directory.CreateDirectory(raiz);

    public void Dispose() => Directory.Delete(raiz, recursive: true);

    private string Archivo(string nombre, string contenido)
    {
        string ruta = Path.Combine(raiz, nombre);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta, contenido);
        return ruta;
    }

    [Fact]
    public void Mueve_creando_las_carpetas_y_borra_el_original()
    {
        string origen = Archivo("entrada.pdf", "documento");
        string destino = Path.Combine(raiz, "2026", "202610", "OCC1.pdf");

        Traslado traslado = MovedorSeguro.Mover(origen, destino);

        Assert.Equal(ResultadoTraslado.Movido, traslado.Resultado);
        Assert.Equal(Huella.Calcular(destino), traslado.Huella);
        Assert.False(File.Exists(origen));
        Assert.Equal("documento", File.ReadAllText(destino));
        Assert.Empty(Directory.GetFiles(raiz, "*.hormiguero-tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Destino_igual_no_se_toca_y_el_original_queda()
    {
        string origen = Archivo("entrada.pdf", "documento");
        string destino = Archivo(Path.Combine("d", "OCC1.pdf"), "documento");

        Traslado traslado = MovedorSeguro.Mover(origen, destino);

        Assert.Equal(ResultadoTraslado.YaEstabaIgual, traslado.Resultado);
        Assert.True(File.Exists(origen));
    }

    [Fact]
    public void Destino_con_otro_contenido_nunca_se_sobrescribe()
    {
        string origen = Archivo("entrada.pdf", "nuevo");
        string destino = Archivo(Path.Combine("d", "OCC1.pdf"), "anterior");

        Traslado traslado = MovedorSeguro.Mover(origen, destino);

        Assert.Equal(ResultadoTraslado.DestinoConOtroContenido, traslado.Resultado);
        Assert.Equal("anterior", File.ReadAllText(destino));
        Assert.Equal("nuevo", File.ReadAllText(origen));
    }

    [Fact]
    public void Origen_que_se_esta_escribiendo_no_se_mueve()
    {
        string origen = Archivo("entrada.pdf", "a medias");
        string destino = Path.Combine(raiz, "d", "OCC1.pdf");

        Traslado traslado;
        using (new FileStream(origen, FileMode.Open, FileAccess.Write, FileShare.Read))
        {
            traslado = MovedorSeguro.Mover(origen, destino);
        }

        Assert.Equal(ResultadoTraslado.OrigenEnUso, traslado.Resultado);
        Assert.True(File.Exists(origen));
        Assert.False(File.Exists(destino));
    }

    [Fact]
    public void Muchos_traslados_seguidos_no_pierden_nada()
    {
        for (int i = 0; i < 200; i++)
        {
            string origen = Archivo($"e{i}.pdf", $"documento {i}");
            Traslado traslado = MovedorSeguro.Mover(
                origen,
                Path.Combine(raiz, "d", $"{i % 7}", $"D{i}.pdf")
            );
            Assert.Equal(ResultadoTraslado.Movido, traslado.Resultado);
        }

        string[] guardados = Directory.GetFiles(
            Path.Combine(raiz, "d"),
            "*.pdf",
            SearchOption.AllDirectories
        );
        Assert.Equal(200, guardados.Length);
        Assert.Empty(Directory.GetFiles(raiz, "e*.pdf"));
    }

    [Fact]
    public void Papelera_de_un_archivo_que_no_existe_lanza_error()
    {
        Assert.Throws<FileNotFoundException>(() =>
            MovedorSeguro.APapelera(Path.Combine(raiz, "no-existe.pdf"))
        );
    }
}
