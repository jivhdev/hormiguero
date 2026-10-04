using System.Windows;
using Hormiguero.Diseno;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Buscadero;

public partial class App : Application
{
    public static SqliteConnection? Base { get; private set; }

    // HORMIGUERO_BASE permite abrir una base de prueba (datos sintéticos) sin
    // tocar la del usuario; sin ella se usa la base común de siempre.
    public static string RutaBase { get; } =
        Environment.GetEnvironmentVariable("HORMIGUERO_BASE") is { Length: > 0 } ruta
            ? ruta
            : BaseComun.RutaPorDefecto;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Tema.Aplicar(this, ModoTema.Sistema);

        // Un error inesperado de la pantalla se avisa y la app sigue abierta.
        DispatcherUnhandledException += (_, error) =>
        {
            MessageBox.Show(
                $"Ocurrió un error inesperado:\n{error.Exception.Message}",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            error.Handled = true;
        };

        try
        {
            Base = BaseComun.Abrir(RutaBase);
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
