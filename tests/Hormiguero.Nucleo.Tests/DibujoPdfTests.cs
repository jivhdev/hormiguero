using Hormiguero.Nucleo.Pdf;

namespace Hormiguero.Nucleo.Tests;

public class DibujoPdfTests
{
    [Fact]
    public void Dibuja_la_primera_pagina()
    {
        byte[] pdf = PdfDePrueba.ConTexto();

        ImagenPagina imagen = DibujoPdf.Dibujar(pdf, pagina: 0, zoom: 1.0);

        // A4 (595 x 842 puntos) a 96 ppp: sale vertical, con fondo blanco y el texto dibujado.
        Assert.True(imagen.Ancho > 0 && imagen.Alto > 0 && imagen.Ancho < imagen.Alto);
        Assert.Equal(imagen.Ancho * imagen.Alto * 4, imagen.PixelesBgra.Length);
        Assert.Equal([255, 255, 255, 255], imagen.PixelesBgra[..4]);
        Assert.True(HayAlgoDibujado(imagen));
    }

    [Fact]
    public void Zoom_doble_duplica_el_tamano()
    {
        byte[] pdf = PdfDePrueba.ConTexto();

        ImagenPagina al100 = DibujoPdf.Dibujar(pdf, pagina: 0, zoom: 1.0);
        ImagenPagina al200 = DibujoPdf.Dibujar(pdf, pagina: 0, zoom: 2.0);

        Assert.InRange(al200.Ancho, al100.Ancho * 2 - 1, al100.Ancho * 2 + 1);
        Assert.InRange(al200.Alto, al100.Alto * 2 - 1, al100.Alto * 2 + 1);
    }

    [Fact]
    public void Respeta_la_rotacion()
    {
        ImagenPagina sinRotar = DibujoPdf.Dibujar(PdfDePrueba.ConTexto(), pagina: 0, zoom: 1.0);
        ImagenPagina rotada = DibujoPdf.Dibujar(PdfDePrueba.Rotada90(), pagina: 0, zoom: 1.0);

        // Rotada 90 grados: ancho y alto salen intercambiados respecto de la página sin rotar.
        Assert.InRange(rotada.Ancho, sinRotar.Alto - 1, sinRotar.Alto + 1);
        Assert.InRange(rotada.Alto, sinRotar.Ancho - 1, sinRotar.Ancho + 1);
    }

    [Fact]
    public void Pagina_fuera_de_rango_falla_claro()
    {
        byte[] pdf = PdfDePrueba.ConTexto();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DibujoPdf.Dibujar(pdf, pagina: 1, zoom: 1.0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DibujoPdf.Dibujar(pdf, pagina: -1, zoom: 1.0)
        );
    }

    [Fact]
    public async Task Dos_dibujos_a_la_vez()
    {
        byte[] pdf = PdfDePrueba.SinTexto();

        Task<ImagenPagina> primero = Task.Run(() => DibujoPdf.Dibujar(pdf, pagina: 0, zoom: 1.0));
        Task<ImagenPagina> segundo = Task.Run(() => DibujoPdf.Dibujar(pdf, pagina: 1, zoom: 2.0));

        ImagenPagina[] imagenes = await Task.WhenAll(primero, segundo);

        Assert.All(imagenes, imagen => Assert.True(imagen.Ancho > 0 && imagen.Alto > 0));
    }

    private static bool HayAlgoDibujado(ImagenPagina imagen)
    {
        byte[] pixeles = imagen.PixelesBgra;
        for (int i = 0; i < pixeles.Length; i += 4)
        {
            if (pixeles[i] != 255 || pixeles[i + 1] != 255 || pixeles[i + 2] != 255)
            {
                return true;
            }
        }

        return false;
    }
}
