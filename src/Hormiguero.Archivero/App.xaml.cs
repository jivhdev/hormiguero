using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Archivero.Datos;
using Archivero.Servicios;
using Archivero.Vistas;
using Hormiguero.Diseno;

namespace Archivero;

public partial class App : System.Windows.Application
{
    private const string NombreMutexInstanciaUnica = "Archivero.InstanciaUnica";
    private const int SW_RESTORE = 9;

    private Mutex? _mutexInstanciaUnica;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Fase B-1 (D-66): diseño común de Hormiguero, claro u oscuro según Windows.
        Tema.Aplicar(
            this,
            Enum.TryParse<ModoTema>(
                Hormiguero.Nucleo.Datos.DatosDeApp.LeerPreferencia("tema.archivero"),
                out var modoTema
            )
                ? modoTema
                : ModoTema.Sistema
        );

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
        else if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HORMIGUERO_DATOS")))
        {
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

        if (string.IsNullOrWhiteSpace(datosDePrueba))
        {
            UsarCarpetaComunDeHormiguero();
            PublicadorDatosDocumentoService.PublicacionAutomaticaActiva = true;

            // Fase B-4 (D-66): avisa a las otras apps de cada documento guardado.
            ClasificadorService.DocumentoGuardado += AvisarAHormiguero;
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

    // Fase B-3 (D-66): los datos viven en la carpeta común de Hormiguero. La primera vez
    // se copian solos desde %LocalAppData%\Archivero, que queda intacto como respaldo.
    private static void UsarCarpetaComunDeHormiguero()
    {
        string baseAnterior = BaseDeDatos.RutaArchivo;
        string logAnterior = AuditoriaService.RutaLog;

        BaseDeDatos.RutaArchivo = Hormiguero.Nucleo.Datos.DatosDeApp.Preparar(
            "archivero",
            baseAnterior
        );

        string logNuevo = System.IO.Path.Combine(
            Hormiguero.Nucleo.Datos.DatosDeApp.Carpeta,
            "archivero-auditoria.log"
        );
        if (!System.IO.File.Exists(logNuevo) && System.IO.File.Exists(logAnterior))
        {
            System.IO.File.Copy(logAnterior, logNuevo);
        }
        AuditoriaService.RutaLog = logNuevo;
    }

    // Fase B-4 (D-66): avisa a las otras apps (Buscadero lo encuentra al instante).
    // Si falla, el documento igual quedó guardado: solo se anota en la auditoría.
    private static void AvisarAHormiguero(string rutaFinal)
    {
        try
        {
            using var conexion = Hormiguero.Nucleo.Datos.BaseComun.Abrir(
                Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun
            );
            new Hormiguero.Nucleo.Datos.DocumentosGuardados(conexion).Registrar(
                rutaFinal,
                "Archivero"
            );
        }
        catch (Exception error)
        {
            AuditoriaService.Registrar("AVISO_HORMIGUERO_FALLIDO", $"{rutaFinal}: {error.Message}");
        }
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
