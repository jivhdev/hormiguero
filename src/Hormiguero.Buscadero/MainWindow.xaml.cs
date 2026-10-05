using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Buscadero.App.Marcas;
using Buscadero.App.Pdf;
using Buscadero.Core.Busqueda;
using Buscadero.Core.Carpetas;
using Buscadero.Core.Indexado;
using Buscadero.Core.Lineas;
using Buscadero.Core.Marcas;
using Microsoft.Win32;

namespace Buscadero.App;

public partial class MainWindow : Window
{
    private const double ZoomMinimo = 0.1;
    private const double ZoomMaximo = 5.0;
    private const double PasoZoom = 1.25;
    private const double TamanoCirculo = 0.024;
    private const double TamanoTickEquis = 0.024;
    private const double LargoRayaPredeterminada = 0.024;

    // Colores de las cajas de la cadena: salen del tema activo (claro u oscuro), fase B-1.
    private static Brush Pincel(string clave) => (Brush)Application.Current.FindResource(clave);

    private static Brush FondoCajaVacia => Pincel("Hormiguero.Desactivado");
    private static Brush BordeCajaVacia => Pincel("Hormiguero.Borde");
    private static Brush FondoCajaConDocumento => Pincel("Hormiguero.ExitoSuave");
    private static Brush BordeCajaConDocumento => Pincel("Hormiguero.Exito");
    private static Brush FondoCajaActual => Pincel("Hormiguero.AvisoSuave");
    private static Brush BordeCajaActual => Pincel("Hormiguero.Aviso");

    private readonly ServicioCarpetas _servicioCarpetas;
    private readonly Indexador _indexador;
    private readonly ServicioBusqueda _servicioBusqueda;
    private readonly IndexadoEnSegundoPlano _indexadoEnSegundoPlano;
    private readonly RepositorioMarcas _repositorioMarcas;
    private readonly ServicioLineas _lineas;

    private enum ModoInteraccionPdf
    {
        Ninguno,
        Marcas,
        SeleccionarTexto,
    }

    private ModoInteraccionPdf _modoInteraccion = ModoInteraccionPdf.Ninguno;

    private bool _buscando;
    private CancellationTokenSource? _cancelacionBusqueda;
    private string? _documentoActual;
    private int _paginaActual;
    private int _totalPaginas;
    private double _zoom = 1.0;

    private SesionMarcas? _sesionMarcas;
    private Marca? _moviendo;
    private long? _cadenaActivaId;
    private string? _documentoEspiado;
    private DispatcherTimer? _temporizadorClicPrevisualizacion;
    private Point _desplazamiento;
    private Point? _vistaPrevia;
    private Point _inicioArrastre;
    private Point _finArrastre;
    private bool _arrastrando;

    public MainWindow()
    {
        InitializeComponent();

        // Fase B-3 (D-66): los datos viven en la carpeta común de Hormiguero, no junto al
        // programa (cada copia o actualización del programa empezaba vacía). La primera vez
        // se copian solos desde la carpeta Datos de junto al programa, que queda intacta.
        var rutaBaseDeDatos = Hormiguero.Nucleo.Datos.DatosDeApp.Preparar(
            "buscadero",
            System.IO.Path.Combine(AppContext.BaseDirectory, "Datos", "buscadero.db")
        );
        _servicioCarpetas = new ServicioCarpetas(new RepositorioCarpetas(rutaBaseDeDatos));

        var repositorioIndice = new RepositorioIndice(rutaBaseDeDatos);
        _indexador = new Indexador(repositorioIndice);
        // Fase B-2a: el índice se mantiene al día en segundo plano; buscar ya no
        // recorre todas las carpetas cada vez.
        _indexadoEnSegundoPlano = new IndexadoEnSegundoPlano(
            _indexador,
            () => _servicioCarpetas.ObtenerTodas().Select(c => c.Ruta).ToList()
        );
        var importadorDeGuardados = new ImportadorDeGuardados(
            repositorioIndice,
            () => _servicioCarpetas.ObtenerTodas().Select(c => c.Ruta).ToList(),
            Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun
        );
        _servicioBusqueda = new ServicioBusqueda(
            _servicioCarpetas,
            _indexador,
            repositorioIndice,
            _indexadoEnSegundoPlano,
            importadorDeGuardados
        );
        Closed += (_, _) => _indexadoEnSegundoPlano.Dispose();
        // Base común según la carpeta de datos activa: respeta HORMIGUERO_DATOS, así las
        // pruebas con datos sintéticos nunca escriben marcas en la base real.
        _repositorioMarcas = new RepositorioMarcas(
            Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun
        );
        Closed += (_, _) => _repositorioMarcas.Dispose();
        var repositorioLineas = new RepositorioLineas(
            Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun
        );
        repositorioLineas.ErrorRevisionMotor += mensaje =>
            Dispatcher.Invoke(() => MostrarMensaje(mensaje));
        repositorioLineas.RevisionMotorCompletada += () =>
            Dispatcher.Invoke(ActualizarContadorDudosos);
        _lineas = new ServicioLineas(repositorioLineas);
        Closed += (_, _) => _lineas.Dispose();

        _ = EvaluarAlertasAlAbrirAsync();
        CargarCarpetas();
        ActualizarSugerenciasCarpeta();
        RefrescarLineas();
        _indexadoEnSegundoPlano.Pedir();
        ActualizarContadorAlertas();
    }

    private async Task EvaluarAlertasAlAbrirAsync()
    {
        string ruta = Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun;
        try
        {
            await Task.Run(() =>
            {
                using var conexion = Hormiguero.Nucleo.Datos.BaseComun.Abrir(ruta);
                new Hormiguero.Nucleo.Datos.EvaluadorAlertas(conexion).Evaluar();
            });
        }
        catch (Exception error)
        {
            MostrarMensaje($"No se pudieron evaluar las alertas: {error.Message}");
        }
        ActualizarContadorAlertas();
    }

    private void CargarCarpetas()
    {
        var carpetas = _servicioCarpetas.ObtenerTodas();
        ListaCarpetas.ItemsSource = carpetas
            .Select(c => new CarpetaVm
            {
                Id = c.Id,
                Ruta = c.Ruta,
                Estado = _servicioCarpetas.EsAccesible(c.Ruta) ? "Accesible" : "No accesible",
            })
            .ToList();
    }

    private void MostrarMensaje(string mensaje) => TextoMensaje.Text = mensaje;

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
        if (_buscando)
        {
            return;
        }

        var numero = TextoNumero.Text.Trim();
        if (string.IsNullOrWhiteSpace(numero))
        {
            TextoEstadoBusqueda.Text = "Ingrese un número de documento.";
            return;
        }

        // Caso-14: el filtro de carpeta solo tiene efecto con el alcance "Carpeta específica".
        var filtro =
            ObtenerAlcanceSeleccionado() == AlcanceBusqueda.CarpetaEspecifica
                ? ComboFiltroCarpeta.Text.Trim()
                : string.Empty;

        _buscando = true;
        BotonBuscar.IsEnabled = false;
        BarraProgreso.Visibility = Visibility.Visible;
        BotonCancelarBusqueda.Visibility = Visibility.Visible;
        PanelSinResultados.Visibility = Visibility.Collapsed;
        ListaResultados.Visibility = Visibility.Collapsed;
        TextoEstadoBusqueda.Text = "Buscando...";

        _cancelacionBusqueda = new CancellationTokenSource();
        var token = _cancelacionBusqueda.Token;

        var progreso = new Progress<ProgresoIndexado>(p =>
        {
            if (!p.Omitida)
            {
                TextoEstadoBusqueda.Text =
                    $"Indexando: {p.CarpetaActual} ({p.ArchivosIndexados} archivos)...";
            }
        });

