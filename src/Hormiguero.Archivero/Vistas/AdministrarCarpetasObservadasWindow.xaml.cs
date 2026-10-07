using System.Windows;
using System.Windows.Controls;
using Archivero.Datos;

namespace Archivero.Vistas;

public partial class AdministrarCarpetasObservadasWindow : Window
{
    private readonly CarpetasObservadasRepository _repositorio = new();
    private List<CarpetaObservadaExterna> _carpetas;

    public AdministrarCarpetasObservadasWindow()
    {
        InitializeComponent();
        _carpetas = _repositorio.Leer().ToList();
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
    }

    private void Examinar_Click(object sender, RoutedEventArgs e)
    {
        using var dialogo = new System.Windows.Forms.FolderBrowserDialog
        {
            SelectedPath = TxtRuta.Text,
            ShowNewFolderButton = false,
        };
        if (dialogo.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            TxtRuta.Text = dialogo.SelectedPath;
    }

    private void Agregar_Click(object sender, RoutedEventArgs e)
    {
        if (!Validar())
            return;
        var carpeta = Crear(Guid.NewGuid());
        _carpetas.Add(carpeta);
        _repositorio.Guardar(_carpetas);
        ActualizarLista(carpeta.Id);
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaCarpetas.SelectedItem is not CarpetaObservadaExterna actual || !Validar())
            return;
        var editada = Crear(actual.Id);
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

    private CarpetaObservadaExterna Crear(Guid id) =>
        new(
            id,
            TxtNombre.Text.Trim(),
            System.IO.Path.GetFullPath(TxtRuta.Text.Trim()),
            ChkSubcarpetas.IsChecked == true,
            ChkActiva.IsChecked == true
        );

    private void Listo_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
