using System.Threading;
using System.Windows;
using Buscadero.App;
using Buscadero.Core.Lineas;
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
                principal.Show();
                principal.UpdateLayout();
                principal.Close();
                using var lineas = new ServicioLineas(
                    new RepositorioLineas(Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun)
                );
                foreach (var tema in new[] { ModoTema.Claro, ModoTema.Oscuro })
                {
                    Tema.Aplicar(aplicacion, tema);
                    Window[] ventanas =
                    [
                        new DialogoModeloGuiado(lineas),
                        new DialogoAlertas(
                            Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun,
                            _ => { }
                        ),
                        new DialogoRecordatorio(1),
                        new DialogoCalculadoraFechas(),
                        new DialogoFeriados(),
                        new DialogoReglaAlerta(),
                        new DialogoCadenasSimples(),
                        new DialogoDudosos(lineas),
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
