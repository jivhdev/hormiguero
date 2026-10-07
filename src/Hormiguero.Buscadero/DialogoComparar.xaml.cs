using System.Windows;
using Buscadero.Core.Busqueda;

namespace Buscadero.App;

public partial class DialogoComparar : Window
{
    private readonly ServicioBusqueda _busqueda;

    public DialogoComparar(ServicioBusqueda busqueda)
    {
        InitializeComponent();
        _busqueda = busqueda;
        ComboDocumentos.IsEnabled = false;
        TextoAyuda.Text = "Busque el segundo documento para compararlo.";
    }

    public string? RutaSeleccionada { get; private set; }

    private void BotonBuscar_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new DialogoBuscarDocumento(_busqueda) { Owner = this };
        if (dialogo.ShowDialog() == true && dialogo.RutaSeleccionada is not null)
        {
            RutaSeleccionada = dialogo.RutaSeleccionada;
            DialogResult = true;
        }
    }

    private void Aceptar_Click(object sender, RoutedEventArgs e) => BotonBuscar_Click(sender, e);
}
