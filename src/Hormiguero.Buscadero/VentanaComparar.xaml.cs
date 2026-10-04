using System.Windows;
using Buscadero.Core.Marcas;

namespace Buscadero.App;

public partial class VentanaComparar : Window
{
    public VentanaComparar(
        RepositorioMarcas repositorioMarcas,
        string rutaIzquierda,
        string rutaDerecha
    )
    {
        InitializeComponent();

        TextoComparacion.Text =
            $"{System.IO.Path.GetFileName(rutaIzquierda)}   vs   {System.IO.Path.GetFileName(rutaDerecha)}";

        // Sólo el primer documento (el observado) es editable; el segundo es de sólo lectura.
        VisorIzquierda.Abrir(rutaIzquierda, repositorioMarcas, editable: true);
        VisorDerecha.Abrir(rutaDerecha, repositorioMarcas, editable: false);
    }

    private void BotonDejar_Click(object sender, RoutedEventArgs e) => Close();
}
