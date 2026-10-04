using Hormiguero.Buscadero.Logica;
using Hormiguero.Nucleo.Datos;

namespace Hormiguero.Buscadero.Tests;

public class AgrupadorTests
{
    private static DocumentoIndexado CrearDocumento(
        string ruta,
        string nombre,
        string? huella,
        string estado,
        bool tieneTexto,
        (string Numero, string Prefijo, string Sufijo, string Origen)[] numeros,
        DateTime? modificado = null
    )
    {
        var carpeta = Path.GetDirectoryName(ruta) ?? "";
        return new DocumentoIndexado(
            ruta,
            carpeta,
            nombre,
            1000L,
            modificado ?? new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            huella,
            estado,
            tieneTexto,
            numeros
        );
    }

    [Fact]
    public void Copias_exactas_una_vez()
    {
        string huella = "huella123";
        string carpeta1 = @"C:\carpeta1";
        string carpeta2 = @"C:\carpeta2";

        var doc1 = CrearDocumento(
            Path.Combine(carpeta1, "OCC104523.pdf"),
            "OCC104523.pdf",
            huella,
            "correcto",
            true,
            new[] { ("104523", "OCC", "", "nombre") }
        );

        var doc2 = CrearDocumento(
            Path.Combine(carpeta2, "OCC104523.pdf"),
            "OCC104523.pdf",
            huella,
            "correcto",
            true,
            new[] { ("104523", "OCC", "", "nombre") }
        );

        var resultado = Agrupador.Agrupar([doc1, doc2], "104523");

        Assert.Single(resultado);
        Assert.Equal("OCC 104523", resultado[0].Titulo);
        Assert.Single(resultado[0].Versiones);
        Assert.Equal("original", resultado[0].Versiones[0].Etiqueta);
        Assert.Equal(2, resultado[0].Versiones[0].Rutas.Count);
        Assert.Equal(2, resultado[0].Carpetas.Count);
        Assert.Contains(carpeta1, resultado[0].Carpetas);
        Assert.Contains(carpeta2, resultado[0].Carpetas);
    }

    [Fact]
    public void Cedible_va_detras_del_original()
    {
        string carpeta = @"C:\carpeta";

        var original = CrearDocumento(
            Path.Combine(carpeta, "FCV25001.pdf"),
            "FCV25001.pdf",
            "huella1",
            "correcto",
            true,
            new[] { ("25001", "FCV", "", "nombre") }
        );

        var cedible = CrearDocumento(
            Path.Combine(carpeta, "FCV25001_CEDIBLE.pdf"),
            "FCV25001_CEDIBLE.pdf",
            "huella2",
            "correcto",
            true,
            new[] { ("25001", "FCV", "CEDIBLE", "nombre") }
        );

        var resultado = Agrupador.Agrupar([original, cedible], "25001");

        Assert.Single(resultado);
        Assert.Equal(2, resultado[0].Versiones.Count);
        Assert.Equal("original", resultado[0].Versiones[0].Etiqueta);
        Assert.Equal("cedible", resultado[0].Versiones[1].Etiqueta);
    }

    [Fact]
    public void Escaneado_y_texto_juntos()
    {
        string carpeta = @"C:\carpeta";

        var escaneado = CrearDocumento(
            Path.Combine(carpeta, "5521.pdf"),
            "5521.pdf",
            "huella1",
            "correcto",
            false,
            new[] { ("5521", "", "", "nombre") }
        );

        var conTexto = CrearDocumento(
            Path.Combine(carpeta, "5521_con_texto.pdf"),
            "5521_con_texto.pdf",
            "huella2",
            "correcto",
            true,
            new[] { ("5521", "", "", "nombre") }
        );

        var resultado = Agrupador.Agrupar([escaneado, conTexto], "5521");

        Assert.Single(resultado);
        Assert.Equal(2, resultado[0].Versiones.Count);
        Assert.Equal("original", resultado[0].Versiones[0].Etiqueta);
        Assert.Equal("escaneado", resultado[0].Versiones[1].Etiqueta);
    }

