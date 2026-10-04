using Buscadero.Core.Lineas;

namespace Buscadero.Core.Tests;

public sealed class LineasTests
{
    [Fact]
    public void Modelo_DocumentosEnOrdenConAnexoYRamificado()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, facturacion, orden, guia, remito, _) = CrearModelos(entorno);

        var arbol = entorno.ServicioLineas.ObtenerArbolPlantilla(compra.Id);

        Assert.Equal(2, arbol.Count);
        Assert.Equal(orden.Id, arbol[0].Vagon.Id);
        Assert.Equal("Orden de Compra", arbol[0].Vagon.Nombre);
        Assert.Equal("Guía", arbol[1].Vagon.Nombre);
        Assert.True(arbol[1].Vagon.EsMultiple);
        Assert.Equal(facturacion.Id, arbol[1].Vagon.ModeloCadenaHijaId);

        var anexo = Assert.Single(arbol[0].Hijos);
        Assert.Equal(remito.Id, anexo.Vagon.Id);
        Assert.True(anexo.Vagon.EsAnexo);
    }

    [Fact]
    public void AgregarAnexo_SinPadre_Lanza()
    {
        using var entorno = new EntornoDePrueba();
        var compra = entorno.ServicioLineas.CrearPlantilla("Compra");

        Assert.Throws<InvalidOperationException>(() =>
            entorno.ServicioLineas.AgregarVagon(compra.Id, null, "Remito", false, true, null)
        );
    }

    [Fact]
    public void AgregarDocumentoEnOrdenConPadre_Lanza()
    {
        using var entorno = new EntornoDePrueba();
        var compra = entorno.ServicioLineas.CrearPlantilla("Compra");
        var orden = entorno.ServicioLineas.AgregarVagon(
            compra.Id,
            null,
            "Orden",
            false,
            false,
            null
        );

        Assert.Throws<InvalidOperationException>(() =>
            entorno.ServicioLineas.AgregarVagon(compra.Id, orden.Id, "Guía", false, false, null)
        );
    }

    [Fact]
    public void AgregarRamificado_SinModeloHijo_Lanza()
    {
        using var entorno = new EntornoDePrueba();
        var compra = entorno.ServicioLineas.CrearPlantilla("Compra");

        Assert.Throws<InvalidOperationException>(() =>
            entorno.ServicioLineas.AgregarVagon(compra.Id, null, "Guía", true, false, null)
        );
    }

    [Fact]
    public void AgregarRamificado_NoPuedeApuntarASuPropioModelo()
    {
        using var entorno = new EntornoDePrueba();
        var compra = entorno.ServicioLineas.CrearPlantilla("Compra");

        Assert.Throws<InvalidOperationException>(() =>
            entorno.ServicioLineas.AgregarVagon(compra.Id, null, "Guía", true, false, compra.Id)
        );
    }

    [Fact]
    public void CrearCadena_CopiaDocumentosNoAnexoIncluidoElRamificado()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, _, _, _, _) = CrearModelos(entorno);

        var cadena = entorno.ServicioLineas.CrearInstancia(compra.Id, "Compra 1");
        var arbol = entorno.ServicioLineas.ObtenerArbolInstancia(cadena.Id);

        Assert.Equal(2, arbol.Count);
        Assert.Contains(arbol, n => n.Vagon.Nombre == "Guía" && n.Vagon.EsMultiple);
        Assert.DoesNotContain(arbol, n => n.Vagon.Nombre == "Remito");
        Assert.All(arbol, n => Assert.Empty(n.Hijos));
    }

    [Fact]
    public void AgregarCadenaHija_CreaUnaCadenaAparteEnlazada()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, _, _, _, _) = CrearModelos(entorno);
        var cadena = entorno.ServicioLineas.CrearInstancia(compra.Id, "Compra 1");
        var guia = DocumentoPorNombre(entorno, cadena.Id, "Guía");

        var hija = entorno.ServicioLineas.AgregarCadenaHija(cadena.Id, guia.Id);
        var hijaArbol = entorno.ServicioLineas.ObtenerArbolInstancia(hija.Id);

        var factura = Assert.Single(hijaArbol);
        Assert.Equal("Factura", factura.Vagon.Nombre);
        Assert.Equal(cadena.Id, hija.CadenaMadreId);
        Assert.Equal(guia.Id, hija.InstanciaVagonPadreId);

        entorno.ServicioLineas.AgregarCadenaHija(cadena.Id, guia.Id);
        Assert.Equal(2, entorno.ServicioLineas.ObtenerCadenasHijas(guia.Id).Count);
    }

    [Fact]
    public void Ramificaciones_SeAnidanSinLimite()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, _, _, _, _) = CrearModelos(entorno);

        var ventas = entorno.ServicioLineas.CrearPlantilla("Ventas");
        var pedido = entorno.ServicioLineas.AgregarVagon(
            ventas.Id,
            null,
            "Pedido",
            true,
            false,
            compra.Id
        );

        var cadenaVentas = entorno.ServicioLineas.CrearInstancia(ventas.Id, "Venta 1");
        var pedidoFila = DocumentoPorNombre(entorno, cadenaVentas.Id, "Pedido");
        var compraHija = entorno.ServicioLineas.AgregarCadenaHija(cadenaVentas.Id, pedidoFila.Id);
        var guiaHija = DocumentoPorNombre(entorno, compraHija.Id, "Guía");
        var facturacionNieta = entorno.ServicioLineas.AgregarCadenaHija(compraHija.Id, guiaHija.Id);

        Assert.Equal(
            "Factura",
            Assert
                .Single(entorno.ServicioLineas.ObtenerArbolInstancia(facturacionNieta.Id))
                .Vagon.Nombre
        );
        Assert.Equal(pedido.Id, pedidoFila.PlantillaVagonId);
    }

    [Fact]
    public void AgregarAnexo_LoAgregaSoloCuandoSePide()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, _, _, remito, _) = CrearModelos(entorno);
        var cadena = entorno.ServicioLineas.CrearInstancia(compra.Id, "Compra 1");
        var orden = DocumentoPorNombre(entorno, cadena.Id, "Orden de Compra");

        entorno.ServicioLineas.AgregarAnexo(cadena.Id, remito.Id, orden.Id);

        var raiz = entorno
            .ServicioLineas.ObtenerArbolInstancia(cadena.Id)
            .First(n => n.Vagon.Id == orden.Id);
        var anexo = Assert.Single(raiz.Hijos);
        Assert.True(anexo.Vagon.EsAnexo);
        Assert.Equal(remito.Id, anexo.Vagon.PlantillaVagonId);
    }

    [Fact]
    public void BorrarModelo_NoAfectaLaCadenaYSirveSuSnapshot()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, _, _, _, _) = CrearModelos(entorno);
        var cadena = entorno.ServicioLineas.CrearInstancia(compra.Id, "Compra 1");
        var guia = DocumentoPorNombre(entorno, cadena.Id, "Guía");

        entorno.ServicioLineas.BorrarPlantilla(compra.Id);

        Assert.Single(entorno.ServicioLineas.ObtenerInstancias());
        // El modelo hijo sigue existiendo, así que se puede agregar una cadena hija usando el snapshot.
        entorno.ServicioLineas.AgregarCadenaHija(cadena.Id, guia.Id);
        Assert.Single(entorno.ServicioLineas.ObtenerCadenasHijas(guia.Id));
    }

    [Fact]
    public void VincularYDesvincularDocumento_EsUnoPorDocumento()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, _, _, _, _) = CrearModelos(entorno);
        var cadena = entorno.ServicioLineas.CrearInstancia(compra.Id, "Compra 1");
        var orden = DocumentoPorNombre(entorno, cadena.Id, "Orden de Compra");

        entorno.ServicioLineas.VincularDocumento(orden.Id, @"C:\docs\12345.pdf");
        Assert.Equal("12345.pdf", DocumentoPorId(entorno, cadena.Id, orden.Id).NombreDocumento);

        entorno.ServicioLineas.VincularDocumento(orden.Id, @"C:\docs\99999.pdf");
        Assert.Equal("99999.pdf", DocumentoPorId(entorno, cadena.Id, orden.Id).NombreDocumento);

        entorno.ServicioLineas.DesvincularDocumento(orden.Id);
        Assert.Null(DocumentoPorId(entorno, cadena.Id, orden.Id).NombreDocumento);
    }

    [Fact]
    public void QuitarDocumentoRamificado_EliminaSusCadenasHijas()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, _, _, _, _) = CrearModelos(entorno);
        var cadena = entorno.ServicioLineas.CrearInstancia(compra.Id, "Compra 1");
        var guia = DocumentoPorNombre(entorno, cadena.Id, "Guía");
        entorno.ServicioLineas.AgregarCadenaHija(cadena.Id, guia.Id);

        entorno.ServicioLineas.QuitarVagonInstancia(guia.Id);

        Assert.Empty(entorno.ServicioLineas.ObtenerCadenasHijas(guia.Id));
        Assert.Single(entorno.ServicioLineas.ObtenerArbolInstancia(cadena.Id));
    }

    [Fact]
    public void BorrarCadena_DisuelveLaLinea()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, _, _, _, _) = CrearModelos(entorno);
        var cadena = entorno.ServicioLineas.CrearInstancia(compra.Id, "Compra 1");

        entorno.ServicioLineas.BorrarInstancia(cadena.Id);

        Assert.Empty(entorno.ServicioLineas.ObtenerInstancias());
        Assert.Equal(2, entorno.ServicioLineas.ObtenerPlantillas().Count);
    }

    [Fact]
    public void CrearCadenaVacia_GeneraNombreYQuedaDisponible()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, _, _, _, _) = CrearModelos(entorno);

        var cadena = entorno.ServicioLineas.CrearCadenaVacia(compra.Id);

        Assert.False(string.IsNullOrWhiteSpace(cadena.Nombre));
        Assert.Single(entorno.ServicioLineas.ObtenerCadenasDeModelo(compra.Id));
    }

    [Fact]
    public void BuscarCadenasDeDocumento_EncuentraMadreYCadenaHija()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, _, _, _, _) = CrearModelos(entorno);
        var cadena = entorno.ServicioLineas.CrearCadenaVacia(compra.Id);
        var orden = DocumentoPorNombre(entorno, cadena.Id, "Orden de Compra");
        var guia = DocumentoPorNombre(entorno, cadena.Id, "Guía");
        entorno.ServicioLineas.VincularDocumento(orden.Id, @"C:\d\orden.pdf");
        var hija = entorno.ServicioLineas.AgregarCadenaHija(cadena.Id, guia.Id);
        var factura = DocumentoPorNombre(entorno, hija.Id, "Factura");
        entorno.ServicioLineas.VincularDocumento(factura.Id, @"C:\d\factura.pdf");

        var porMadre = entorno.ServicioLineas.BuscarCadenasDeDocumento(@"C:\d\orden.pdf");
        var porHija = entorno.ServicioLineas.BuscarCadenasDeDocumento(@"C:\d\factura.pdf");

        Assert.Equal(cadena.Id, Assert.Single(porMadre).CadenaRaiz.Id);
        Assert.Equal(cadena.Id, Assert.Single(porHija).CadenaRaiz.Id);

        var camino = entorno.ServicioLineas.ObtenerCaminoPlano(cadena.Id, factura.Id);
        Assert.Contains(camino, d => d.Id == orden.Id);
        Assert.Contains(camino, d => d.Id == guia.Id);
        Assert.Contains(camino, d => d.Id == factura.Id);
    }

    [Fact]
    public void ModelosHijo_NoAparecenEntreLosIndependientes()
    {
        using var entorno = new EntornoDePrueba();
        var independiente = entorno.ServicioLineas.CrearPlantilla("Compra");
        var hijo = entorno.ServicioLineas.CrearPlantilla("Facturación", esModeloHijo: true);

        var independientes = entorno.ServicioLineas.ObtenerModelosIndependientes();

        Assert.Contains(independientes, m => m.Id == independiente.Id);
        Assert.DoesNotContain(independientes, m => m.Id == hijo.Id);
    }

    [Fact]
    public void ObtenerCaminoCompleto_IncluyeDocumentosVacios()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, _, _, _, _) = CrearModelos(entorno);
        var cadena = entorno.ServicioLineas.CrearCadenaVacia(compra.Id);
        var orden = DocumentoPorNombre(entorno, cadena.Id, "Orden de Compra");
        var guia = DocumentoPorNombre(entorno, cadena.Id, "Guía");
        entorno.ServicioLineas.VincularDocumento(guia.Id, @"C:\d\guia.pdf");
        var hija = entorno.ServicioLineas.AgregarCadenaHija(cadena.Id, guia.Id);
        var factura = DocumentoPorNombre(entorno, hija.Id, "Factura");

        var camino = entorno.ServicioLineas.ObtenerCaminoCompleto(cadena.Id, guia.Id);
        Assert.Contains(camino, d => d.Id == orden.Id); // vacío, igual aparece
        Assert.Contains(camino, d => d.Id == guia.Id);
        Assert.Contains(camino, d => d.Id == factura.Id); // incluye una cadena hija aunque el observado esté en la madre

        var caminoRama = entorno.ServicioLineas.ObtenerCaminoCompleto(cadena.Id, factura.Id);
        Assert.Contains(caminoRama, d => d.Id == factura.Id); // vacío, igual aparece
    }

    [Fact]
    public void ObtenerTodosLosDocumentos_IncluyeArchivosDeCadenasHijasAnidadas()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, _, _, _, _) = CrearModelos(entorno);

        var ventas = entorno.ServicioLineas.CrearPlantilla("Ventas");
        entorno.ServicioLineas.AgregarVagon(ventas.Id, null, "Pedido", true, false, compra.Id);

        var cadenaVentas = entorno.ServicioLineas.CrearInstancia(ventas.Id, "Venta 1");
        var pedido = DocumentoPorNombre(entorno, cadenaVentas.Id, "Pedido");
        var compraHija = entorno.ServicioLineas.AgregarCadenaHija(cadenaVentas.Id, pedido.Id);
        var guiaNieta = DocumentoPorNombre(entorno, compraHija.Id, "Guía");
        var facturacionBisnieta = entorno.ServicioLineas.AgregarCadenaHija(
            compraHija.Id,
            guiaNieta.Id
        );
        var facturaBisnieta = DocumentoPorNombre(entorno, facturacionBisnieta.Id, "Factura");

        // El archivo vive dos niveles mas abajo que compraHija (compraHija -> facturacionBisnieta -> Factura).
        entorno.ServicioLineas.VincularDocumento(facturaBisnieta.Id, @"C:\d\factura.pdf");

        var documentos = entorno.ServicioLineas.ObtenerTodosLosDocumentos(compraHija.Id);

        Assert.Contains(
            documentos,
            d => d.Id == facturaBisnieta.Id && d.NombreDocumento == "factura.pdf"
        );
    }

    [Fact]
    public void ObtenerNombreVisible_SinPreferencia_UsaElNombreGenerico()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, _, _, _, _) = CrearModelos(entorno);
        var cadena = entorno.ServicioLineas.CrearInstancia(compra.Id, "Cadena 1");

        Assert.Equal("Cadena 1", entorno.ServicioLineas.ObtenerNombreVisible(cadena));
    }

    [Fact]
    public void ObtenerNombreVisible_PorDocumento_SinArchivoTodaviaUsaElGenerico()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, orden, _, _, _) = CrearModelos(entorno);
        entorno.ServicioLineas.ConfigurarPreferenciaNombre(
            compra.Id,
            PreferenciaNombreCadena.PorDocumento,
            orden.Id
        );
        var cadena = entorno.ServicioLineas.CrearInstancia(compra.Id, "Cadena 1");

        Assert.Equal("Cadena 1", entorno.ServicioLineas.ObtenerNombreVisible(cadena));
    }

    [Fact]
    public void ObtenerNombreVisible_PorDocumento_ConArchivoUsaElNombreDelArchivo()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, orden, _, _, _) = CrearModelos(entorno);
        entorno.ServicioLineas.ConfigurarPreferenciaNombre(
            compra.Id,
            PreferenciaNombreCadena.PorDocumento,
            orden.Id
        );
        var cadena = entorno.ServicioLineas.CrearInstancia(compra.Id, "Cadena 1");
        var ordenInstancia = DocumentoPorNombre(entorno, cadena.Id, "Orden de Compra");
        entorno.ServicioLineas.VincularDocumento(ordenInstancia.Id, @"C:\d\orden-123.pdf");

        Assert.Equal("orden-123.pdf", entorno.ServicioLineas.ObtenerNombreVisible(cadena));
    }

    [Fact]
    public void ObtenerNombreVisible_Personalizado_SeComportaComoGenerico()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, orden, _, _, _) = CrearModelos(entorno);
        entorno.ServicioLineas.ConfigurarPreferenciaNombre(
            compra.Id,
            PreferenciaNombreCadena.Personalizado,
            orden.Id
        );
        var cadena = entorno.ServicioLineas.CrearInstancia(compra.Id, "Cadena 1");

        Assert.Equal("Cadena 1", entorno.ServicioLineas.ObtenerNombreVisible(cadena));
    }

    [Fact]
    public void ConfigurarPreferenciaNombre_PorDocumentoSinElegirUno_Lanza()
    {
        using var entorno = new EntornoDePrueba();
        var (compra, _, _, _, _, _) = CrearModelos(entorno);

        Assert.Throws<InvalidOperationException>(() =>
            entorno.ServicioLineas.ConfigurarPreferenciaNombre(
                compra.Id,
                PreferenciaNombreCadena.PorDocumento,
                null
            )
        );
    }

    [Fact]
    public void ModelosCreadosAntesDeCaso12_QuedanEnGenericoPorDefecto()
    {
        using var entorno = new EntornoDePrueba();
        var compra = entorno.ServicioLineas.CrearPlantilla("Compra");

        var recargada = entorno.ServicioLineas.ObtenerPlantillas().Single(p => p.Id == compra.Id);

        Assert.Equal(PreferenciaNombreCadena.Generico, recargada.PreferenciaNombre);
        Assert.Null(recargada.VagonNombreId);
    }

    private static InstanciaVagon DocumentoPorNombre(
        EntornoDePrueba entorno,
        long cadenaId,
        string nombre
    ) =>
        entorno
            .ServicioLineas.ObtenerArbolInstancia(cadenaId)
            .First(n => n.Vagon.Nombre == nombre)
            .Vagon;

    private static InstanciaVagon DocumentoPorId(
        EntornoDePrueba entorno,
        long cadenaId,
        long documentoId
    ) =>
        entorno
            .ServicioLineas.ObtenerArbolInstancia(cadenaId)
            .First(n => n.Vagon.Id == documentoId)
            .Vagon;

    private static (
        PlantillaLinea Compra,
        PlantillaLinea Facturacion,
        PlantillaVagon Orden,
        PlantillaVagon Guia,
        PlantillaVagon Remito,
        PlantillaVagon Factura
    ) CrearModelos(EntornoDePrueba entorno)
    {
        var facturacion = entorno.ServicioLineas.CrearPlantilla("Facturación de Guía");
        var factura = entorno.ServicioLineas.AgregarVagon(
            facturacion.Id,
            null,
            "Factura",
            false,
            false,
            null
        );

        var compra = entorno.ServicioLineas.CrearPlantilla("Compra");
        var orden = entorno.ServicioLineas.AgregarVagon(
            compra.Id,
            null,
            "Orden de Compra",
            false,
            false,
            null
        );
        var guia = entorno.ServicioLineas.AgregarVagon(
            compra.Id,
            null,
            "Guía",
            true,
            false,
            facturacion.Id
        );
        var remito = entorno.ServicioLineas.AgregarVagon(
            compra.Id,
            orden.Id,
            "Remito",
            false,
            true,
            null
        );

        return (compra, facturacion, orden, guia, remito, factura);
    }
}
