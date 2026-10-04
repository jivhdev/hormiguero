using System.Windows;
using Archivero.Datos;

namespace Archivero.Vistas;

public class OpcionPatron
{
    public PatronReconocimiento Patron { get; }
    public string Descripcion { get; }

    public OpcionPatron(PatronReconocimiento patron, int numero)
    {
        Patron = patron;
        var emisor = patron.Marcas.FirstOrDefault(m => m.Campo == CampoMarca.Emisor)?.TextoReferencia;
        Descripcion = string.IsNullOrWhiteSpace(emisor)
            ? $"Patrón {numero}"
            : $"Patrón {numero} — Emisor marcado como \"{emisor}\"";
    }
}

public partial class ElegirPatronWindow : Window
{
    public PatronReconocimiento? PatronElegido { get; private set; }

    public ElegirPatronWindow(IEnumerable<PatronReconocimiento> patrones)
    {
        InitializeComponent();
        var opciones = patrones.Select((p, indice) => new OpcionPatron(p, indice + 1)).ToList();
        ListaPatrones.ItemsSource = opciones;
        if (opciones.Count > 0)
        {
            ListaPatrones.SelectedIndex = 0;
        }
    }

    private void BtnElegir_Click(object sender, RoutedEventArgs e)
    {
        if (ListaPatrones.SelectedItem is not OpcionPatron opcion)
        {
            System.Windows.MessageBox.Show(this, "Elegí un patrón de la lista.", "Archivero",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        PatronElegido = opcion.Patron;
        DialogResult = true;
        Close();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
