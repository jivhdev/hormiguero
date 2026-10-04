using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Buscadero.App.Marcas;
using Buscadero.App.Pdf;
using Buscadero.Core.Marcas;

namespace Buscadero.App;

public partial class VisorComparacion : UserControl
{
    private const double ZoomMinimo = 0.1;
    private const double ZoomMaximo = 5.0;
    private const double PasoZoom = 1.25;
    private const double TamanoFija = 0.024;

    private readonly IReadOnlyList<Marca> _vacio = Array.Empty<Marca>();

    private RepositorioMarcas? _repositorioMarcas;
    private SesionMarcas? _sesionMarcas;
    private bool _esEditable;

    private string? _ruta;
    private int _paginaActual;
    private int _totalPaginas;
    private double _zoom = 1.0;
    private IReadOnlyList<Marca> _marcas = Array.Empty<Marca>();

    private bool _arrastrando;
    private Point _inicioArrastre;
    private Point _finArrastre;

    public VisorComparacion()
    {
        InitializeComponent();
    }

    public void Abrir(string ruta, RepositorioMarcas repositorioMarcas, bool editable = false)
    {
        _repositorioMarcas = repositorioMarcas;
        _esEditable = editable;
        PanelMarcas.Visibility = editable ? Visibility.Visible : Visibility.Collapsed;

        try
        {
            _totalPaginas = VisorPdf.ObtenerTotalPaginas(ruta);
            _ruta = ruta;
            _paginaActual = 0;
            _zoom = 1.0;
            _sesionMarcas = editable ? new SesionMarcas(repositorioMarcas, ruta) : null;
            _marcas = editable ? _vacio : repositorioMarcas.ObtenerPorDocumento(ruta);
            CheckEditable.IsChecked = editable;
            TextoDocumento.Text = System.IO.Path.GetFileName(ruta);
            TextoVacio.Visibility = Visibility.Collapsed;
            RenderizarPagina();
            ActualizarBotonesMarcas();
        }
        catch (Exception excepcion)
        {
            _ruta = null;
            Imagen.Source = null;
            Capa.Children.Clear();
            TextoDocumento.Text = System.IO.Path.GetFileName(ruta);
            TextoVacio.Text = $"No se pudo abrir el PDF.\n\nDetalle: {excepcion.Message}";
            TextoVacio.Visibility = Visibility.Visible;
        }
    }

    private IEnumerable<Marca> MarcasActuales => _sesionMarcas?.Marcas ?? _marcas;

