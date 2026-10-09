using Buscadero.Core.Lineas;
using Hormiguero.Nucleo.Datos;

namespace Hormiguero.Core.Tests;

public sealed class EsquemasCadenaUiTests
{
    [Fact]
    public void ValidaProveedorLugaresInicioYPareja()
    {
        var compartidos = new[]
        {
            new TipoDatoCompartido(
                1,
                2,
                "Guía",
                "Proveedor",
                "Factura",
                "Proveedor",
                "numero_guia",
                "N.º de guía"
            ),
        };
        Assert.Equal(
            "Escriba o seleccione un proveedor.",
            AsistenteEsquemaCadena.Validar(" ", [], [], compartidos)
        );
        Assert.Equal(
            "Agregue al menos un documento del proceso.",
            AsistenteEsquemaCadena.Validar("Proveedor", [], [], compartidos)
        );
        var lugares = new[]
        {
            new DefinicionLugarEsquema(0, 1, false, "Guía"),
            new DefinicionLugarEsquema(1, 2, false, "Factura"),
        };
        Assert.Equal(
            "Marque al menos un documento que inicie la cadena.",
            AsistenteEsquemaCadena.Validar("Proveedor", lugares, [], compartidos)
        );
        lugares = [lugares[0] with { IniciaCadena = true }, lugares[1]];
        Assert.Null(
            AsistenteEsquemaCadena.Validar(
                "Proveedor",
                lugares,
                [new(0, 1, "numero_guia")],
                compartidos
            )
        );
        Assert.Equal(
            "La pareja debe usar un dato que compartan ambos tipos de documento.",
            AsistenteEsquemaCadena.Validar(
                "Proveedor",
                lugares,
                [new(0, 1, "otro_dato")],
                compartidos
            )
        );
    }

    [Fact]
    public void ArmaFilasDelArbolIncluyendoLugaresFaltantesYLugaresEmparejados()
    {
        var lugares = new[]
        {
            new LugarEsquema(10, 1, 0, 1, true, "Orden propia"),
            new LugarEsquema(11, 1, 1, 2, false, "Guía"),
            new LugarEsquema(12, 1, 2, 3, false, "Factura"),
        };
        var esquema = new EsquemaCadena(
            1,
            "PROVEEDOR",
            "PROVEEDOR",
            true,
            null,
            lugares,
            [new(1, 11, 12, "numero_guia")],
            [],
            []
        );
        var documentos = new[]
        {
            new DocumentoLugarCadena(20, 100, 10, "Orden propia", 0, null, null, "OC 88"),
            new DocumentoLugarCadena(21, 101, 11, "Guía", 1, 5, null, "Guía"),
        };
        var version = new VersionDocumentoCadena(
            100,
            "oc.pdf",
            "oc.pdf",
            "Orden",
            "Empresa",
            DateTime.Today,
            "88"
        );
        var filas = AsistenteEsquemaCadena.ArmarArbol(
            esquema,
            documentos,
            new Dictionary<long, VersionDocumentoCadena> { [100] = version }
        );
        Assert.Equal(3, filas.Count);
        Assert.Equal("88", filas[0].Documentos[0].Numero);
        Assert.True(filas[1].EsPareja);
        Assert.True(filas[2].EsPareja);
        Assert.True(filas[2].Documentos[0].Falta);
    }
}
