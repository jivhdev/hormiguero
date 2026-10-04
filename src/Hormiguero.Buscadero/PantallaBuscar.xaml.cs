using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Hormiguero.Buscadero.Logica;
using Hormiguero.Diseno.Controles;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Buscadero;

public sealed partial class PantallaBuscar : UserControl
{
    private readonly SqliteConnection _conexion;
    private readonly ControlIndice _controlIndice;
    private readonly ObservableCollection<ResultadoPresentacion> _resultados = [];
    private CancellationTokenSource? _cts;
    private Window? _ventana;

    public event EventHandler<ResultadoBusqueda>? Elegido;

    public VisorPdf VisorDocumento { get; } = new();

    public PantallaBuscar(SqliteConnection conexion, ControlIndice control)
    {
        InitializeComponent();
        // Se elige aquí y no en el XAML: allí el aviso de selección llega antes
        // de que exista el panel de carpeta específica.
        ComboAlcance.SelectedIndex = 0;
        _conexion = conexion;
        _controlIndice = control;

        ListaResultados.ItemsSource = _resultados;

        // Atajos (Alt+A, Alt+S, Alt+D) en toda la ventana
        Loaded += (_, _) =>
        {
            _ventana = Window.GetWindow(this);
            if (_ventana is not null)
            {
                _ventana.PreviewKeyDown += Atajo;
            }
            CampoNumero.Focus();
            CampoNumero.SelectAll();
        };
        Unloaded += (_, _) =>
        {
            if (_ventana is not null)
            {
                _ventana.PreviewKeyDown -= Atajo;
            }
        };

        // Cargar carpetas para el ComboBox de carpeta específica
        var carpetas = new CarpetasConfiguradas(_conexion).Listar();
        foreach (string carpeta in carpetas)
        {
            ComboCarpetaEspecifica.Items.Add(carpeta);
        }
    }

    private void Atajo(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _cts is not null)
        {
            Cancelar();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.Alt)
        {
            return;
        }

        if (e.SystemKey == Key.A)
        {
            CampoNumero.Focus();
            CampoNumero.SelectAll();
            e.Handled = true;
        }
        else if (e.SystemKey == Key.S)
        {
            if (BtnBuscar.IsEnabled && BtnBuscar.Visibility == Visibility.Visible)
            {
                Buscar();
            }
            e.Handled = true;
        }
        else if (e.SystemKey == Key.D)
        {
            if (BtnBuscarEnTodas.Visibility == Visibility.Visible)
            {
                BuscarEnTodas();
            }
            e.Handled = true;
        }
    }

    private void CampoNumero_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Buscar();
            e.Handled = true;
        }
    }

    private void BtnBuscar_Click(object sender, RoutedEventArgs e) => Buscar();

    private void BtnCancelar_Click(object sender, RoutedEventArgs e) => Cancelar();

    private void BtnBuscarEnTodas_Click(object sender, RoutedEventArgs e) => BuscarEnTodas();

    private void ComboAlcance_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboAlcance.SelectedItem is ComboBoxItem item)
        {
            string tag = item.Tag?.ToString() ?? "";
            PanelCarpetaEspecifica.Visibility =
                tag == "CarpetaEspecifica" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ListaResultados_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListaResultados.SelectedItem is ResultadoPresentacion presentacion)
        {
            // Se abre la versión principal (original antes que cedible, REQ-004).
            Visor.Child = VisorDocumento;
            _ = VisorDocumento.AbrirAsync(presentacion.Original.Versiones[0].Rutas[0]);
            Elegido?.Invoke(this, presentacion.Original);
        }
    }

    private async void Buscar()
    {
        // Una búsqueda a la vez: mientras corre, Enter y Alt+S no hacen nada.
        if (_cts is not null)
        {
            return;
        }

        string texto = CampoNumero.Text;
        string? numero = Buscador.Normalizar(texto);

        if (numero == null)
        {
            Estado.Text = "Escribe un número";
            return;
        }

        BtnBuscar.IsEnabled = false;
        BtnCancelar.Visibility = Visibility.Visible;
        Estado.Text = "Buscando…";
        BtnBuscarEnTodas.Visibility = Visibility.Collapsed;
        _resultados.Clear();

        _controlIndice.Pausar();
        _cts = new CancellationTokenSource();

        try
        {
            AlcanceBusqueda alcance = ObtenerAlcance();
            string? carpetaEspecifica = ObtenerCarpetaEspecifica();

            CancellationToken token = _cts.Token;
            string cadena = _conexion.ConnectionString;
            // La búsqueda corre en otro hilo con su propia conexión: una
            // SqliteConnection no se comparte entre hilos.
            var documentos = await Task.Run(
                () =>
                {
                    using var conexion = new SqliteConnection(cadena);
                    conexion.Open();
                    return new Buscador(conexion).Buscar(numero, alcance, carpetaEspecifica, token);
                },
                token
            );

            var agrupados = await Task.Run(() => Agrupador.Agrupar(documentos, numero), _cts.Token);

            foreach (var r in agrupados)
            {
                _resultados.Add(new ResultadoPresentacion(r));
            }

            if (_resultados.Count == 0)
            {
                Estado.Text = $"No encontré el documento {numero} en tus carpetas";
            }
            else
            {
                ActualizarEstado(alcance);
            }
        }
        catch (OperationCanceledException)
        {
            Estado.Text = "Búsqueda cancelada";
        }
        catch (Exception ex)
        {
            Estado.Text = "No se pudo buscar: " + ex.Message;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _controlIndice.Reanudar();
            BtnBuscar.IsEnabled = true;
            BtnCancelar.Visibility = Visibility.Collapsed;
        }
    }

    private void Cancelar()
    {
        _cts?.Cancel();
    }

    private void ActualizarEstado(AlcanceBusqueda alcance)
    {
        Estado.Text = _resultados.Count == 1 ? "1 resultado" : $"{_resultados.Count} resultados";
        if (alcance == AlcanceBusqueda.PrimeraCoincidencia)
        {
            BtnBuscarEnTodas.Visibility = Visibility.Visible;
        }
    }

    private void BuscarEnTodas()
    {
        foreach (ComboBoxItem item in ComboAlcance.Items)
        {
            if (item.Tag as string == nameof(AlcanceBusqueda.TodasLasCarpetas))
            {
                ComboAlcance.SelectedItem = item;
                break;
            }
        }
        Buscar();
    }

    private AlcanceBusqueda ObtenerAlcance()
    {
        if (ComboAlcance.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return Enum.Parse<AlcanceBusqueda>(tag);
        }
        return AlcanceBusqueda.PrimeraCoincidencia;
    }

    private string? ObtenerCarpetaEspecifica()
    {
        if (
            ComboAlcance.SelectedItem is ComboBoxItem item
            && item.Tag?.ToString() == "CarpetaEspecifica"
        )
        {
            return ComboCarpetaEspecifica.SelectedItem as string;
        }
        return null;
    }
}

// Lo que muestra cada fila de resultados (REQ-004).
internal sealed class ResultadoPresentacion(ResultadoBusqueda resultado)
{
    public ResultadoBusqueda Original { get; } = resultado;

    public string Titulo => Original.Titulo;

    public string Detalle
    {
        get
        {
            var partes = new List<string>
            {
                string.Join(" · ", Original.Versiones.Select(v => v.Etiqueta)),
            };
            if (Original.Carpetas.Count > 1)
            {
                partes.Add($"En {Original.Carpetas.Count} carpetas");
            }
            partes.Add(Original.Modificado.ToString("dd-MM-yyyy"));
            return string.Join(" · ", partes);
        }
    }
}
