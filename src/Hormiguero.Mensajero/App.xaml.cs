using System.Windows;
using Hormiguero.Diseno;
using Hormiguero.Mensajero.Core;

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
        try
        {
            using var almacen = AlmacenMensajero.AbrirComun();
            almacen.ImportarClientesPrecargados(
                System.IO.Path.Combine(
                    AppContext.BaseDirectory,
                    "Configuracion",
                    "clientes-jcv.json"
                )
            );
        }
        catch
        {
            // Un archivo privado ausente o inválido no impide abrir Mensajero.
        }
        MainWindow = new VentanaPrincipal();
        MainWindow.Show();
    }
}
