using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Buscadero.App.Pdf;
using Buscadero.Core.Busqueda;
using Buscadero.Core.Indexado;
using Buscadero.Core.Marcas;
using Buscadero.Core.Pdf;
using Hormiguero.Nucleo.Pdf;

namespace Buscadero.App;

public partial class PanelBusquedaSecundario : UserControl
{
    private ServicioBusqueda? _servicioBusqueda;
    private RepositorioMarcas? _repositorioMarcas;
    private CancellationTokenSource? _cancelacionBusqueda;
    private SesionMarcas? _sesionMarcas;
    private string? _documentoActual;
    private InfoPdf? _infoTextoActual;
    private IReadOnlyList<PalabraPdf> _palabrasSeleccionadas = [];
    private string _textoSeleccionado = string.Empty;
    private Point _inicioSeleccionTexto;
    private Point _finSeleccionTexto;
    private bool _arrastrandoSeleccionTexto;
    private int _generacionDocumento;
    private int _paginaActual;
    private int _totalPaginas;
    private double _zoom = 1;
    private IReadOnlyList<OpcionCarpetaBusqueda> _carpetasAlcance = [];
    private int _generacionSelectorCarpetas;

    public PanelBusquedaSecundario()
    {
        InitializeComponent();
    }

    public void Configurar(ServicioBusqueda servicioBusqueda, RepositorioMarcas repositorioMarcas)
    {
        _servicioBusqueda = servicioBusqueda;
        _repositorioMarcas = repositorioMarcas;
    }

    public void Limpiar()
    {
        _cancelacionBusqueda?.Cancel();
        _generacionDocumento++;
        _documentoActual = null;
        BotonImprimir.IsEnabled = false;
        BotonAbrirEnVisor.IsEnabled = false;
        BotonCopiarPdf.IsEnabled = false;
        BotonCopiarTextoSeleccionado.IsEnabled = false;
        _infoTextoActual = null;
        _palabrasSeleccionadas = [];
        _textoSeleccionado = string.Empty;
        _sesionMarcas = null;
        ImagenPdf.Source = null;
        CanvasMarcas.Children.Clear();
        ListaResultados.ItemsSource = null;
        ListaResultados.Visibility = Visibility.Collapsed;
        BarraVisor.Visibility = Visibility.Collapsed;
        TextoEstado.Text = string.Empty;
        TextoVisorVacio.Text = "Ingrese otro número y busque.";
        TextoVisorVacio.Visibility = Visibility.Visible;
    }

    private async void BotonBuscar_Click(object sender, RoutedEventArgs e) => await BuscarAsync();

