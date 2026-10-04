using System.Windows;

namespace Buscadero.App;

public sealed class OpcionLista
{
    public required long Id { get; init; }
    public required string Etiqueta { get; init; }
}

public partial class DialogoLista : Window
{
    public DialogoLista()
    {
        InitializeComponent();
    }

    public static long? Elegir(
        Window propietario,
        string titulo,
        string etiqueta,
        IReadOnlyList<OpcionLista> opciones
    )
    {
        if (opciones.Count == 0)
        {
            return null;
        }

        var dialogo = new DialogoLista { Owner = propietario, Title = titulo };
        dialogo.Etiqueta.Text = etiqueta;
        dialogo.Lista.ItemsSource = opciones;
        dialogo.Lista.SelectedIndex = 0;

        if (
            dialogo.ShowDialog() != true
            || dialogo.Lista.SelectedItem is not OpcionLista seleccionada
        )
        {
            return null;
        }

        return seleccionada.Id;
    }

    private void Aceptar_Click(object sender, RoutedEventArgs e)
    {
        if (Lista.SelectedItem is null)
        {
            return;
        }

        DialogResult = true;
    }
}
