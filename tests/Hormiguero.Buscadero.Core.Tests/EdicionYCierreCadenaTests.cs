using Buscadero.Core.Lineas;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Buscadero.Core.Tests;

public sealed class EdicionYCierreCadenaTests
{
    [Fact]
    public void BorrarVagonInstancia_AnulaAnexosYCadenaHijaSinExcepcion()
    {
        using var entorno = new EntornoDePrueba();
        var modeloHijo = entorno.ServicioLineas.CrearPlantilla("Modelo hijo", esModeloHijo: true);
        entorno.ServicioLineas.AgregarVagon(
            modeloHijo.Id,
            null,
            "Documento hijo",
            false,
            false,
            null
        );
        entorno.ServicioLineas.AgregarVagon(
            modeloHijo.Id,
            null,
            "Otro documento",
            false,
            false,
            null
        );

        var modelo = entorno.ServicioLineas.CrearPlantilla("Modelo principal");
        var principal = entorno.ServicioLineas.AgregarVagon(
            modelo.Id,
            null,
            "Principal",
            false,
            false,
            null
        );
        var anexo = entorno.ServicioLineas.AgregarVagon(
            modelo.Id,
            principal.Id,
            "Anexo",
            false,
            true,
            null
        );
        var ramificado = entorno.ServicioLineas.AgregarVagon(
            modelo.Id,
            null,
            "Ramificado",
            true,
            false,
            modeloHijo.Id
        );
        entorno.ServicioLineas.AgregarVagon(modelo.Id, null, "Final", false, false, null);

        var cadena = entorno.ServicioLineas.CrearInstancia(modelo.Id, "Cadena principal");
        var vagones = entorno
            .ServicioLineas.ObtenerArbolInstancia(cadena.Id)
            .Select(n => n.Vagon)
            .ToList();
        var vagonPrincipal = Assert.Single(vagones, v => v.PlantillaVagonId == principal.Id);
        var vagonAnexo = entorno.ServicioLineas.AgregarAnexo(
            cadena.Id,
            anexo.Id,
            vagonPrincipal.Id
        );
        var vagonRamificado = Assert.Single(vagones, v => v.PlantillaVagonId == ramificado.Id);
        var cadenaHija = entorno.ServicioLineas.AgregarCadenaHija(cadena.Id, vagonRamificado.Id);
        var segundaCadenaHija = entorno.ServicioLineas.AgregarCadenaHija(
            cadena.Id,
            vagonRamificado.Id
        );

        entorno.ServicioLineas.QuitarVagonInstancia(vagonPrincipal.Id);

        Assert.DoesNotContain(
            entorno.ServicioLineas.ObtenerArbolInstancia(cadena.Id),
            n => n.Vagon.Id == vagonPrincipal.Id
        );
        Assert.DoesNotContain(
            entorno.ServicioLineas.ObtenerArbolInstancia(cadena.Id).SelectMany(n => n.Hijos),
            n => n.Vagon.Id == vagonAnexo.Id
        );

        entorno.ServicioLineas.QuitarVagonInstancia(vagonRamificado.Id);

        Assert.Throws<InvalidOperationException>(() =>
            entorno.ServicioLineas.ObtenerInstancia(cadenaHija.Id)
        );
        Assert.Throws<InvalidOperationException>(() =>
            entorno.ServicioLineas.ObtenerInstancia(segundaCadenaHija.Id)
        );
        Assert.Single(entorno.ServicioLineas.ObtenerArbolInstancia(cadena.Id));
    }

    [Fact]
    public void BorrarVagonModelo_ConAnexosYCadenaYaCreadaNoFalla()
    {
        using var entorno = new EntornoDePrueba();
        var modelo = entorno.ServicioLineas.CrearPlantilla("Modelo");
        var principal = entorno.ServicioLineas.AgregarVagon(
            modelo.Id,
            null,
            "Principal",
            false,
            false,
            null
        );
        entorno.ServicioLineas.AgregarVagon(modelo.Id, principal.Id, "Anexo", false, true, null);
        entorno.ServicioLineas.AgregarVagon(modelo.Id, null, "Final", false, false, null);
        var cadena = entorno.ServicioLineas.CrearInstancia(modelo.Id, "Cadena");

        entorno.ServicioLineas.BorrarVagon(principal.Id);

        Assert.Equal(
            "Final",
            Assert.Single(entorno.ServicioLineas.ObtenerArbolPlantilla(modelo.Id)).Vagon.Nombre
        );
        Assert.Equal(2, entorno.ServicioLineas.ObtenerArbolInstancia(cadena.Id).Count);
    }

    [Fact]
    public void EditarModelo_PermiteRenombrarYMoverDocumentos()
    {
        using var entorno = new EntornoDePrueba();
        var modelo = entorno.ServicioLineas.CrearPlantilla("Modelo");
        var primero = entorno.ServicioLineas.AgregarVagon(
            modelo.Id,
            null,
            "Primero",
            false,
            false,
            null
        );
        var segundo = entorno.ServicioLineas.AgregarVagon(
            modelo.Id,
            null,
            "Segundo",
            false,
            false,
            null
        );
        var tercero = entorno.ServicioLineas.AgregarVagon(
            modelo.Id,
            null,
            "Tercero",
            false,
            false,
            null
        );

        entorno.ServicioLineas.ActualizarVagon(segundo.Id, "Renombrado", false, false, null);
        entorno.ServicioLineas.MoverVagon(tercero.Id, -1);

        var orden = entorno
            .ServicioLineas.ObtenerArbolPlantilla(modelo.Id)
            .Select(n => n.Vagon)
            .ToList();
        Assert.Equal([primero.Id, tercero.Id, segundo.Id], orden.Select(v => v.Id));
        Assert.Equal("Renombrado", orden[2].Nombre);
        entorno.ServicioLineas.BorrarVagon(tercero.Id);
        Assert.Equal(
            [primero.Id, segundo.Id],
            entorno.ServicioLineas.ObtenerArbolPlantilla(modelo.Id).Select(n => n.Vagon.Id)
        );
    }

    [Fact]
    public void BorradoFallido_SeRegistraEnAuditoria()
    {
        using var entorno = new EntornoDePrueba();
        var modelo = entorno.ServicioLineas.CrearPlantilla("Modelo");
        entorno.ServicioLineas.AgregarVagon(modelo.Id, null, "Documento", false, false, null);
        var cadena = entorno.ServicioLineas.CrearInstancia(modelo.Id, "Cadena");
        var vagon = Assert.Single(entorno.ServicioLineas.ObtenerArbolInstancia(cadena.Id)).Vagon;
        using (var conexion = BaseComun.Abrir(entorno.RutaBaseComun))
        using (var cmd = conexion.CreateCommand())
        {
            cmd.CommandText =
                "CREATE TRIGGER impedir_borrado BEFORE UPDATE OF estado ON vagones_cadena BEGIN SELECT RAISE(ABORT, 'fallo de prueba'); END;";
            cmd.ExecuteNonQuery();
        }

        Assert.Throws<SqliteException>(() => entorno.ServicioLineas.QuitarVagonInstancia(vagon.Id));

        using var lectura = BaseComun.Abrir(entorno.RutaBaseComun);
        using var auditoria = lectura.CreateCommand();
        auditoria.CommandText =
            "SELECT COUNT(*) FROM auditoria WHERE app='Buscadero' AND accion='error_operacion' AND origen='Quitar documento de una cadena';";
        Assert.Equal(1L, Convert.ToInt64(auditoria.ExecuteScalar()));
    }
}
