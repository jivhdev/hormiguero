using Hormiguero.Buscadero.Logica;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Buscadero.Tests;

public sealed class PreferenciaImpresionTests : IDisposable
{
    private readonly SqliteConnection conexion;

    public PreferenciaImpresionTests()
    {
        conexion = new SqliteConnection("Data Source=:memory:");
        conexion.Open();
        Hormiguero.Nucleo.Datos.Migraciones.Aplicar(
            conexion,
            Hormiguero.Nucleo.Datos.Migraciones.Todas
        );
    }

    public void Dispose() => conexion.Dispose();

    [Fact]
    public void Por_defecto_imprime_directo()
    {
        var preferencia = new PreferenciaImpresion(conexion);

        Assert.Equal(ModoImpresion.Directa, preferencia.Modo);
        Assert.Null(preferencia.Impresora);
    }

    [Fact]
    public void Guarda_una_impresora_fija()
    {
        new PreferenciaImpresion(conexion).Guardar(ModoImpresion.ImpresoraFija, "Oficina 2");

        var leida = new PreferenciaImpresion(conexion);
        Assert.Equal(ModoImpresion.ImpresoraFija, leida.Modo);
        Assert.Equal("Oficina 2", leida.Impresora);
    }

    [Fact]
    public void Volver_a_directa_ignora_la_impresora_fija()
    {
        var preferencia = new PreferenciaImpresion(conexion);
        preferencia.Guardar(ModoImpresion.ImpresoraFija, "Oficina 2");
        preferencia.Guardar(ModoImpresion.Directa);

        Assert.Equal(ModoImpresion.Directa, preferencia.Modo);
        Assert.Null(preferencia.Impresora);
    }

    [Fact]
    public void Impresora_fija_sin_nombre_no_se_acepta()
    {
        var preferencia = new PreferenciaImpresion(conexion);

        Assert.ThrowsAny<ArgumentException>(() =>
            preferencia.Guardar(ModoImpresion.ImpresoraFija, " ")
        );
    }
}
