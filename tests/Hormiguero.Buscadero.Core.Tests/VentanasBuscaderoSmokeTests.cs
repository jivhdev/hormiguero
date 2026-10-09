using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Buscadero.App;
using Hormiguero.Diseno;

namespace Hormiguero.Buscadero.Core.Tests;

public sealed class VentanasBuscaderoSmokeTests
{
    [Fact]
    public void VentanasDeAlertas_AbrenConDatosTemporalesEnHiloSta()
    {
        string carpeta = Path.Combine(Path.GetTempPath(), $"Buscadero-alertas-{Guid.NewGuid():N}");
        Directory.CreateDirectory(carpeta);
        string? anterior = Environment.GetEnvironmentVariable("HORMIGUERO_DATOS");
        Environment.SetEnvironmentVariable("HORMIGUERO_DATOS", carpeta);
        Exception? error = null;
        var hilo = new Thread(() =>
        {
            try
            {
                var aplicacion = new App();
                aplicacion.InitializeComponent();
                aplicacion.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Tema.Aplicar(aplicacion, ModoTema.Claro);
                var principal = new MainWindow();
                principal.Width = 1366;
                principal.Height = 768;
                principal.Show();
                principal.UpdateLayout();
                Assert.False(Assert.IsType<Button>(principal.FindName("BotonImprimir")).IsEnabled);
                Assert.IsType<TextBox>(principal.FindName("MaestroNumero"));
                Assert.IsType<System.Windows.Controls.DataGrid>(
                    principal.FindName("MaestroResultados")
                );
                Assert.IsType<Button>(principal.FindName("MaestroVerCadena"));
                var botonSegundaBusqueda = Assert.IsType<Button>(
                    principal.FindName("BotonSegundaBusqueda")
                );
                botonSegundaBusqueda.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                principal.UpdateLayout();
                var panelSecundario = Assert.IsType<PanelBusquedaSecundario>(
                    principal.FindName("PanelBusquedaSecundario")
                );
                Assert.Equal(Visibility.Visible, panelSecundario.Visibility);
                Assert.False(
                    Assert.IsType<Button>(panelSecundario.FindName("BotonImprimir")).IsEnabled
                );
                principal.Close();
                foreach (var tema in new[] { ModoTema.Claro, ModoTema.Oscuro })
                {
                    Tema.Aplicar(aplicacion, tema);
                    Window[] ventanas =
                    [
                        new DialogoAlertas(
                            Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun,
                            _ => { }
                        ),
                        new DialogoCalculadoraFechas(),
                        new DialogoFeriados(),
                        new DialogoReglaAlerta(),
                        new DialogoCadenasSimples(),
                        new DialogoVistaCadenas(),
                        new DialogoEsquemaCadena(),
                        new DialogoListaEsquemas(),
                        new DialogoDudososCadena(1, "Prueba"),
                        new DialogoDudosos(),
                    ];
                    foreach (var ventana in ventanas)
                    {
                        ventana.Show();
                        ventana.UpdateLayout();
                        ventana.Close();
                    }
                }
                aplicacion.Shutdown();
            }
            catch (Exception excepcion)
            {
                error = excepcion;
            }
        });
        hilo.SetApartmentState(ApartmentState.STA);
        using var bloqueoVentanas = new Mutex(false, @"Local\Hormiguero.PruebasVentanas");
        var bloqueoTomado = false;
        try
        {
            bloqueoTomado = bloqueoVentanas.WaitOne(TimeSpan.FromMinutes(5));
            Assert.True(bloqueoTomado, "Otra prueba de ventanas no terminó a tiempo.");
            hilo.Start();
            Assert.True(hilo.Join(TimeSpan.FromMinutes(5)), "Las ventanas no terminaron de abrir.");
            Assert.Null(error);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HORMIGUERO_DATOS", anterior);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            for (int intento = 0; intento < 20; intento++)
            {
                try
                {
                    Directory.Delete(carpeta, recursive: true);
                    break;
                }
                catch (IOException) when (intento < 19)
                {
                    Thread.Sleep(100);
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                }
                catch (UnauthorizedAccessException) when (intento < 19)
                {
                    Thread.Sleep(100);
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                }
            }

            if (bloqueoTomado)
            {
                bloqueoVentanas.ReleaseMutex();
            }
        }
    }
}
