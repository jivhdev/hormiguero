using Buscadero.Core.Lineas;
using Hormiguero.Nucleo.Datos;

namespace Buscadero.Core.Tests;

public sealed class ReglasVagonTests
{
    [Fact]
    public void FraseRegla_UsaLosNombresElegidos()
    {
        var frase = TextoReglaVagon.CrearFrase(
            "Guía",
            "Guías Cobelcar",
            "N° OC",
            "Orden de compra",
            "N° OC"
        );

        Assert.Equal(
            "Completar Guía con documentos de Guías Cobelcar cuando N° OC sea igual a N° OC de Orden de compra",
            frase
        );
    }

    [Fact]
    public void OpcionesRegla_UsanLoPublicadoYLaReglaConservaValoresIniciales()
    {
        using var entorno = new EntornoDePrueba();
        using var conexion = BaseComun.Abrir(entorno.RutaBaseComun);
        long identificacion = new Identificaciones(conexion).Guardar(
            new(0, "Guía", "Cobelcar", "{}")
        );
        long campo = new RepositorioDocumentosDatos(conexion).GuardarCampo(
            new(0, identificacion, "N° OC", "numero_oc", "texto", true, "marca")
        );

        var opciones = Assert.Single(entorno.ServicioLineas.ListarOpcionesRegla());
        Assert.Equal("Guía de Cobelcar", opciones.Nombre);
        Assert.Equal(campo, Assert.Single(opciones.Campos).Id);
        var regla = new ReglaVagonConfigurada(1, identificacion, campo, 2, campo);
        Assert.True(regla.IgnorarEspacios);
        Assert.False(regla.IgnorarGuiones);
        Assert.False(regla.IgnorarCerosIniciales);
        Assert.Equal(6, regla.LargoMinimo);
    }

    [Fact]
    public void ContadorYResolucionDudosa_ActualizanLaListaYGuardanHistorial()
    {
        using var entorno = new EntornoDePrueba();
        using var conexion = BaseComun.Abrir(entorno.RutaBaseComun);
        long identificacion = new Identificaciones(conexion).Guardar(
            new(0, "Compra", "Cobelcar", "{}")
        );
        var datos = new RepositorioDocumentosDatos(conexion);
        long campo = datos.GuardarCampo(
            new(0, identificacion, "N° OC", "numero_oc", "texto", true, "marca")
        );
        var modelo = entorno.ServicioLineas.CrearPlantilla("Compras");
        var origen = entorno.ServicioLineas.AgregarVagon(
            modelo.Id,
            null,
            "Orden",
            false,
            false,
            null
        );
        var destino = entorno.ServicioLineas.AgregarVagon(
            modelo.Id,
            null,
            "Guía",
            false,
            false,
            null
        );
        var cadena = entorno.ServicioLineas.CrearInstancia(modelo.Id, "Compra 1");
        var vagones = entorno
            .ServicioLineas.ObtenerArbolInstancia(cadena.Id)
            .Select(n => n.Vagon)
            .ToDictionary(v => v.PlantillaVagonId!.Value);
        var referencia = datos.PublicarDocumento(
            "orden.pdf",
            10,
            DateTime.UtcNow,
            "huella-orden",
            "Cobelcar",
            "Compra",
            [new("N° OC", "numero_oc", "OC123456", "OC123456", "manual")]
        );
        var propuesto = datos.PublicarDocumento(
            "guia.pdf",
            10,
            DateTime.UtcNow,
            "huella-guia",
            "Cobelcar",
            "Compra",
            [new("N° OC", "numero_oc", "OC123456", "OC123456", "manual")]
        );
        var enlaces = new RepositorioReglasYEnlaces(conexion);
        enlaces.CrearEnlace(vagones[origen.Id].Id, referencia.Version.Id, "manual");
        long regla = enlaces.GuardarRegla(
            new(
                0,
                destino.Id,
                identificacion,
                campo,
                origen.Id,
                campo,
                "igual",
                true,
                false,
                false,
                6,
                "activa"
            )
        );
        long dudoso = enlaces.CrearDudoso(
            vagones[destino.Id].Id,
            propuesto.Version.Id,
            regla,
            "Hay 2 documentos con el mismo dato."
        );

        Assert.Equal(1, entorno.ServicioLineas.ContarDudosos());
        var item = Assert.Single(entorno.ServicioLineas.ListarDudosos());
        Assert.Equal("orden.pdf", item.NombreDocumentoComparado);
        Assert.Equal("OC123456", item.ValorPropuesto);
        Assert.Equal("Hay varios documentos con ese mismo dato.", item.Motivo);
        Assert.True(entorno.ServicioLineas.AceptarDudoso(dudoso));
        Assert.Equal(0, entorno.ServicioLineas.ContarDudosos());
        Assert.Contains(
            entorno.ServicioLineas.HistorialEnlaces(vagones[destino.Id].Id),
            e => e.Id == dudoso && e.Estado == "activo"
        );
    }
}
