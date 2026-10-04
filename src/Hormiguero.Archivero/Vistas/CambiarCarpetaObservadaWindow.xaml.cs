using System.IO;
using System.Windows;
using System.Windows.Forms;
using Archivero.Servicios;

namespace Archivero.Vistas;

public partial class CambiarCarpetaObservadaWindow : Window
{
    private readonly CarpetaObservadaService _servicio;

    public string? CarpetaNueva { get; private set; }

    public CambiarCarpetaObservadaWindow(CarpetaObservadaService servicio, string carpetaActual)
    {
        InitializeComponent();
        _servicio = servicio;

        TxtNombre.Text = _servicio.NombrePorDefecto;
        TxtUbicacion.Text = Path.GetDirectoryName(carpetaActual) ?? _servicio.UbicacionPorDefecto;
        TxtNombre.TextChanged += (_, _) => ActualizarVistaPrevia();

        ActualizarVistaPrevia();
    }

    private void ActualizarVistaPrevia()
    {
        TxtError.Visibility = Visibility.Collapsed;
        TxtVistaPrevia.Text = $"Se va a crear: {ObtenerRutaPropuesta()}";
    }

    private string ObtenerRutaPropuesta() => Path.Combine(TxtUbicacion.Text, TxtNombre.Text);

    private void BtnCambiarUbicacion_Click(object sender, RoutedEventArgs e)
    {
        using var dialogo = new FolderBrowserDialog
        {
            SelectedPath = TxtUbicacion.Text,
            Description = "Elegir dónde va a vivir la nueva carpeta observada"
        };

        if (dialogo.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtUbicacion.Text = dialogo.SelectedPath;
            ActualizarVistaPrevia();
        }
    }

    private void BtnCambiar_Click(object sender, RoutedEventArgs e)
    {
        var nombre = TxtNombre.Text.Trim();
        var ubicacion = TxtUbicacion.Text.Trim();

        if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(ubicacion))
        {
            MostrarError("Completar el nombre y la ubicación de la carpeta.");
            return;
        }

        try
        {
            CarpetaNueva = _servicio.CrearYMarcarComoObservada(ubicacion, nombre);
            DialogResult = true;
            Close();
        }
        catch (CarpetaYaExisteException ex)
        {
            MostrarError(ex.Message);
        }
    }

    private void MostrarError(string mensaje)
    {
        TxtError.Text = mensaje;
        TxtError.Visibility = Visibility.Visible;
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
