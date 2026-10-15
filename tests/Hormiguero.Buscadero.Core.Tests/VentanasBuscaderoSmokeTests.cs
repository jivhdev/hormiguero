using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
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
                EjecutarAceptacionEsquema(aplicacion);
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

    private static void EjecutarAceptacionEsquema(App aplicacion)
    {
        Tema.Aplicar(aplicacion, ModoTema.Claro);
        var asistente = new DialogoEsquemaDiccionario { Width = 1366, Height = 768 };
        asistente.Show();
        asistente.UpdateLayout();
        AssertBotonesDentro(asistente, "Anterior", "Siguiente", "Cancelar");
        Assert.IsType<ComboBox>(asistente.FindName("Proveedor")).Text = "PROVEEDOR DEMO SPA";
        Assert.IsType<TextBox>(asistente.FindName("Nombre")).Text = "PROVEEDOR DEMO SPA";
        Pulsar(asistente, "Siguiente");
        foreach (
            string id in new[]
            {
                "nota_venta_propia",
                "oc_propia",
                "guia_proveedor",
                "factura_proveedor",
                "factura_propia",
            }
        )
        {
            var tipos = Assert.IsType<ListBox>(asistente.FindName("Tipos"));
            tipos.SelectedItem = tipos
                .Items.Cast<object>()
                .Single(item => Propiedad<string>(item, "Id") == id);
            Pulsar(asistente, "AgregarTipo");
        }
        Pulsar(asistente, "Siguiente");
        Pulsar(asistente, "Siguiente");
        AgregarEnlace(asistente, 1, 2, "oc_propia", "uno_a_varios");
        AgregarEnlace(asistente, 2, 3, "guia_proveedor", "uno_a_uno");
        Pulsar(asistente, "Siguiente");
        Assert.IsType<TextBox>(asistente.FindName("ModoNuevo")).Text = "Retiro";
        PulsarPorContenido(asistente, "Agregar");
        Assert.IsType<TextBox>(asistente.FindName("ModoNuevo")).Text = "Despacho";
        PulsarPorContenido(asistente, "Agregar");
        var selectorDecide = Assert.IsType<ComboBox>(asistente.FindName("LugarDecideModo"));
        selectorDecide.SelectedItem = selectorDecide
            .Items.Cast<object>()
            .Single(item => Propiedad<int>(item, "Orden") == 0);
        Pulsar(asistente, "Siguiente");
        foreach (var tema in new[] { ModoTema.Claro, ModoTema.Oscuro, ModoTema.Claro })
        {
            Tema.Aplicar(aplicacion, tema);
            ConfigurarSiguienteAviso(asistente);
        }
        Assert.Equal(3, Assert.IsType<ListBox>(asistente.FindName("ListaAvisos")).Items.Count);
        Pulsar(asistente, "Siguiente");
        Assert.Equal(
            "Paso 7 de 7",
            Assert.IsType<TextBlock>(asistente.FindName("NumeroPaso")).Text
        );
        Pulsar(asistente, "Guardar");
        asistente.Close();

        var lista = new DialogoListaEsquemas();
        lista.Show();
        var esquemas = Assert.IsType<ListBox>(lista.FindName("Esquemas"));
        esquemas.SelectedItem = esquemas
            .Items.Cast<object>()
            .Single(item => Propiedad<string>(item, "Proveedor") == "PROVEEDOR DEMO SPA");
        Exception? error = null;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        timer.Tick += (_, _) =>
        {
            var editado = Application
                .Current!.Windows.Cast<Window>()
                .SingleOrDefault(window => window.Title == "Esquema de documentos");
            if (editado is null)
                return;
            timer.Stop();
            try
            {
                VerificarEsquemaReabierto(editado);
            }
            catch (Exception excepcion)
            {
                error = excepcion;
            }
            finally
            {
                editado.Close();
            }
        };
        timer.Start();
        PulsarPorContenido(lista, "Editar");
        Assert.Null(error);
        lista.Close();
    }

    private static void VerificarEsquemaReabierto(Window editado)
    {
        editado.UpdateLayout();
        AssertBotonesDentro(editado, "Anterior", "Siguiente", "Cancelar");
        var lugares = Assert.IsType<DataGrid>(editado.FindName("Lugares"));
        Assert.Equal(
            new[]
            {
                "NVV propia",
                "OC propia",
                "Gu\u00eda del proveedor",
                "Factura del proveedor",
                "Factura propia",
            },
            lugares.Items.Cast<object>().Select(item => Propiedad<string>(item, "Nombre"))
        );
        var enlaces = Assert
            .IsType<ListBox>(editado.FindName("Enlaces"))
            .Items.Cast<string>()
            .ToArray();
        Assert.Contains(
            enlaces,
            texto =>
                texto.Contains(
                    "OC propia puede tener varias Gu\u00edas del proveedor",
                    StringComparison.Ordinal
                )
        );
        Assert.Contains(
            enlaces,
            texto =>
                texto.Contains(
                    "Gu\u00eda del proveedor se enlaza con Factura del proveedor",
                    StringComparison.Ordinal
                )
        );
        Assert.Equal(
            new[] { "Retiro", "Despacho" },
            Assert.IsType<ListBox>(editado.FindName("ModosLista")).Items.Cast<string>()
        );
        Assert.Equal(
            "NVV propia",
            Propiedad<string>(
                Assert.IsType<ComboBox>(editado.FindName("LugarDecideModo")).SelectedItem!,
                "Nombre"
            )
        );
        var avisos = Assert
            .IsType<ListBox>(editado.FindName("ListaAvisos"))
            .Items.Cast<object>()
            .ToArray();
        Assert.Equal(3, avisos.Length);
        Assert.Contains(
            avisos,
            aviso =>
                Propiedad<string>(aviso, "Frase")
                    .Contains("fecha de retiro", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Contains(
            avisos,
            aviso =>
                Propiedad<string>(aviso, "Frase")
                    .Contains("7 d\u00edas h\u00e1biles", StringComparison.Ordinal)
        );
        Assert.Contains(
            avisos,
            aviso =>
                Propiedad<string>(aviso, "Frase")
                == "Listo para facturar cuando est\u00e9n Gu\u00eda del proveedor + Factura del proveedor, hasta que llegue Factura propia"
        );
        var plazo = avisos.Single(aviso => Propiedad<string>(aviso, "Tipo") == "plazo");
        using var parametros = System.Text.Json.JsonDocument.Parse(
            Propiedad<string>(plazo, "ParametrosJson")
        );
        Assert.True(parametros.RootElement.GetProperty("porLinea").GetBoolean());
    }

    private static void AgregarEnlace(
        Window dialogo,
        int ordenA,
        int ordenB,
        string datoId,
        string cardinalidad
    )
    {
        var a = Assert.IsType<ComboBox>(dialogo.FindName("LugarA"));
        var b = Assert.IsType<ComboBox>(dialogo.FindName("LugarB"));
        a.SelectedItem = a
            .Items.Cast<object>()
            .Single(item => Propiedad<int>(item, "Orden") == ordenA);
        b.SelectedItem = b
            .Items.Cast<object>()
            .Single(item => Propiedad<int>(item, "Orden") == ordenB);
        var dato = Assert.IsType<ComboBox>(dialogo.FindName("Dato"));
        dato.SelectedItem = dato
            .Items.Cast<object>()
            .Single(item => Propiedad<string>(item, "Id") == datoId);
        var cardinalidadCombo = Assert.IsType<ComboBox>(dialogo.FindName("Cardinalidad"));
        cardinalidadCombo.SelectedItem = cardinalidadCombo
            .Items.Cast<ComboBoxItem>()
            .Single(item => Equals(item.Tag, cardinalidad));
        PulsarPorContenido(dialogo, "Agregar enlace");
    }

    private static void ConfigurarSiguienteAviso(DialogoEsquemaDiccionario asistente)
    {
        Exception? error = null;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        timer.Tick += (_, _) =>
        {
            var editor = Application
                .Current!.Windows.Cast<Window>()
                .SingleOrDefault(window => window.Title == "Editar aviso");
            if (editor is null)
                return;
            timer.Stop();
            try
            {
                int indiceAviso = Assert
                    .IsType<ListBox>(asistente.FindName("ListaAvisos"))
                    .Items.Count;
                var tipo = indiceAviso switch
                {
                    0 => "TipoAviso0",
                    1 => "TipoAviso1",
                    _ => "TipoAviso2",
                };
                var tarjeta = Assert.IsType<Button>(editor.FindName(tipo));
                if (indiceAviso > 0)
                    tarjeta.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("Elegida", AutomationProperties.GetItemStatus(tarjeta));
                Assert.Contains(
                    "elegida",
                    AutomationProperties.GetName(tarjeta),
                    StringComparison.OrdinalIgnoreCase
                );
                var workArea = SystemParameters.WorkArea;
                var guardar = Assert.IsType<Button>(editor.FindName("GuardarAviso"));
                Point esquinaGuardar = guardar.PointToScreen(
                    new Point(guardar.ActualWidth, guardar.ActualHeight)
                );
                Assert.InRange(esquinaGuardar.X, workArea.Left, workArea.Right);
                Assert.InRange(esquinaGuardar.Y, workArea.Top, workArea.Bottom);
                if (indiceAviso == 0)
                {
                    var dato = Assert.IsType<ComboBox>(editor.FindName("DatoAviso"));
                    dato.Text = "Fecha de retiro";
                    var texto = Assert.IsType<TextBox>(editor.FindName("TextoAviso"));
                    Assert.Equal("Ingresar fecha de retiro", texto.Text);
                    texto.Text = "Completar fecha de retiro";
                    dato.SelectedItem = dato
                        .Items.Cast<object>()
                        .Single(item => Propiedad<string>(item, "Id") == "codigo_obra");
                    Assert.Equal("Completar fecha de retiro", texto.Text);
                    dato.Text = "Fecha de retiro";
                    var modo =
                        editor.Content is DockPanel root
                        && root.Children.OfType<ScrollViewer>().Single().Content
                            is StackPanel contenido
                            ? contenido.Children.OfType<ComboBox>().Last()
                            : throw new InvalidOperationException(
                                "No se encontró el selector de modo."
                            );
                    modo.SelectedIndex = 1;
                }
                else if (indiceAviso == 1)
                {
                    var origen = Assert.IsType<ComboBox>(editor.FindName("OrigenAviso"));
                    origen.SelectedItem = origen
                        .Items.Cast<object>()
                        .Single(item =>
                            item.GetType().Name != "DatoAvisoOpcion"
                            && Propiedad<int>(item, "Orden") == 2
                        );
                    var destino = Assert.IsType<ComboBox>(editor.FindName("DestinoAviso"));
                    destino.SelectedItem = destino
                        .Items.Cast<object>()
                        .Single(item => Propiedad<int>(item, "Orden") == 3);
                    var linea = Assert.IsType<CheckBox>(FindVisualChild<CheckBox>(editor));
                    Assert.True(linea.IsEnabled);
                    linea.IsChecked = true;
                    Assert.Contains(
                        "7 días hábiles",
                        Assert.IsType<TextBlock>(editor.FindName("FraseAviso")).Text,
                        StringComparison.Ordinal
                    );
                    Assert.Contains(
                        "(por línea)",
                        Assert.IsType<TextBlock>(editor.FindName("FraseAviso")).Text,
                        StringComparison.Ordinal
                    );
                }
                else
                {
                    var cuando = Assert.IsType<ComboBox>(editor.FindName("CuandoAviso"));
                    cuando.SelectedItem = cuando
                        .Items.Cast<object>()
                        .Single(item => Propiedad<int>(item, "Orden") == 2);
                    var tambien = Assert.IsType<ComboBox>(editor.FindName("CondicionAviso"));
                    tambien.SelectedItem = tambien
                        .Items.Cast<object>()
                        .Single(item => Propiedad<int>(item, "Orden") == 3);
                    var destino = Assert.IsType<ComboBox>(editor.FindName("DestinoAviso"));
                    destino.SelectedItem = destino
                        .Items.Cast<object>()
                        .Single(item => Propiedad<int>(item, "Orden") == 4);
                    Assert.Equal(
                        "Listos para facturar",
                        Assert.IsType<TextBox>(editor.FindName("TextoAviso")).Text
                    );
                    Assert.Equal(
                        "Listo para facturar cuando estén Guía del proveedor + Factura del proveedor, hasta que llegue Factura propia.",
                        Assert.IsType<TextBlock>(editor.FindName("FraseAviso")).Text
                    );
                }
                guardar.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception error)
            {
                timer.Stop();
                editor.Close();
                error = new InvalidOperationException("Falló la preparación STA del aviso.", error);
            }
        };
        timer.Start();
        Pulsar(asistente, "BotonAgregarAviso");
        Assert.Null(error);
    }

    private static void AssertBotonesDentro(Window ventana, params string[] nombres)
    {
        ventana.UpdateLayout();
        foreach (string nombre in nombres)
        {
            var boton = Assert.IsAssignableFrom<FrameworkElement>(ventana.FindName(nombre));
            Point esquina = boton.PointToScreen(new Point(boton.ActualWidth, boton.ActualHeight));
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
    }

    private static void PulsarPorContenido(Window dialogo, string contenido)
    {
        var boton = FindVisualChildren<Button>(dialogo)
            .Single(button => Equals(button.Content, contenido));
        boton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
                yield return typed;
            foreach (var nested in FindVisualChildren<T>(child))
                yield return nested;
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject => FindVisualChildren<T>(parent).FirstOrDefault();

    private static T Propiedad<T>(object objeto, string nombre) =>
        (T)objeto.GetType().GetProperty(nombre)!.GetValue(objeto)!;

    private static void Pulsar(Window dialogo, string nombre) =>
        Assert
            .IsType<Button>(dialogo.FindName(nombre))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
