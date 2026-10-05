using System.Windows;
using Hormiguero.Nucleo.Datos;

namespace Buscadero.App;

public partial class DialogoRecordatorio : Window
{
    private readonly long _cadenaId;

    public DialogoRecordatorio(long cadenaId)
    {
        InitializeComponent();
        _cadenaId = cadenaId;
        Fecha.SelectedDate = DateTime.Today;
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Texto.Text) || Fecha.SelectedDate is not DateTime fecha)
        {
            MessageBox.Show(
                this,
                "Ingrese un texto y una fecha.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }
        try
        {
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            new RepositorioAlertas(conexion).CrearAlertaManual(
                Texto.Text,
                DateOnly.FromDateTime(fecha),
                _cadenaId
            );
            DialogResult = true;
        }
        catch (Exception error)
        {
            MessageBox.Show(
                this,
                error.Message,
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();
}
