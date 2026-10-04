using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Tests;

public sealed class IdentificacionesYAuditoriaTests : IDisposable
{
    private readonly SqliteConnection conexion;

    public IdentificacionesYAuditoriaTests()
    {
        conexion = new SqliteConnection("Data Source=:memory:");
        conexion.Open();
        Migraciones.Aplicar(conexion, Migraciones.Todas);
    }

    public void Dispose() => conexion.Dispose();

    [Fact]
    public void Guarda_y_lista_una_identificacion()
    {
        var identificaciones = new Identificaciones(conexion);

        long id = identificaciones.Guardar(new Identificacion(0, "Factura", "Proveedor A", "{}"));

        var guardada = Assert.Single(identificaciones.Listar());
        Assert.Equal(new Identificacion(id, "Factura", "Proveedor A", "{}"), guardada);
    }

    [Fact]
    public void Editar_reemplaza_la_misma_fila()
    {
        var identificaciones = new Identificaciones(conexion);
        long id = identificaciones.Guardar(new Identificacion(0, "Factura", "Proveedor A", "{}"));

        identificaciones.Guardar(new Identificacion(id, "Factura", "Proveedor A", "{\"v\":2}"));

        Assert.Equal("{\"v\":2}", Assert.Single(identificaciones.Listar()).Datos);
    }

    [Fact]
    public void Tipo_y_emisor_repetidos_no_se_aceptan()
    {
        var identificaciones = new Identificaciones(conexion);
        identificaciones.Guardar(new Identificacion(0, "Factura", "Proveedor A", "{}"));

        Assert.Throws<InvalidOperationException>(() =>
            identificaciones.Guardar(new Identificacion(0, "factura", "proveedor a", "{}"))
        );
    }

    [Fact]
    public void Editar_una_que_no_existe_lanza_error()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new Identificaciones(conexion).Guardar(new Identificacion(99, "Factura", "X", "{}"))
        );
    }

    [Fact]
    public void Quitar_borra_solo_esa()
    {
        var identificaciones = new Identificaciones(conexion);
        long a = identificaciones.Guardar(new Identificacion(0, "Factura", "A", "{}"));
        identificaciones.Guardar(new Identificacion(0, "Factura", "B", "{}"));

        Assert.True(identificaciones.Quitar(a));
        Assert.False(identificaciones.Quitar(a));
        Assert.Equal("B", Assert.Single(identificaciones.Listar()).Emisor);
    }

    [Fact]
    public void Auditoria_devuelve_los_mas_nuevos_primero_y_por_app()
    {
        var auditoria = new Auditoria(conexion);
        auditoria.Registrar("Archivero", "guardar", @"C:\e\1.pdf", @"C:\d\1.pdf", "h1", "ok");
        auditoria.Registrar("Archivero", "descartar", @"C:\e\2.pdf", null, "h2", "ok");
        auditoria.Registrar("Mensajero", "enviar", @"C:\x.pdf", null, null, "ok");

        var recientes = auditoria.Recientes("Archivero", 10);

        Assert.Equal(2, recientes.Count);
        Assert.Equal("descartar", recientes[0].Accion);
        Assert.Null(recientes[0].Destino);
        Assert.Equal(@"C:\d\1.pdf", recientes[1].Destino);
    }
}
