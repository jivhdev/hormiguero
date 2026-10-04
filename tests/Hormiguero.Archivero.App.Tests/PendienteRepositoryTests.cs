using Archivero.Datos;

namespace Archivero.Tests;

public class PendienteRepositoryTests : IDisposable
{
    private readonly string _rutaDbTemporal;

    public PendienteRepositoryTests()
    {
        _rutaDbTemporal = Path.Combine(Path.GetTempPath(), $"archivero-tests-{Guid.NewGuid():N}.db");
        BaseDeDatos.RutaArchivo = _rutaDbTemporal;
        BaseDeDatos.AsegurarEsquema();
    }

    [Fact]
    public void Agregar_LaPrimeraVez_DevuelveTrueYGuardaElMotivo()
    {
        var repo = new PendienteRepository();

        var esNuevo = repo.Agregar(@"C:\obs\archivo.pdf", MotivoPendiente.NuevoDocumento);

        Assert.True(esNuevo);
        var pendiente = Assert.Single(repo.ObtenerTodos());
        Assert.Equal(MotivoPendiente.NuevoDocumento, pendiente.Motivo);
    }

    [Fact]
    public void Agregar_DeNuevoConElMismoMotivo_DevuelveFalseYNoDuplica()
    {
        var repo = new PendienteRepository();
        repo.Agregar(@"C:\obs\archivo.pdf", MotivoPendiente.NuevoDocumento);

        var esNuevo = repo.Agregar(@"C:\obs\archivo.pdf", MotivoPendiente.NuevoDocumento);

        Assert.False(esNuevo);
        Assert.Single(repo.ObtenerTodos());
    }

    [Fact]
    public void Agregar_ConMotivoDistinto_ActualizaElMotivoSinDuplicar()
    {
        var repo = new PendienteRepository();
        repo.Agregar(@"C:\obs\archivo.pdf", MotivoPendiente.NuevoDocumento);

        var esNuevo = repo.Agregar(@"C:\obs\archivo.pdf", MotivoPendiente.Duplicado);

        Assert.False(esNuevo);
        var pendiente = Assert.Single(repo.ObtenerTodos());
        Assert.Equal(MotivoPendiente.Duplicado, pendiente.Motivo);
    }

    [Fact]
    public void Quitar_DevuelveTrueSoloSiHabiaAlgoQueBorrar()
    {
        var repo = new PendienteRepository();
        repo.Agregar(@"C:\obs\archivo.pdf", MotivoPendiente.NuevoDocumento);

        Assert.True(repo.Quitar(@"C:\obs\archivo.pdf"));
        Assert.False(repo.Quitar(@"C:\obs\archivo.pdf"));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (File.Exists(_rutaDbTemporal))
        {
            File.Delete(_rutaDbTemporal);
        }
    }
}
