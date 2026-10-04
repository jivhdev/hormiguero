using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Tests;

public sealed class DocumentosGuardadosTests : IDisposable
{
    private readonly SqliteConnection conexion;

    public DocumentosGuardadosTests()
    {
        conexion = new SqliteConnection("Data Source=:memory:");
        conexion.Open();
        Migraciones.Aplicar(conexion, Migraciones.Todas);
    }

    public void Dispose() => conexion.Dispose();

    [Fact]
    public void Devuelve_solo_lo_nuevo_en_orden()
    {
        var guardados = new DocumentosGuardados(conexion);
        long primero = guardados.Registrar(@"C:\Docs\2026\OCC1.pdf", "Archivero");
        guardados.Registrar(@"C:\Docs\2026\OCC2.pdf", "Archivero");
        guardados.Registrar(@"C:\Docs\2026\OCC3.pdf", "Archivero");

        var nuevos = guardados.Despues(primero);

        Assert.Equal(
            [@"C:\Docs\2026\OCC2.pdf", @"C:\Docs\2026\OCC3.pdf"],
            nuevos.Select(g => g.Ruta)
        );
        Assert.All(nuevos, g => Assert.Equal("Archivero", g.App));
    }

    [Fact]
    public void Sin_novedades_devuelve_vacio()
    {
        var guardados = new DocumentosGuardados(conexion);
        long ultimo = guardados.Registrar(@"C:\Docs\OCC1.pdf", "Archivero");

        Assert.Empty(guardados.Despues(ultimo));
    }

    [Fact]
    public void Respeta_el_maximo_pedido()
    {
        var guardados = new DocumentosGuardados(conexion);
        for (int i = 0; i < 5; i++)
        {
            guardados.Registrar($@"C:\Docs\OCC{i}.pdf", "Archivero");
        }

        Assert.Equal(2, guardados.Despues(0, maximo: 2).Count);
    }
}
