using System.Windows;
using Hormiguero.Diseno;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Buscadero;

public partial class App : Application
{
    public static SqliteConnection? Base { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Tema.Aplicar(this, ModoTema.Sistema);

        try
        {
            Base = BaseComun.Abrir(BaseComun.RutaPorDefecto);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se pudo abrir la base de datos:\n{ex.Message}",
                "Error al iniciar Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            Shutdown();
            return;
        }

        var ventana = new VentanaPrincipal();
        ventana.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Base?.Dispose();
        base.OnExit(e);
    }
}
