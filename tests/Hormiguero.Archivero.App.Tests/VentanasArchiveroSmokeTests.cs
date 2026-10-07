using System.Threading;
using System.Windows;
using Archivero.Datos;
using Archivero.Vistas;
using Hormiguero.Diseno;
using Microsoft.Data.Sqlite;

namespace Archivero.Tests;

public sealed class VentanasArchiveroSmokeTests
{
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
