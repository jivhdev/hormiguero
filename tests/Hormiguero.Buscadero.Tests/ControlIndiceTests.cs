using System.Diagnostics;
using Hormiguero.Buscadero.Logica;
using Hormiguero.Nucleo.Datos;

namespace Hormiguero.Buscadero.Tests;

public class ControlIndiceTests
{
    [Fact]
    public void Respeta_el_limite_de_velocidad()
    {
        var control = new ControlIndice(5);
        var reloj = Stopwatch.StartNew();

        for (int i = 0; i < 10; i++)
        {
            control.EsperarTurno(CancellationToken.None);
        }

        Assert.True(reloj.ElapsedMilliseconds >= 1750, $"Tardó {reloj.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task La_pausa_detiene_hasta_reanudar()
    {
        var control = new ControlIndice(1000);
        control.Pausar();

        var tarea = Task.Run(() => control.EsperarTurno(CancellationToken.None));

        Assert.False(await Termina(tarea, 300));
        control.Reanudar();
        Assert.True(await Termina(tarea, 2000));
    }

    [Fact]
    public async Task Cancelar_en_pausa_no_espera()
    {
        var control = new ControlIndice(1000);
        control.Pausar();
        using var cancelar = new CancellationTokenSource();

        var tarea = Task.Run(() => control.EsperarTurno(cancelar.Token));
        Assert.False(await Termina(tarea, 200));
        await cancelar.CancelAsync();

        Assert.True(await Termina(tarea, 2000));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tarea);
    }

    [Fact]
    public void Cuenta_archivos_y_carpetas()
    {
        string raiz = Path.Combine(Path.GetTempPath(), "hormiguero-control-" + Guid.NewGuid());
        string baseDatos = Path.Combine(raiz, "base.db");
        string documentos = Path.Combine(raiz, "docs");
        Directory.CreateDirectory(Path.Combine(documentos, "2025"));
        Directory.CreateDirectory(Path.Combine(documentos, "2026"));
        File.WriteAllBytes(Path.Combine(documentos, "OCC100.pdf"), PdfDePrueba.ConTexto());
        File.WriteAllBytes(Path.Combine(documentos, "2025", "OCC200.pdf"), PdfDePrueba.ConTexto());
        File.WriteAllBytes(Path.Combine(documentos, "2026", "OCC300.pdf"), PdfDePrueba.ConTexto());

        try
        {
            var control = new ControlIndice(1000);
            int avisos = 0;
            control.Cambio += () => Interlocked.Increment(ref avisos);

            using (var conexion = BaseComun.Abrir(baseDatos))
            {
                new Indexador(conexion).Revisar(documentos, CancellationToken.None, control);
            }

            Assert.Equal(3, control.ArchivosRevisados);
            Assert.Equal(3, control.CarpetasRevisadas);
            Assert.Equal(6, avisos);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(raiz, recursive: true);
        }
    }

    private static async Task<bool> Termina(Task tarea, int milisegundos) =>
        await Task.WhenAny(tarea, Task.Delay(milisegundos)) == tarea;
}
