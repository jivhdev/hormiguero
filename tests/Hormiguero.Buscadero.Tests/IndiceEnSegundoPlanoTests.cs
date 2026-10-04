using Hormiguero.Buscadero.Logica;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Buscadero.Tests;

public sealed class IndiceEnSegundoPlanoTests : IDisposable
{
    private readonly string raiz = Path.Combine(
        Path.GetTempPath(),
        "HormigueroBuscaderoTests",
        Guid.NewGuid().ToString("N")
    );

    private string RutaBase => Path.Combine(raiz, "hormiguero.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(raiz))
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    private string CarpetaConPdfs(string nombre, params string[] archivos)
    {
        string carpeta = Path.Combine(raiz, nombre);
        Directory.CreateDirectory(carpeta);
        foreach (string archivo in archivos)
        {
            File.WriteAllBytes(Path.Combine(carpeta, archivo), PdfDePrueba.ConTexto());
        }
        return carpeta;
    }

    private void Configurar(params string[] carpetas)
    {
        using SqliteConnection conexion = BaseComun.Abrir(RutaBase);
        var configuradas = new CarpetasConfiguradas(conexion);
        foreach (string carpeta in carpetas)
        {
            configuradas.Agregar(carpeta);
        }
    }

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

    [Fact]
    public async Task Arma_el_indice_de_todas_las_carpetas()
    {
        Configurar(
            CarpetaConPdfs("a", "OCC100.pdf", "OCC101.pdf"),
            CarpetaConPdfs("b", "FCV200.pdf", "FCV201.pdf")
        );

        using var indice = new IndiceEnSegundoPlano(RutaBase, new ControlIndice(1000));
        indice.Revisar();

        Assert.True(
            await Esperar(() =>
                indice.Estado == EstadoIndice.AlDia && indice.DocumentosIndexados == 4
            ),
            $"Estado {indice.Estado}, {indice.DocumentosIndexados} documentos, error {indice.UltimoError}"
        );
    }

    [Fact]
    public async Task Carpeta_que_no_existe_no_detiene()
    {
        string falta = Path.Combine(raiz, "no-existe");
        Configurar(CarpetaConPdfs("a", "OCC100.pdf"), falta);

        using var indice = new IndiceEnSegundoPlano(RutaBase, new ControlIndice(1000));
        indice.Revisar();

        Assert.True(await Esperar(() => indice.Estado == EstadoIndice.AlDia));
        Assert.Equal(1, indice.DocumentosIndexados);
        Assert.Contains(Path.GetFullPath(falta), indice.NoDisponibles);
    }

    [Fact]
    public async Task Retoma_la_carpeta_cuando_vuelve()
    {
        string falta = Path.Combine(raiz, "vuelve");
        Configurar(falta);

        using var indice = new IndiceEnSegundoPlano(
            RutaBase,
            new ControlIndice(1000),
            TimeSpan.FromMilliseconds(200)
        );
        indice.Revisar();
        Assert.True(await Esperar(() => indice.NoDisponibles.Count == 1));

        CarpetaConPdfs("vuelve", "OCC300.pdf");

        Assert.True(
            await Esperar(() => indice.DocumentosIndexados == 1 && indice.NoDisponibles.Count == 0)
        );
    }

    [Fact]
    public async Task Carpeta_caida_no_borra_su_indice()
    {
        string carpeta = CarpetaConPdfs("red", "OCC400.pdf", "OCC401.pdf");
        Configurar(carpeta);

        using (var indice = new IndiceEnSegundoPlano(RutaBase, new ControlIndice(1000)))
        {
            indice.Revisar();
            Assert.True(await Esperar(() => indice.DocumentosIndexados == 2));
        }

        // Simula la red caída: la carpeta deja de verse.
        Directory.Move(carpeta, carpeta + "-oculta");

        using (var indice = new IndiceEnSegundoPlano(RutaBase, new ControlIndice(1000)))
        {
            indice.Revisar();
            Assert.True(await Esperar(() => indice.NoDisponibles.Count == 1));
            Assert.Equal(2, indice.DocumentosIndexados);
        }
    }

    [Fact]
    public void Dispose_detiene_sin_colgarse()
    {
        var indice = new IndiceEnSegundoPlano(RutaBase, new ControlIndice(1000));
        var reloj = System.Diagnostics.Stopwatch.StartNew();

        indice.Dispose();

        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(5));
    }
}
