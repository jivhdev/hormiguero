using System.Windows;
using System.Windows.Controls;
using Hormiguero.Buscadero.Logica;

namespace Hormiguero.Buscadero;

public partial class VentanaPrincipal : Window
{
    public VentanaPrincipal()
    {
        InitializeComponent();

        var carpetas = new CarpetasConfiguradas(App.Base!);

        if (!carpetas.HayAlguna)
        {
            var pantalla = new PantallaCarpetas(carpetas);
            pantalla.Listo += (_, _) => MostrarBuscador();
            Contenido.Content = pantalla;
        }
        else
        {
            MostrarBuscador();
        }
    }

    private void MostrarBuscador()
    {
        var texto = new TextBlock
        {
            Text = "Aquí irá el buscador",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        texto.SetResourceReference(TextBlock.FontSizeProperty, "Hormiguero.TextoTitulo");
        texto.SetResourceReference(TextBlock.ForegroundProperty, "Hormiguero.TextoSecundario");
        Contenido.Content = texto;
    }
}