    private void RenderizarPagina()
    {
        if (_ruta is null)
        {
            return;
        }

        try
        {
            Imagen.Source = VisorPdf.RenderizarPagina(_ruta, _paginaActual);
            AplicarZoom();
            TextoPagina.Text = $"Página {_paginaActual + 1} de {_totalPaginas}";
            BotonAnterior.IsEnabled = _paginaActual > 0;
            BotonSiguiente.IsEnabled = _paginaActual < _totalPaginas - 1;
            RefrescarMarcas();
        }
        catch (Exception excepcion)
        {
            Imagen.Source = null;
            Capa.Children.Clear();
            MessageBox.Show(
                Window.GetWindow(this),
                $"No se pudo mostrar la página.\n\nDetalle: {excepcion.Message}",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void AplicarZoom() => Contenedor.LayoutTransform = new ScaleTransform(_zoom, _zoom);

    private void CambiarZoom(double nuevoZoom)
    {
        _zoom = Math.Clamp(nuevoZoom, ZoomMinimo, ZoomMaximo);
        AplicarZoom();
    }

    private void RefrescarMarcas()
    {
        Capa.Children.Clear();
        if (CheckMarcas.IsChecked != true || Imagen.Source is not BitmapSource mapa)
        {
            return;
        }

        foreach (var marca in MarcasActuales.Where(m => m.Pagina == _paginaActual))
        {
            var forma = DibujoMarcas.CrearForma(marca, mapa.Width, mapa.Height);
            if (forma is not null)
            {
                Capa.Children.Add(forma);
            }
        }

        if (_arrastrando)
        {
            DibujarVistaPrevia(mapa.Width, mapa.Height);
        }
    }

    private void DibujarVistaPrevia(double ancho, double alto)
    {
        var x0 = Math.Min(_inicioArrastre.X, _finArrastre.X);
        var y0 = Math.Min(_inicioArrastre.Y, _finArrastre.Y);
        var w = Math.Abs(_finArrastre.X - _inicioArrastre.X);
        var h = Math.Abs(_finArrastre.Y - _inicioArrastre.Y);
        if (w < 0.001 && h < 0.001)
        {
            return;
        }

        var tipo = ComboHerramienta.SelectedIndex == 2 ? TipoMarca.Raya : TipoMarca.Circulo;
        var provisional = new Marca
        {
            Id = Guid.Empty,
            Tipo = tipo,
            Pagina = _paginaActual,
            X = x0,
            Y = y0,
            Ancho = w,
            Alto = h,
        };

        var forma = DibujoMarcas.CrearForma(provisional, ancho, alto);
        if (forma is not null)
        {
            Capa.Children.Add(forma);
        }
    }

    private static (double X, double Y) Normalizar(Point posicion, BitmapSource mapa) =>
        (Math.Clamp(posicion.X / mapa.Width, 0, 1), Math.Clamp(posicion.Y / mapa.Height, 0, 1));

    private void Capa_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (
            !_esEditable
            || CheckEditable.IsChecked != true
            || _sesionMarcas is null
            || Imagen.Source is not BitmapSource mapa
        )
        {
            return;
        }

        var (x, y) = Normalizar(e.GetPosition(Capa), mapa);
        switch (ComboHerramienta.SelectedIndex)
        {
            case 0:
                AgregarMarcaCentrada(TipoMarca.Tick, x, y);
                break;
            case 1:
                AgregarMarcaCentrada(TipoMarca.Equis, x, y);
                break;
            case 2:
            case 3:
                _inicioArrastre = new Point(x, y);
                _finArrastre = _inicioArrastre;
                _arrastrando = true;
                Capa.CaptureMouse();
                RefrescarMarcas();
                break;
            case 4:
                ColocarTexto(x, y);
                break;
        }
    }

    private void Capa_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_arrastrando || Imagen.Source is not BitmapSource mapa)
        {
            return;
        }

        var (fx, fy) = Normalizar(e.GetPosition(Capa), mapa);
        _finArrastre = new Point(fx, fy);
        RefrescarMarcas();
    }

