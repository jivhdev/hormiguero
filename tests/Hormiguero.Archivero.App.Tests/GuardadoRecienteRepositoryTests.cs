using Archivero.Datos;

namespace Archivero.Tests;

public class GuardadoRecienteRepositoryTests : IDisposable
{
    private readonly string _rutaDbTemporal;

    public GuardadoRecienteRepositoryTests()
    {
        _rutaDbTemporal = Path.Combine(Path.GetTempPath(), $"archivero-tests-{Guid.NewGuid():N}.db");
        BaseDeDatos.RutaArchivo = _rutaDbTemporal;
        BaseDeDatos.AsegurarEsquema();
    }

    [Fact]
    public void ObtenerTodos_SinNadaGuardado_DevuelveListaVacia()
    {
        var repo = new GuardadoRecienteRepository();

        Assert.Empty(repo.ObtenerTodos());
    }

    [Fact]
    public void Agregar_DevuelveLosMasRecientesPrimero()
    {
        var repo = new GuardadoRecienteRepository();

        repo.Agregar(@"C:\Destino\uno.pdf");
        repo.Agregar(@"C:\Destino\dos.pdf");
        repo.Agregar(@"C:\Destino\tres.pdf");

        var resultado = repo.ObtenerTodos();

        Assert.Equal(3, resultado.Count);
        Assert.Equal(@"C:\Destino\tres.pdf", resultado[0].RutaFinal);
        Assert.Equal(@"C:\Destino\dos.pdf", resultado[1].RutaFinal);
        Assert.Equal(@"C:\Destino\uno.pdf", resultado[2].RutaFinal);
    }

    [Fact]
    public void Agregar_ConMasDeElMaximo_RecortaLosMasViejos()
    {
        // Caso-6, punto 2: Javier pidio recordar al menos los ultimos 10, con un limite
        // razonable para no crecer sin control -- se usa 20 como tope.
        var repo = new GuardadoRecienteRepository();

        for (var i = 1; i <= 25; i++)
        {
            repo.Agregar($@"C:\Destino\{i}.pdf");
        }

        var resultado = repo.ObtenerTodos();

        Assert.Equal(20, resultado.Count);
        Assert.Equal(@"C:\Destino\25.pdf", resultado[0].RutaFinal);
        Assert.Equal(@"C:\Destino\6.pdf", resultado[^1].RutaFinal);
        Assert.DoesNotContain(resultado, g => g.RutaFinal == @"C:\Destino\5.pdf");
    }

    [Fact]
    public void Agregar_SobreviveARecrearElRepositorio_QuedaPersistidoEnLaBase()
    {
        // Simula el escenario real de Caso-6: Archivero se cierra de verdad y se vuelve a abrir
        // (MainWindow y su repositorio se recrean); el historial no debe perderse.
        new GuardadoRecienteRepository().Agregar(@"C:\Destino\antes-de-cerrar.pdf");

        var repoNuevo = new GuardadoRecienteRepository();
        var resultado = repoNuevo.ObtenerTodos();

        Assert.Single(resultado);
        Assert.Equal(@"C:\Destino\antes-de-cerrar.pdf", resultado[0].RutaFinal);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_rutaDbTemporal);
    }
}
