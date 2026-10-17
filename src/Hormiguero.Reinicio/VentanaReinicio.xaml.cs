using System.Windows;

namespace Hormiguero.Reinicio;

public partial class VentanaReinicio : Window
{
    public bool BorrarClasificaciones => BorrarClasificacionesCheck.IsChecked == true;

    public VentanaReinicio() => InitializeComponent();

    private void Reiniciar_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
