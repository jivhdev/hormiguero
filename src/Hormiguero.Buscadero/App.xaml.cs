using System.Windows;
using System.Windows.Threading;
using Buscadero.Core.Lineas;
using Hormiguero.Diseno;

namespace Buscadero.App;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += Aplicacion_DispatcherUnhandledException;
    }

    // Fase B-1 (D-66): diseño común de Hormiguero, claro u oscuro según Windows.
    protected override void OnStartup(StartupEventArgs e)
    {
        Tema.Aplicar(
            this,
            Enum.TryParse<ModoTema>(
                Hormiguero.Nucleo.Datos.DatosDeApp.LeerPreferencia("tema.buscadero"),
                out var modoTema
            )
                ? modoTema
                : ModoTema.Sistema
        );
        base.OnStartup(e);
    }

    private static void Aplicacion_DispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e
    )
    {
        try
        {
            using var lineas = new RepositorioLineas(
                Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun
            );
            lineas.RegistrarErrorOperacion("Error no controlado de la interfaz", e.Exception);
        }
        catch
        {
            // La notificación debe mostrarse aunque la base de datos no esté disponible.
        }

        MessageBox.Show(
            $"Buscadero encontró un error y registró el incidente. Puede continuar si la ventana responde.\n\n{e.Exception.Message}",
            "Buscadero",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );
        e.Handled = true;
    }
}