    private async void TextoNumero_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await BuscarAsync();
        }
    }

    private async Task BuscarAsync()
    {
        var numero = TextoNumero.Text.Trim();
        if (numero.Length == 0)
        {
            TextoEstado.Text = "Ingrese un número de documento.";
            return;
        }

        _cancelacionBusqueda?.Cancel();
        _cancelacionBusqueda?.Dispose();
        _cancelacionBusqueda = new CancellationTokenSource();
        var cancelacion = _cancelacionBusqueda;
        var token = cancelacion.Token;
        var numeroBusqueda = numero;
        var filtro =
            ComboAlcance.SelectedIndex == 2
                ? (ComboFiltroCarpeta.SelectedItem as OpcionCarpetaBusqueda)?.Ruta
                    ?? ComboFiltroCarpeta.Text.Trim()
                : string.Empty;
        var carpetaSinIndexar =
            filtro.Length > 0 && !_servicioBusqueda!.EstaCarpetaIndexada(filtro);
        BotonBuscar.IsEnabled = false;
        BarraProgreso.Visibility = Visibility.Visible;
        TextoEstado.Text = "Buscando...";
        try
        {
            var modo = ComboModo.SelectedIndex switch
            {
                1 => ModoBusqueda.Exacto,
                2 => ModoBusqueda.Alfanumerico,
                3 => ModoBusqueda.SoloNumero,
                4 => ModoBusqueda.SoloLetras,
                _ => ModoBusqueda.Todos,
            };
            var alcance = ComboAlcance.SelectedIndex switch
            {
                1 => AlcanceBusqueda.TodasLasCarpetas,
                2 => AlcanceBusqueda.CarpetaEspecifica,
                _ => AlcanceBusqueda.PrimeraCoincidencia,
            };
            var resultados = await Task.Run(
                () => _servicioBusqueda!.Buscar(numeroBusqueda, filtro, modo, alcance, token),
                token
            );
            token.ThrowIfCancellationRequested();
            ListaResultados.ItemsSource = resultados
                .Select(r => new ResultadoBusquedaVm
                {
                    Ruta = r.Ruta,
                    Carpeta = r.Carpeta,
                    Nombre = r.Nombre,
                    Modificado = r.FechaModificacion.ToString("yyyy-MM-dd HH:mm"),
                })
                .ToList();
            if (resultados.Count == 0)
            {
                TextoEstado.Text = "Sin coincidencias.";
                ListaResultados.Visibility = Visibility.Collapsed;
            }
            else if (resultados.Count == 1)
            {
                TextoEstado.Text = "1 coincidencia encontrada.";
                ListaResultados.Visibility = Visibility.Collapsed;
                await AbrirDocumentoAsync(resultados[0].Ruta);
            }
            else
            {
                TextoEstado.Text =
                    $"{resultados.Count} coincidencias. Elija un documento para abrirlo.";
                ListaResultados.Visibility = Visibility.Visible;
            }
            if (carpetaSinIndexar)
            {
                TextoEstado.Text +=
                    " Esta carpeta aún se está indexando; puede faltar algún resultado.";
            }
        }
        catch (OperationCanceledException)
        {
            TextoEstado.Text = "Búsqueda cancelada.";
        }
        catch (Exception excepcion)
        {
            TextoEstado.Text = $"No se pudo completar la búsqueda: {excepcion.Message}";
        }
        finally
        {
            if (ReferenceEquals(_cancelacionBusqueda, cancelacion))
            {
                BotonBuscar.IsEnabled = true;
                BarraProgreso.Visibility = Visibility.Collapsed;
            }
        }
    }

    private async void ComboAlcance_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboFiltroCarpeta is not null)
        {
            ComboFiltroCarpeta.IsEnabled = ComboAlcance.SelectedIndex == 2;
            if (ComboAlcance.SelectedIndex == 2)
            {
                ComboFiltroCarpeta.Text = string.Empty;
                await ActualizarCarpetasAlcanceAsync();
            }
        }
    }

    private async Task ActualizarCarpetasAlcanceAsync()
    {
        if (_servicioBusqueda is null)
        {
            return;
        }

        var generacion = ++_generacionSelectorCarpetas;
        var texto = ComboFiltroCarpeta.Text;
        var carpetas = await Task.Run(_servicioBusqueda.ObtenerCarpetasParaSelector);
        if (generacion != _generacionSelectorCarpetas)
        {
            return;
        }

        _carpetasAlcance = carpetas;
        ComboFiltroCarpeta.ItemsSource = ServicioBusqueda.FiltrarCarpetasParaSelector(
            carpetas,
            texto
        );
        ComboFiltroCarpeta.Text = texto;
    }

    private async void ComboFiltroCarpeta_DropDownOpened(object sender, EventArgs e) =>
        await ActualizarCarpetasAlcanceAsync();

    private void ComboFiltroCarpeta_TextChanged(object sender, TextChangedEventArgs e)
    {
        var texto = ComboFiltroCarpeta.Text;
        if (
            ComboFiltroCarpeta.SelectedItem is OpcionCarpetaBusqueda seleccionada
            && seleccionada.Texto == texto
        )
        {
            return;
        }

        if (ComboAlcance.SelectedIndex == 2)
        {
            var coincidencias = ServicioBusqueda.FiltrarCarpetasParaSelector(
                _carpetasAlcance,
                texto
            );
            ComboFiltroCarpeta.ItemsSource = coincidencias;
            ComboFiltroCarpeta.Text = texto;
            ComboFiltroCarpeta.IsDropDownOpen = coincidencias.Count > 0;
        }
    }

    private async void ListaResultados_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ListaResultados.SelectedItem is ResultadoBusquedaVm seleccionado)
        {
            await AbrirDocumentoAsync(seleccionado.Ruta);
        }
    }

    private async Task AbrirDocumentoAsync(string ruta)
    {
        var generacion = ++_generacionDocumento;
        _documentoActual = ruta;
        BotonImprimir.IsEnabled = false;
        BotonAbrirEnVisor.IsEnabled = false;
        BotonCopiarPdf.IsEnabled = false;
        TextoVisorVacio.Text = "Cargando documento...";
        try
        {
            var totalPaginas = await Task.Run(() => VisorPdf.ObtenerTotalPaginas(ruta));
            var infoTexto = await Task.Run(() => LeerInfoTexto(ruta));
            if (generacion != _generacionDocumento)
                return;
            var sesion = await Task.Run(() => new SesionMarcas(_repositorioMarcas!, ruta));
            if (generacion != _generacionDocumento)
                return;
            _totalPaginas = totalPaginas;
            _infoTextoActual = infoTexto;
            _palabrasSeleccionadas = [];
            _textoSeleccionado = string.Empty;
            BotonCopiarTextoSeleccionado.IsEnabled = false;
            _paginaActual = 0;
            _zoom = 1;
            _sesionMarcas = sesion;
            TextoDocumento.Text = System.IO.Path.GetFileName(ruta);
            BotonImprimir.IsEnabled = true;
            BotonAbrirEnVisor.IsEnabled = true;
            BotonCopiarPdf.IsEnabled = true;
            BarraVisor.Visibility = Visibility.Visible;
            TextoVisorVacio.Visibility = Visibility.Collapsed;
            await RenderizarPaginaAsync(generacion);
        }
        catch (Exception excepcion)
        {
            if (generacion != _generacionDocumento)
                return;
            _documentoActual = null;
            BotonImprimir.IsEnabled = false;
            BotonAbrirEnVisor.IsEnabled = false;
            BotonCopiarPdf.IsEnabled = false;
            BotonCopiarTextoSeleccionado.IsEnabled = false;
            _infoTextoActual = null;
            _palabrasSeleccionadas = [];
            _textoSeleccionado = string.Empty;
            _sesionMarcas = null;
            ImagenPdf.Source = null;
            CanvasMarcas.Children.Clear();
            BarraVisor.Visibility = Visibility.Collapsed;
            TextoVisorVacio.Text = $"No se pudo abrir el PDF: {excepcion.Message}";
            TextoVisorVacio.Visibility = Visibility.Visible;
        }
    }

    private static InfoPdf LeerInfoTexto(string ruta)
    {
        using var archivo = new System.IO.FileStream(
            ruta,
            System.IO.FileMode.Open,
            System.IO.FileAccess.Read,
            System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete
        );
        return LectorPdf.Leer(archivo);
    }

    private async Task RenderizarPaginaAsync(int generacion)
    {
        if (_documentoActual is not string ruta)
            return;
        var documento = ruta;
        var pagina = _paginaActual;
        try
        {
            var fuente = await Task.Run(() => VisorPdf.RenderizarPagina(documento, pagina));
            if (
                generacion != _generacionDocumento
                || documento != _documentoActual
                || pagina != _paginaActual
            )
                return;
            ImagenPdf.Source = fuente;
            ContenedorPagina.LayoutTransform = new ScaleTransform(_zoom, _zoom);
            TextoPagina.Text = $"{pagina + 1}/{_totalPaginas}";
            DibujarMarcas(fuente);
        }
        catch (Exception excepcion)
        {
            if (generacion == _generacionDocumento)
            {
                TextoEstado.Text = $"No se pudo mostrar la página: {excepcion.Message}";
            }
        }
    }

    private void DibujarMarcas(BitmapSource mapa)
    {
        CanvasMarcas.Children.Clear();
        CanvasMarcas.Width = mapa.PixelWidth;
        CanvasMarcas.Height = mapa.PixelHeight;
        DibujarPalabrasSeleccionadas(mapa);
        if (_arrastrandoSeleccionTexto)
            DibujarRectanguloSeleccion(mapa);
        if (_sesionMarcas is null)
            return;
        var pincel = (Brush)FindResource("Hormiguero.Aviso");
        foreach (var marca in _sesionMarcas.Marcas.Where(m => m.Pagina == _paginaActual))
        {
            var x = marca.X * mapa.PixelWidth;
            var y = marca.Y * mapa.PixelHeight;
            var ancho = Math.Max(18, marca.Ancho * mapa.PixelWidth);
            var alto = Math.Max(18, marca.Alto * mapa.PixelHeight);
            FrameworkElement forma = marca.Tipo switch
            {
                TipoMarca.Circulo => new Ellipse
                {
                    Width = ancho,
                    Height = alto,
                    Stroke = pincel,
                    StrokeThickness = 3,
                },
                TipoMarca.Raya => new Line
                {
                    X1 = x,
                    Y1 = y,
                    X2 = x + ancho,
                    Y2 = y + alto,
                    Stroke = pincel,
                    StrokeThickness = 3,
                },
                TipoMarca.Texto => new TextBlock
                {
                    Text = marca.Texto ?? "Texto",
                    Foreground = pincel,
                    FontSize = 18,
                },
                TipoMarca.Equis => new TextBlock
                {
                    Text = "×",
                    Foreground = pincel,
                    FontSize = 32,
                    FontWeight = FontWeights.Bold,
                },
                _ => new TextBlock
                {
                    Text = "✓",
                    Foreground = pincel,
                    FontSize = 32,
                    FontWeight = FontWeights.Bold,
                },
            };
            Canvas.SetLeft(forma, x);
            Canvas.SetTop(forma, y);
            CanvasMarcas.Children.Add(forma);
        }
    }

    private async void BotonPaginaAnterior_Click(object sender, RoutedEventArgs e)
    {
        if (_paginaActual > 0)
        {
            LimpiarSeleccionTexto();
            _paginaActual--;
            await RenderizarPaginaAsync(_generacionDocumento);
        }
    }

    private async void BotonPaginaSiguiente_Click(object sender, RoutedEventArgs e)
    {
        if (_paginaActual + 1 < _totalPaginas)
        {
            LimpiarSeleccionTexto();
            _paginaActual++;
            await RenderizarPaginaAsync(_generacionDocumento);
        }
    }

    private void LimpiarSeleccionTexto()
    {
        _palabrasSeleccionadas = [];
        _textoSeleccionado = string.Empty;
        BotonCopiarTextoSeleccionado.IsEnabled = false;
    }

    private void BotonVerEnCarpeta_Click(object sender, RoutedEventArgs e)
    {
        if (_documentoActual is null)
            return;
        try
        {
            Process.Start(
                new ProcessStartInfo("explorer.exe", $"/select,\"{_documentoActual}\"")
                {
                    UseShellExecute = true,
                }
            );
        }
        catch (Exception excepcion)
        {
            TextoEstado.Text = $"No se pudo abrir la carpeta: {excepcion.Message}";
        }
    }

    private void BotonImprimir_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button boton)
        {
            boton.ContextMenu.PlacementTarget = boton;
            boton.ContextMenu.IsOpen = true;
        }
    }

    private void BotonAbrirEnVisor_Click(object sender, RoutedEventArgs e)
    {
        if (_documentoActual is not string ruta)
            return;
        try
        {
            Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            _ = MostrarAvisoImpresionAsync($"No se pudo abrir el PDF: {error.Message}");
        }
    }

    private void DibujarPalabrasSeleccionadas(BitmapSource mapa)
    {
        if (_infoTextoActual is null || _palabrasSeleccionadas.Count == 0)
            return;
        var (anchoPagina, altoPagina) = _infoTextoActual.TamanosPagina[_paginaActual];
        foreach (var palabra in _palabrasSeleccionadas)
        {
            var rectangulo = new Rectangle
            {
                Width = palabra.Ancho / anchoPagina * mapa.PixelWidth,
                Height = palabra.Alto / altoPagina * mapa.PixelHeight,
                Fill = (Brush)FindResource("Hormiguero.AvisoSuave"),
                Stroke = (Brush)FindResource("Hormiguero.Aviso"),
                StrokeThickness = 1,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(rectangulo, palabra.X / anchoPagina * mapa.PixelWidth);
            Canvas.SetTop(
                rectangulo,
                (altoPagina - palabra.Y - palabra.Alto) / altoPagina * mapa.PixelHeight
            );
            CanvasMarcas.Children.Add(rectangulo);
        }
    }

    private void DibujarRectanguloSeleccion(BitmapSource mapa)
    {
        var rectangulo = new Rectangle
        {
            Width = Math.Abs(_finSeleccionTexto.X - _inicioSeleccionTexto.X) * mapa.PixelWidth,
            Height = Math.Abs(_finSeleccionTexto.Y - _inicioSeleccionTexto.Y) * mapa.PixelHeight,
            Fill = (Brush)FindResource("Hormiguero.AvisoSuave"),
            Stroke = (Brush)FindResource("Hormiguero.Aviso"),
            StrokeThickness = 1,
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(
            rectangulo,
            Math.Min(_inicioSeleccionTexto.X, _finSeleccionTexto.X) * mapa.PixelWidth
        );
        Canvas.SetTop(
            rectangulo,
            Math.Min(_inicioSeleccionTexto.Y, _finSeleccionTexto.Y) * mapa.PixelHeight
        );
        CanvasMarcas.Children.Add(rectangulo);
    }

    private void BotonCopiarPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_documentoActual is not string ruta)
            return;
        try
        {
            var archivos = new StringCollection();
            archivos.Add(ruta);
            Clipboard.SetFileDropList(archivos);
            _ = MostrarAvisoImpresionAsync("PDF copiado");
        }
        catch (Exception error)
        {
            _ = MostrarAvisoImpresionAsync($"No se pudo copiar el PDF: {error.Message}");
        }
    }

    private void BotonCopiarTextoSeleccionado_Click(object sender, RoutedEventArgs e) =>
        CopiarTextoSeleccionado();

    public void CopiarTextoSeleccionado()
    {
        if (string.IsNullOrWhiteSpace(_textoSeleccionado))
        {
            string mensaje = _infoTextoActual is { Palabras.Count: > 0 }
                ? "Seleccione texto primero."
                : "Este PDF no tiene texto seleccionable (es una imagen)";
            _ = MostrarAvisoImpresionAsync(mensaje);
            return;
        }

        try
        {
            Clipboard.SetText(_textoSeleccionado);
            _ = MostrarAvisoImpresionAsync("Texto copiado");
        }
        catch (Exception error)
        {
            _ = MostrarAvisoImpresionAsync($"No se pudo copiar el texto: {error.Message}");
        }
    }

    public bool AtenderCtrlC()
    {
        if (BotonModoSeleccionarTexto.IsChecked != true && _palabrasSeleccionadas.Count == 0)
            return false;
        CopiarTextoSeleccionado();
        return true;
    }

    private void BotonModoMarcas_Checked(object sender, RoutedEventArgs e) =>
        BotonModoSeleccionarTexto.IsChecked = false;

    private void BotonModoMarcas_Unchecked(object sender, RoutedEventArgs e) { }

    private void BotonModoSeleccionarTexto_Checked(object sender, RoutedEventArgs e) =>
        BotonModoMarcas.IsChecked = false;

    private void BotonModoSeleccionarTexto_Unchecked(object sender, RoutedEventArgs e) { }

    private void Panel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C && AtenderCtrlC())
        {
            e.Handled = true;
        }
    }

    private async void OpcionImprimir_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string etiqueta })
        {
            await ImprimirAsync(_documentoActual, Enum.Parse<OpcionImpresion>(etiqueta));
        }
    }

    public void ImprimirPrimeraPagina() =>
        _ = ImprimirAsync(_documentoActual, OpcionImpresion.PrimeraPagina);

    private async Task ImprimirAsync(string? ruta, OpcionImpresion opcion)
    {
        if (ruta is null)
        {
            return;
        }

        var cantidad = Math.Min(ImpresionDocumento.CuantasPaginas(opcion), _totalPaginas);
        try
        {
            await ImpresionDocumento.ImprimirAsync(
                ruta,
                opcion,
                (bytes, cuantas, impresora) => ImpresionPdf.Imprimir(bytes, cuantas, impresora)
            );
            _ = MostrarAvisoImpresionAsync(
                $"Enviado a imprimir: {cantidad} {(cantidad == 1 ? "página" : "páginas")}."
            );
        }
        catch (Exception excepcion)
        {
            _ = MostrarAvisoImpresionAsync(
                $"No se pudo imprimir el documento: {excepcion.Message}"
            );
        }
    }

    private async Task MostrarAvisoImpresionAsync(string mensaje)
    {
        TextoEstado.Text = mensaje;
        await Task.Delay(TimeSpan.FromSeconds(5));
        if (TextoEstado.Text == mensaje)
        {
            TextoEstado.Text = string.Empty;
        }
    }

    private void LienzoVisor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;
        _zoom = Math.Clamp(_zoom * (e.Delta > 0 ? 1.2 : 1 / 1.2), 0.2, 4);
        ContenedorPagina.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        e.Handled = true;
    }

    private void CanvasMarcas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (
            BotonModoSeleccionarTexto.IsChecked == true
            && ImagenPdf.Source is BitmapSource mapaTexto
        )
        {
            _inicioSeleccionTexto = NormalizarSeleccion(e.GetPosition(CanvasMarcas), mapaTexto);
            _finSeleccionTexto = _inicioSeleccionTexto;
            _arrastrandoSeleccionTexto = true;
            CanvasMarcas.CaptureMouse();
            DibujarMarcas(mapaTexto);
            e.Handled = true;
            return;
        }

        if (
            !BotonModoMarcas.IsChecked.GetValueOrDefault()
            || _sesionMarcas is null
            || ImagenPdf.Source is not BitmapSource mapa
        )
            return;
        var posicion = e.GetPosition(CanvasMarcas);
        var x = Math.Clamp(posicion.X / mapa.PixelWidth, 0, 0.98);
        var y = Math.Clamp(posicion.Y / mapa.PixelHeight, 0, 0.98);
        var tipo = ComboHerramienta.SelectedIndex switch
        {
            1 => TipoMarca.Equis,
            2 => TipoMarca.Raya,
            3 => TipoMarca.Circulo,
            4 => TipoMarca.Texto,
            _ => TipoMarca.Tick,
        };
        _sesionMarcas.Agregar(
            tipo,
            _paginaActual,
            x,
            y,
            0.025,
            0.025,
            tipo == TipoMarca.Texto ? "Texto" : null
        );
        DibujarMarcas(mapa);
    }

    private static Point NormalizarSeleccion(Point posicion, BitmapSource mapa) =>
        new(
            Math.Clamp(posicion.X / mapa.PixelWidth, 0, 1),
            Math.Clamp(posicion.Y / mapa.PixelHeight, 0, 1)
        );

    private void CanvasMarcas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_arrastrandoSeleccionTexto || ImagenPdf.Source is not BitmapSource mapa)
            return;
        _finSeleccionTexto = NormalizarSeleccion(e.GetPosition(CanvasMarcas), mapa);
        DibujarMarcas(mapa);
    }

    private void Panel_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_arrastrandoSeleccionTexto || ImagenPdf.Source is not BitmapSource mapa)
            return;
        _arrastrandoSeleccionTexto = false;
        if (CanvasMarcas.IsMouseCaptured)
            CanvasMarcas.ReleaseMouseCapture();
        _finSeleccionTexto = NormalizarSeleccion(e.GetPosition(CanvasMarcas), mapa);
        double x = Math.Min(_inicioSeleccionTexto.X, _finSeleccionTexto.X);
        double y = Math.Min(_inicioSeleccionTexto.Y, _finSeleccionTexto.Y);
        double ancho = Math.Abs(_finSeleccionTexto.X - _inicioSeleccionTexto.X);
        double alto = Math.Abs(_finSeleccionTexto.Y - _inicioSeleccionTexto.Y);
        if (ancho > 0 && alto > 0 && _infoTextoActual is not null)
        {
            var resultado = SeleccionTextoPdf.Seleccionar(
                _infoTextoActual,
                _paginaActual,
                x,
                y,
                ancho,
                alto
            );
            _palabrasSeleccionadas = resultado.Palabras;
            _textoSeleccionado = resultado.Texto;
        }
        else
        {
            _palabrasSeleccionadas = [];
            _textoSeleccionado = string.Empty;
        }
        BotonCopiarTextoSeleccionado.IsEnabled = _palabrasSeleccionadas.Count > 0;
        DibujarMarcas(mapa);
        if (_palabrasSeleccionadas.Count == 0 && _infoTextoActual is { Palabras.Count: 0 })
            _ = MostrarAvisoImpresionAsync("Este PDF no tiene texto seleccionable (es una imagen)");
    }
}
