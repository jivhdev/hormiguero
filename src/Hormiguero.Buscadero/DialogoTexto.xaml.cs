using System.Windows;

namespace Buscadero.App;

public partial class DialogoTexto : Window
{
    public DialogoTexto()
    {
        InitializeComponent();
        Loaded += (_, _) => TextoEntrada.Focus();
    }

    public string Texto => TextoEntrada.Text;

    public static string? Pedir(
        Window propietario,
        string titulo,
        string etiqueta,
        string valorInicial = ""
    )
    {
        var dialogo = new DialogoTexto { Owner = propietario, Title = titulo };
        dialogo.Etiqueta.Text = etiqueta;
        dialogo.TextoEntrada.Text = valorInicial;
        return dialogo.ShowDialog() == true ? dialogo.Texto : null;
    }

    private void Aceptar_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
