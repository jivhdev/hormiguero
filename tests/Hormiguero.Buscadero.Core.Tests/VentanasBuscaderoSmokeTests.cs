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
                ProbarAsistenteEsquema();
                ProbarEditorReglasEsquema();
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

    private static void ProbarAsistenteEsquema()
    {
        long facturaId;
        long ordenId;
        using (
            var conexion = Hormiguero.Nucleo.Datos.BaseComun.Abrir(
                Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun
            )
        )
        {
            var identificaciones = new Hormiguero.Nucleo.Datos.Identificaciones(conexion);
            facturaId = identificaciones.Guardar(
                new(0, "Factura propia", "MI EMPRESA DEMO", "{}", "Emitido")
            );
            ordenId = identificaciones.Guardar(
                new(0, "OC propia", "MI EMPRESA DEMO", "{}", "Emitido")
            );
            identificaciones.Guardar(
                new(0, "Nota de venta propia", "MI EMPRESA DEMO", "{}", "Emitido")
            );
            identificaciones.Guardar(new(0, "Guía", "PROVEEDOR DEMO SPA", "{}"));
            identificaciones.Guardar(new(0, "Factura", "PROVEEDOR DEMO SPA", "{}"));
        }

        var dialogo = new DialogoEsquemaCadena();
        Exception? error = null;
        dialogo.Loaded += (_, _) =>
        {
            var temporizador = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            temporizador.Tick += (_, _) =>
            {
                temporizador.Stop();
                try
                {
                    var disponibles = Assert.IsType<ListBox>(dialogo.FindName("TiposDisponibles"));
                    var proceso = Assert.IsType<ListBox>(dialogo.FindName("TiposProceso"));
                    Assert.Equal("Texto", disponibles.DisplayMemberPath);
                    var factura = disponibles
                        .Items.Cast<object>()
                        .Single(item => TextoTipo(item) == "Factura propia · MI EMPRESA DEMO");
                    var texto = TextoTipo(factura);
                    Assert.DoesNotContain("TipoVm", texto);
                    Assert.DoesNotContain("Identificacion {", texto);
                    disponibles.SelectedItem = factura;
                    Pulsar(dialogo, "BotonAgregarTipo");
                    Assert.Single(proceso.Items);
                    Assert.Equal("Factura propia · MI EMPRESA DEMO", TextoTipo(proceso.Items[0]!));
                    Assert.DoesNotContain(
                        disponibles.Items.Cast<object>(),
                        item => TextoTipo(item) == texto
                    );

                    var orden = disponibles
                        .Items.Cast<object>()
                        .Single(item => TextoTipo(item) == "OC propia · MI EMPRESA DEMO");
                    disponibles.SelectedItem = orden;
                    Pulsar(dialogo, "BotonAgregarTipo");
                    proceso.SelectedIndex = 1;
                    Pulsar(dialogo, "BotonSubirTipo");
                    Assert.Equal(ordenId, IdTipo(proceso.Items[0]!));

                    Assert.IsType<ComboBox>(dialogo.FindName("Proveedor")).Text =
                        "PROVEEDOR DEMO SPA";
                    Pulsar(dialogo, "Siguiente");
                    Pulsar(dialogo, "Siguiente");
                    var inicios = Assert.IsType<ListBox>(dialogo.FindName("Inicios"));
                    inicios.Items[0]!
                        .GetType()
                        .GetProperty("Inicia")!
                        .SetValue(inicios.Items[0], true);
                    Pulsar(dialogo, "Siguiente");
                    Pulsar(dialogo, "Siguiente");
                    Assert.Equal(
                        "Paso 5 de 7",
                        Assert.IsType<TextBlock>(dialogo.FindName("NumeroPaso")).Text
                    );
                    Pulsar(dialogo, "Siguiente");
                    Assert.Equal(
                        "Paso 6 de 7",
                        Assert.IsType<TextBlock>(dialogo.FindName("NumeroPaso")).Text
                    );
                    Assert.IsType<ListBox>(dialogo.FindName("Avisos"));
                    Pulsar(dialogo, "Siguiente");
                    Assert.Equal(
                        "Paso 7 de 7",
                        Assert.IsType<TextBlock>(dialogo.FindName("NumeroPaso")).Text
                    );
                    Pulsar(dialogo, "Guardar");
                }
                catch (Exception excepcion)
                {
                    error = excepcion;
                    dialogo.Close();
                }
            };
            temporizador.Start();
        };
        dialogo.ShowDialog();
        if (error is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();

        using var conexionFinal = Hormiguero.Nucleo.Datos.BaseComun.Abrir(
            Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun
        );
        var esquema = new Hormiguero.Nucleo.Datos.RepositorioEsquemas(
            conexionFinal
        ).ObtenerPorProveedor("PROVEEDOR DEMO SPA");
        Assert.NotNull(esquema);
        Assert.Equal(
            new[] { ordenId, facturaId },
            esquema.Lugares.OrderBy(l => l.Orden).Select(l => l.IdentificacionId)
        );
        var esquemas = new Hormiguero.Nucleo.Datos.RepositorioEsquemas(conexionFinal);
        esquemas.GuardarReglaAlerta(
            esquema.Id,
            0,
            null,
            "falta_dato",
            "{\"dato\":\"Fecha de retiro\",\"texto\":\"Ingresar fecha de retiro\"}"
        );
        using var cadena = conexionFinal.CreateCommand();
        cadena.CommandText =
            "INSERT INTO cadenas(modelo_id,nombre_modelo_origen,nombre,fecha_creacion,estructura_json,proveedor,cliente,esquema_id) VALUES(NULL,NULL,'Cadena sintética','2026-10-09','{}','PROVEEDOR DEMO SPA','Cliente de prueba',$esquema);";
        cadena.Parameters.AddWithValue("$esquema", esquema.Id);
        cadena.ExecuteNonQuery();
    }

    private static void ProbarEditorReglasEsquema()
    {
        var lugares = new[]
        {
            new LugarAlertaVm(0, "Orden de compra"),
            new LugarAlertaVm(1, "Guía"),
        };
        foreach (int tipo in Enumerable.Range(0, 3))
        {
            var dialogo = new DialogoReglaEsquema(lugares, ["Retiro"], [])
            {
                Width = 760,
                Height = 768,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
            };
            dialogo.Show();
            dialogo.UpdateLayout();
            Assert.True(dialogo.ActualHeight <= 700);
            if (tipo == 1)
                Pulsar(dialogo, "TipoPlazo");
            else if (tipo == 2)
                Pulsar(dialogo, "TipoListo");
            dialogo.UpdateLayout();

            Assert.Equal(
                tipo == 0 ? Visibility.Visible : Visibility.Collapsed,
                Assert.IsType<StackPanel>(dialogo.FindName("FaltaCampos")).Visibility
            );
            Assert.Equal(
                tipo == 1 ? Visibility.Visible : Visibility.Collapsed,
                Assert.IsType<StackPanel>(dialogo.FindName("PlazoCampos")).Visibility
            );
            Assert.Equal(
                tipo == 2 ? Visibility.Visible : Visibility.Collapsed,
                Assert.IsType<StackPanel>(dialogo.FindName("ListoCampos")).Visibility
            );
            var guardar = Assert.IsType<Button>(dialogo.FindName("Guardar"));
            Assert.True(guardar.IsVisible);
            Point extremo = guardar.TranslatePoint(new Point(0, guardar.ActualHeight), dialogo);
            Assert.True(extremo.Y <= dialogo.ActualHeight);
            dialogo.Close();
        }
    }

    private static string TextoTipo(object tipo) =>
        tipo.GetType().GetProperty("Texto")?.GetValue(tipo)?.ToString() ?? "";

    private static long IdTipo(object tipo)
    {
        var identificacion = tipo.GetType().GetProperty("Identificacion")!.GetValue(tipo)!;
        return (long)identificacion.GetType().GetProperty("Id")!.GetValue(identificacion)!;
    }

    private static void Pulsar(Window dialogo, string nombre) =>
        Assert
            .IsType<Button>(dialogo.FindName(nombre))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
