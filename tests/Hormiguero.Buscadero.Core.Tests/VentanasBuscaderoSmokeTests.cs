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
                foreach (var tema in new[] { ModoTema.Claro, ModoTema.Oscuro })
                {
                    Tema.Aplicar(aplicacion, tema);
                    Window[] ventanas =
                    [
                        new DialogoAlertas(
                            Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun,
                            _ => { }
                        ),
                        new DialogoRecordatorio(1),
                        new DialogoCalculadoraFechas(),
                        new DialogoFeriados(),
                        new DialogoReglaAlerta(
                            1,
                            [
                                new PlantillaVagon
                                {
                                    Id = 1,
                                    PlantillaId = 1,
                                    Orden = 0,
                                    Nombre = "Guía",
                                    EsMultiple = false,
                                    EsAnexo = false,
                                },
                                new PlantillaVagon
                                {
                                    Id = 2,
                                    PlantillaId = 1,
                                    Orden = 1,
                                    Nombre = "Factura",
                                    EsMultiple = false,
                                    EsAnexo = false,
                                },
                            ]
                        ),
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
        try
        {
            hilo.Start();
            Assert.True(
                hilo.Join(TimeSpan.FromSeconds(45)),
                "Las ventanas no terminaron de abrir."
            );
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
            }
        }
    }
}