    [Fact]
    public void Tipos_distintos_separados()
    {
        string carpeta = @"C:\carpeta";

        var occ = CrearDocumento(
            Path.Combine(carpeta, "OCC104523.pdf"),
            "OCC104523.pdf",
            "huella1",
            "correcto",
            true,
            new[] { ("104523", "OCC", "", "nombre") }
        );

        var fcv = CrearDocumento(
            Path.Combine(carpeta, "FCV104523.pdf"),
            "FCV104523.pdf",
            "huella2",
            "correcto",
            true,
            new[] { ("104523", "FCV", "", "nombre") }
        );

        var resultado = Agrupador.Agrupar([occ, fcv], "104523");

        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, r => r.Tipo == "OCC");
        Assert.Contains(resultado, r => r.Tipo == "FCV");
    }

    [Fact]
    public void Lista_vacia()
    {
        var resultado = Agrupador.Agrupar([], "104523");
        Assert.Empty(resultado);
    }

    [Fact]
    public void Mas_reciente_primero()
    {
        string carpeta = @"C:\carpeta";

        var occAntiguo = CrearDocumento(
            Path.Combine(carpeta, "OCC104523_antiguo.pdf"),
            "OCC104523_antiguo.pdf",
            "huella1",
            "correcto",
            true,
            new[] { ("104523", "OCC", "", "nombre") },
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        );

        var fcvReciente = CrearDocumento(
            Path.Combine(carpeta, "FCV104523_reciente.pdf"),
            "FCV104523_reciente.pdf",
            "huella2",
            "correcto",
            true,
            new[] { ("104523", "FCV", "", "nombre") },
            new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        );

        var resultado = Agrupador.Agrupar([occAntiguo, fcvReciente], "104523");

        Assert.Equal(2, resultado.Count);
        Assert.Equal("FCV 104523", resultado[0].Titulo);
        Assert.Equal(
            new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            resultado[0].Modificado
        );
        Assert.Equal("OCC 104523", resultado[1].Titulo);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), resultado[1].Modificado);
    }

    [Fact]
    public void Version_sin_tipo_si_solo_en_texto()
    {
        string carpeta = @"C:\carpeta";

        var doc = CrearDocumento(
            Path.Combine(carpeta, "documento.pdf"),
            "documento.pdf",
            "huella1",
            "correcto",
            true,
            new[] { ("104523", "", "", "texto") }
        );

        var resultado = Agrupador.Agrupar([doc], "104523");

        Assert.Single(resultado);
        Assert.Equal("", resultado[0].Tipo);
        Assert.Equal("104523", resultado[0].Titulo);
    }

    [Fact]
    public void Orden_versiones_original_escaneado_cedible()
    {
        string carpeta = @"C:\carpeta";

        var cedible = CrearDocumento(
            Path.Combine(carpeta, "FCV25001_CEDIBLE.pdf"),
            "FCV25001_CEDIBLE.pdf",
            "huella3",
            "correcto",
            true,
            new[] { ("25001", "FCV", "CEDIBLE", "nombre") }
        );

        var original = CrearDocumento(
            Path.Combine(carpeta, "FCV25001.pdf"),
            "FCV25001.pdf",
            "huella1",
            "correcto",
            true,
            new[] { ("25001", "FCV", "", "nombre") }
        );

        var escaneado = CrearDocumento(
            Path.Combine(carpeta, "FCV25001_escaneado.pdf"),
            "FCV25001_escaneado.pdf",
            "huella2",
            "correcto",
            false,
            new[] { ("25001", "FCV", "", "nombre") }
        );

        var resultado = Agrupador.Agrupar([cedible, original, escaneado], "25001");

        Assert.Single(resultado);
        Assert.Equal(3, resultado[0].Versiones.Count);
        Assert.Equal("original", resultado[0].Versiones[0].Etiqueta);
        Assert.Equal("escaneado", resultado[0].Versiones[1].Etiqueta);
        Assert.Equal("cedible", resultado[0].Versiones[2].Etiqueta);
    }

    [Fact]
    public void Tipo_sale_del_numero_buscado()
    {
        var doc = CrearDocumento(
            @"C:\carpeta\GD777 OCC104523.pdf",
            "GD777 OCC104523.pdf",
            "huella1",
            "correcto",
            true,
            new[] { ("777", "GD", "", "nombre"), ("104523", "OCC", "", "nombre") }
        );

        var resultado = Agrupador.Agrupar([doc], "104523");

        Assert.Single(resultado);
        Assert.Equal("OCC", resultado[0].Tipo);
        Assert.Equal("104523", resultado[0].Numero);
    }
}
