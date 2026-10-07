using System.Threading;
using System.Windows;
using Archivero.Datos;
using Archivero.Servicios;
using Archivero.Vistas;
using Hormiguero.Diseno;
using Microsoft.Data.Sqlite;

namespace Archivero.Tests;

public sealed class VentanasArchiveroSmokeTests
{
    [Fact]
    public void Diccionario_SeFiltraPorCategoriaYDerivaEmitidoRecibido()
    {
        Assert.Contains("Ventas propias", AsistenteClasificacionService.Categorias);
        Assert.Contains("Del cliente", AsistenteClasificacionService.Categorias);
        Assert.Contains("Compras propias", AsistenteClasificacionService.Categorias);
        Assert.Contains("Del proveedor", AsistenteClasificacionService.Categorias);
        Assert.Contains("Otros", AsistenteClasificacionService.Categorias);
        Assert.Contains(
            AsistenteClasificacionService.DocumentosDeCategoria("Del proveedor"),
            dato => dato.EtiquetaTipo == "Factura del proveedor"
        );
        Assert.Equal("Emitido", AsistenteClasificacionService.GrupoDocumento("Ventas propias"));
        Assert.Equal("Emitido", AsistenteClasificacionService.GrupoDocumento("Compras propias"));
        Assert.Equal("Recibido", AsistenteClasificacionService.GrupoDocumento("Del cliente"));
        Assert.Equal("Recibido", AsistenteClasificacionService.GrupoDocumento("Del proveedor"));
        Assert.Equal(string.Empty, AsistenteClasificacionService.GrupoDocumento("Otros"));
    }

    [Fact]
    public void NombreEstandarYValidacionPorPaso_UsanTextosConcretos()
    {
        var id = Hormiguero
            .Nucleo.Datos.DiccionarioDatosEnlazantes.Todos.Single(d =>
                d.EtiquetaTipo == "Factura del proveedor"
            )
            .Id;
        Assert.Equal(
            "Factura del proveedor · Cobelcar",
            AsistenteClasificacionService.NombreEstandar(id, "Cobelcar")
        );
        Assert.Equal("Factura del proveedor", AsistenteClasificacionService.NombreDocumento(id));
        Assert.Equal(string.Empty, AsistenteClasificacionService.NombreEstandar(id, " "));
        Assert.NotNull(AsistenteClasificacionService.ValidarDocumento(" "));
        Assert.Null(AsistenteClasificacionService.ValidarDocumento("Factura del proveedor"));
        Assert.NotNull(AsistenteClasificacionService.ValidarEmisor("Cobelcar", false));
        Assert.Null(AsistenteClasificacionService.ValidarEmisor("Cobelcar", true));
        Assert.NotNull(AsistenteClasificacionService.ValidarNumero("", true, false));
        Assert.Null(AsistenteClasificacionService.ValidarNumero("0000025378", true, false));
        Assert.NotNull(AsistenteClasificacionService.ValidarNumero("", false, true));
        Assert.Null(AsistenteClasificacionService.ValidarNumero("", true, true));
    }

