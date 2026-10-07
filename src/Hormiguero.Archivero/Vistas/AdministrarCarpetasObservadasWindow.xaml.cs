using System.Windows;
using System.Windows.Controls;
using Archivero.Datos;
using Hormiguero.Nucleo.Datos;

namespace Archivero.Vistas;

public partial class AdministrarCarpetasObservadasWindow : Window
{
    private readonly CarpetasObservadasRepository _repositorio = new();
    private List<CarpetaObservadaExterna> _carpetas;

    public AdministrarCarpetasObservadasWindow()
    {
        InitializeComponent();
        _carpetas = _repositorio.Leer().ToList();
        foreach (var dato in DiccionarioDatosEnlazantes.Todos)
            ComboDato.Items.Add(
                new ComboBoxItem { Content = $"{dato.Grupo} · {dato.Nombre}", Tag = dato }
            );
        ComboReconocimiento.SelectedIndex = 0;
        ComboAccion.SelectedIndex = 0;
        ActualizarLista();
    }

    private void ActualizarLista(Guid? seleccionar = null)
    {
        ListaCarpetas.ItemsSource = null;
        ListaCarpetas.ItemsSource = _carpetas;
        if (seleccionar is not null)
            ListaCarpetas.SelectedItem = _carpetas.FirstOrDefault(c => c.Id == seleccionar);
    }

