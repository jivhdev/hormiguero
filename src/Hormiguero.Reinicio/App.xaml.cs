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

        string carpeta = DatosDeApp.Carpeta;
        DateTime fecha = DateTime.Now;
        string respaldo = ReinicioServicio.RutaRespaldo(carpeta, fecha);
        MessageBoxResult respuesta = MessageBox.Show(
            $"Esto deja Archivero, Buscadero y Mensajero como recién instalados. Se conservan solo los clientes de ClickFactura (RUT, razón social y correos). Todo lo demás queda guardado como respaldo en: {respaldo}\n\n¿Continuar?",
            "Reiniciar Hormiguero",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No
        );
        if (respuesta != MessageBoxResult.Yes)
        {
            Shutdown();
            return;
        }

        ResultadoReinicio resultado = new ReinicioServicio().Ejecutar(carpeta, fecha);
        if (resultado.Exitoso)
        {
            MessageBox.Show(
                $"Listo. Se conservaron {resultado.ClientesConservados} clientes de ClickFactura. Respaldo en: {resultado.RutaRespaldo}",
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
