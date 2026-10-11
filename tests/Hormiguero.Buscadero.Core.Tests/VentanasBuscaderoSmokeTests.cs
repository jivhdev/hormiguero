using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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
                        new DialogoAvisos(
                            Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun,
                            _ => { }
                        ),
                        new DialogoCalculadoraFechas(),
                        new DialogoFeriados(),
                        new DialogoReglaAlerta(),
                        new DialogoCadenasSimples(),
                        new DialogoVistaCadenas(),
                        new DialogoEsquemaDiccionario(),
                        new DialogoListaEsquemas(),
                        new DialogoDudosos(),
                    ];
                    foreach (var ventana in ventanas)
                    {
                        ventana.Show();
                        ventana.UpdateLayout();
                        if (ventana is DialogoEsquemaDiccionario asistente)
                        {
                            asistente.Width = 900;
                            asistente.Height = Math.Min(680, SystemParameters.WorkArea.Height - 40);
                            asistente.UpdateLayout();
                            foreach (
                                string nombreBoton in new[] { "Anterior", "Siguiente", "Cancelar" }
                            )
                            {
                                var botonPie = Assert.IsAssignableFrom<FrameworkElement>(
                                    asistente.FindName(nombreBoton)
                                );
                                Point esquina = botonPie.PointToScreen(
                                    new Point(botonPie.ActualWidth, botonPie.ActualHeight)
                                );
                                Assert.InRange(
                                    esquina.X,
                                    SystemParameters.WorkArea.Left,
                                    SystemParameters.WorkArea.Right
                                );
                                Assert.InRange(
                                    esquina.Y,
                                    SystemParameters.WorkArea.Top,
                                    SystemParameters.WorkArea.Bottom
                                );
                            }
                            Assert.Equal(
                                "Paso 1 de 7",
                                Assert.IsType<TextBlock>(asistente.FindName("NumeroPaso")).Text
                            );
                            var proveedor = Assert.IsType<ComboBox>(
                                asistente.FindName("Proveedor")
                            );
                            proveedor.Text = "PROVEEDOR DE PRUEBA";
                            Assert.Equal(
                                "Proceso con PROVEEDOR DE PRUEBA",
                                Assert.IsType<TextBox>(asistente.FindName("Nombre")).Text
                            );
                            Assert.IsType<TextBox>(asistente.FindName("Nombre")).Text =
                                "Proceso de prueba";
                            Pulsar(asistente, "Siguiente");
                            var tipos = Assert.IsType<ListBox>(asistente.FindName("Tipos"));
                            Assert.Equal(
                                Visibility.Visible,
                                Assert.IsType<Grid>(asistente.FindName("PanelTipos")).Visibility
                            );
                            Assert.Equal(
                                "Documentos del proceso",
                                Assert.IsType<TextBlock>(asistente.FindName("TituloPaso")).Text
                            );
                            Assert.True(tipos.Items.Count > 0);
                            foreach (var item in tipos.Items)
                            {
                                string nombre = (string)
                                    item.GetType().GetProperty("NombreDocumento")!.GetValue(item)!;
                                string id = (string)
                                    item.GetType().GetProperty("Id")!.GetValue(item)!;
                                Assert.False(nombre.StartsWith("N°", StringComparison.Ordinal));
                                Assert.DoesNotContain(
                                    "RUT",
                                    nombre,
                                    StringComparison.OrdinalIgnoreCase
                                );
                                Assert.DoesNotContain(
                                    "razón social",
                                    nombre,
                                    StringComparison.OrdinalIgnoreCase
                                );
                                Assert.DoesNotContain(
                                    id,
                                    new[]
                                    {
                                        "rut_proveedor",
                                        "nombre_proveedor",
                                        "rut_cliente",
                                        "nombre_cliente",
                                        "rut_propio",
                                        "nombre_propio",
                                    }
                                );
                            }
                            tipos.SelectedIndex = 0;
                            Pulsar(asistente, "AgregarTipo");
                            var seleccionados = Assert.IsType<ListBox>(
                                asistente.FindName("LugaresProceso")
                            );
                            Assert.Single(seleccionados.Items);
                            Assert.Equal(
                                tipos
                                    .Items[0]
                                    .GetType()
                                    .GetProperty("NombreDocumento")!
                                    .GetValue(tipos.Items[0]),
                                seleccionados
                                    .Items[0]
                                    .GetType()
                                    .GetProperty("Nombre")!
                                    .GetValue(seleccionados.Items[0])
                            );
                            Pulsar(asistente, "Siguiente");
                            var lugares = Assert.IsType<System.Windows.Controls.DataGrid>(
                                asistente.FindName("Lugares")
                            );
                            lugares.Items[0]!
                                .GetType()
                                .GetProperty("Inicia")!
                                .SetValue(lugares.Items[0], true);
                            Pulsar(asistente, "Siguiente");
                            Pulsar(asistente, "Siguiente");
                            Pulsar(asistente, "Siguiente");
                            Pulsar(asistente, "Siguiente");
                            Assert.Equal(
                                "Paso 7 de 7",
                                Assert.IsType<TextBlock>(asistente.FindName("NumeroPaso")).Text
                            );
                            Assert.IsType<TextBlock>(asistente.FindName("PanelResumen"));
                        }
                        ventana.Close();
                    }
                }
                aplicacion.Shutdown();
            }
            catch (Exception excepcion)
            {
                error = excepcion;
                foreach (Window ventana in Application.Current?.Windows.Cast<Window>() ?? [])
                    ventana.Close();
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

    private static void Pulsar(Window dialogo, string nombre) =>
        Assert
            .IsType<Button>(dialogo.FindName(nombre))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