    private void ListaCarpetas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListaCarpetas.SelectedItem is not CarpetaObservadaExterna carpeta)
            return;
        TxtNombre.Text = carpeta.Nombre;
        TxtRuta.Text = carpeta.Ruta;
        ChkSubcarpetas.IsChecked = carpeta.IncluirSubcarpetas;
        ChkActiva.IsChecked = carpeta.Activa;
        ChkSeguirPeriodo.IsChecked = carpeta.SeguirPeriodo;
        ComboFormatoPeriodo.SelectedIndex = carpeta.FormatoPeriodo switch
        {
            "AAAA_MM" => 1,
            "AAAAMM" => 2,
            "AAAA" => 3,
            _ => 0,
        };
        ComboReconocimiento.SelectedIndex = carpeta.ModoReconocimiento == "TipoPorCarpeta" ? 1 : 0;
        TxtEmisor.Text = carpeta.Emisor;
        ComboDato.SelectedIndex = Enumerable
            .Range(0, ComboDato.Items.Count)
            .FirstOrDefault(i =>
                ((ComboBoxItem)ComboDato.Items[i]).Tag is DatoEnlazante dato
                && dato.Id == carpeta.DatoIdentificador
            );
        ComboAccion.SelectedIndex = carpeta.AccionAlLlegar switch
        {
            "SoloRegistrar" => 1,
            "ImprimirPrimeraPagina" => 2,
            "ImprimirTodo" => 3,
            "Avisar" => 4,
            "AvisarImprimirPrimeraPagina" => 5,
            _ => 0,
        };
    }

    private void Examinar_Click(object sender, RoutedEventArgs e)
    {
        using var dialogo = new System.Windows.Forms.FolderBrowserDialog
        {
            SelectedPath = TxtRuta.Text,
            ShowNewFolderButton = false,
        };
        if (dialogo.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtRuta.Text = dialogo.SelectedPath;
            var periodo = PeriodosCarpetaObservada.Detectar(dialogo.SelectedPath);
            if (
                periodo is not null
                && System.Windows.MessageBox.Show(
                    this,
                    $"Parece una carpeta por mes. ¿Observar «{periodo.Value.RutaBase}» y seguir el mes automáticamente?",
                    "Carpetas observadas",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                ) == MessageBoxResult.Yes
            )
            {
                TxtRuta.Text = periodo.Value.RutaBase;
                ChkSeguirPeriodo.IsChecked = true;
                ComboFormatoPeriodo.SelectedIndex = periodo.Value.Formato switch
                {
                    "AAAA_MM" => 1,
                    "AAAAMM" => 2,
                    "AAAA" => 3,
                    _ => 0,
                };
            }
        }
    }

    private void ComboReconocimiento_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelTipoFijo is not null)
            PanelTipoFijo.Visibility =
                ComboReconocimiento.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Agregar_Click(object sender, RoutedEventArgs e)
    {
        if (!Validar())
            return;
        var carpeta = Crear(Guid.NewGuid()) with { Agregada = DateTime.Now };
        _carpetas.Add(carpeta);
        _repositorio.Guardar(_carpetas);
        ActualizarLista(carpeta.Id);
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaCarpetas.SelectedItem is not CarpetaObservadaExterna actual || !Validar())
            return;
        var editada = Crear(actual.Id) with { Agregada = actual.Agregada };
        _carpetas[_carpetas.IndexOf(actual)] = editada;
        _repositorio.Guardar(_carpetas);
        ActualizarLista(editada.Id);
    }

    private void Quitar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaCarpetas.SelectedItem is not CarpetaObservadaExterna actual)
            return;
        _carpetas.Remove(actual);
        _repositorio.Guardar(_carpetas);
        ActualizarLista();
        TxtNombre.Clear();
        TxtRuta.Clear();
    }

    private bool Validar()
    {
        if (string.IsNullOrWhiteSpace(TxtNombre.Text) || string.IsNullOrWhiteSpace(TxtRuta.Text))
        {
            System.Windows.MessageBox.Show(
                this,
                "Escribe un nombre y una ruta.",
                "Carpetas observadas",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return false;
        }
        if (
            ComboReconocimiento.SelectedIndex == 1
            && (
                (ComboDato.SelectedItem as ComboBoxItem)?.Tag is not DatoEnlazante
                || string.IsNullOrWhiteSpace(TxtEmisor.Text)
            )
        )
        {
            System.Windows.MessageBox.Show(
                this,
                "Elige un dato identificador y escribe el emisor.",
                "Carpetas observadas",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return false;
        }
        Guid? idSeleccionado = (ListaCarpetas.SelectedItem as CarpetaObservadaExterna)?.Id;
        if (
            _carpetas.Any(c =>
                c.Id != idSeleccionado
                && string.Equals(
                    c.Nombre,
                    TxtNombre.Text.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
            )
        )
        {
            System.Windows.MessageBox.Show(
                this,
                "Ya existe una carpeta con ese nombre.",
                "Carpetas observadas",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return false;
        }
        try
        {
            _ = System.IO.Path.GetFullPath(TxtRuta.Text.Trim());
        }
        catch (Exception ex)
            when (ex is ArgumentException or NotSupportedException or System.IO.PathTooLongException
            )
        {
            System.Windows.MessageBox.Show(
                this,
                $"La ruta no es válida: {ex.Message}",
                "Carpetas observadas",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return false;
        }
        return true;
    }

    private CarpetaObservadaExterna Crear(Guid id)
    {
        var dato = (ComboDato.SelectedItem as ComboBoxItem)?.Tag as DatoEnlazante;
        string accion = ComboAccion.SelectedIndex switch
        {
            1 => "SoloRegistrar",
            2 => "ImprimirPrimeraPagina",
            3 => "ImprimirTodo",
            4 => "Avisar",
            5 => "AvisarImprimirPrimeraPagina",
            _ => "Configuracion",
        };
        return new(
            id,
            TxtNombre.Text.Trim(),
            System.IO.Path.GetFullPath(TxtRuta.Text.Trim()),
            ChkSubcarpetas.IsChecked == true,
            ChkActiva.IsChecked == true,
            (ListaCarpetas.SelectedItem as CarpetaObservadaExterna)?.Agregada,
            ComboReconocimiento.SelectedIndex == 1 ? "TipoPorCarpeta" : "Configuraciones",
            dato?.Id,
            TxtEmisor.Text.Trim(),
            ChkSeguirPeriodo.IsChecked == true,
            ComboFormatoPeriodo.SelectedIndex switch
            {
                1 => "AAAA_MM",
                2 => "AAAAMM",
                3 => "AAAA",
                _ => "AAAA_AAAAMM",
            },
            accion
        );
    }

    private void Listo_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
