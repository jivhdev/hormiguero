using System.Diagnostics;
using System.Windows;
using Hormiguero.Nucleo.Datos;

namespace Hormiguero.Reinicio;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (new[] { "Archivero", "Buscadero", "Mensajero" }.Any(HayProcesoAbierto))
        {
            MessageBox.Show(
                "Cierra Archivero, Buscadero y Mensajero antes de reiniciar Hormiguero.",
                "Reiniciar Hormiguero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            Shutdown();
            return;
        }

        var ventana = new VentanaReinicio();
        if (ventana.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        string carpeta = DatosDeApp.Carpeta;
        DateTime fecha = DateTime.Now;
        ResultadoReinicio resultado = new ReinicioServicio().Ejecutar(
            carpeta,
            fecha,
            ventana.BorrarClasificaciones
        );
        if (resultado.Exitoso)
        {
            MessageBox.Show(
                $"Listo. Se conservaron las configuraciones indicadas y toda la base de Mensajero. Respaldo en: {resultado.RutaRespaldo}",
                "Reiniciar Hormiguero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        else
        {
            MessageBox.Show(
                resultado.Mensaje,
                "No se pudo reiniciar Hormiguero",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
        Shutdown();
    }

    private static bool HayProcesoAbierto(string nombre)
    {
        Process[] procesos = Process.GetProcessesByName(nombre);
        foreach (Process proceso in procesos)
        {
            proceso.Dispose();
        }
        return procesos.Length > 0;
    }
}
