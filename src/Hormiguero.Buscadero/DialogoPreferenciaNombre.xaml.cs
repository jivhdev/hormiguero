using System.Windows;
using Buscadero.Core.Lineas;

namespace Buscadero.App;

public sealed class OpcionDocumentoNombre
{
    public required long Id { get; init; }
    public required string Nombre { get; init; }
}

public partial class DialogoPreferenciaNombre : Window
{
    public DialogoPreferenciaNombre(IReadOnlyList<OpcionDocumentoNombre> documentos)
    {
        InitializeComponent();
        ComboDocumento.ItemsSource = documentos;
        if (documentos.Count > 0)
        {
            ComboDocumento.SelectedIndex = 0;
        }
    }

    public PreferenciaNombreCadena Preferencia { get; private set; } =
        PreferenciaNombreCadena.Generico;

    public long? VagonNombreId { get; private set; }

    private void Opcion_Changed(object sender, RoutedEventArgs e)
    {
        if (ComboDocumento is null)
        {
            return;
        }

        ComboDocumento.IsEnabled = OpcionPorDocumento.IsChecked == true;
    }

    private void Continuar_Click(object sender, RoutedEventArgs e)
    {
        if (OpcionPorDocumento.IsChecked == true)
        {
            if (ComboDocumento.SelectedItem is not OpcionDocumentoNombre elegido)
            {
                MessageBox.Show(
                    this,
                    "Elija qué documento va a servir como nombre.",
                    "Buscadero",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            Preferencia = PreferenciaNombreCadena.PorDocumento;
            VagonNombreId = elegido.Id;
        }
        else if (OpcionPersonalizado.IsChecked == true)
        {
            Preferencia = PreferenciaNombreCadena.Personalizado;
            VagonNombreId = null;
        }
        else
        {
            Preferencia = PreferenciaNombreCadena.Generico;
            VagonNombreId = null;
        }

        DialogResult = true;
    }

    public static (PreferenciaNombreCadena Preferencia, long? VagonNombreId) Pedir(
        Window propietario,
        IReadOnlyList<OpcionDocumentoNombre> documentos
    )
    {
        var dialogo = new DialogoPreferenciaNombre(documentos) { Owner = propietario };
        return dialogo.ShowDialog() == true
            ? (dialogo.Preferencia, dialogo.VagonNombreId)
            : (PreferenciaNombreCadena.Generico, null);
    }
}
