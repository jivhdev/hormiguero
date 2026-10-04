using Archivero.Datos;

namespace Archivero.Tests;

/// <summary>Caso-11, punto 4: atajos de guardado rápido del flujo de "distribuir".</summary>
public class AtajoGuardadoRapidoRepositoryTests : IDisposable
{
    private readonly string _rutaDbTemporal;

    public AtajoGuardadoRapidoRepositoryTests()
    {
        _rutaDbTemporal = Path.Combine(Path.GetTempPath(), $"archivero-tests-{Guid.NewGuid():N}.db");
        BaseDeDatos.RutaArchivo = _rutaDbTemporal;
        BaseDeDatos.AsegurarEsquema();
    }

    [Fact]
    public void ObtenerTodos_LaPrimeraVez_EstaVacio()
    {
        Assert.Empty(new AtajoGuardadoRapidoRepository().ObtenerTodos());
    }

    [Fact]
    public void Guardar_ConservaLaCombinacionCompleta()
    {
        var repo = new AtajoGuardadoRapidoRepository();

        repo.Guardar("Guías firmadas", @"C:\Guias", FormatoCarpeta.AnioMes, @"yyyy\MM",
            [OperacionNombre.DejarSoloNumeros, OperacionNombre.QuitarCerosIzquierda]);

        var atajo = Assert.Single(repo.ObtenerTodos());
        Assert.Equal("Guías firmadas", atajo.Nombre);
        Assert.Equal(@"C:\Guias", atajo.CarpetaMadre);
        Assert.Equal(FormatoCarpeta.AnioMes, atajo.Formato);
        Assert.Equal(@"yyyy\MM", atajo.Patron);
        Assert.Equal([OperacionNombre.DejarSoloNumeros, OperacionNombre.QuitarCerosIzquierda], atajo.ReglaNombre);
    }

    [Fact]
    public void Guardar_DirectoSinPatronNiRegla_SeGuardaIgual()
    {
        var repo = new AtajoGuardadoRapidoRepository();

        repo.Guardar("Recibos", @"C:\Recibos", FormatoCarpeta.Directo, null, []);

        var atajo = Assert.Single(repo.ObtenerTodos());
        Assert.Null(atajo.Patron);
        Assert.Empty(atajo.ReglaNombre);
    }

    [Fact]
    public void Guardar_ConUnNombreQueYaExiste_ReemplazaElAtajoEnVezDeDuplicarlo()
    {
        var repo = new AtajoGuardadoRapidoRepository();

        repo.Guardar("Guías", @"C:\Viejo", FormatoCarpeta.Directo, null, []);
        Assert.True(repo.ExisteNombre("Guías"));
        repo.Guardar("Guías", @"C:\Nuevo", FormatoCarpeta.Anio, "yyyy", [OperacionNombre.Borrar]);

        var atajo = Assert.Single(repo.ObtenerTodos());
        Assert.Equal(@"C:\Nuevo", atajo.CarpetaMadre);
        Assert.Equal(FormatoCarpeta.Anio, atajo.Formato);
    }

    [Fact]
    public void ObtenerTodos_LosDevuelveOrdenadosPorNombre()
    {
        var repo = new AtajoGuardadoRapidoRepository();
        repo.Guardar("Zeta", @"C:\Z", FormatoCarpeta.Directo, null, []);
        repo.Guardar("Alfa", @"C:\A", FormatoCarpeta.Directo, null, []);

        Assert.Equal(["Alfa", "Zeta"], repo.ObtenerTodos().Select(a => a.Nombre));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_rutaDbTemporal);
    }
}