    private void Control_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_arrastrando || _sesionMarcas is null || Imagen.Source is not BitmapSource mapa)
        {
            return;
        }

        _arrastrando = false;
        if (Capa.IsMouseCaptured)
        {
            Capa.ReleaseMouseCapture();
        }

        var (finX, finY) = Normalizar(e.GetPosition(Capa), mapa);
        var esRaya = ComboHerramienta.SelectedIndex == 2;
        var x0 = Math.Min(_inicioArrastre.X, finX);
        var y0 = Math.Min(_inicioArrastre.Y, finY);
        var ancho = Math.Abs(finX - _inicioArrastre.X);
        var alto = Math.Abs(finY - _inicioArrastre.Y);

        if (ancho < 0.01 && alto < 0.01)
        {
            if (esRaya)
            {
                x0 = Math.Clamp(finX, 0, 1 - TamanoFija);
                _sesionMarcas.Agregar(TipoMarca.Raya, _paginaActual, x0, finY, TamanoFija, 0);
            }
            else
            {
                x0 = Math.Clamp(finX - (TamanoFija / 2), 0, Math.Max(0, 1 - TamanoFija));
                var yC = Math.Clamp(finY - (TamanoFija / 2), 0, Math.Max(0, 1 - TamanoFija));
                _sesionMarcas.Agregar(
                    TipoMarca.Circulo,
                    _paginaActual,
                    x0,
                    yC,
                    TamanoFija,
                    TamanoFija
                );
            }
        }
        else
        {
            _sesionMarcas.Agregar(
                esRaya ? TipoMarca.Raya : TipoMarca.Circulo,
                _paginaActual,
                x0,
                y0,
                ancho,
                alto
            );
        }

        RefrescarMarcas();
        ActualizarBotonesMarcas();
    }

    private void AgregarMarcaCentrada(TipoMarca tipo, double x, double y)
    {
        if (_sesionMarcas is null)
        {
            return;
        }

        var marcaX = Math.Clamp(x - (TamanoFija / 2), 0, Math.Max(0, 1 - TamanoFija));
        var marcaY = Math.Clamp(y - (TamanoFija / 2), 0, Math.Max(0, 1 - TamanoFija));
        _sesionMarcas.Agregar(tipo, _paginaActual, marcaX, marcaY, TamanoFija, TamanoFija);
        RefrescarMarcas();
        ActualizarBotonesMarcas();
    }

    private void ColocarTexto(double x, double y)
    {
        if (_sesionMarcas is null || Imagen.Source is not BitmapSource mapa)
        {
            return;
        }

        var dialogo = new DialogoTexto { Owner = Window.GetWindow(this) };
        if (dialogo.ShowDialog() != true || string.IsNullOrWhiteSpace(dialogo.Texto))
        {
            return;
        }

        var texto = dialogo.Texto.Trim();
        var ancho = Math.Clamp(texto.Length * 0.012, 0.05, 0.9);
        _sesionMarcas.Agregar(
            TipoMarca.Texto,
            _paginaActual,
            Math.Clamp(x, 0, 1 - ancho),
            Math.Clamp(y, 0, 0.98),
            ancho,
            0.025,
            texto
        );
        RefrescarMarcas();
        ActualizarBotonesMarcas();
    }

    private void ActualizarBotonesMarcas()
    {
        BotonDeshacer.IsEnabled = _sesionMarcas?.PuedeDeshacer == true;
        BotonRehacer.IsEnabled = _sesionMarcas?.PuedeRehacer == true;
        BotonBorrarMarcas.IsEnabled = _sesionMarcas?.Marcas.Count > 0;
        Capa.Cursor =
            _esEditable && CheckEditable.IsChecked == true ? Cursors.Cross : Cursors.Arrow;
    }

    private void CheckEditable_Click(object sender, RoutedEventArgs e)
    {
        if (!_esEditable && CheckEditable.IsChecked == true)
        {
            CheckEditable.IsChecked = false;
        }

        ActualizarBotonesMarcas();
    }

    private void BotonDeshacer_Click(object sender, RoutedEventArgs e)
    {
        if (_sesionMarcas?.Deshacer() == true)
        {
            RefrescarMarcas();
            ActualizarBotonesMarcas();
        }
    }

    private void BotonRehacer_Click(object sender, RoutedEventArgs e)
    {
        if (_sesionMarcas?.Rehacer() == true)
        {
            RefrescarMarcas();
            ActualizarBotonesMarcas();
        }
    }

    private void BotonBorrarMarcas_Click(object sender, RoutedEventArgs e)
    {
        if (_sesionMarcas is null || _sesionMarcas.Marcas.Count == 0)
        {
            return;
        }

        if (
            MessageBox.Show(
                Window.GetWindow(this),
                "¿Borrar todas las marcas de este documento?",
                "Buscadero",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            ) != MessageBoxResult.Yes
        )
        {
            return;
        }

        _sesionMarcas.BorrarTodas();
        RefrescarMarcas();
        ActualizarBotonesMarcas();
    }

    private void BotonAnterior_Click(object sender, RoutedEventArgs e)
    {
        if (_paginaActual > 0)
        {
            _paginaActual--;
            RenderizarPagina();
        }
    }

    private void BotonSiguiente_Click(object sender, RoutedEventArgs e)
    {
        if (_paginaActual < _totalPaginas - 1)
        {
            _paginaActual++;
            RenderizarPagina();
        }
    }

    private void BotonAcercar_Click(object sender, RoutedEventArgs e) =>
        CambiarZoom(_zoom * PasoZoom);

    private void BotonAlejar_Click(object sender, RoutedEventArgs e) =>
        CambiarZoom(_zoom / PasoZoom);

    private void BotonAjustar_Click(object sender, RoutedEventArgs e)
    {
        if (Imagen.Source is not BitmapSource mapa || mapa.Width == 0)
        {
            return;
        }

        var anchoDisponible = Lienzo.ViewportWidth > 0 ? Lienzo.ViewportWidth : Lienzo.ActualWidth;
        if (anchoDisponible > 0)
        {
            CambiarZoom(anchoDisponible / mapa.Width);
        }
    }

    private void CheckMarcas_Click(object sender, RoutedEventArgs e) => RefrescarMarcas();

    private void Lienzo_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        CambiarZoom(e.Delta > 0 ? _zoom * PasoZoom : _zoom / PasoZoom);
        e.Handled = true;
    }
}
