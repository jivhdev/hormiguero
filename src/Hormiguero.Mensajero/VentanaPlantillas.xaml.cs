using System.Windows;
using System.Windows.Controls;
using Hormiguero.Mensajero.Core;

namespace Hormiguero.Mensajero.App;

public partial class VentanaPlantillas : Window
{
    private readonly AlmacenMensajero almacen;
    private readonly IReadOnlyList<PlantillaMensaje> plantillas = PlantillasMensajero.Listar();
    private readonly Dictionary<string, string> textosGuardados = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> borradores = new(StringComparer.Ordinal);
    private bool actualizando;

    public VentanaPlantillas(AlmacenMensajero almacen)
    {
        InitializeComponent();
        this.almacen = almacen;
        foreach (PlantillaMensaje plantilla in plantillas)
        {
            string texto = almacen.LeerValor(plantilla.Clave);
            if (texto.Length == 0)
                texto = plantilla.TextoPredeterminado;
            textosGuardados[plantilla.Clave] = texto;
            borradores[plantilla.Clave] = texto;
        }

        ListaPlantillas.ItemsSource = plantillas;
        ListaPlantillas.DisplayMemberPath = nameof(PlantillaMensaje.Nombre);
        ListaPlantillas.SelectionChanged += ListaPlantillas_SelectionChanged;
        CampoTexto.TextChanged += CampoTexto_TextChanged;
        BotonGuardar.Click += Guardar_Click;
        BotonPredeterminado.Click += Predeterminado_Click;
        BotonCerrar.Click += (_, _) => Close();
        Closing += Ventana_Closing;
        ListaPlantillas.SelectedIndex = 0;
    }

    private void ListaPlantillas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListaPlantillas.SelectedItem is not PlantillaMensaje plantilla)
            return;
        actualizando = true;
        CampoTexto.Text = borradores[plantilla.Clave];
        ListaMarcadores.ItemsSource = plantilla.Marcadores;
        CampoVistaPrevia.Text = PlantillasMensajero.CrearVistaPrevia(
            plantilla.Clave,
            CampoTexto.Text
        );
        actualizando = false;
    }

    private void CampoTexto_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (actualizando || ListaPlantillas.SelectedItem is not PlantillaMensaje plantilla)
            return;
        borradores[plantilla.Clave] = CampoTexto.Text;
        CampoVistaPrevia.Text = PlantillasMensajero.CrearVistaPrevia(
            plantilla.Clave,
            CampoTexto.Text
        );
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaPlantillas.SelectedItem is not PlantillaMensaje plantilla)
            return;
        try
        {
            GuardarPlantilla(plantilla);
        }
        catch (Exception excepcion)
        {
            MensajeroLog.RegistrarError("Guardar plantilla de mensaje", excepcion);
            MessageBox.Show(
                this,
                $"No se pudo guardar la plantilla: {excepcion.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void Predeterminado_Click(object sender, RoutedEventArgs e)
    {
        if (ListaPlantillas.SelectedItem is not PlantillaMensaje plantilla)
            return;
        if (
            MessageBox.Show(
                this,
                "¿Desea volver al texto predeterminado de esta plantilla? El cambio se guardará de inmediato.",
                "Volver al texto predeterminado",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            ) != MessageBoxResult.Yes
        )
            return;

        try
        {
            PlantillasMensajero.VolverAlTextoPredeterminado(almacen, plantilla.Clave);
        }
        catch (Exception excepcion)
        {
            MensajeroLog.RegistrarError("Restaurar plantilla de mensaje", excepcion);
            MessageBox.Show(
                this,
                $"No se pudo restaurar la plantilla: {excepcion.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            return;
        }
        textosGuardados[plantilla.Clave] = plantilla.TextoPredeterminado;
        borradores[plantilla.Clave] = plantilla.TextoPredeterminado;
        CampoTexto.Text = plantilla.TextoPredeterminado;
    }

    private void Ventana_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!HayCambiosSinGuardar())
            return;
        MessageBoxResult respuesta = MessageBox.Show(
            this,
            "Hay cambios sin guardar. ¿Desea guardarlos antes de cerrar?",
            "Cambios sin guardar",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning
        );
        if (respuesta == MessageBoxResult.Cancel)
        {
            e.Cancel = true;
            return;
        }
        if (respuesta == MessageBoxResult.Yes)
        {
            try
            {
                foreach (PlantillaMensaje plantilla in plantillas)
                    if (borradores[plantilla.Clave] != textosGuardados[plantilla.Clave])
                        GuardarPlantilla(plantilla);
            }
            catch (Exception excepcion)
            {
                e.Cancel = true;
                MensajeroLog.RegistrarError("Guardar plantillas de mensaje", excepcion);
                MessageBox.Show(
                    this,
                    $"No se pudieron guardar los cambios: {excepcion.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }

    private void GuardarPlantilla(PlantillaMensaje plantilla)
    {
        almacen.GuardarValor(plantilla.Clave, borradores[plantilla.Clave]);
        textosGuardados[plantilla.Clave] = borradores[plantilla.Clave];
    }

    private bool HayCambiosSinGuardar() =>
        plantillas.Any(plantilla =>
            borradores[plantilla.Clave] != textosGuardados[plantilla.Clave]
        );
}
