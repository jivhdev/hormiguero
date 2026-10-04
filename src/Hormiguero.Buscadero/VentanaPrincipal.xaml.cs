using System.Windows;
using System.Windows.Controls;
using Hormiguero.Buscadero.Logica;

namespace Hormiguero.Buscadero;

public partial class VentanaPrincipal : Window
{
    private readonly ControlIndice _controlIndice = new();

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
        var pantalla = new PantallaBuscar(App.Base!, _controlIndice);
        Contenido.Content = pantalla;
    }
}