    [Fact]
    public void VentanasDeIdentificacionYConfiguracion_AbrenEnHiloSta()
    {
        var raiz = Path.Combine(Path.GetTempPath(), $"Archivero-ventanas-{Guid.NewGuid():N}");
        Directory.CreateDirectory(raiz);
        var rutaAnterior = BaseDeDatos.RutaArchivo;
        var rutaPdf = CreadorPdfDePrueba.Crear(raiz, "EMISOR", "TIPO");
        var rutaPdfOscuro = CreadorPdfDePrueba.Crear(raiz, "EMISOR", "TIPO OSCURO");
        Exception? error = null;
        BaseDeDatos.RutaArchivo = Path.Combine(raiz, "archivero.db");
        BaseDeDatos.AsegurarEsquema();

        var hilo = new Thread(() =>
        {
            try
            {
                var aplicacion = new Archivero.App();
                aplicacion.InitializeComponent();
                aplicacion.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                Tema.Aplicar(aplicacion, ModoTema.Claro);

                var identificar = new IdentificarDocumentoWindow(rutaPdf);
                identificar.Width = 1366;
                identificar.Height = 768;
                identificar.Show();
                identificar.UpdateLayout();
                var tipoPaso = typeof(IdentificarDocumentoWindow).GetNestedType(
                    "Paso",
                    System.Reflection.BindingFlags.NonPublic
                )!;
                var mostrarPaso = typeof(IdentificarDocumentoWindow).GetMethod(
                    "MostrarPaso",
                    System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic
                )!;
                var titulo = (System.Windows.Controls.TextBlock)
                    identificar.FindName("TxtTituloPaso")!;
                var listaDocumentos = (System.Windows.Controls.ListBox)
                    identificar.FindName("ListaDocumentos")!;
                var categoria = (System.Windows.Controls.ComboBox)
                    identificar.FindName("CmbCategoriaDocumento")!;
                categoria.SelectedItem = "Ventas propias";
                listaDocumentos.SelectedItem = AsistenteClasificacionService
                    .DocumentosDeCategoria("Ventas propias")
                    .Single(d => d.Id == "factura_propia");
                identificar.UpdateLayout();
                var siguiente = (System.Windows.Controls.Button)
                    identificar.FindName("BtnSiguiente")!;
                siguiente.RaiseEvent(
                    new System.Windows.RoutedEventArgs(
                        System.Windows.Controls.Primitives.ButtonBase.ClickEvent
                    )
                );
                identificar.UpdateLayout();
                if (!titulo.Text.StartsWith("Paso 2 de 6", StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Elegir un documento y avanzar no llegó al paso del emisor."
                    );

                var emisor = (System.Windows.Controls.ComboBox)identificar.FindName("CmbEmisor")!;
                var emisorDePrueba = $"Emisor de prueba {Guid.NewGuid():N}";
                var eventoTexto = (System.Windows.Controls.TextChangedEventHandler)
                    Delegate.CreateDelegate(
                        typeof(System.Windows.Controls.TextChangedEventHandler),
                        identificar,
                        typeof(IdentificarDocumentoWindow).GetMethod(
                            "CmbEmisor_TextChanged",
                            System.Reflection.BindingFlags.Instance
                                | System.Reflection.BindingFlags.NonPublic
                        )!
                    );
                emisor.RemoveHandler(
                    System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                    eventoTexto
                );
                emisor.Text = emisorDePrueba;
                typeof(IdentificarDocumentoWindow)
                    .GetField(
                        "_emisor",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )!
                    .SetValue(identificar, emisorDePrueba);
                var marcas =
                    (Dictionary<CampoMarca, Marca>)
                        typeof(IdentificarDocumentoWindow)
                            .GetField(
                                "_marcas",
                                System.Reflection.BindingFlags.Instance
                                    | System.Reflection.BindingFlags.NonPublic
                            )!
                            .GetValue(identificar)!;
                marcas[CampoMarca.Emisor] = new(CampoMarca.Emisor, 0, 0.1, 0.1, 0.1, 0.05);
                var datos =
                    (System.Collections.ObjectModel.ObservableCollection<DatoEnlazanteEdicion>)
                        typeof(IdentificarDocumentoWindow)
                            .GetField(
                                "_datosEnlazantes",
                                System.Reflection.BindingFlags.Instance
                                    | System.Reflection.BindingFlags.NonPublic
                            )!
                            .GetValue(identificar)!;
                var datoNumero = datos.Single(d => d.DefineTipo);
                datoNumero.Incluido = true;
                datoNumero.Marcado = true;
                datoNumero.ValorLeido = "123";
                datoNumero.Pagina = 0;
                datoNumero.X = 0.1;
                datoNumero.Y = 0.1;
                datoNumero.Ancho = 0.1;
                datoNumero.Alto = 0.05;
                ((System.Windows.Controls.TextBox)identificar.FindName("TxtCarpetaDestino")!).Text =
                    raiz;
                (
                    (System.Windows.Controls.RadioButton)identificar.FindName("RbGuardarDirecto")!
                ).IsChecked = true;
                (
                    (System.Windows.Controls.RadioButton)identificar.FindName("RbMantenerNombre")!
                ).IsChecked = true;
                var pasos = new[] { "Numero", "OtrosDatos", "Guardar", "Resumen" };
                for (var indice = 0; indice < pasos.Length; indice++)
                {
                    mostrarPaso.Invoke(identificar, [Enum.Parse(tipoPaso, pasos[indice])]);
                    identificar.UpdateLayout();
                    if (
                        !titulo.Text.StartsWith($"Paso {indice + 3} de 6", StringComparison.Ordinal)
                    )
                        throw new InvalidOperationException(
                            $"No se mostró correctamente el paso {indice + 1}."
                        );
                }
                identificar.Close();

                var sinTexto = new IdentificarSinTextoWindow(rutaPdf);
                sinTexto.Width = 1366;
                sinTexto.Height = 768;
                sinTexto.Show();
                sinTexto.UpdateLayout();
                sinTexto.Close();

                var configuracion = new AdministrarClasificacionesWindow();
                configuracion.Show();
                configuracion.UpdateLayout();
                configuracion.Close();

                var duplicado = new ResolverDuplicadoWindow(rutaPdf, rutaPdf);
                duplicado.Show();
                duplicado.UpdateLayout();
                duplicado.Close();

                var administrarAtajos = new AdministrarAtajosWindow();
                administrarAtajos.Show();
                administrarAtajos.UpdateLayout();
                administrarAtajos.Close();

                var guardarAtajo = new GuardarAtajoWindow(
                    "Acceso de prueba",
                    new AtajoGuardadoRapidoRepository()
                );
                guardarAtajo.Show();
                guardarAtajo.UpdateLayout();
                guardarAtajo.Close();

                Tema.Aplicar(aplicacion, ModoTema.Oscuro);
                var identificarOscuro = new IdentificarDocumentoWindow(rutaPdfOscuro);
                identificarOscuro.Width = 1366;
                identificarOscuro.Height = 768;
                identificarOscuro.Show();
                identificarOscuro.UpdateLayout();
                identificarOscuro.Close();

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
            BaseDeDatos.RutaArchivo = rutaAnterior;
            SqliteConnection.ClearAllPools();
            for (var intento = 0; ; intento++)
            {
                try
                {
                    Directory.Delete(raiz, recursive: true);
                    break;
                }
                catch (IOException) when (intento < 19)
                {
                    SqliteConnection.ClearAllPools();
                    Thread.Sleep(100);
                }
                catch (UnauthorizedAccessException) when (intento < 19)
                {
                    SqliteConnection.ClearAllPools();
                    Thread.Sleep(100);
                }
            }

            if (bloqueoTomado)
            {
                bloqueoVentanas.ReleaseMutex();
            }
        }
    }
}