        try
        {
            var modo = ObtenerModoSeleccionado();
            var alcance = ObtenerAlcanceSeleccionado();
            var resultados = await Task.Run(
                () => _servicioBusqueda.Buscar(numero, filtro, modo, alcance, token, progreso),
                token
            );

            ListaResultados.ItemsSource = resultados
                .Select(r => new ResultadoBusquedaVm
                {
                    Ruta = r.Ruta,
                    Carpeta = r.Carpeta,
                    Nombre = r.Nombre,
                    Modificado = r.FechaModificacion.ToString("yyyy-MM-dd HH:mm"),
                })
                .ToList();

            ActualizarSugerenciasCarpeta();

            if (resultados.Count == 0)
            {
                TextoEstadoBusqueda.Text = "Sin coincidencias.";
                PanelSinResultados.Visibility = Visibility.Visible;
            }
            else if (resultados.Count == 1)
            {
                TextoEstadoBusqueda.Text = "1 coincidencia encontrada.";
                await AbrirDocumentoAsync(resultados[0].Ruta);
            }
            else
            {
                TextoEstadoBusqueda.Text =
                    $"{resultados.Count} coincidencias encontradas. Haga doble clic para abrir una.";
                ListaResultados.Visibility = Visibility.Visible;
            }
        }
        catch (OperationCanceledException)
        {
            TextoEstadoBusqueda.Text = "Búsqueda cancelada.";
        }
        catch (Exception excepcion)
        {
            TextoEstadoBusqueda.Text = $"Ocurrió un error al buscar: {excepcion.Message}";
        }
        finally
        {
            _buscando = false;
            BotonBuscar.IsEnabled = true;
            BarraProgreso.Visibility = Visibility.Collapsed;
            BotonCancelarBusqueda.Visibility = Visibility.Collapsed;
            _cancelacionBusqueda?.Dispose();
            _cancelacionBusqueda = null;
        }
    }

    private void BotonCancelarBusqueda_Click(object sender, RoutedEventArgs e) =>
        _cancelacionBusqueda?.Cancel();

    private void ActualizarSugerenciasCarpeta()
    {
        var textoActual = ComboFiltroCarpeta.Text;
        ComboFiltroCarpeta.ItemsSource = _servicioBusqueda.ObtenerSugerenciasCarpeta();
        ComboFiltroCarpeta.Text = textoActual;
    }

    private ModoBusqueda ObtenerModoSeleccionado() =>
        ComboModo.SelectedIndex switch
        {
            1 => ModoBusqueda.Exacto,
            2 => ModoBusqueda.Alfanumerico,
            3 => ModoBusqueda.SoloNumero,
            4 => ModoBusqueda.SoloLetras,
            _ => ModoBusqueda.Todos,
        };

    private AlcanceBusqueda ObtenerAlcanceSeleccionado() =>
        ComboAlcance.SelectedIndex switch
        {
            1 => AlcanceBusqueda.TodasLasCarpetas,
            2 => AlcanceBusqueda.CarpetaEspecifica,
            _ => AlcanceBusqueda.PrimeraCoincidencia,
        };

    private void ComboAlcance_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboFiltroCarpeta is null)
        {
            return;
        }

        // Caso-14: el filtro de carpeta solo tiene sentido con "Carpeta específica" —
        // blindado para que no quede ambigüedad de qué alcance está activo.
        var esCarpetaEspecifica = ObtenerAlcanceSeleccionado() == AlcanceBusqueda.CarpetaEspecifica;
        ComboFiltroCarpeta.IsEnabled = esCarpetaEspecifica;

        if (esCarpetaEspecifica)
        {
            // Caso-15: al elegir este alcance, mostrar de entrada solo las carpetas madre
            // (nada de subcarpetas ni "recientes" — eso queda para el autocompletado general).
            ComboFiltroCarpeta.Text = string.Empty;
            ComboFiltroCarpeta.ItemsSource = _servicioBusqueda.ObtenerCarpetasMadre();
        }
    }

    private void ComboFiltroCarpeta_TextChanged(object sender, TextChangedEventArgs e)
    {
        var texto = ComboFiltroCarpeta.Text;

        if (ComboFiltroCarpeta.SelectedItem is string seleccionada && seleccionada == texto)
        {
            // El texto cambió porque el usuario eligió una sugerencia, no porque está
            // escribiendo — no pisar la selección volviendo a filtrar (Caso-14).
            return;
        }

        if (ObtenerAlcanceSeleccionado() == AlcanceBusqueda.CarpetaEspecifica)
        {
            // Caso-15: filtrado instantáneo en memoria, solo entre las carpetas madre ya
            // cargadas — sin consultar la base de datos en cada letra.
            var coincidenciasMadre = _servicioBusqueda
                .ObtenerCarpetasMadre()
                .Where(ruta =>
                    string.IsNullOrEmpty(texto)
                    || ruta.Contains(texto, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();

            ComboFiltroCarpeta.ItemsSource = coincidenciasMadre;
            ComboFiltroCarpeta.Text = texto;
            ComboFiltroCarpeta.IsDropDownOpen = coincidenciasMadre.Count > 0;
            return;
        }

        // Autocompletado general (Caso-11/14, sin cambios): busca por nombre entre todas
        // las carpetas ya indexadas, no solo las madre.
        if (texto.Length < 3)
        {
            return;
        }

        var coincidencias = _servicioBusqueda.BuscarCarpetasPorNombre(texto);
        if (coincidencias.Count == 0)
        {
            return;
        }

        ComboFiltroCarpeta.ItemsSource = coincidencias;
        ComboFiltroCarpeta.Text = texto;
        ComboFiltroCarpeta.IsDropDownOpen = true;
    }

    private void BotonIrACarpetas_Click(object sender, RoutedEventArgs e) =>
        Pestanas.SelectedIndex = 2;

    private void BotonModificarNumero_Click(object sender, RoutedEventArgs e)
    {
        TextoNumero.Focus();
        TextoNumero.SelectAll();
    }

    private async void ListaResultados_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ListaResultados.SelectedItem is not ResultadoBusquedaVm seleccionado)
        {
            return;
        }

        await AbrirDocumentoAsync(seleccionado.Ruta);

        ListaResultados.ItemsSource = null;
        ListaResultados.Visibility = Visibility.Collapsed;
        TextoEstadoBusqueda.Text = string.Empty;
    }

    private async Task AbrirDocumentoAsync(string ruta)
    {
        try
        {
            var totalPaginas = await Task.Run(() => VisorPdf.ObtenerTotalPaginas(ruta));

            _documentoActual = ruta;
            _totalPaginas = totalPaginas;
            _paginaActual = 0;
            _zoom = 1.0;
            var sesion = await Task.Run(() => new SesionMarcas(_repositorioMarcas, ruta));
            // Si mientras cargaban las marcas se abrió otro documento, esta carga ya no sirve.
            if (_documentoActual != ruta)
            {
                return;
            }
            _sesionMarcas = sesion;

            // Caso-15: ningún modo de interacción queda activo por defecto al abrir un documento.
            _modoInteraccion = ModoInteraccionPdf.Ninguno;
            BotonModoMarcas.IsChecked = false;
            BotonModoSeleccionarTexto.IsChecked = false;

            TextoDocumentoAbierto.Text = System.IO.Path.GetFileName(ruta);
            BarraVisor.Visibility = Visibility.Visible;
            BarraMarcas.Visibility = Visibility.Visible;
            TextoVisorVacio.Visibility = Visibility.Collapsed;

            await RenderizarPaginaActualAsync();
            RefrescarPrevisualizacion();
            RefrescarCadenas();
        }
        catch (Exception excepcion)
        {
            _documentoActual = null;
            _sesionMarcas = null;
            ImagenPdf.Source = null;
            CanvasMarcas.Children.Clear();
            BarraVisor.Visibility = Visibility.Collapsed;
            BarraMarcas.Visibility = Visibility.Collapsed;
            TextoVisorVacio.Visibility = Visibility.Visible;
            MessageBox.Show(
                this,
                $"No se pudo abrir el PDF.\n\nDetalle: {excepcion.Message}",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private async Task RenderizarPaginaActualAsync()
    {
        if (_documentoActual is null)
        {
            return;
        }

        var ruta = _documentoActual;
        var pagina = _paginaActual;

        try
        {
            var fuente = await Task.Run(() => VisorPdf.RenderizarPagina(ruta, pagina));
            ImagenPdf.Source = fuente;
            AplicarZoom();
            ActualizarControlesPagina();
            RefrescarMarcas();
            ActualizarBotonesMarcas();
        }
        catch (Exception excepcion)
        {
            ImagenPdf.Source = null;
            CanvasMarcas.Children.Clear();
            MessageBox.Show(
                this,
                $"No se pudo mostrar la página del PDF.\n\nDetalle: {excepcion.Message}",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void ActualizarControlesPagina()
    {
        TextoPagina.Text = $"Página {_paginaActual + 1} de {_totalPaginas}";
        BotonPaginaAnterior.IsEnabled = _paginaActual > 0;
        BotonPaginaSiguiente.IsEnabled = _paginaActual < _totalPaginas - 1;
    }

    private void AplicarZoom()
    {
        ContenedorPagina.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        TextoZoom.Text = $"{_zoom * 100:0}%";
    }

    private async void BotonPaginaAnterior_Click(object sender, RoutedEventArgs e)
    {
        if (_paginaActual > 0)
        {
            _paginaActual--;
            await RenderizarPaginaActualAsync();
        }
    }

    private async void BotonPaginaSiguiente_Click(object sender, RoutedEventArgs e)
    {
        if (_paginaActual < _totalPaginas - 1)
        {
            _paginaActual++;
            await RenderizarPaginaActualAsync();
        }
    }

    private void BotonAcercar_Click(object sender, RoutedEventArgs e) =>
        CambiarZoom(_zoom * PasoZoom);

    private void BotonAlejar_Click(object sender, RoutedEventArgs e) =>
        CambiarZoom(_zoom / PasoZoom);

    private void CambiarZoom(double nuevoZoom)
    {
        _zoom = Math.Clamp(nuevoZoom, ZoomMinimo, ZoomMaximo);
        AplicarZoom();
    }

    private void BotonAjustar_Click(object sender, RoutedEventArgs e)
    {
        if (ImagenPdf.Source is not BitmapSource mapaBits || mapaBits.PixelWidth == 0)
        {
            return;
        }

        var anchoDisponible =
            LienzoVisor.ViewportWidth > 0 ? LienzoVisor.ViewportWidth : LienzoVisor.ActualWidth;
        if (anchoDisponible <= 0)
        {
            return;
        }

        CambiarZoom(anchoDisponible / mapaBits.Width);
    }

    private void BotonVerEnCarpeta_Click(object sender, RoutedEventArgs e)
    {
        if (_documentoActual is null)
        {
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{_documentoActual}\"",
                    UseShellExecute = true,
                }
            );
        }
        catch (Exception excepcion)
        {
            MessageBox.Show(
                this,
                $"No se pudo abrir la ubicación del archivo.\n\nDetalle: {excepcion.Message}",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void LienzoVisor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        CambiarZoom(e.Delta > 0 ? _zoom * PasoZoom : _zoom / PasoZoom);
        e.Handled = true;
    }

    private void ComboHerramienta_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ActualizarBotonesMarcas();

    private void CheckMostrarMarcas_Click(object sender, RoutedEventArgs e) => RefrescarMarcas();

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

        var confirmacion = MessageBox.Show(
            this,
            "¿Borrar todas las marcas de este documento?",
            "Buscadero",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (confirmacion == MessageBoxResult.Yes)
        {
            _sesionMarcas.BorrarTodas();
            RefrescarMarcas();
            ActualizarBotonesMarcas();
        }
    }

    private void ActualizarBotonesMarcas()
    {
        if (CanvasMarcas is null)
        {
            return;
        }

        BotonDeshacer.IsEnabled = _sesionMarcas?.PuedeDeshacer == true;
        BotonRehacer.IsEnabled = _sesionMarcas?.PuedeRehacer == true;
        BotonBorrarMarcas.IsEnabled = _sesionMarcas?.Marcas.Count > 0;
        CanvasMarcas.Cursor = Cursors.Cross;
    }

    private void RefrescarMarcas()
    {
        CanvasMarcas.Children.Clear();

        if (_sesionMarcas is null || ImagenPdf.Source is not BitmapSource mapa)
        {
            return;
        }

        if (CheckMostrarMarcas.IsChecked != true)
        {
            return;
        }

        var ancho = mapa.Width;
        var alto = mapa.Height;

        foreach (var marca in _sesionMarcas.Marcas.Where(m => m.Pagina == _paginaActual))
        {
            var efectiva = marca;
            if (_moviendo is not null && marca.Id == _moviendo.Id && _vistaPrevia is Point previa)
            {
                efectiva = ClonarConPosicion(marca, previa.X, previa.Y);
            }

            var forma = DibujoMarcas.CrearForma(efectiva, ancho, alto);
            if (forma is not null)
            {
                CanvasMarcas.Children.Add(forma);
            }
        }

        if (_arrastrando)
        {
            DibujarVistaPrevia(ancho, alto);
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
            CanvasMarcas.Children.Add(forma);
        }
    }

    private static Marca ClonarConPosicion(Marca marca, double x, double y) =>
        new()
        {
            Id = marca.Id,
            Tipo = marca.Tipo,
            Pagina = marca.Pagina,
            X = x,
            Y = y,
            Ancho = marca.Ancho,
            Alto = marca.Alto,
            Texto = marca.Texto,
        };

    private void BotonModoMarcas_Click(object sender, RoutedEventArgs e)
    {
        if (BotonModoMarcas.IsChecked == true)
        {
            _modoInteraccion = ModoInteraccionPdf.Marcas;
            BotonModoSeleccionarTexto.IsChecked = false;
        }
        else
        {
            _modoInteraccion = ModoInteraccionPdf.Ninguno;
        }
    }

    private void BotonModoSeleccionarTexto_Click(object sender, RoutedEventArgs e)
    {
        if (BotonModoSeleccionarTexto.IsChecked == true)
        {
            _modoInteraccion = ModoInteraccionPdf.SeleccionarTexto;
            BotonModoMarcas.IsChecked = false;
        }
        else
        {
            _modoInteraccion = ModoInteraccionPdf.Ninguno;
        }
    }

    private void CanvasMarcas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Caso-15: un clic solo coloca una marca si el modo "Marcas" está activo a
        // propósito — ninguno de los dos modos (marcas / seleccionar texto) es el
        // default implícito.
        if (_modoInteraccion != ModoInteraccionPdf.Marcas)
        {
            return;
        }

        if (_sesionMarcas is null || ImagenPdf.Source is not BitmapSource mapa)
        {
            return;
        }

        var (x, y) = Normalizar(e.GetPosition(CanvasMarcas), mapa);

        switch (ComboHerramienta.SelectedIndex)
        {
            case 0:
                AgregarMarcaCentrada(TipoMarca.Tick, x, y, TamanoTickEquis);
                break;
            case 1:
                AgregarMarcaCentrada(TipoMarca.Equis, x, y, TamanoTickEquis);
                break;
            case 2:
            case 3:
                _inicioArrastre = new Point(x, y);
                _finArrastre = _inicioArrastre;
                _arrastrando = true;
                CanvasMarcas.CaptureMouse();
                RefrescarMarcas();
                break;
            case 4:
                var textoExistente = BuscarMarca(x, y, TipoMarca.Texto);
                if (textoExistente is not null)
                {
                    IniciarMovimiento(textoExistente, x, y);
                }
                else
                {
                    ColocarTexto(x, y);
                }

                break;
        }
    }

    private void CanvasMarcas_MouseMove(object sender, MouseEventArgs e)
    {
        if (ImagenPdf.Source is not BitmapSource mapa)
        {
            return;
        }

        if (_arrastrando)
        {
            var (x, y) = Normalizar(e.GetPosition(CanvasMarcas), mapa);
            _finArrastre = new Point(x, y);
            RefrescarMarcas();
            return;
        }

        if (_moviendo is null)
        {
            return;
        }

        var (mx, my) = Normalizar(e.GetPosition(CanvasMarcas), mapa);
        var nuevaX = Math.Clamp(mx - _desplazamiento.X, 0, 0.98);
        var nuevaY = Math.Clamp(my - _desplazamiento.Y, 0, 0.98);
        _vistaPrevia = new Point(nuevaX, nuevaY);
        RefrescarMarcas();
    }

    private void Ventana_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_sesionMarcas is null || ImagenPdf.Source is not BitmapSource mapa)
        {
            return;
        }

        if (_moviendo is not null)
        {
            var (mx, my) = Normalizar(e.GetPosition(CanvasMarcas), mapa);
            var nuevaX = Math.Clamp(mx - _desplazamiento.X, 0, Math.Max(0, 1 - _moviendo.Ancho));
            var nuevaY = Math.Clamp(my - _desplazamiento.Y, 0, Math.Max(0, 1 - _moviendo.Alto));

            _sesionMarcas.Mover(_moviendo.Id, nuevaX, nuevaY);
            _moviendo = null;
            _vistaPrevia = null;
            if (CanvasMarcas.IsMouseCaptured)
            {
                CanvasMarcas.ReleaseMouseCapture();
            }

            RefrescarMarcas();
            ActualizarBotonesMarcas();
            return;
        }

        if (!_arrastrando)
        {
            return;
        }

        _arrastrando = false;
        if (CanvasMarcas.IsMouseCaptured)
        {
            CanvasMarcas.ReleaseMouseCapture();
        }

        var (finX, finY) = Normalizar(e.GetPosition(CanvasMarcas), mapa);
        var esRaya = ComboHerramienta.SelectedIndex == 2;
        var x0 = Math.Min(_inicioArrastre.X, finX);
        var y0 = Math.Min(_inicioArrastre.Y, finY);
        var ancho = Math.Abs(finX - _inicioArrastre.X);
        var alto = Math.Abs(finY - _inicioArrastre.Y);

        if (ancho < 0.01 && alto < 0.01)
        {
            if (esRaya)
            {
                x0 = Math.Clamp(finX, 0, 1 - LargoRayaPredeterminada);
                y0 = finY;
                _sesionMarcas.Agregar(
                    TipoMarca.Raya,
                    _paginaActual,
                    x0,
                    y0,
                    LargoRayaPredeterminada,
                    0
                );
            }
            else
            {
                x0 = Math.Clamp(finX - (TamanoCirculo / 2), 0, Math.Max(0, 1 - TamanoCirculo));
                y0 = Math.Clamp(finY - (TamanoCirculo / 2), 0, Math.Max(0, 1 - TamanoCirculo));
                _sesionMarcas.Agregar(
                    TipoMarca.Circulo,
                    _paginaActual,
                    x0,
                    y0,
                    TamanoCirculo,
                    TamanoCirculo
                );
            }

            RefrescarMarcas();
            ActualizarBotonesMarcas();
            return;
        }

        var tipo = esRaya ? TipoMarca.Raya : TipoMarca.Circulo;
        _sesionMarcas.Agregar(tipo, _paginaActual, x0, y0, ancho, alto);
        RefrescarMarcas();
        ActualizarBotonesMarcas();
    }

    private void AgregarMarcaCentrada(TipoMarca tipo, double x, double y, double tamano)
    {
        var marcaX = Math.Clamp(x - (tamano / 2), 0, Math.Max(0, 1 - tamano));
        var marcaY = Math.Clamp(y - (tamano / 2), 0, Math.Max(0, 1 - tamano));
        _sesionMarcas!.Agregar(tipo, _paginaActual, marcaX, marcaY, tamano, tamano);
        RefrescarMarcas();
        ActualizarBotonesMarcas();
    }

    private void ColocarTexto(double x, double y)
    {
        if (ImagenPdf.Source is not BitmapSource mapa)
        {
            return;
        }

        var dialogo = new DialogoTexto { Owner = this };
        if (dialogo.ShowDialog() != true || string.IsNullOrWhiteSpace(dialogo.Texto))
        {
            return;
        }

        var texto = dialogo.Texto.Trim();
        var (anchoPx, altoPx) = MedirTexto(texto, mapa.Height);
        var ancho = anchoPx / mapa.Width;
        var alto = altoPx / mapa.Height;

        _sesionMarcas!.Agregar(
            TipoMarca.Texto,
            _paginaActual,
            Math.Clamp(x, 0, 0.98),
            Math.Clamp(y, 0, 0.98),
            ancho,
            alto,
            texto
        );
        RefrescarMarcas();
        ActualizarBotonesMarcas();
    }

    private static (double Ancho, double Alto) MedirTexto(string texto, double altoPagina)
    {
        var bloque = new TextBlock
        {
            Text = texto,
            FontWeight = FontWeights.SemiBold,
            FontSize = DibujoMarcas.TamanoFuente(altoPagina),
        };
        bloque.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return (bloque.DesiredSize.Width, bloque.DesiredSize.Height);
    }

    private void IniciarMovimiento(Marca marca, double x, double y)
    {
        _moviendo = marca;
        _desplazamiento = new Point(x - marca.X, y - marca.Y);
        _vistaPrevia = new Point(marca.X, marca.Y);
        CanvasMarcas.CaptureMouse();
    }

    private Marca? BuscarMarca(double x, double y, TipoMarca? tipo = null)
    {
        if (_sesionMarcas is null)
        {
            return null;
        }

        Marca? encontrada = null;
        foreach (
            var marca in _sesionMarcas.Marcas.Where(m =>
                m.Pagina == _paginaActual && (tipo is null || m.Tipo == tipo)
            )
        )
        {
            if (
                x >= marca.X
                && x <= marca.X + marca.Ancho
                && y >= marca.Y
                && y <= marca.Y + marca.Alto
            )
            {
                encontrada = marca;
            }
        }

        return encontrada;
    }

    private static (double X, double Y) Normalizar(Point posicion, BitmapSource mapa) =>
        (Math.Clamp(posicion.X / mapa.Width, 0, 1), Math.Clamp(posicion.Y / mapa.Height, 0, 1));

    private void BotonExaminar_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFolderDialog { Title = "Seleccionar carpeta a observar" };
        if (dialogo.ShowDialog() == true)
        {
            TextoRuta.Text = dialogo.FolderName;
        }
    }

    private void BotonAgregar_Click(object sender, RoutedEventArgs e)
    {
        var resultado = _servicioCarpetas.Agregar(TextoRuta.Text);
        switch (resultado)
        {
            case ResultadoAgregarCarpeta.Agregada:
                TextoRuta.Clear();
                MostrarMensaje(string.Empty);
                CargarCarpetas();
                _indexadoEnSegundoPlano.Pedir();
                break;
            case ResultadoAgregarCarpeta.YaConfigurada:
                MostrarMensaje("Esa carpeta ya está configurada.");
                break;
            case ResultadoAgregarCarpeta.RutaInvalida:
                MostrarMensaje("Ingrese o seleccione una ruta válida.");
                break;
        }
    }

    private void BotonQuitar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaCarpetas.SelectedItem is not CarpetaVm seleccionada)
        {
            MostrarMensaje("Seleccione una carpeta de la lista.");
            return;
        }

        _servicioCarpetas.Quitar(seleccionada.Id);
        _indexador.QuitarDelIndice(seleccionada.Ruta);
        MostrarMensaje(string.Empty);
        CargarCarpetas();
    }

    private void BotonActualizarUbicacion_Click(object sender, RoutedEventArgs e)
    {
        if (ListaCarpetas.SelectedItem is not CarpetaVm seleccionada)
        {
            MostrarMensaje("Seleccione una carpeta de la lista.");
            return;
        }

        var dialogo = new OpenFolderDialog { Title = "Seleccionar nueva ubicación" };
        if (dialogo.ShowDialog() == true)
        {
            _servicioCarpetas.ActualizarRuta(seleccionada.Id, dialogo.FolderName);
            _indexador.QuitarDelIndice(seleccionada.Ruta);
            MostrarMensaje(string.Empty);
            CargarCarpetas();
        }
    }

    private void BotonVerificar_Click(object sender, RoutedEventArgs e)
    {
        CargarCarpetas();
        MostrarMensaje("Verificación de accesibilidad actualizada.");
    }

    // ----- Cadenas documentales -----

    private void RefrescarLineas()
    {
        RefrescarPlantillas();
        RefrescarPrevisualizacion();
        RefrescarCadenas();
        ActualizarContadorDudosos();
    }

    private void ActualizarContadorDudosos() =>
        BotonDudosos.Content = $"{_lineas.ContarDudosos()} dudosos";

    private void BotonDudosos_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new DialogoDudosos(_lineas) { Owner = this };
        dialogo.ShowDialog();
        ActualizarContadorDudosos();
    }

    private void ActualizarContadorAlertas()
    {
        try
        {
            using var conexion = Hormiguero.Nucleo.Datos.BaseComun.Abrir(
                Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun
            );
            var alertas = new Hormiguero.Nucleo.Datos.RepositorioAlertas(conexion)
                .Listar(limite: 10000)
                .Alertas;
            int abiertas = alertas.Count(a => a.Estado is "pendiente" or "vencida");
            int vencidas = alertas.Count(a => a.Estado == "vencida");
            BotonAlertas.Content = $"{abiertas} alertas";
            BotonAlertas.Background =
                vencidas > 0
                    ? (Brush)Application.Current.FindResource("Hormiguero.AvisoSuave")
                    : (Brush)Application.Current.FindResource("Hormiguero.Superficie");
            BotonAlertas.BorderBrush =
                vencidas > 0
                    ? (Brush)Application.Current.FindResource("Hormiguero.Aviso")
                    : (Brush)Application.Current.FindResource("Hormiguero.Borde");
        }
        catch (Exception error)
        {
            BotonAlertas.Content = "Alertas no disponibles";
            MostrarMensaje($"No se pudieron cargar las alertas: {error.Message}");
        }
    }

    private void BotonAlertas_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var ruta = Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun;
            using (var conexion = Hormiguero.Nucleo.Datos.BaseComun.Abrir(ruta))
                new Hormiguero.Nucleo.Datos.EvaluadorAlertas(conexion).Evaluar();
            var dialogo = new DialogoAlertas(ruta, MostrarCadena) { Owner = this };
            dialogo.ShowDialog();
        }
        catch (Exception error)
        {
            MostrarMensaje($"No se pudieron abrir las alertas: {error.Message}");
        }
        ActualizarContadorAlertas();
    }

    private void BotonRecordarme_Click(object sender, RoutedEventArgs e)
    {
        if (_cadenaActivaId is not long cadenaId)
            return;
        var dialogo = new DialogoRecordatorio(cadenaId) { Owner = this };
        if (dialogo.ShowDialog() == true)
        {
            ActualizarContadorAlertas();
            ActualizarAlertasCadena(cadenaId);
        }
    }

    private void BotonCalcularFecha_Click(object sender, RoutedEventArgs e) =>
        new DialogoCalculadoraFechas { Owner = this }.ShowDialog();

    private void BotonFeriados_Click(object sender, RoutedEventArgs e) =>
        new DialogoFeriados { Owner = this }.ShowDialog();

    private void RefrescarPlantillas()
    {
        var idSeleccionada = (ListaPlantillas.SelectedItem as PlantillaLinea)?.Id;
        var plantillas = _lineas.ObtenerPlantillas();
        ListaPlantillas.ItemsSource = plantillas;
        if (idSeleccionada is not null)
        {
            ListaPlantillas.SelectedItem = plantillas.FirstOrDefault(p => p.Id == idSeleccionada);
        }

        RefrescarArbolPlantilla();
    }

    private void RefrescarArbolPlantilla()
    {
        TextoEstadoPlantilla.Text = string.Empty;
        ArbolPlantilla.ItemsSource = ListaPlantillas.SelectedItem is PlantillaLinea plantilla
            ? _lineas.ObtenerArbolPlantilla(plantilla.Id).Select(ConvertirPlantilla).ToList()
            : null;
    }

    private static VagonPlantillaVm ConvertirPlantilla(NodoPlantilla nodo) =>
        new()
        {
            Vagon = nodo.Vagon,
            Texto =
                nodo.Vagon.Nombre
                + (nodo.Vagon.EsMultiple ? "  (ramificado)" : string.Empty)
                + (nodo.Vagon.EsAnexo ? "  (anexo)" : string.Empty),
            Hijos = nodo.Hijos.Select(ConvertirPlantilla).ToList(),
        };

    private void ListaPlantillas_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RefrescarArbolPlantilla();

    private void BotonReglaAlerta_Click(object sender, RoutedEventArgs e)
    {
        if (ListaPlantillas.SelectedItem is not PlantillaLinea modelo)
        {
            MostrarMensaje("Elija un modelo de cadena para configurar el aviso.");
            return;
        }
        var vagones = _lineas
            .ObtenerArbolPlantilla(modelo.Id)
            .SelectMany(nodo => new[] { nodo.Vagon }.Concat(nodo.Hijos.Select(hijo => hijo.Vagon)))
            .ToList();
        if (vagones.Count < 2)
        {
            MostrarMensaje("El modelo necesita al menos dos documentos para configurar el aviso.");
            return;
        }
        var dialogo = new DialogoReglaAlerta(modelo.Id, vagones) { Owner = this };
        if (dialogo.ShowDialog() == true)
            MostrarMensaje("El aviso quedó configurado para este modelo.");
    }

    private void ArbolPlantilla_SelectedItemChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e
    ) { }

    private void RefrescarCadenas()
    {
        if (PanelModelos.Visibility == Visibility.Visible)
        {
            return;
        }

        if (_cadenaActivaId is long cadenaId)
        {
            MostrarCadena(cadenaId);
            return;
        }

        PanelCadena.Visibility = Visibility.Collapsed;
        PanelEntrada.Visibility = Visibility.Visible;
        BotonVincularCadena.IsEnabled = _documentoActual is not null;
        TextoEntrada.Text = _documentoActual is null
            ? "Abra un documento y vínclelo a una cadena documental."
            : "El documento observado no pertenece a ninguna cadena documental. Puede vincularlo a una existente o crear una nueva.";
    }

    private void MostrarCadena(long cadenaId)
    {
        try
        {
            var cadena = _lineas.ObtenerInstancia(cadenaId);
            TextoCadenaActiva.Text = $"Cadena: {_lineas.ObtenerNombreVisible(cadena)}";
        }
        catch
        {
            _cadenaActivaId = null;
            PanelCadena.Visibility = Visibility.Collapsed;
            PanelEntrada.Visibility = Visibility.Visible;
            return;
        }

        _cadenaActivaId = cadenaId;
        PanelModelos.Visibility = Visibility.Collapsed;
        PanelEntrada.Visibility = Visibility.Collapsed;
        PanelCadena.Visibility = Visibility.Visible;
        ListaFilasCadena.ItemsSource = ConstruirFilas(cadenaId, string.Empty);
        ActualizarAlertasCadena(cadenaId);
        Pestanas.SelectedIndex = 1;
    }

    private void ActualizarAlertasCadena(long cadenaId)
    {
        try
        {
            using var conexion = Hormiguero.Nucleo.Datos.BaseComun.Abrir(
                Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun
            );
            var alertas = new Hormiguero.Nucleo.Datos.RepositorioAlertas(conexion)
                .Listar(cadenaId: cadenaId, limite: 10000)
                .Alertas;
            var abiertas = alertas.Where(a => a.Estado is "pendiente" or "vencida").ToList();
            TextoAlertasCadena.Text =
                abiertas.Count == 0
                    ? "No hay avisos pendientes para esta cadena."
                    : string.Join(
                        "\n",
                        abiertas.Select(a =>
                            $"{a.Texto} — {Buscadero.Core.Alertas.PresentacionAlertas.ParaCuando(a.FechaObjetivo, DateOnly.FromDateTime(DateTime.Today), Hormiguero.Nucleo.Utilidades.TipoDias.Habiles)} ({(a.Estado == "vencida" ? "Vencida" : "Pendiente")})"
                        )
                    );
        }
        catch (Exception error)
        {
            TextoAlertasCadena.Text =
                $"No se pudieron cargar los avisos de esta cadena: {error.Message}";
        }
    }

    private async void CajaCadena_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || (sender as FrameworkElement)?.Tag is not CajaCadenaVm caja)
        {
            return;
        }

        if (caja.RutaDocumento is null)
        {
            return;
        }

        await AbrirVistaRapidaAsync(caja.RutaDocumento, caja.NombreDocumento ?? caja.Texto);
    }

    private async Task AbrirVistaRapidaAsync(string ruta, string nombreVisible)
    {
        if (_documentoActual is null)
        {
            return;
        }

        if (
            string.Equals(
                ruta,
                _documentoEspiado ?? _documentoActual,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        try
        {
            var fuente = await Task.Run(() => VisorPdf.RenderizarPagina(ruta, 0));

            _documentoEspiado = ruta;
            ImagenPdf.Source = fuente;
            AplicarZoom();
            CanvasMarcas.Children.Clear();

            BarraVisor.IsEnabled = false;
            BarraMarcas.Visibility = Visibility.Collapsed;
            BarraEspiar.Visibility = Visibility.Visible;
            BotonVolverEspiar.Content = $"Volver a {System.IO.Path.GetFileName(_documentoActual)}";
            BotonObservarEspiado.Content = $"Observar {nombreVisible}";
        }
        catch (Exception excepcion)
        {
            MessageBox.Show(
                this,
                $"No se pudo abrir la vista rápida.\n\nDetalle: {excepcion.Message}",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private async void BotonVolverEspiar_Click(object sender, RoutedEventArgs e)
    {
        _documentoEspiado = null;
        BarraEspiar.Visibility = Visibility.Collapsed;
        BarraVisor.IsEnabled = true;
        BarraMarcas.Visibility = Visibility.Visible;
        await RenderizarPaginaActualAsync();
    }

    private async void BotonObservarEspiado_Click(object sender, RoutedEventArgs e)
    {
        if (_documentoEspiado is not string ruta)
        {
            return;
        }

        _documentoEspiado = null;
        BarraEspiar.Visibility = Visibility.Collapsed;
        BarraVisor.IsEnabled = true;
        await AbrirDocumentoAsync(ruta);
    }

    private List<FilaCadenaVm> ConstruirFilas(long cadenaId, string prefijo)
    {
        var filas = new List<FilaCadenaVm>();
        var documentos = _lineas.ObtenerArbolInstancia(cadenaId);
        filas.Add(
            new FilaCadenaVm { Prefijo = prefijo, Cajas = ConstruirCajas(cadenaId, documentos) }
        );

        foreach (var nodo in documentos)
        {
            if (!nodo.Vagon.EsMultiple)
            {
                continue;
            }

            foreach (var hija in _lineas.ObtenerCadenasHijas(nodo.Vagon.Id))
            {
                var filasHijas = ConstruirFilas(hija.Id, "└→ ");
                if (filasHijas.Count > 0)
                {
                    var tieneArchivo = TieneAlgunArchivoVinculado(hija.Id);
                    var accion = new AccionCadenaVm
                    {
                        Texto = "Quitar cadena hija",
                        Accion = TipoAccion.QuitarCadenaHija,
                        InstanciaId = hija.Id,
                        RequiereConfirmacion = tieneArchivo,
                    };

                    filasHijas[0] = new FilaCadenaVm
                    {
                        Prefijo = filasHijas[0].Prefijo,
                        Cajas = AgregarAccionAPrimera(filasHijas[0].Cajas, accion),
                    };
                }

                filas.AddRange(filasHijas);
            }
        }

        return filas;
    }

    private List<CajaCadenaVm> ConstruirCajas(long cadenaId, IReadOnlyList<NodoInstancia> nodos)
    {
        var cajas = new List<CajaCadenaVm>();
        var indice = 0;
        foreach (var nodo in nodos)
        {
            indice++;
            cajas.Add(ConstruirCaja(cadenaId, nodo.Vagon, esUltima: indice == nodos.Count));
            foreach (var anexo in nodo.Hijos)
            {
                cajas.Add(ConstruirCaja(cadenaId, anexo.Vagon, esUltima: false));
            }
        }

        return cajas;
    }

    private CajaCadenaVm ConstruirCaja(long cadenaId, InstanciaVagon documento, bool esUltima)
    {
        var acciones = new List<AccionCadenaVm>();

        if (documento.NombreDocumento is null)
        {
            acciones.Add(
                new AccionCadenaVm
                {
                    Texto = "Buscar y vincular...",
                    Accion = TipoAccion.BuscarVincular,
                    InstanciaId = cadenaId,
                    InstanciaVagonId = documento.Id,
                }
            );
        }
        else
        {
            acciones.Add(
                new AccionCadenaVm
                {
                    Texto = "Deshacer vínculo",
                    Accion = TipoAccion.QuitarVinculo,
                    InstanciaId = cadenaId,
                    InstanciaVagonId = documento.Id,
                }
            );
        }

        if (documento.EsAnexo)
        {
            acciones.Add(
                new AccionCadenaVm
                {
                    Texto = "Quitar anexo",
                    Accion = TipoAccion.QuitarAnexo,
                    InstanciaId = cadenaId,
                    InstanciaVagonId = documento.Id,
                    RequiereConfirmacion = documento.NombreDocumento is not null,
                }
            );
        }

        if (documento.PlantillaVagonId is long plantillaVagonId)
        {
            var estructura = _lineas.BuscarNodoEstructura(cadenaId, plantillaVagonId);
            if (estructura is not null && !documento.EsAnexo)
            {
                var materializados = _lineas
                    .ObtenerArbolInstancia(cadenaId)
                    .Where(n =>
                        n.Vagon.PadreId == documento.Id
                        && n.Vagon.EsAnexo
                        && n.Vagon.PlantillaVagonId is not null
                    )
                    .Select(n => n.Vagon.PlantillaVagonId!.Value)
                    .ToHashSet();

                foreach (
                    var anexo in estructura.Hijos.Where(h =>
                        h.EsAnexo && !materializados.Contains(h.PlantillaVagonId)
                    )
                )
                {
                    acciones.Add(
                        new AccionCadenaVm
                        {
                            Texto = $"+ Agregar documento anexo: {anexo.Nombre}",
                            Accion = TipoAccion.AgregarAnexo,
                            InstanciaId = cadenaId,
                            InstanciaVagonId = documento.Id,
                            PlantillaVagonId = anexo.PlantillaVagonId,
                        }
                    );
                }
            }

            if (documento.EsMultiple)
            {
                var nombreHijo = estructura?.NombreModeloCadenaHija ?? "cadena hija";
                acciones.Add(
                    new AccionCadenaVm
                    {
                        Texto = $"+ Agregar cadena hija: {nombreHijo}",
                        Accion = TipoAccion.AgregarCadenaHija,
                        InstanciaId = cadenaId,
                        InstanciaVagonId = documento.Id,
                        PlantillaVagonId = plantillaVagonId,
                    }
                );
            }
        }

        return new CajaCadenaVm
        {
            Texto =
                documento.Nombre
                + " — "
                + (documento.NombreDocumento ?? "(vacío)")
                + (documento.AvisoDocumentoModificado ? " — el contenido cambió" : string.Empty),
            ColorFondo = documento.NombreDocumento is null ? FondoCajaVacia : FondoCajaConDocumento,
            ColorBorde = documento.NombreDocumento is null ? BordeCajaVacia : BordeCajaConDocumento,
            Imagen = CargarMiniatura(documento.RutaDocumento),
            Conector = esUltima ? string.Empty : "→",
            Acciones = acciones,
            RutaDocumento = documento.RutaDocumento,
            NombreDocumento = documento.NombreDocumento,
        };
    }

    private bool TieneAlgunArchivoVinculado(long cadenaId)
    {
        try
        {
            return _lineas
                .ObtenerTodosLosDocumentos(cadenaId)
                .Any(d => d.NombreDocumento is not null);
        }
        catch
        {
            // Caso-10: ante cualquier error inesperado al evaluar esto, es preferible
            // pedir confirmacion de mas que borrar una cadena hija sin avisar.
            return true;
        }
    }

    private static CajaCadenaVm AgregarAccion(CajaCadenaVm caja, AccionCadenaVm accion) =>
        new()
        {
            Texto = caja.Texto,
            ColorFondo = caja.ColorFondo,
            ColorBorde = caja.ColorBorde,
            Imagen = caja.Imagen,
            Conector = caja.Conector,
            Acciones = caja.Acciones.Concat(new[] { accion }).ToList(),
        };

    private static IReadOnlyList<CajaCadenaVm> AgregarAccionAPrimera(
        IReadOnlyList<CajaCadenaVm> cajas,
        AccionCadenaVm accion
    )
    {
        var lista = cajas.ToList();
        if (lista.Count > 0)
        {
            lista[0] = AgregarAccion(lista[0], accion);
        }

        return lista;
    }

    private static BitmapSource? CargarMiniatura(string? ruta)
    {
        if (ruta is null || !System.IO.File.Exists(ruta))
        {
            return null;
        }

        try
        {
            return VisorPdf.RenderizarPagina(ruta, 0);
        }
        catch
        {
            return null;
        }
    }

    private void RefrescarPrevisualizacion()
    {
        if (_documentoActual is null)
        {
            PanelPrevisualizacion.Visibility = Visibility.Collapsed;
            return;
        }

        var coincidencias = _lineas.BuscarCadenasDeDocumento(_documentoActual);
        if (coincidencias.Count == 0)
        {
            PanelPrevisualizacion.Visibility = Visibility.Collapsed;
            return;
        }

        var coincidencia = coincidencias[0];
        _cadenaActivaId = coincidencia.CadenaRaiz.Id;
        var camino = _lineas.ObtenerCaminoCompleto(
            coincidencia.CadenaRaiz.Id,
            coincidencia.Documento.Id
        );
        var cajas = new List<CajaCadenaVm>();
        for (var indice = 0; indice < camino.Count; indice++)
        {
            var documento = camino[indice];
            var esActual = documento.Id == coincidencia.Documento.Id;
            cajas.Add(
                new CajaCadenaVm
                {
                    Texto =
                        documento.Nombre
                        + " — "
                        + (documento.NombreDocumento ?? "(vacío)")
                        + (
                            documento.AvisoDocumentoModificado
                                ? " — el contenido cambió"
                                : string.Empty
                        ),
                    ColorFondo =
                        esActual ? FondoCajaActual
                        : documento.NombreDocumento is null ? FondoCajaVacia
                        : FondoCajaConDocumento,
                    ColorBorde =
                        esActual ? BordeCajaActual
                        : documento.NombreDocumento is null ? BordeCajaVacia
                        : BordeCajaConDocumento,
                    Conector = indice == camino.Count - 1 ? string.Empty : "→",
                    Acciones = Array.Empty<AccionCadenaVm>(),
                    RutaDocumento = documento.RutaDocumento,
                    NombreDocumento = documento.NombreDocumento,
                }
            );
        }

        ListaPrevisualizacion.ItemsSource = cajas;
        PanelPrevisualizacion.Visibility = Visibility.Visible;
    }

    private void BotonAbrirModelos_Click(object sender, RoutedEventArgs e)
    {
        PanelModelos.Visibility = Visibility.Visible;
        PanelEntrada.Visibility = Visibility.Collapsed;
        PanelCadena.Visibility = Visibility.Collapsed;
        RefrescarPlantillas();
    }

    private void BotonVolverModelos_Click(object sender, RoutedEventArgs e)
    {
        PanelModelos.Visibility = Visibility.Collapsed;
        RefrescarCadenas();
    }

    private void BotonModeloGuiado_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new DialogoModeloGuiado(_lineas) { Owner = this };
        if (dialogo.ShowDialog() == true)
        {
            RefrescarPlantillas();
            RefrescarCadenas();
        }
    }

    private void BotonVincularCadena_Click(object sender, RoutedEventArgs e) => AbrirVinculacion();

    private void BotonVincularVisualizado_Click(object sender, RoutedEventArgs e) =>
        AbrirVinculacion();

    private void BotonVincularDocumentoAVacio_Click(object sender, RoutedEventArgs e)
    {
        if (_documentoActual is null)
        {
            TextoEstadoCadena.Text = "Abra primero un documento en la pestaña Documento.";
            return;
        }

        if (_cadenaActivaId is not long cadenaId)
        {
            AbrirVinculacion();
            return;
        }

        var destino = DialogoElegirDestino.Elegir(
            this,
            _lineas.ObtenerTodosLosDocumentos(cadenaId)
        );
        if (destino is null)
        {
            return;
        }

        if (destino.NombreDocumento is not null)
        {
            MessageBox.Show(
                this,
                "Este documento ya tiene un archivo vinculado y no admite documentos anexos.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        _lineas.VincularDocumento(destino.Id, _documentoActual);
        RefrescarPrevisualizacion();
        MostrarCadena(cadenaId);
    }

    private void BotonComparar_Click(object sender, RoutedEventArgs e)
    {
        if (_documentoActual is null)
        {
            return;
        }

        var dialogo = new DialogoComparar(_lineas, _servicioBusqueda, _documentoActual)
        {
            Owner = this,
        };
        if (dialogo.ShowDialog() != true || dialogo.RutaSeleccionada is not string segunda)
        {
            return;
        }

        if (string.Equals(segunda, _documentoActual, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                this,
                "Elija un documento distinto del que está observando.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }

        var ventana = new VentanaComparar(_repositorioMarcas, _documentoActual, segunda)
        {
            Owner = this,
        };
        ventana.ShowDialog();
    }

    private void AbrirVinculacion()
    {
        if (_documentoActual is null)
        {
            TextoEstadoCadena.Text = "Abra primero un documento en la pestaña Documento.";
            return;
        }

        var dialogo = new DialogoVincularCadena(_lineas, _servicioBusqueda, _documentoActual)
        {
            Owner = this,
        };
        if (dialogo.ShowDialog() != true)
        {
            return;
        }

        var cadenaId = dialogo.CadenaResultanteId ?? dialogo.CadenaCreadaId;
        if (cadenaId is not long id)
        {
            return;
        }

        _cadenaActivaId = id;
        Pestanas.SelectedIndex = 1;
        RefrescarPrevisualizacion();
        MostrarCadena(id);
    }

    private void BotonCerrarCadena_Click(object sender, RoutedEventArgs e)
    {
        _cadenaActivaId = null;
        RefrescarCadenas();
    }

    private void BotonBorrarCadena_Click(object sender, RoutedEventArgs e)
    {
        if (_cadenaActivaId is not long cadenaId)
        {
            return;
        }

        var confirmado = DialogoEspera.Pedir(
            this,
            "¿Borrar la cadena documental activa? Se perderá todo su trabajo de relación. Esta acción no se puede deshacer."
        );
        if (!confirmado)
        {
            return;
        }

        _lineas.BorrarInstancia(cadenaId);
        _cadenaActivaId = null;
        RefrescarPrevisualizacion();
        RefrescarCadenas();
    }

    private void CasillaPrevisualizacion_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not CajaCadenaVm casilla)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            _temporizadorClicPrevisualizacion?.Stop();
            _temporizadorClicPrevisualizacion = null;
            if (_cadenaActivaId is long cadenaId)
            {
                Pestanas.SelectedIndex = 1;
                MostrarCadena(cadenaId);
            }

            return;
        }

        if (e.ClickCount != 1)
        {
            return;
        }

        // Caso-14: un clic simple espia el documento (vista rapida); recien el doble
        // clic navega a la vista completa. Se demora la accion del clic simple lo que
        // dura la ventana de doble clic del sistema, para poder distinguir ambos casos.
        _temporizadorClicPrevisualizacion?.Stop();
        _temporizadorClicPrevisualizacion = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400),
        };
        _temporizadorClicPrevisualizacion.Tick += async (_, _) =>
        {
            _temporizadorClicPrevisualizacion?.Stop();
            _temporizadorClicPrevisualizacion = null;
            if (casilla.RutaDocumento is string ruta)
            {
                await AbrirVistaRapidaAsync(ruta, casilla.NombreDocumento ?? casilla.Texto);
            }
        };
        _temporizadorClicPrevisualizacion.Start();
    }

    private void BotonAccionCadena_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not AccionCadenaVm accion)
        {
            return;
        }

        try
        {
            switch (accion.Accion)
            {
                case TipoAccion.AgregarCadenaHija:
                    _lineas.AgregarCadenaHija(accion.InstanciaId, accion.InstanciaVagonId);
                    break;
                case TipoAccion.AgregarAnexo:
                    _lineas.AgregarAnexo(
                        accion.InstanciaId,
                        accion.PlantillaVagonId,
                        accion.InstanciaVagonId
                    );
                    break;
                case TipoAccion.BuscarVincular:
                    if (!BuscarVincularEnDocumento(accion.InstanciaVagonId))
                    {
                        return;
                    }

                    break;
                case TipoAccion.QuitarVinculo:
                    _lineas.DesvincularDocumento(accion.InstanciaVagonId);
                    break;
                case TipoAccion.QuitarCadenaHija:
                    if (
                        accion.RequiereConfirmacion
                        && !DialogoEspera.Pedir(
                            this,
                            "La cadena hija tiene documentos vinculados. ¿Quitarla de todos modos? Se perderá su trabajo de relación."
                        )
                    )
                    {
                        return;
                    }

                    _lineas.BorrarInstancia(accion.InstanciaId);
                    break;
                case TipoAccion.QuitarAnexo:
                    if (
                        accion.RequiereConfirmacion
                        && !DialogoEspera.Pedir(
                            this,
                            "El documento anexo tiene un archivo vinculado. ¿Quitarlo de todos modos?"
                        )
                    )
                    {
                        return;
                    }

                    _lineas.QuitarVagonInstancia(accion.InstanciaVagonId);
                    break;
            }

            RefrescarPrevisualizacion();
            MostrarCadena(_cadenaActivaId ?? accion.InstanciaId);
        }
        catch (Exception excepcion)
        {
            TextoEstadoCadena.Text = excepcion.Message;
        }
    }

    private bool BuscarVincularEnDocumento(long instanciaVagonId)
    {
        var dialogo = new DialogoBuscarDocumento(_servicioBusqueda) { Owner = this };
        if (dialogo.ShowDialog() != true || dialogo.RutaSeleccionada is null)
        {
            return false;
        }

        _lineas.VincularDocumento(instanciaVagonId, dialogo.RutaSeleccionada);
        return true;
    }

    private void BotonNuevaPlantilla_Click(object sender, RoutedEventArgs e)
    {
        var nombre = DialogoTexto.Pedir(this, "Nuevo modelo de cadena", "Nombre del modelo:");
        if (nombre is null)
        {
            return;
        }

        try
        {
            _lineas.CrearPlantilla(nombre);
            RefrescarPlantillas();
        }
        catch (Exception excepcion)
        {
            TextoEstadoPlantilla.Text = excepcion.Message;
        }
    }

    private void BotonRenombrarPlantilla_Click(object sender, RoutedEventArgs e)
    {
        if (ListaPlantillas.SelectedItem is not PlantillaLinea plantilla)
        {
            TextoEstadoPlantilla.Text = "Seleccione un modelo de la lista.";
            return;
        }

        var nombre = DialogoTexto.Pedir(
            this,
            "Renombrar modelo",
            "Nuevo nombre:",
            plantilla.Nombre
        );
        if (nombre is null)
        {
            return;
        }

        try
        {
            _lineas.RenombrarPlantilla(plantilla.Id, nombre);
            RefrescarPlantillas();
        }
        catch (Exception excepcion)
        {
            TextoEstadoPlantilla.Text = excepcion.Message;
        }
    }

    private void BotonBorrarPlantilla_Click(object sender, RoutedEventArgs e)
    {
        if (ListaPlantillas.SelectedItem is not PlantillaLinea plantilla)
        {
            TextoEstadoPlantilla.Text = "Seleccione un modelo de la lista.";
            return;
        }

        var confirmacion = MessageBox.Show(
            this,
            $"¿Borrar el modelo de cadena \"{plantilla.Nombre}\"? Las cadenas ya creadas no se modifican.",
            "Buscadero",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );
        if (confirmacion != MessageBoxResult.Yes)
        {
            return;
        }

        _lineas.BorrarPlantilla(plantilla.Id);
        RefrescarPlantillas();
    }

    private void BotonAgregarVagon_Click(object sender, RoutedEventArgs e)
    {
        if (ListaPlantillas.SelectedItem is not PlantillaLinea plantilla)
        {
            TextoEstadoPlantilla.Text = "Seleccione un modelo de la lista.";
            return;
        }

        var principal = (ArbolPlantilla.SelectedItem as VagonPlantillaVm)?.Vagon;
        var modelos = _lineas.ObtenerModelosIndependientes();
        if (
            !DialogoVagon.Pedir(
                this,
                "Agregar documento",
                string.Empty,
                false,
                false,
                null,
                modelos,
                out var nombre,
                out var esMultiple,
                out var esAnexo,
                out var modeloHijoId,
                out var crearModeloNuevo
            )
        )
        {
            return;
        }

        try
        {
            if (esMultiple && crearModeloNuevo)
            {
                var nombreModelo = DialogoTexto.Pedir(
                    this,
                    "Nuevo modelo de cadena",
                    "Nombre del modelo de cadena hija:"
                );
                if (nombreModelo is null)
                {
                    return;
                }

                modeloHijoId = _lineas.CrearPlantilla(nombreModelo).Id;
                RefrescarPlantillas();
            }

            if (esAnexo && principal is null)
            {
                TextoEstadoPlantilla.Text =
                    "Seleccione el documento principal del anexo en el árbol.";
                return;
            }

            _lineas.AgregarVagon(
                plantilla.Id,
                esAnexo ? principal!.Id : null,
                nombre,
                esMultiple,
                esAnexo,
                modeloHijoId
            );
            RefrescarArbolPlantilla();
        }
        catch (Exception excepcion)
        {
            TextoEstadoPlantilla.Text = excepcion.Message;
        }
    }

    private void BotonEditarVagon_Click(object sender, RoutedEventArgs e)
    {
        if (ArbolPlantilla.SelectedItem is not VagonPlantillaVm vm)
        {
            TextoEstadoPlantilla.Text = "Seleccione un documento en el árbol.";
            return;
        }

        var modelos = _lineas.ObtenerModelosIndependientes();
        if (
            !DialogoVagon.Pedir(
                this,
                "Editar documento",
                vm.Vagon.Nombre,
                vm.Vagon.EsMultiple,
                vm.Vagon.EsAnexo,
                vm.Vagon.ModeloCadenaHijaId,
                modelos,
                out var nombre,
                out var esMultiple,
                out var esAnexo,
                out var modeloHijoId,
                out var crearModeloNuevo
            )
        )
        {
            return;
        }

        try
        {
            if (esMultiple && crearModeloNuevo)
            {
                var nombreModelo = DialogoTexto.Pedir(
                    this,
                    "Nuevo modelo de cadena",
                    "Nombre del modelo de cadena hija:"
                );
                if (nombreModelo is null)
                {
                    return;
                }

                modeloHijoId = _lineas.CrearPlantilla(nombreModelo).Id;
                RefrescarPlantillas();
            }

            _lineas.ActualizarVagon(vm.Vagon.Id, nombre, esMultiple, esAnexo, modeloHijoId);
            RefrescarArbolPlantilla();
        }
        catch (Exception excepcion)
        {
            TextoEstadoPlantilla.Text = excepcion.Message;
        }
    }

    private void BotonBorrarVagon_Click(object sender, RoutedEventArgs e)
    {
        if (ArbolPlantilla.SelectedItem is not VagonPlantillaVm vm)
        {
            TextoEstadoPlantilla.Text = "Seleccione un documento en el árbol.";
            return;
        }

        var confirmacion = MessageBox.Show(
            this,
            $"¿Borrar el documento \"{vm.Vagon.Nombre}\" y sus anexos?",
            "Buscadero",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );
        if (confirmacion != MessageBoxResult.Yes)
        {
            return;
        }

        _lineas.BorrarVagon(vm.Vagon.Id);
        RefrescarArbolPlantilla();
    }
}

