using Archivero.Datos;
using Archivero.Servicios;

namespace Archivero.Tests;

/// <summary>
/// Caso-9, mejora 1. Estos tests describen el comportamiento pedido y tienen que fallar contra
/// el esqueleto sin implementar (ValidadorRutaService no valida nada todavía) -- recién pasan
/// después de escribir la lógica real.
/// </summary>
public class ValidadorRutaServiceTests
{
    // ----- a) Caracteres de control y nulos: se rechaza, nunca se sanea -----

    [Theory]
    [InlineData("Factura\u0000Falsa")]
    [InlineData("Factura\u0001Falsa")]
    [InlineData("Factura\u001FFalsa")]
    [InlineData("Factura\u007FFalsa")]
    [InlineData("Factura\nFalsa")]
    [InlineData("Factura\rFalsa")]
    public void ValidarYSanearSegmento_ConCaracterDeControlOByteNulo_Rechaza(string texto)
    {
        var ex = Assert.Throws<ValidacionSeguridadException>(() => ValidadorRutaService.ValidarYSanearSegmento(texto));

        Assert.Equal(MotivoPendiente.TextoConCaracteresInvalidos, ex.Motivo);
    }

    // ----- b) Caracteres inválidos de Windows: se sanea, salvo nombres reservados -----

    [Theory]
    [InlineData("Factura<1>.pdf", "Factura_1_.pdf")]
    [InlineData("Reporte: Ventas", "Reporte_ Ventas")]
    [InlineData("A\"B/C\\D|E?F*G", "A_B_C_D_E_F_G")]
    public void ValidarYSanearSegmento_ConCaracteresInvalidosDeWindows_LosReemplazaPorGuionBajo(string texto, string esperado)
    {
        var resultado = ValidadorRutaService.ValidarYSanearSegmento(texto);

        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("com9")]
    [InlineData("LPT1")]
    [InlineData("lpt9")]
    [InlineData("CON.pdf")]
    [InlineData("con.txt")]
    public void ValidarYSanearSegmento_ConNombreReservadoDeWindows_Rechaza(string texto)
    {
        var ex = Assert.Throws<ValidacionSeguridadException>(() => ValidadorRutaService.ValidarYSanearSegmento(texto));

        Assert.Equal(MotivoPendiente.NombreReservadoPorWindows, ex.Motivo);
    }

    [Fact]
    public void ValidarYSanearSegmento_ConNombreParecidoAUnReservadoPeroNoExacto_NoRechaza()
    {
        // "CONTRATO" no es "CON": el nombre reservado se compara exacto, no como prefijo.
        var resultado = ValidadorRutaService.ValidarYSanearSegmento("CONTRATO.pdf");

        Assert.Equal("CONTRATO.pdf", resultado);
    }

    // ----- c) Caracteres válidos: no se tocan -----

    [Theory]
    [InlineData("Factura (1).pdf")]
    [InlineData("Recibo & Comprobante.pdf")]
    [InlineData("Guía N° 123-456_v2.pdf")]
    [InlineData("Orden #45 [urgente] {rev}.pdf")]
    [InlineData("50% Descuento + IVA = Total.pdf")]
    public void ValidarYSanearSegmento_ConCaracteresValidos_NoLosToca(string texto)
    {
        var resultado = ValidadorRutaService.ValidarYSanearSegmento(texto);

        Assert.Equal(texto, resultado);
    }

    // ----- d) Espacios y puntos sobrantes -----

    [Theory]
    [InlineData("  Factura.pdf", "Factura.pdf")]
    [InlineData("Factura.pdf   ", "Factura.pdf")]
    [InlineData("Factura...", "Factura")]
    [InlineData("  Factura  ", "Factura")]
    public void ValidarYSanearSegmento_ConEspaciosOPuntosSobrantes_LosQuita(string texto, string esperado)
    {
        var resultado = ValidadorRutaService.ValidarYSanearSegmento(texto);

        Assert.Equal(esperado, resultado);
    }

    // ----- e) Path traversal: contención por segmentos, nunca por substring -----

    [Fact]
    public void ValidarContenidaEnCarpeta_ConRutaRealmenteDentro_NoRechaza()
    {
        ValidadorRutaService.ValidarContenidaEnCarpeta(@"C:\Docs\Foo\archivo.pdf", @"C:\Docs\Foo");
    }

    [Fact]
    public void ValidarContenidaEnCarpeta_ConCarpetaQueSoloComparteElPrefijoDeTexto_Rechaza()
    {
        // "C:\Docs\FooBar" NO es "C:\Docs\Foo" solo porque el texto empieza igual.
        var ex = Assert.Throws<ValidacionSeguridadException>(() =>
            ValidadorRutaService.ValidarContenidaEnCarpeta(@"C:\Docs\FooBar\archivo.pdf", @"C:\Docs\Foo"));

        Assert.Equal(MotivoPendiente.RutaFueraDeCarpetaConfigurada, ex.Motivo);
    }

    [Fact]
    public void ValidarContenidaEnCarpeta_ConRutaQueEscapaHaciaArriba_Rechaza()
    {
        var rutaResuelta = Path.GetFullPath(@"C:\Docs\Foo\..\..\Windows\evil.pdf");

        var ex = Assert.Throws<ValidacionSeguridadException>(() =>
            ValidadorRutaService.ValidarContenidaEnCarpeta(rutaResuelta, @"C:\Docs\Foo"));

        Assert.Equal(MotivoPendiente.RutaFueraDeCarpetaConfigurada, ex.Motivo);
    }

    [Fact]
    public void ValidarContenidaEnCarpeta_ConLaCarpetaMismaSinArchivo_NoRechaza()
    {
        ValidadorRutaService.ValidarContenidaEnCarpeta(@"C:\Docs\Foo", @"C:\Docs\Foo");
    }

    // ----- f) Largos máximos -----

    [Fact]
    public void ValidarLargos_ConNombreDeMasDe200Caracteres_Rechaza()
    {
        var nombreLargo = new string('a', 201);

        var ex = Assert.Throws<ValidacionSeguridadException>(() =>
            ValidadorRutaService.ValidarLargos(nombreLargo, @"C:\Docs\" + nombreLargo + ".pdf"));

        Assert.Equal(MotivoPendiente.NombreORutaDemasiadoLarga, ex.Motivo);
    }

    [Fact]
    public void ValidarLargos_ConNombreDeExactamente200Caracteres_NoRechaza()
    {
        var nombre = new string('a', 200);

        ValidadorRutaService.ValidarLargos(nombre, @"C:\Docs\" + nombre + ".pdf");
    }

    [Fact]
    public void ValidarLargos_ConRutaCompletaDeMasDe240Caracteres_Rechaza()
    {
        var rutaLarga = @"C:\" + new string('a', 240);

        var ex = Assert.Throws<ValidacionSeguridadException>(() =>
            ValidadorRutaService.ValidarLargos("nombre", rutaLarga));

        Assert.Equal(MotivoPendiente.NombreORutaDemasiadoLarga, ex.Motivo);
    }
}
