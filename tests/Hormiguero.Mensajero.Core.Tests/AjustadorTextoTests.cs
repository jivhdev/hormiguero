using Hormiguero.Mensajero.Core;

namespace Hormiguero.Mensajero.Core.Tests;

public sealed class AjustadorTextoTests
{
    [Fact]
    public void Ajusta_el_ejemplo_del_bloque_con_el_resultado_exacto()
    {
        const string texto =
            "PROYECTO LA FORESTA 2-\n"
            + "ENTREGAR EN AV  BOSQUE DE MONTEMAR CON LOS MEDANOS VIÑA DEL MAR\n"
            + "REF ENTRE SHELL Y COPEC\n"
            + "CONTACTO ALFREDO A 9 9275 6239\n"
            + "HORARIO 08:00 A 12:00 Y 14:00 A 17:00 LUNES A VIERNES";

        ResultadoAjusteTexto resultado = AjustadorTexto.Ajustar(texto, 4, 57);

        Assert.Equal(
            [
                "PROYECTO LA FORESTA 2- ENTREGAR EN AV BOSQUE DE MONTEMAR",
                "CON LOS MEDANOS VIÑA DEL MAR REF ENTRE SHELL Y COPEC",
                "CONTACTO ALFREDO A 9 9275 6239 HORARIO 08:00 A 12:00 Y",
                "14:00 A 17:00 LUNES A VIERNES",
            ],
            resultado.Lineas
        );
        Assert.True(resultado.Cabe);
    }

    [Fact]
    public void Conserva_los_saltos_si_cabe()
    {
        ResultadoAjusteTexto resultado = AjustadorTexto.Ajustar("AA BB\nCC", 2, 5);

        Assert.Equal(["AA BB", "CC"], resultado.Lineas);
    }

    [Fact]
    public void Une_los_renglones_si_asi_usa_menos_lineas()
    {
        ResultadoAjusteTexto resultado = AjustadorTexto.Ajustar("AA\nBB\nCC", 2, 5);

        Assert.Equal(["AA BB", "CC"], resultado.Lineas);
    }

    [Fact]
    public void Informa_el_texto_y_la_cantidad_que_queda_fuera()
    {
        ResultadoAjusteTexto resultado = AjustadorTexto.Ajustar("AA BB CC DD EE", 2, 5);

        Assert.Equal(["AA BB", "CC DD"], resultado.Lineas);
        Assert.Equal("EE", resultado.TextoFuera);
        Assert.Equal(2, resultado.CaracteresSobran);
    }

    [Fact]
    public void Avisa_si_una_palabra_supera_el_ancho_sin_cortarla()
    {
        ResultadoAjusteTexto resultado = AjustadorTexto.Ajustar(new string('X', 6), 2, 5);

        Assert.True(resultado.TienePalabraLarga);
        Assert.False(resultado.Cabe);
        Assert.Equal(new string('X', 6), resultado.TextoFuera);
        Assert.Equal(6, resultado.CaracteresSobran);
    }

    [Fact]
    public void Conserva_en_el_sobrante_la_palabra_larga_tras_agotar_las_lineas()
    {
        string palabra = new('X', 6);
        ResultadoAjusteTexto resultado = AjustadorTexto.Ajustar(
            $"AA BB CC EE FF DD {palabra}",
            2,
            5
        );

        Assert.Equal("FF DD " + palabra, resultado.TextoFuera);
        Assert.Equal(12, resultado.CaracteresSobran);
    }

    [Fact]
    public void Normaliza_espacios_dobles_y_extremos()
    {
        ResultadoAjusteTexto resultado = AjustadorTexto.Ajustar("  AA   BB  \n  CC   ", 2, 5);

        Assert.Equal(["AA BB", "CC"], resultado.Lineas);
    }

    [Fact]
    public void Cuenta_eñe_y_tildes_como_un_caracter()
    {
        ResultadoAjusteTexto resultado = AjustadorTexto.Ajustar("ñáé", 1, 3);

        Assert.Equal(["ñáé"], resultado.Lineas);
        Assert.True(resultado.Cabe);
    }

    [Fact]
    public void Rellena_cada_linea_al_ancho_sin_saltos()
    {
        ResultadoAjusteTexto resultado = AjustadorTexto.Ajustar("AA\nB", 2, 4);

        Assert.Equal("AA  B       ", resultado.Relleno(4, 3));
        Assert.Equal("AA" + Environment.NewLine + "B", resultado.ConSaltosDeLinea);
    }
}
