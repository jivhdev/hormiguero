using System.Collections.Concurrent;
using Hormiguero.Archivero.Logica;

namespace Hormiguero.Archivero.Tests;

public sealed class VigilanteEntradaTests : IDisposable
{
    private readonly string raiz = Path.Combine(
        Path.GetTempPath(),
        "HormigueroVigilanteTests",
        Guid.NewGuid().ToString("N")
    );

    public VigilanteEntradaTests() => Directory.CreateDirectory(raiz);

    public void Dispose() => Directory.Delete(raiz, recursive: true);

    private static async Task<bool> Esperar(Func<bool> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < limite)
        {
            if (condicion())
            {
                return true;
            }
            await Task.Delay(20);
        }
        return condicion();
    }

    private static string Nombre(string ruta) => Path.GetFileName(ruta);

    [Fact]
    public async Task Al_iniciar_avisa_lo_que_ya_estaba()
    {
        File.WriteAllText(Path.Combine(raiz, "antes.pdf"), "x");
        var llegados = new ConcurrentBag<string>();
        using var vigilante = new VigilanteEntrada([raiz]);
        vigilante.Llego += ruta => llegados.Add(Nombre(ruta));

        vigilante.Iniciar();

        Assert.True(await Esperar(() => llegados.Contains("antes.pdf")));
        Assert.True(await Esperar(() => vigilante.Estados[raiz] == EstadoCarpeta.Vigilando));
    }

    [Fact]
    public async Task Avisa_un_pdf_nuevo_e_ignora_lo_demas()
    {
        var llegados = new ConcurrentBag<string>();
        using var vigilante = new VigilanteEntrada([raiz]);
        vigilante.Llego += ruta => llegados.Add(Nombre(ruta));
        vigilante.Iniciar();
        Assert.True(await Esperar(() => vigilante.Estados[raiz] == EstadoCarpeta.Vigilando));

        File.WriteAllText(Path.Combine(raiz, "nota.txt"), "x");
        File.WriteAllText(Path.Combine(raiz, "nuevo.PDF"), "x");

        Assert.True(await Esperar(() => llegados.Contains("nuevo.PDF")));
        Assert.DoesNotContain("nota.txt", llegados);
    }

    [Fact]
    public async Task Carpeta_que_no_existe_se_retoma_cuando_aparece()
    {
        string falta = Path.Combine(raiz, "red");
        var llegados = new ConcurrentBag<string>();
        using var vigilante = new VigilanteEntrada([falta], TimeSpan.FromMilliseconds(100));
        vigilante.Llego += ruta => llegados.Add(Nombre(ruta));
        vigilante.Iniciar();
        Assert.True(await Esperar(() => vigilante.Estados[falta] == EstadoCarpeta.NoDisponible));

        Directory.CreateDirectory(falta);
        File.WriteAllText(Path.Combine(falta, "llego.pdf"), "x");

        Assert.True(await Esperar(() => vigilante.Estados[falta] == EstadoCarpeta.Vigilando));
        Assert.True(await Esperar(() => llegados.Contains("llego.pdf")));
    }

    [Fact]
    public async Task Revisar_ahora_vuelve_a_avisar_todo()
    {
        File.WriteAllText(Path.Combine(raiz, "uno.pdf"), "x");
        int avisos = 0;
        using var vigilante = new VigilanteEntrada([raiz]);
        vigilante.Llego += _ => Interlocked.Increment(ref avisos);
        vigilante.Iniciar();
        Assert.True(await Esperar(() => Volatile.Read(ref avisos) >= 1));
        int antes = Volatile.Read(ref avisos);

        vigilante.RevisarAhora();

        Assert.True(await Esperar(() => Volatile.Read(ref avisos) > antes));
    }

    [Fact]
    public async Task Despues_de_cerrar_no_avisa_mas()
    {
        int avisos = 0;
        var vigilante = new VigilanteEntrada([raiz]);
        vigilante.Llego += _ => Interlocked.Increment(ref avisos);
        vigilante.Iniciar();
        Assert.True(await Esperar(() => vigilante.Estados[raiz] == EstadoCarpeta.Vigilando));

        vigilante.Dispose();
        File.WriteAllText(Path.Combine(raiz, "tarde.pdf"), "x");
        await Task.Delay(500);

        Assert.Equal(0, Volatile.Read(ref avisos));
    }
}
