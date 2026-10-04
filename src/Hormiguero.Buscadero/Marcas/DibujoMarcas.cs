using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Buscadero.Core.Marcas;

namespace Buscadero.App.Marcas;

public static class DibujoMarcas
{
    public static double TamanoFuente(double altoPagina) => Math.Max(12, altoPagina * 0.016);

    public static UIElement? CrearForma(Marca marca, double ancho, double alto)
    {
        var x = marca.X * ancho;
        var y = marca.Y * alto;
        var w = marca.Ancho * ancho;
        var h = marca.Alto * alto;
        var color = marca.Tipo == TipoMarca.Raya ? Brushes.Green : Brushes.Black;

        switch (marca.Tipo)
        {
            case TipoMarca.Raya:
                return new Line
                {
                    X1 = x,
                    Y1 = y,
                    X2 = x + w,
                    Y2 = y + h,
                    Stroke = color,
                    StrokeThickness = 2,
                    IsHitTestVisible = false,
                };

            case TipoMarca.Circulo:
            {
                var elipse = new Ellipse
                {
                    Width = w,
                    Height = h,
                    Stroke = color,
                    StrokeThickness = 2,
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(elipse, x);
                Canvas.SetTop(elipse, y);
                return elipse;
            }

            case TipoMarca.Tick:
            {
                var polilinea = new Polyline
                {
                    Stroke = color,
                    StrokeThickness = 2,
                    IsHitTestVisible = false,
                };
                polilinea.Points.Add(new Point(x, y + (h * 0.55)));
                polilinea.Points.Add(new Point(x + (w * 0.35), y + h));
                polilinea.Points.Add(new Point(x + w, y));
                return polilinea;
            }

            case TipoMarca.Equis:
            {
                var contenedor = new Canvas
                {
                    Width = w,
                    Height = h,
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(contenedor, x);
                Canvas.SetTop(contenedor, y);
                contenedor.Children.Add(
                    new Line
                    {
                        X1 = 0,
                        Y1 = 0,
                        X2 = w,
                        Y2 = h,
                        Stroke = color,
                        StrokeThickness = 2,
                    }
                );
                contenedor.Children.Add(
                    new Line
                    {
                        X1 = w,
                        Y1 = 0,
                        X2 = 0,
                        Y2 = h,
                        Stroke = color,
                        StrokeThickness = 2,
                    }
                );
                return contenedor;
            }

            case TipoMarca.Texto:
            {
                var etiqueta = new TextBlock
                {
                    Text = marca.Texto ?? string.Empty,
                    Foreground = color,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = TamanoFuente(alto),
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(etiqueta, x);
                Canvas.SetTop(etiqueta, y);
                return etiqueta;
            }
        }

        return null;
    }
}
