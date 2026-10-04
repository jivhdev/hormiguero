using Hormiguero.Nucleo.Pdf;

namespace Hormiguero.Nucleo.Tests;

public class ZonaPdfTests
{
    private static InfoPdf Info(params PalabraPdf[] palabras)
    {
        List<PalabraPdf> lista = [.. palabras];
        int paginas = lista.Count == 0 ? 1 : lista.Max(palabra => palabra.Pagina);
        var tieneTexto = Enumerable.Range(1, paginas).Select(_ => true).ToList();
        return new InfoPdf(EstadoPdf.Correcto, paginas, tieneTexto, lista);
    }

    private static PalabraPdf Palabra(
        string texto,
        double x,
        double y,
        double ancho,
        double alto,
        int pagina = 1
    ) => new(texto, pagina, x, y, ancho, alto);

    [Fact]
    public void Toma_solo_las_palabras_dentro()
    {
        InfoPdf info = Info(Palabra("dentro", 10, 10, 20, 10), Palabra("fuera", 100, 100, 20, 10));
        var zona = new Zona(1, 0, 0, 50, 50);

        Assert.Equal("dentro", ZonaPdf.Texto(info, zona));
    }

    [Fact]
    public void Ordena_por_lineas_de_arriba_hacia_abajo()
    {
        InfoPdf info = Info(
            Palabra("abajo", 10, 0, 10, 10),
            Palabra("derecha", 60, 40, 10, 10),
            Palabra("izquierda", 10, 40, 10, 10)
        );
        var zona = new Zona(1, 0, 0, 100, 100);

        Assert.Equal("izquierda derecha abajo", ZonaPdf.Texto(info, zona));
    }

    [Fact]
    public void Palabra_cortada_por_el_borde_cuenta_por_su_centro()
    {
        InfoPdf info = Info(Palabra("cortada", 40, 40, 20, 10), Palabra("fuera", 90, 40, 20, 10));
        var zona = new Zona(1, 0, 0, 50, 50);

        Assert.Equal("cortada", ZonaPdf.Texto(info, zona));
    }

    [Fact]
    public void Otra_pagina_no_cuenta()
    {
        InfoPdf info = Info(
            Palabra("pagina1", 10, 10, 10, 10),
            Palabra("pagina2", 10, 10, 10, 10, pagina: 2)
        );
        var zona = new Zona(1, 0, 0, 50, 50);

        Assert.Equal("pagina1", ZonaPdf.Texto(info, zona));
    }

    [Fact]
    public void Zona_vacia_devuelve_texto_vacio()
    {
        InfoPdf info = Info(Palabra("lejos", 200, 200, 10, 10));
        var zona = new Zona(1, 0, 0, 50, 50);

        Assert.Equal("", ZonaPdf.Texto(info, zona));
    }

    [Fact]
    public void Texto_en_fraccion_convierte_desde_arriba_a_la_izquierda()
    {
        InfoPdf info = Info(
            Palabra("arriba", 10, 80, 20, 10),
            Palabra("abajo", 80, 10, 20, 10)
        ) with
        {
            TamanosPagina = [(100, 100)],
        };

        Assert.Equal("arriba", ZonaPdf.TextoEnFraccion(info, 0, 0, 0, 0.5, 0.5));
        Assert.Equal("abajo", ZonaPdf.TextoEnFraccion(info, 0, 0.5, 0.5, 0.5, 0.5));
    }

    [Fact]
    public void Texto_en_fraccion_de_pagina_inexistente_devuelve_vacio()
    {
        InfoPdf info = Info() with { TamanosPagina = [(100, 100)] };

        Assert.Equal("", ZonaPdf.TextoEnFraccion(info, 1, 0, 0, 1, 1));
    }

    [Fact]
    public void Zona_sin_tamano_lanza_error()
    {
        InfoPdf info = Info();

        Assert.Throws<ArgumentNullException>(() => ZonaPdf.Texto(null!, new Zona(1, 0, 0, 10, 10)));
        Assert.Throws<ArgumentNullException>(() => ZonaPdf.Texto(info, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ZonaPdf.Texto(info, new Zona(1, 0, 0, 0, 10))
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ZonaPdf.Texto(info, new Zona(1, 0, 0, 10, 0))
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ZonaPdf.Texto(info, new Zona(1, 0, 0, -5, 10))
        );
    }

    [Fact]
    public void Fraccion_sin_tamano_devuelve_texto_vacio()
    {
        var info = new InfoPdf(EstadoPdf.Correcto, 1, [true], []) { TamanosPagina = [(595, 842)] };

        Assert.Equal("", ZonaPdf.TextoEnFraccion(info, 0, 0.1, 0.1, 0, 0.2));
    }
}
