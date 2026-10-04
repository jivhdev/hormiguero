using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Archivero.Datos;
using Archivero.Servicios;
using Archivero.Vistas;

namespace Archivero;

public partial class App : System.Windows.Application
{
    private const string NombreMutexInstanciaUnica = "Archivero.InstanciaUnica";
    private const int SW_RESTORE = 9;

    private Mutex? _mutexInstanciaUnica;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Hormiguero (D-65): ARCHIVERO_DATOS abre la app con datos de prueba en otra carpeta,
        // sin tocar los reales ni chocar con la instancia que el usuario tenga abierta.
        // Sin la variable, todo sigue exactamente igual.
        string? datosDePrueba = Environment.GetEnvironmentVariable("ARCHIVERO_DATOS");
        string nombreMutex = NombreMutexInstanciaUnica;
        if (!string.IsNullOrWhiteSpace(datosDePrueba))
        {
            BaseDeDatos.RutaArchivo = System.IO.Path.Combine(datosDePrueba, "archivero.db");
            AuditoriaService.RutaLog = System.IO.Path.Combine(datosDePrueba, "auditoria.log");
            nombreMutex += ".prueba";
        }

        _mutexInstanciaUnica = new Mutex(
            initiallyOwned: true,
            nombreMutex,
            out var esInstanciaNueva
        );
        if (!esInstanciaNueva)
        {
            // Ya hay una instancia de Archivero corriendo: la segunda apertura solo enfoca
            // la ventana de la primera (REQ-005), no abre nada nuevo.
            EnfocarInstanciaExistente();
            Shutdown();
            return;
        }

        BaseDeDatos.AsegurarEsquema();

        var configuracion = new ConfiguracionRepository();
        var servicioCarpeta = new CarpetaObservadaService(configuracion);

        var carpetaObservada = servicioCarpeta.ObtenerCarpetaConfigurada();

        if (string.IsNullOrEmpty(carpetaObservada))
        {
            var onboarding = new OnboardingWindow(servicioCarpeta);
            var confirmado = onboarding.ShowDialog();

            if (confirmado != true || onboarding.CarpetaCreada is null)
            {
                Shutdown();
                return;
            }

            carpetaObservada = onboarding.CarpetaCreada;
        }

        var vigilancia = new VigilanciaCarpetaService(carpetaObservada);

        // MainWindow tiene que suscribirse a los eventos de vigilancia (CarpetaObservadaNoDisponible
        // en particular) ANTES de Iniciar(): si la carpeta observada ya no existe desde el
        // arranque, Iniciar() avisa de inmediato, y ese aviso se perdía en silencio porque
        // todavía no había nadie escuchando (bug real: la carpeta desapareció y Archivero nunca
        // dijo nada, ni siquiera al reabrirlo).
        var ventanaPrincipal = new MainWindow(carpetaObservada, vigilancia);
        MainWindow = ventanaPrincipal;

        vigilancia.Iniciar();

        ventanaPrincipal.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutexInstanciaUnica?.ReleaseMutex();
        _mutexInstanciaUnica?.Dispose();
        base.OnExit(e);
    }

    private static void EnfocarInstanciaExistente()
    {
        var ventana = FindWindow(null, "Archivero");
        if (ventana == IntPtr.Zero)
        {
            return;
        }

        ShowWindow(ventana, SW_RESTORE);
        SetForegroundWindow(ventana);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
