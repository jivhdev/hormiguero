using System.Windows;
using Buscadero.Core.Lineas;

namespace Buscadero.App;

public partial class DialogoElegirDestino : Window
{
    public DialogoElegirDestino()
    {
        InitializeComponent();
    }

    public InstanciaVagon? Destino { get; private set; }

    public static InstanciaVagon? Elegir(
        Window propietario,
        IReadOnlyList<InstanciaVagon> documentos
    )
    {
        if (documentos.Count == 0)
        {
            MessageBox.Show(
                propietario,
                "La cadena no tiene documentos.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return null;
        }

        var dialogo = new DialogoElegirDestino { Owner = propietario };
        dialogo.ComboDestino.ItemsSource = documentos
            .Select(d => new DestinoVm
            {
                Documento = d,
                Etiqueta = $"{d.Nombre} — {(d.NombreDocumento ?? "(vacío)")}",
            })
            .ToList();
        dialogo.ComboDestino.SelectedIndex = 0;
        return dialogo.ShowDialog() == true ? dialogo.Destino : null;
    }

    private void Aceptar_Click(object sender, RoutedEventArgs e)
    {
        if (ComboDestino.SelectedItem is not DestinoVm seleccionado)
        {
            return;
        }

        Destino = seleccionado.Documento;
        DialogResult = true;
    }
}
