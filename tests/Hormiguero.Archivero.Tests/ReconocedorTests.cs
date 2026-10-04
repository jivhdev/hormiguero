using Hormiguero.Archivero.Logica;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Pdf;

namespace Hormiguero.Archivero.Tests;

public class ReconocedorTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 4);

    // Zonas de prueba: tipo arriba, emisor al medio, número y fecha abajo.
    private static readonly Zona ZonaTipo = new(1, 0, 700, 300, 50);
    private static readonly Zona ZonaEmisor = new(1, 0, 600, 300, 50);
    private static readonly Zona ZonaNumero = new(1, 0, 500, 300, 50);
    private static readonly Zona ZonaFecha = new(1, 0, 400, 300, 50);

    private static PalabraPdf En(string texto, double y, double x = 10) =>
        new(texto, 1, x, y, 40, 10);

    private static InfoPdf Pdf(params PalabraPdf[] palabras) =>
        new(EstadoPdf.Correcto, 1, [true], palabras);

    private static InfoPdf Factura(string numero = "000104523", string? fecha = null)
    {
        var palabras = new List<PalabraPdf>
        {
            En("FACTURA", 720),
            En("ELECTRONICA", 720, 60),
            En("Proveedor", 620),
            En("Uno", 620, 60),
            En("N°", 520),
            En(numero, 520, 60),
        };
        if (fecha is not null)
        {
            palabras.Add(En(fecha, 420));
        }
        return Pdf([.. palabras]);
    }

    private static Identificacion Config(
        long id = 1,
        string textoTipo = "FACTURA ELECTRONICA",
        string textoEmisor = "Proveedor Uno",
        Zona? zonaFecha = null
    ) =>
        new(
            id,
            "Factura",
            "Proveedor Uno " + id,
            new ConfiguracionArchivo(
                ZonaTipo,
                textoTipo,
                ZonaEmisor,
                textoEmisor,
                ZonaNumero,
                zonaFecha,
                new ReglaDestino(@"C:\Docs", FormaCarpeta.AnioYMes, [ParteNombre.Numero], "")
            ).AJson()
        );

    [Fact]
    public void Reconoce_cuando_todas_las_zonas_coinciden()
    {
        Reconocimiento r = Reconocedor.Reconocer(Factura(), "scan.pdf", [Config()], Hoy);

        Assert.Equal(ResultadoReconocimiento.Reconocido, r.Resultado);
        Assert.Equal("000104523", r.Datos!.Numero);
        Assert.Equal(Hoy, r.Datos.Fecha);
        Assert.False(r.Datos.EsCedible);
    }

    [Fact]
    public void Mayusculas_y_espacios_no_importan()
    {
        Reconocimiento r = Reconocedor.Reconocer(
            Factura(),
            "scan.pdf",
            [Config(textoTipo: "factura   electronica")],
            Hoy
        );

        Assert.Equal(ResultadoReconocimiento.Reconocido, r.Resultado);
    }

    [Fact]
    public void Coincidencia_parcial_no_se_archiva()
    {
        Reconocimiento r = Reconocedor.Reconocer(
            Factura(),
            "scan.pdf",
            [Config(textoEmisor: "Proveedor Dos")],
            Hoy
        );

        Assert.Equal(ResultadoReconocimiento.PorReconocer, r.Resultado);
    }

    [Fact]
    public void Dos_configuraciones_iguales_no_se_archiva()
    {
        Reconocimiento r = Reconocedor.Reconocer(
            Factura(),
            "scan.pdf",
            [Config(1), Config(2)],
            Hoy
        );

        Assert.Equal(ResultadoReconocimiento.PorReconocer, r.Resultado);
        Assert.Equal("Coincide con dos configuraciones", r.Detalle);
    }

    [Fact]
    public void Sin_numero_queda_por_reconocer()
    {
        Reconocimiento r = Reconocedor.Reconocer(Factura("S/N"), "scan.pdf", [Config()], Hoy);

        Assert.Equal(ResultadoReconocimiento.PorReconocer, r.Resultado);
        Assert.Equal("No se encontró el número", r.Detalle);
    }

    [Fact]
    public void Sin_texto_o_danado_va_a_guardar_a_mano()
    {
        var escaneado = new InfoPdf(EstadoPdf.Correcto, 1, [false], []);
        var danado = new InfoPdf(EstadoPdf.Danado, 0, [], []);

        Assert.Equal(
            ResultadoReconocimiento.SinTexto,
            Reconocedor.Reconocer(escaneado, "a.pdf", [Config()], Hoy).Resultado
        );
        Assert.Equal("PDF dañado", Reconocedor.Reconocer(danado, "a.pdf", [Config()], Hoy).Detalle);
    }

    [Fact]
    public void La_fecha_sale_de_su_zona()
    {
        Reconocimiento r = Reconocedor.Reconocer(
            Factura(fecha: "15-09-2026"),
            "scan.pdf",
            [Config(zonaFecha: ZonaFecha)],
            Hoy
        );

        Assert.Equal(new DateOnly(2026, 9, 15), r.Datos!.Fecha);
    }

    [Fact]
    public void Fecha_ilegible_no_se_archiva()
    {
        Reconocimiento r = Reconocedor.Reconocer(
            Factura(fecha: "pronto"),
            "scan.pdf",
            [Config(zonaFecha: ZonaFecha)],
            Hoy
        );

        Assert.Equal(ResultadoReconocimiento.PorReconocer, r.Resultado);
        Assert.Equal("No se pudo leer la fecha", r.Detalle);
    }

    [Fact]
    public void Cedible_por_el_nombre_o_por_el_texto()
    {
        InfoPdf conPalabra = Pdf([.. Factura().Palabras, En("CEDIBLE", 300)]);

        Assert.True(
            Reconocedor.Reconocer(Factura(), "F25001 cedible.pdf", [Config()], Hoy).Datos!.EsCedible
        );
        Assert.True(
            Reconocedor.Reconocer(conPalabra, "scan.pdf", [Config()], Hoy).Datos!.EsCedible
        );
    }

    [Theory]
    [InlineData("04-10-2026")]
    [InlineData("4/10/2026")]
    [InlineData("2026-10-04")]
    [InlineData("Fecha: 04.10.2026")]
    [InlineData("4 de octubre de 2026")]
    public void Lee_fechas_en_formatos_comunes(string texto)
    {
        Assert.Equal(new DateOnly(2026, 10, 4), Reconocedor.LeerFecha(texto));
    }

    [Fact]
    public void La_configuracion_va_y_vuelve_en_json()
    {
        var original = ConfiguracionArchivo.DeJson(Config(zonaFecha: ZonaFecha).Datos);

        var copia = ConfiguracionArchivo.DeJson(original.AJson());

        Assert.Equal(original.ZonaFecha, copia.ZonaFecha);
        Assert.Equal(original.Destino.Partes, copia.Destino.Partes);
        Assert.Equal(original.TextoEmisor, copia.TextoEmisor);
    }
}
