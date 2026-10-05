using System.Windows;
using System.Windows.Controls;
using Buscadero.Core.Alertas;
using Buscadero.Core.Lineas;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Utilidades;

namespace Buscadero.App;

public partial class DialogoReglaAlerta : Window
{
    private readonly long _modeloId;

    public DialogoReglaAlerta(long modeloId, IReadOnlyList<PlantillaVagon> vagones)
    {
        InitializeComponent();
        _modeloId = modeloId;
        Origen.ItemsSource = vagones;
        Destino.ItemsSource = vagones;
        Origen.SelectedIndex = 0;
        Destino.SelectedIndex = vagones.Count > 1 ? 1 : 0;
        Aviso.Text = "Documento esperando documento";
        try
        {
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            var calendarios = new RepositorioCalendariosFeriados(conexion).ListarCalendarios();
            Calendario.ItemsSource = calendarios;
            Calendario.SelectedItem = calendarios.FirstOrDefault(c => c.Predeterminado);
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
        ActualizarFrase();
    }

    private void ActualizarFrase_Click(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
            ActualizarFrase();
    }

    private void ActualizarFrase()
    {
        if (
            Origen.SelectedItem is not PlantillaVagon origen
            || Destino.SelectedItem is not PlantillaVagon destino
            || !int.TryParse(Dias.Text, out int dias)
            || Modo.SelectedIndex < 0
        )
        {
            Frase.Text = "Complete los datos del aviso.";
            return;
        }
        var tipo = Modo.SelectedIndex == 0 ? TipoDias.Habiles : TipoDias.Corridos;
        Frase.Text = PresentacionAlertas.CrearFraseRegla(
            origen.Nombre,
            dias,
            tipo,
            destino.Nombre,
            Aviso.Text.Trim()
        );
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (
            Origen.SelectedItem is not PlantillaVagon origen
            || Destino.SelectedItem is not PlantillaVagon destino
            || origen.Id == destino.Id
            || !int.TryParse(Dias.Text, out int dias)
            || dias is < 0 or > CalculoFechas.CantidadMaxima
            || string.IsNullOrWhiteSpace(Aviso.Text)
        )
        {
            MessageBox.Show(
                this,
                "Elija dos documentos distintos, una espera válida y escriba el aviso.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }
        try
        {
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            new RepositorioAlertas(conexion).CrearRegla(
                Aviso.Text.Trim(),
                _modeloId,
                origen.Id,
                destino.Id,
                "vagon_completado",
                dias,
                Modo.SelectedIndex == 0 ? TipoDias.Habiles : TipoDias.Corridos,
                Aviso.Text.Trim(),
                (Calendario.SelectedItem as CalendarioFeriados)?.Id
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
