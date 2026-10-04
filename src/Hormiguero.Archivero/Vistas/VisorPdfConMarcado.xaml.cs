using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Archivero.Datos;
using Archivero.Servicios.Pdf;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using UserControl = System.Windows.Controls.UserControl;
using TextBlock = System.Windows.Controls.TextBlock;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using Canvas = System.Windows.Controls.Canvas;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace Archivero.Vistas;

public partial class VisorPdfConMarcado : UserControl
{
    private string _rutaPdf = string.Empty;
    private int _paginaActual;
    private int _totalPaginas = 1;
    private int _anchoPaginaActual;
    private int _altoPaginaActual;
    private Point? _inicioArrastre;
    private Rectangle? _rectanguloArrastre;
    private List<(CampoMarca Campo, int Pagina, RectanguloFraccion Rect)> _marcasGuardadas = [];

    public event Action<int, RectanguloFraccion>? MarcaRealizada;

    public VisorPdfConMarcado()
    {
        InitializeComponent();
        ActualizarTextoZoom();
    }

    private void SliderZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var escala = e.NewValue / 100.0;
        EscalaZoom.ScaleX = escala;
        EscalaZoom.ScaleY = escala;
        ActualizarTextoZoom();
    }

    private void ActualizarTextoZoom()
    {
        if (TxtZoom is not null)
        {
            TxtZoom.Text = $"{SliderZoom.Value:0}%";
        }
    }

    public void CargarPdf(string rutaPdf)
    {
        _rutaPdf = rutaPdf;
        _paginaActual = 0;
        _totalPaginas = Math.Max(1, LectorPdf.ContarPaginas(rutaPdf));
        MostrarPaginaActual();
    }

    /// <summary>Reemplaza el conjunto de marcas ya confirmadas que se dibujan en la página (una por campo).</summary>
    public void MostrarMarcas(IEnumerable<(CampoMarca Campo, int Pagina, RectanguloFraccion Rect)> marcas)
    {
        _marcasGuardadas = marcas.ToList();
        RedibujarMarcasPersistentes();
    }

    private void MostrarPaginaActual()
    {
        var pagina = LectorPdf.RenderizarPagina(_rutaPdf, _paginaActual);
        _anchoPaginaActual = pagina.Ancho;
        _altoPaginaActual = pagina.Alto;

        var bitmap = BitmapSource.Create(
            pagina.Ancho, pagina.Alto, 96, 96, PixelFormats.Bgra32, null, pagina.PixelesBgra, pagina.Ancho * 4);
        bitmap.Freeze();

        ImagenPagina.Source = bitmap;
        LienzoPagina.Width = pagina.Ancho;
        LienzoPagina.Height = pagina.Alto;

        TxtPagina.Text = $"Página {_paginaActual + 1} de {_totalPaginas}";
        BtnAnterior.IsEnabled = _paginaActual > 0;
        BtnSiguiente.IsEnabled = _paginaActual < _totalPaginas - 1;

        RedibujarMarcasPersistentes();
    }

    private void RedibujarMarcasPersistentes()
    {
        CapaMarcas.Children.Clear();

        foreach (var (campo, pagina, rect) in _marcasGuardadas)
        {
            if (pagina != _paginaActual)
            {
                continue;
            }

            DibujarMarcaPersistente(campo, rect);
        }
    }

    private void DibujarMarcaPersistente(CampoMarca campo, RectanguloFraccion rect)
    {
        var color = ColorParaCampo(campo);
        var x = rect.X * _anchoPaginaActual;
        var y = rect.Y * _altoPaginaActual;
        var ancho = rect.Ancho * _anchoPaginaActual;
        var alto = rect.Alto * _altoPaginaActual;

        var rectangulo = new Rectangle
        {
            Width = ancho,
            Height = alto,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 3,
            Fill = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B))
        };
        Canvas.SetLeft(rectangulo, x);
        Canvas.SetTop(rectangulo, y);
        CapaMarcas.Children.Add(rectangulo);

        var etiqueta = new TextBlock
        {
            Text = NombreCampo(campo),
            Background = new SolidColorBrush(color),
            Foreground = Brushes.White,
            Padding = new Thickness(3, 1, 3, 1),
            FontSize = 12
        };
        Canvas.SetLeft(etiqueta, x);
        Canvas.SetTop(etiqueta, Math.Max(0, y - 18));
        CapaMarcas.Children.Add(etiqueta);
    }

    private static Color ColorParaCampo(CampoMarca campo) => campo switch
    {
        CampoMarca.Emisor => Colors.Crimson,
        CampoMarca.Tipo => Colors.DodgerBlue,
        CampoMarca.Fecha => Colors.SeaGreen,
        CampoMarca.NombreArchivo => Colors.DarkOrange,
        _ => Colors.Gray
    };

    private static string NombreCampo(CampoMarca campo) => campo switch
    {
        CampoMarca.Emisor => "Emisor",
        CampoMarca.Tipo => "Tipo",
        CampoMarca.Fecha => "Fecha",
        CampoMarca.NombreArchivo => "Nombre archivo",
        _ => campo.ToString()
    };

    private void BtnAnterior_Click(object sender, RoutedEventArgs e)
    {
        if (_paginaActual > 0)
        {
            _paginaActual--;
            MostrarPaginaActual();
        }
    }

    private void BtnSiguiente_Click(object sender, RoutedEventArgs e)
    {
        if (_paginaActual < _totalPaginas - 1)
        {
            _paginaActual++;
            MostrarPaginaActual();
        }
    }

    private void LienzoPagina_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ImagenPagina.Source is null)
        {
            return;
        }

        _inicioArrastre = e.GetPosition(LienzoPagina);
        LienzoPagina.CaptureMouse();

        _rectanguloArrastre = new Rectangle
        {
            Stroke = Brushes.DodgerBlue,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(60, 30, 144, 255))
        };
        CapaMarcas.Children.Add(_rectanguloArrastre);
    }

    private void LienzoPagina_MouseMove(object sender, MouseEventArgs e)
    {
        if (_inicioArrastre is not { } inicio || _rectanguloArrastre is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var actual = e.GetPosition(LienzoPagina);
        var x = Math.Min(inicio.X, actual.X);
        var y = Math.Min(inicio.Y, actual.Y);
        var ancho = Math.Abs(actual.X - inicio.X);
        var alto = Math.Abs(actual.Y - inicio.Y);

        Canvas.SetLeft(_rectanguloArrastre, x);
        Canvas.SetTop(_rectanguloArrastre, y);
        _rectanguloArrastre.Width = ancho;
        _rectanguloArrastre.Height = alto;
    }

    private void LienzoPagina_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_inicioArrastre is not { } inicio)
        {
            return;
        }

        LienzoPagina.ReleaseMouseCapture();
        var fin = e.GetPosition(LienzoPagina);
        _inicioArrastre = null;

        // El rectangulo de arrastre es siempre transitorio: si se confirma una marca, el
        // dueño del visor va a llamar a MostrarMarcas() y este redibujo lo reemplaza; si no
        // se confirma nada, no debe quedar una caja suelta en pantalla.
        if (_rectanguloArrastre is not null)
        {
            CapaMarcas.Children.Remove(_rectanguloArrastre);
            _rectanguloArrastre = null;
        }

        if (Math.Abs(fin.X - inicio.X) < 4 || Math.Abs(fin.Y - inicio.Y) < 4)
        {
            return;
        }

        var fraccion = ConvertirAFraccion(inicio, fin);
        if (fraccion is not null)
        {
            MarcaRealizada?.Invoke(_paginaActual, fraccion);
        }
    }

    private RectanguloFraccion? ConvertirAFraccion(Point inicio, Point fin)
    {
        if (_anchoPaginaActual <= 0 || _altoPaginaActual <= 0)
        {
            return null;
        }

        double NormalizarX(double x) => Math.Clamp(x / _anchoPaginaActual, 0, 1);
        double NormalizarY(double y) => Math.Clamp(y / _altoPaginaActual, 0, 1);

        var x0 = NormalizarX(Math.Min(inicio.X, fin.X));
        var x1 = NormalizarX(Math.Max(inicio.X, fin.X));
        var y0 = NormalizarY(Math.Min(inicio.Y, fin.Y));
        var y1 = NormalizarY(Math.Max(inicio.Y, fin.Y));

        return new RectanguloFraccion(x0, y0, x1 - x0, y1 - y0);
    }
}
