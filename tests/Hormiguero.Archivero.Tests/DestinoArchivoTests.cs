using Hormiguero.Archivero.Logica;
using Xunit;

namespace Hormiguero.Archivero.Tests;

public class DestinoArchivoTests
{
    [Fact]
    public void Carpeta_anio_y_mes()
    {
        var regla = new ReglaDestino(@"C:\Documentos", FormaCarpeta.AnioYMes, [], "");
        var fecha = new DateOnly(2026, 10, 4);

        var resultado = DestinoArchivo.Carpeta(regla, fecha);

        Assert.Equal(@"C:\Documentos\2026\202610", resultado);
    }

    [Fact]
    public void Carpeta_solo_anio()
    {
        var regla = new ReglaDestino(@"C:\Documentos", FormaCarpeta.SoloAnio, [], "");
        var fecha = new DateOnly(2026, 10, 4);

        var resultado = DestinoArchivo.Carpeta(regla, fecha);

        Assert.Equal(@"C:\Documentos\2026", resultado);
    }

    [Fact]
    public void Carpeta_directo()
    {
        var regla = new ReglaDestino(@"C:\Documentos", FormaCarpeta.Directo, [], "");
        var fecha = new DateOnly(2026, 10, 4);

        var resultado = DestinoArchivo.Carpeta(regla, fecha);

        Assert.Equal(@"C:\Documentos", resultado);
    }

    [Fact]
    public void Nombre_tipo_y_numero_sin_separador()
    {
        var regla = new ReglaDestino(
            "",
            FormaCarpeta.Directo,
            new[] { ParteNombre.Tipo, ParteNombre.Numero },
            ""
        );
        var datos = new DatosDocumento(
            "OCC",
            "Emisor",
            "000104523",
            "scan.pdf",
            new DateOnly(2026, 10, 4),
            false
        );

        var resultado = DestinoArchivo.Nombre(regla, datos);

        Assert.Equal("OCC104523.pdf", resultado);
    }

    [Fact]
    public void Nombre_con_separador_y_emisor()
    {
        var regla = new ReglaDestino(
            "",
            FormaCarpeta.Directo,
            new[] { ParteNombre.Tipo, ParteNombre.Emisor, ParteNombre.Numero },
            "_"
        );
        var datos = new DatosDocumento(
            "FCV",
            "Proveedor SA",
            "000001",
            "doc.pdf",
            new DateOnly(2026, 10, 4),
            false
        );

        var resultado = DestinoArchivo.Nombre(regla, datos);

        Assert.Equal("FCV_Proveedor SA_1.pdf", resultado);
    }

    [Fact]
    public void Nombre_original_sin_extension()
    {
        var regla = new ReglaDestino(
            "",
            FormaCarpeta.Directo,
            new[] { ParteNombre.NombreOriginal },
            ""
        );
        var datos = new DatosDocumento(
            "",
            "",
            "",
            "mi_documento_v2.pdf",
            new DateOnly(2026, 10, 4),
            false
        );

        var resultado = DestinoArchivo.Nombre(regla, datos);

        Assert.Equal("mi_documento_v2.pdf", resultado);
    }

    [Fact]
    public void Cedible_agrega_sufijo()
    {
        var regla = new ReglaDestino(
            "",
            FormaCarpeta.Directo,
            new[] { ParteNombre.Tipo, ParteNombre.Numero },
            ""
        );
        var datos = new DatosDocumento(
            "FCV",
            "",
            "25001",
            "doc.pdf",
            new DateOnly(2026, 10, 4),
            true
        );

        var resultado = DestinoArchivo.Nombre(regla, datos);

        Assert.Equal("FCV25001_CEDIBLE.pdf", resultado);
    }

    [Fact]
    public void Cedible_no_repite_sufijo()
    {
        var regla = new ReglaDestino(
            "",
            FormaCarpeta.Directo,
            new[] { ParteNombre.Tipo, ParteNombre.Numero },
            ""
        );
        var datos = new DatosDocumento(
            "FCV",
            "",
            "25001_CEDIBLE",
            "doc.pdf",
            new DateOnly(2026, 10, 4),
            true
        );

        var resultado = DestinoArchivo.Nombre(regla, datos);

        Assert.Equal("FCV25001_CEDIBLE.pdf", resultado);
    }

    [Fact]
    public void Cedible_no_repite_sufijo_mayusculas_distintas()
    {
        var regla = new ReglaDestino(
            "",
            FormaCarpeta.Directo,
            new[] { ParteNombre.Tipo, ParteNombre.Numero },
            ""
        );
        var datos = new DatosDocumento(
            "FCV",
            "",
            "25001_cedible",
            "doc.pdf",
            new DateOnly(2026, 10, 4),
            true
        );

        var resultado = DestinoArchivo.Nombre(regla, datos);

        Assert.Equal("FCV25001_cedible.pdf", resultado);
    }

    [Fact]
    public void Caracteres_invalidos_se_reemplazan()
    {
        var regla = new ReglaDestino("", FormaCarpeta.Directo, new[] { ParteNombre.Emisor }, "");
        var datos = new DatosDocumento("", "A/B", "", "doc.pdf", new DateOnly(2026, 10, 4), false);

        var resultado = DestinoArchivo.Nombre(regla, datos);

        Assert.Equal("A-B.pdf", resultado);
    }

    [Fact]
    public void Sin_partes_lanza_error()
    {
        var regla = new ReglaDestino("", FormaCarpeta.Directo, [], "");
        var datos = new DatosDocumento("", "", "", "doc.pdf", new DateOnly(2026, 10, 4), false);

        Assert.Throws<InvalidOperationException>(() => DestinoArchivo.Nombre(regla, datos));
    }

    [Fact]
    public void Numero_todo_ceros_devuelve_cero()
    {
        var regla = new ReglaDestino("", FormaCarpeta.Directo, new[] { ParteNombre.Numero }, "");
        var datos = new DatosDocumento("", "", "0000", "doc.pdf", new DateOnly(2026, 10, 4), false);

        var resultado = DestinoArchivo.Nombre(regla, datos);

        Assert.Equal("0.pdf", resultado);
    }

    [Fact]
    public void Partes_con_espacios_se_recortan()
    {
        var regla = new ReglaDestino(
            "",
            FormaCarpeta.Directo,
            new[] { ParteNombre.Tipo, ParteNombre.Emisor, ParteNombre.Numero },
            "_"
        );
        var datos = new DatosDocumento(
            "  OCC  ",
            "  Emisor  ",
            "123",
            "doc.pdf",
            new DateOnly(2026, 10, 4),
            false
        );

        var resultado = DestinoArchivo.Nombre(regla, datos);

        Assert.Equal("OCC_Emisor_123.pdf", resultado);
    }

    [Fact]
    public void Ruta_combina_carpeta_y_nombre()
    {
        var regla = new ReglaDestino(
            @"C:\Docs",
            FormaCarpeta.SoloAnio,
            new[] { ParteNombre.Tipo, ParteNombre.Numero },
            ""
        );
        var datos = new DatosDocumento("FCV", "", "1", "doc.pdf", new DateOnly(2026, 10, 4), false);

        var resultado = DestinoArchivo.Ruta(regla, datos);

        Assert.Equal(@"C:\Docs\2026\FCV1.pdf", resultado);
    }
}
