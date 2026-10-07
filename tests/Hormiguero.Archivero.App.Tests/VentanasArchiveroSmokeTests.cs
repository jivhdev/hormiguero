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

                foreach (var modo in new[] { ModoTema.Claro, ModoTema.Oscuro })
                {
                    Tema.Aplicar(aplicacion, modo);

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
                    var pasos = new[]
                    {
                        "QueDocumento",
                        "Emisor",
                        "Numero",
                        "OtrosDatos",
                        "Guardar",
                        "Resumen",
                    };
                    for (var indice = 0; indice < pasos.Length; indice++)
                    {
                        mostrarPaso.Invoke(identificar, [Enum.Parse(tipoPaso, pasos[indice])]);
                        identificar.UpdateLayout();
                        if (
                            !titulo.Text.StartsWith(
                                $"Paso {indice + 1} de 6",
                                StringComparison.Ordinal
                            )
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
