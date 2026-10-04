using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Hormiguero.Nucleo.Pdf;

namespace Hormiguero.Archivero;

public sealed partial class VisorZonas : UserControl
{
    private byte[]? documento;
    private int totalPaginas;
    private double zoom = 1.0;
    private int solicitud;
    private bool dibujando;
    private int altoImagen;
    private (Zona Zona, string Etiqueta)[] zonas = [];
    private Point inicio;
    private Rectangle? seleccion;

    public event EventHandler<Zona>? ZonaMarcada;

    public int PaginaActual { get; private set; } = 1;

    public VisorZonas() => InitializeComponent();

    public void Mostrar(byte[] pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        documento = pdf.ToArray();
        totalPaginas = 0;
        PaginaActual = 1;
        zoom = 1.0;
        zonas = [];
        Redibujar();
    }

    public static Zona APuntos(int pagina, Rect pixeles, double zoom, int altoImagenPx)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pagina, 1);
        ValidarEscala(zoom, altoImagenPx);
        if (
            pixeles.IsEmpty
            || !double.IsFinite(pixeles.X)
            || !double.IsFinite(pixeles.Y)
            || !double.IsFinite(pixeles.Width)
            || !double.IsFinite(pixeles.Height)
        )
        {
            throw new ArgumentOutOfRangeException(nameof(pixeles));
        }
        double escala = 72.0 / (96.0 * zoom);
        double altoPaginaPuntos = altoImagenPx * escala;
        return new Zona(
            pagina,
            pixeles.Left * escala,
            altoPaginaPuntos - (pixeles.Top + pixeles.Height) * escala,
            pixeles.Width * escala,
            pixeles.Height * escala
        );
    }

    public static Rect APixeles(Zona zona, double zoom, int altoImagenPx)
    {
        ArgumentNullException.ThrowIfNull(zona);
        ValidarEscala(zoom, altoImagenPx);
        ArgumentOutOfRangeException.ThrowIfLessThan(zona.Pagina, 1);
        if (
            !double.IsFinite(zona.X)
            || !double.IsFinite(zona.Y)
            || !double.IsFinite(zona.Ancho)
            || !double.IsFinite(zona.Alto)
            || zona.Ancho < 0
            || zona.Alto < 0
        )
        {
            throw new ArgumentOutOfRangeException(nameof(zona));
        }
        double escala = 72.0 / (96.0 * zoom);
        return new Rect(
            zona.X / escala,
            altoImagenPx - (zona.Y + zona.Alto) / escala,
            zona.Ancho / escala,
            zona.Alto / escala
        );
    }

    private static void ValidarEscala(double zoom, int altoImagenPx)
    {
        if (!double.IsFinite(zoom) || zoom <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zoom));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(altoImagenPx);
    }

    public void MostrarZonas(IEnumerable<(Zona Zona, string Etiqueta)> zonas)
    {
        ArgumentNullException.ThrowIfNull(zonas);
        var nuevas = zonas.ToArray();
        foreach (var (zona, _) in nuevas)
        {
            _ = APixeles(zona, 1.0, 1);
        }
        this.zonas = nuevas;
        DibujarZonas();
    }

    private async void Redibujar()
    {
        if (documento is not byte[] pdf)
        {
            return;
        }
        int actual = ++solicitud;
        int pagina = PaginaActual;
        double escala = zoom;
        CancelarSeleccion();
        dibujando = true;
        Pagina.Source = null;
        altoImagen = 0;
        Lienzo.Width = Lienzo.Height = 0;
        Lienzo.Children.Clear();
        ActualizarBarra();
        try
        {
            var resultado = await Task.Run(() =>
            {
                int cantidad = DibujoPdf.Paginas(pdf);
                var imagen = DibujoPdf.Dibujar(pdf, pagina - 1, escala);
                var mapa = BitmapSource.Create(
                    imagen.Ancho,
                    imagen.Alto,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    imagen.PixelesBgra,
                    imagen.Ancho * 4
                );
                mapa.Freeze();
                return (cantidad, imagen, mapa);
            });
            // Un documento posterior puede terminar de dibujarse antes que este.
            if (actual != solicitud)
            {
                return;
            }
            totalPaginas = resultado.cantidad;
            altoImagen = resultado.imagen.Alto;
            Pagina.Source = resultado.mapa;
            Lienzo.Width = resultado.imagen.Ancho;
            Lienzo.Height = altoImagen;
            dibujando = false;
            DibujarZonas();
            ActualizarBarra();
        }
        catch (Exception error)
        {
            if (actual == solicitud)
            {
                documento = null;
                totalPaginas = 0;
                dibujando = false;
                ActualizarBarra();
                Trace.TraceError("{0}", error);
            }
        }
    }

    private void ActualizarBarra()
    {
        bool disponible = documento is not null && !dibujando;
        BtnZoomMenos.IsEnabled = disponible && zoom > 0.5;
        BtnZoomMas.IsEnabled = disponible && zoom < 2.0;
        BtnPaginaAnterior.IsEnabled = disponible && PaginaActual > 1;
        BtnPaginaSiguiente.IsEnabled = disponible && PaginaActual < totalPaginas;
        TxtZoom.Text = $"{zoom * 100:0} %";
        TxtPagina.Text = totalPaginas > 0 ? $"Página {PaginaActual} de {totalPaginas}" : "";
    }

    private void ZoomMenos_Click(object sender, RoutedEventArgs e) => CambiarZoom(-0.25);

    private void ZoomMas_Click(object sender, RoutedEventArgs e) => CambiarZoom(0.25);

    private void CambiarZoom(double cambio)
    {
        if (documento is null || dibujando)
        {
            return;
        }
        zoom = Math.Clamp(zoom + cambio, 0.5, 2.0);
        Redibujar();
    }

    private void PaginaAnterior_Click(object sender, RoutedEventArgs e)
    {
        if (documento is not null && !dibujando && PaginaActual > 1)
        {
            PaginaActual--;
            Redibujar();
        }
    }

    private void PaginaSiguiente_Click(object sender, RoutedEventArgs e)
    {
        if (documento is not null && !dibujando && PaginaActual < totalPaginas)
        {
            PaginaActual++;
            Redibujar();
        }
    }

    private void DibujarZonas()
    {
        CancelarSeleccion();
        Lienzo.Children.Clear();
        if (altoImagen == 0)
        {
            return;
        }
        foreach (var (zona, etiqueta) in zonas)
        {
            if (zona.Pagina != PaginaActual)
            {
                continue;
            }
            Rect pixeles = APixeles(zona, zoom, altoImagen);
            var rectangulo = new Rectangle { StrokeThickness = 2, IsHitTestVisible = false };
            rectangulo.SetResourceReference(Shape.StrokeProperty, "Hormiguero.Principal");
            Ubicar(rectangulo, pixeles);
            Lienzo.Children.Add(rectangulo);
            var texto = new TextBlock { Text = etiqueta, IsHitTestVisible = false };
            texto.SetResourceReference(TextBlock.ForegroundProperty, "Hormiguero.Texto");
            texto.SetResourceReference(TextBlock.FontSizeProperty, "Hormiguero.TextoNormal");
            texto.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(texto, pixeles.Left);
            Canvas.SetTop(texto, pixeles.Top - texto.DesiredSize.Height);
            Lienzo.Children.Add(texto);
        }
    }

    private Point Posicion(MouseEventArgs e)
    {
        Point punto = e.GetPosition(Lienzo);
        return new Point(
            Math.Clamp(punto.X, 0, Lienzo.Width),
            Math.Clamp(punto.Y, 0, Lienzo.Height)
        );
    }

    private void Lienzo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (altoImagen == 0 || dibujando)
        {
            return;
        }
        CancelarSeleccion();
        if (!Lienzo.CaptureMouse())
        {
            return;
        }
        inicio = Posicion(e);
        seleccion = new Rectangle { StrokeThickness = 2, IsHitTestVisible = false };
        seleccion.SetResourceReference(Shape.StrokeProperty, "Hormiguero.Acento");
        var relleno = new Rectangle { Opacity = 0.2, IsHitTestVisible = false };
        relleno.SetResourceReference(Shape.FillProperty, "Hormiguero.Acento");
        // El relleno comparte la geometría sin atenuar el borde.
        seleccion.Tag = relleno;
        Lienzo.Children.Add(relleno);
        Lienzo.Children.Add(seleccion);
        ActualizarSeleccion(new Rect(inicio, inicio));
        e.Handled = true;
    }

    private static void Ubicar(Rectangle rectangulo, Rect pixeles)
    {
        Canvas.SetLeft(rectangulo, pixeles.Left);
        Canvas.SetTop(rectangulo, pixeles.Top);
        rectangulo.Width = pixeles.Width;
        rectangulo.Height = pixeles.Height;
    }

    private void ActualizarSeleccion(Rect pixeles)
    {
        if (seleccion is not null)
        {
            Ubicar(seleccion, pixeles);
            Ubicar((Rectangle)seleccion.Tag, pixeles);
        }
    }

    private void Lienzo_MouseMove(object sender, MouseEventArgs e)
    {
        if (seleccion is null)
        {
            return;
        }
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            CancelarSeleccion();
            return;
        }
        ActualizarSeleccion(new Rect(inicio, Posicion(e)));
        e.Handled = true;
    }

    private void Lienzo_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (seleccion is null)
        {
            return;
        }
        Rect pixeles = new(inicio, Posicion(e));
        CancelarSeleccion();
        e.Handled = true;
        if (pixeles.Width >= 4 && pixeles.Height >= 4)
        {
            ZonaMarcada?.Invoke(this, APuntos(PaginaActual, pixeles, zoom, altoImagen));
        }
    }

    private void Lienzo_LostMouseCapture(object sender, MouseEventArgs e) => CancelarSeleccion();

    private void CancelarSeleccion()
    {
        if (seleccion is not null)
        {
            Lienzo.Children.Remove((Rectangle)seleccion.Tag);
            Lienzo.Children.Remove(seleccion);
            seleccion = null;
        }
        if (Lienzo.IsMouseCaptured)
        {
            Lienzo.ReleaseMouseCapture();
        }
    }
}
