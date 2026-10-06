using System.Windows;
using Hormiguero.Diseno;

namespace Hormiguero.Mensajero.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Tema.Aplicar(
            this,
            Enum.TryParse<ModoTema>(
                Hormiguero.Nucleo.Datos.DatosDeApp.LeerPreferencia("tema.mensajero"),
                out var modoTema
            )
                ? modoTema
                : ModoTema.Sistema
        );
        MainWindow = new VentanaPrincipal();
        MainWindow.Show();
    }
}
