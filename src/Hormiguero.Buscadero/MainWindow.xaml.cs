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
using Buscadero.Core.Pdf;
using Hormiguero.Diseno;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Pdf;
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
    private Point _desplazamiento;
    private Point? _vistaPrevia;
    private Point _inicioArrastre;
    private Point _finArrastre;
    private bool _arrastrando;
    private bool _inicializandoTema = true;
    private int _busquedaMaestraId;
    private DocumentoBuscadorMaestro? _documentoMaestroSeleccionado;

    public MainWindow()
    {
        InitializeComponent();
        MaestroNumero.TextChanged += MaestroFiltroChanged;
        ComboTema.SelectedIndex = IndiceTema(DatosDeApp.LeerPreferencia("tema.buscadero"));
        _inicializandoTema = false;

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
        PanelBusquedaSecundario.Configurar(_servicioBusqueda, _repositorioMarcas);
        Closed += (_, _) => _repositorioMarcas.Dispose();

        _ = EvaluarAlertasAlAbrirAsync();
        CargarCarpetas();
        _ = CargarTiposMaestroAsync();
        ActualizarSugerenciasCarpeta();
        ActualizarContadorDudosos();
        _indexadoEnSegundoPlano.Pedir();
        ActualizarContadorAlertas();
    }

    private static int IndiceTema(string? preferencia) =>
        Enum.TryParse<ModoTema>(preferencia, out var modo) ? (int)modo : (int)ModoTema.Sistema;

    private void ComboTema_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_inicializandoTema || ComboTema.SelectedIndex < 0)
        {
            return;
        }

        var modo = (ModoTema)ComboTema.SelectedIndex;
        DatosDeApp.GuardarPreferencia("tema.buscadero", modo.ToString());
        Tema.Aplicar(Application.Current, modo);
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

    private void BotonSegundaBusqueda_Click(object sender, RoutedEventArgs e)
    {
        ColumnaPanelSecundario.Width = new GridLength(1, GridUnitType.Star);
        PanelBusquedaSecundario.Visibility = Visibility.Visible;
        SeparadorPaneles.Visibility = Visibility.Visible;
        BotonSegundaBusqueda.Visibility = Visibility.Collapsed;
        BotonCerrarSegundaBusqueda.Visibility = Visibility.Visible;
    }

    private void BotonCerrarSegundaBusqueda_Click(object sender, RoutedEventArgs e)
    {
        PanelBusquedaSecundario.Limpiar();
        PanelBusquedaSecundario.Visibility = Visibility.Collapsed;
        SeparadorPaneles.Visibility = Visibility.Collapsed;
        ColumnaPanelSecundario.Width = new GridLength(0);
        BotonSegundaBusqueda.Visibility = Visibility.Visible;
        BotonCerrarSegundaBusqueda.Visibility = Visibility.Collapsed;
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
                ? (ComboFiltroCarpeta.SelectedItem as OpcionCarpetaBusqueda)?.Ruta
                    ?? ComboFiltroCarpeta.Text.Trim()
                : string.Empty;
        var carpetaSinIndexar = filtro.Length > 0 && !_servicioBusqueda.EstaCarpetaIndexada(filtro);

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

            if (carpetaSinIndexar)
            {
                TextoEstadoBusqueda.Text +=
                    " Esta carpeta aún se está indexando; puede faltar algún resultado.";
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

    private async void ComboAlcance_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
            ComboFiltroCarpeta.Text = string.Empty;
            await ActualizarCarpetasAlcanceAsync();
        }
    }

    private IReadOnlyList<OpcionCarpetaBusqueda> _carpetasAlcance = [];
    private int _generacionSelectorCarpetas;

    private async Task ActualizarCarpetasAlcanceAsync()
    {
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
            // El texto cambió porque el usuario eligió una sugerencia, no porque está
            // escribiendo — no pisar la selección volviendo a filtrar (Caso-14).
            return;
        }

        if (ObtenerAlcanceSeleccionado() == AlcanceBusqueda.CarpetaEspecifica)
        {
            // Caso-15 (Javier, 2026-10-06): filtrado instantáneo en memoria, en cualquier
            // parte de la ruta y sin mínimo de letras, sobre la lista completa (madres +
            // subcarpetas ya indexadas, en ese orden) — sin consultar la base en cada letra.
            var coincidenciasCarpeta = ServicioBusqueda.FiltrarCarpetasParaSelector(
                _carpetasAlcance,
                texto
            );

            ComboFiltroCarpeta.ItemsSource = coincidenciasCarpeta;
            ComboFiltroCarpeta.Text = texto;
            ComboFiltroCarpeta.IsDropDownOpen = coincidenciasCarpeta.Count > 0;
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
        BotonImprimir.IsEnabled = false;
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
            BotonImprimir.IsEnabled = true;
            BarraVisor.Visibility = Visibility.Visible;
            BarraMarcas.Visibility = Visibility.Visible;
            TextoVisorVacio.Visibility = Visibility.Collapsed;

            await RenderizarPaginaActualAsync();
        }
        catch (Exception excepcion)
        {
            _documentoActual = null;
            _sesionMarcas = null;
            ImagenPdf.Source = null;
            CanvasMarcas.Children.Clear();
            BarraVisor.Visibility = Visibility.Collapsed;
            BotonImprimir.IsEnabled = false;
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

    private void BotonImprimir_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button boton)
        {
            boton.ContextMenu.PlacementTarget = boton;
            boton.ContextMenu.IsOpen = true;
        }
    }

    private async void OpcionImprimir_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string etiqueta })
        {
            return;
        }

        var opcion = Enum.Parse<OpcionImpresion>(etiqueta);
        await ImprimirDocumentoAsync(_documentoActual, opcion);
    }

    private async Task ImprimirDocumentoAsync(string? ruta, OpcionImpresion opcion)
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
        TextoAvisoImpresion.Text = mensaje;
        await Task.Delay(TimeSpan.FromSeconds(5));
        if (TextoAvisoImpresion.Text == mensaje)
        {
            TextoAvisoImpresion.Text = string.Empty;
        }
    }

    private void Ventana_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || e.Key != Key.P)
        {
            return;
        }

        if (PanelBusquedaSecundario.IsKeyboardFocusWithin)
        {
            PanelBusquedaSecundario.ImprimirPrimeraPagina();
        }
        else if (_documentoActual is not null)
        {
            _ = ImprimirDocumentoAsync(_documentoActual, OpcionImpresion.PrimeraPagina);
        }

        e.Handled = true;
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

    private void BotonCadenasSimples_Click(object sender, RoutedEventArgs e)
    {
        string ruta = Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun;
        try
        {
            using (var conexion = Hormiguero.Nucleo.Datos.BaseComun.Abrir(ruta))
            {
                var repositorioCadenas = new RepositorioCadenas(conexion);
                if (!repositorioCadenas.TransicionCadenasSimplesAceptada())
                {
                    MessageBox.Show(
                        this,
                        "Las cadenas ahora se crean de otra forma. Los modelos anteriores dejan de usarse; los documentos no se tocan.",
                        "Cadenas documentales",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    repositorioCadenas.AceptarTransicionCadenasSimples();
                }
            }
            new DialogoCadenasSimples { Owner = this }.ShowDialog();
            ActualizarContadorAlertas();
        }
        catch (Exception error)
        {
            MostrarMensaje($"No se pudieron abrir las cadenas: {error.Message}");
        }
    }

    private void ActualizarContadorDudosos()
    {
        try
        {
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            int cantidad = new RepositorioReglasYEnlaces(conexion)
                .ListarDudososCadenasSimples()
                .Count;
            BotonDudosos.Content = $"{cantidad} dudosos";
        }
        catch (Exception error)
        {
            BotonDudosos.Content = "Dudosos no disponibles";
            MostrarMensaje($"No se pudieron cargar los dudosos: {error.Message}");
        }
    }

    private void BotonDudosos_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new DialogoDudosos { Owner = this };
        dialogo.ShowDialog();
        ActualizarContadorDudosos();
    }

    private void ActualizarContadorAlertas()
    {
        try
        {
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            var cadenasSimples = new RepositorioCadenas(conexion)
                .ListarCadenasSimples()
                .Select(c => c.Id)
                .ToHashSet();
            var alertas = new RepositorioAlertas(conexion)
                .Listar(limite: 10000)
                .Alertas.Where(a =>
                    a.ReglaId is null
                    && (a.CadenaId is null || cadenasSimples.Contains(a.CadenaId.Value))
                )
                .ToArray();
            int abiertas = alertas.Count(a => a.Estado is "pendiente" or "vencida");
            int vencidas = alertas.Count(a => a.Estado == "vencida");
            BotonAlertas.Content = $"{abiertas} alertas";
            BotonAlertas.Background = (Brush)
                Application.Current.FindResource(
                    vencidas > 0 ? "Hormiguero.AvisoSuave" : "Hormiguero.Superficie"
                );
            BotonAlertas.BorderBrush = (Brush)
                Application.Current.FindResource(
                    vencidas > 0 ? "Hormiguero.Aviso" : "Hormiguero.Borde"
                );
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
            string ruta = DocumentosGuardados.RutaBaseComun;
            using (var conexion = BaseComun.Abrir(ruta))
                new EvaluadorAlertas(conexion).Evaluar();
            new DialogoAlertas(ruta, _ => { }) { Owner = this }.ShowDialog();
        }
        catch (Exception error)
        {
            MostrarMensaje($"No se pudieron abrir las alertas: {error.Message}");
        }
        ActualizarContadorAlertas();
    }

    private void BotonCalcularFecha_Click(object sender, RoutedEventArgs e) =>
        new DialogoCalculadoraFechas { Owner = this }.ShowDialog();

    private void BotonFeriados_Click(object sender, RoutedEventArgs e) =>
        new DialogoFeriados { Owner = this }.ShowDialog();

    private void BotonReglaAlerta_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new DialogoReglaAlerta { Owner = this };
        if (dialogo.ShowDialog() == true)
            MostrarMensaje("La regla quedó configurada para las cadenas simples.");
    }

    private void BotonComparar_Click(object sender, RoutedEventArgs e)
    {
        if (_documentoActual is null)
        {
            return;
        }

        var dialogo = new DialogoComparar(_servicioBusqueda) { Owner = this };
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

    private async Task CargarTiposMaestroAsync()
    {
        try
        {
            var tipos = await Task.Run(() =>
            {
                using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
                return new RepositorioBuscadorMaestro(conexion).Tipos();
            });
            MaestroTipo.ItemsSource = new string?[] { null }
                .Concat(tipos)
                .ToList();
            MaestroTipo.SelectedIndex = 0;
        }
        catch (Exception error)
        {
            MaestroEstado.Text = $"No se pudieron cargar los tipos: {error.Message}";
        }
    }

    private async void MaestroBuscar_Click(object sender, RoutedEventArgs e) =>
        await BuscarMaestroAsync();

    private void MaestroFiltroChanged(object sender, RoutedEventArgs e) => ++_busquedaMaestraId;

    private async Task BuscarMaestroAsync()
    {
        int id = ++_busquedaMaestraId;
        MaestroEstado.Text = "Buscando...";
        MaestroResultados.ItemsSource = null;
        MaestroDatos.ItemsSource = null;
        MaestroDatosCadena.ItemsSource = null;
        _documentoMaestroSeleccionado = null;
        MaestroVerCadena.IsEnabled = false;
        string? tipo = MaestroTipo.SelectedItem as string;
        string grupo = (MaestroGrupo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todos";
        var filtros = new FiltrosBuscadorMaestro(
            MaestroNumero.Text,
            tipo,
            MaestroEmisor.Text,
            MaestroCliente.Text,
            MaestroEncargado.Text,
            grupo,
            MaestroFechaDesde.SelectedDate,
            MaestroFechaHasta.SelectedDate
        );
        try
        {
            var resultados = await Task.Run(() =>
            {
                using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
                return new RepositorioBuscadorMaestro(conexion).Buscar(filtros);
            });
            if (id != _busquedaMaestraId)
                return;
            MaestroResultados.ItemsSource = resultados.Select(f => new MaestroFilaVm(f)).ToList();
            MaestroEstado.Text = $"{resultados.Count} documento(s).";
        }
        catch (Exception error)
        {
            if (id == _busquedaMaestraId)
                MaestroEstado.Text = $"No se pudo completar la búsqueda: {error.Message}";
        }
    }

    private void MaestroLimpiar_Click(object sender, RoutedEventArgs e)
    {
        ++_busquedaMaestraId;
        MaestroNumero.Clear();
        MaestroTipo.SelectedIndex = 0;
        MaestroEmisor.Clear();
        MaestroCliente.Clear();
        MaestroEncargado.Clear();
        MaestroGrupo.SelectedIndex = 2;
        MaestroFechaDesde.SelectedDate = null;
        MaestroFechaHasta.SelectedDate = null;
        MaestroResultados.ItemsSource = null;
        MaestroDatos.ItemsSource = null;
        MaestroDatosCadena.ItemsSource = null;
        MaestroEstado.Text = "";
        MaestroVerCadena.IsEnabled = false;
        _documentoMaestroSeleccionado = null;
    }

    private async void MaestroResultados_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e
    )
    {
        if (MaestroResultados.SelectedItem is not MaestroFilaVm fila)
            return;
        _documentoMaestroSeleccionado = fila.Documento;
        MaestroDatos.ItemsSource = null;
        MaestroDatosCadena.ItemsSource = null;
        MaestroVerCadena.IsEnabled = fila.Documento.CadenaId.HasValue;
        int id = _busquedaMaestraId;
        try
        {
            var detalle = await Task.Run(() =>
            {
                using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
                var repositorio = new RepositorioBuscadorMaestro(conexion);
                var datos = repositorio.ObtenerDatos(fila.Documento.VersionId);
                var documentos = fila.Documento.CadenaId is long cadenaId
                    ? repositorio.ObtenerCadena(cadenaId)
                    : [];
                return (Datos: datos, Documentos: documentos);
            });
            if (
                id != _busquedaMaestraId
                || _documentoMaestroSeleccionado?.VersionId != fila.Documento.VersionId
            )
                return;
            MaestroDatos.ItemsSource = detalle
                .Datos.Select(d => new MaestroDatoVm(d.Nombre, d.Valor))
                .ToList();
            MaestroDatosCadena.ItemsSource = detalle
                .Documentos.Where(d => d.VersionId != fila.Documento.VersionId)
                .Select(d => new MaestroCadenaVm(
                    $"{d.Tipo} · {d.Emisor} — {d.Numeros}",
                    d.Datos.Select(x => new MaestroDatoVm(x.Nombre, x.Valor)).ToList()
                ))
                .ToList();
        }
        catch (Exception error)
        {
            MaestroEstado.Text = $"No se pudieron cargar los datos de la cadena: {error.Message}";
        }
    }

    private void MaestroVerCadena_Click(object sender, RoutedEventArgs e) =>
        new DialogoCadenasSimples { Owner = this }.ShowDialog();

    private void MaestroCopiar_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string valor)
            return;
        try
        {
            Clipboard.SetText(valor);
            MaestroEstado.Text = "✓ Copiado";
        }
        catch (Exception error)
        {
            MaestroEstado.Text = $"No se pudo copiar: {error.Message}";
        }
    }

    private async void MaestroResultados_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (MaestroResultados.SelectedItem is MaestroFilaVm fila)
        {
            try
            {
                await AbrirDocumentoAsync(fila.Documento.Ruta);
            }
            catch (Exception error)
            {
                MaestroEstado.Text = $"No se pudo abrir el PDF: {error.Message}";
            }
        }
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

public sealed class MaestroFilaVm
{
    public DocumentoBuscadorMaestro Documento { get; }

    public MaestroFilaVm(DocumentoBuscadorMaestro documento) => Documento = documento;

    public string TipoEmisor => $"{Documento.Tipo} · {Documento.Emisor}";
    public string Numeros => Documento.Numeros;
    public string FechaTexto => Documento.Fecha?.ToString("dd/MM/yyyy") ?? "";
    public string Cliente => Documento.Cliente;
    public string Encargado => Documento.Encargado;
    public string Cadena => Documento.Cadena;
}

public sealed record MaestroDatoVm(string Nombre, string Valor)
{
    public string Texto => $"{Nombre}: {Valor}";
}

public sealed record MaestroCadenaVm(string Encabezado, IReadOnlyList<MaestroDatoVm> Datos);
