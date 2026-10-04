using System.Windows;
using Hormiguero.Diseno;

namespace Hormiguero.Mensajero.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Tema.Aplicar(this, ModoTema.Sistema);
        MainWindow = new VentanaPrincipal();
        MainWindow.Show();
    }
}