public sealed class CarpetaVm
{
    public required int Id { get; init; }
    public required string Ruta { get; init; }
    public required string Estado { get; init; }
}

public sealed class ResultadoBusquedaVm
{
    public required string Ruta { get; init; }
    public required string Carpeta { get; init; }
    public required string Nombre { get; init; }
    public required string Modificado { get; init; }
}

public sealed class VagonPlantillaVm
{
    public required PlantillaVagon Vagon { get; init; }
    public required string Texto { get; init; }
    public required IReadOnlyList<VagonPlantillaVm> Hijos { get; init; }
}

public sealed class FilaCadenaVm
{
    public required string Prefijo { get; init; }
    public required IReadOnlyList<CajaCadenaVm> Cajas { get; init; }
}

public sealed class CajaCadenaVm
{
    public required string Texto { get; init; }
    public required Brush ColorFondo { get; init; }
    public required Brush ColorBorde { get; init; }
    public BitmapSource? Imagen { get; init; }
    public required string Conector { get; init; }
    public required IReadOnlyList<AccionCadenaVm> Acciones { get; init; }
    public string? RutaDocumento { get; init; }
    public string? NombreDocumento { get; init; }
}

public sealed class AccionCadenaVm
{
    public required string Texto { get; init; }
    public required TipoAccion Accion { get; init; }
    public long InstanciaId { get; init; }
    public long InstanciaVagonId { get; init; }
    public long PlantillaVagonId { get; init; }
    public bool RequiereConfirmacion { get; init; }
}

public enum TipoAccion
{
    AgregarCadenaHija,
    AgregarAnexo,
    BuscarVincular,
    QuitarVinculo,
    QuitarCadenaHija,
    QuitarAnexo,
}
