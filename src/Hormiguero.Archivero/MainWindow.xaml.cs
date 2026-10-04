using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Archivero.Datos;
using Archivero.Servicios;
using Archivero.Vistas;

namespace Archivero;

/// <summary>Fila de "Guardados automáticamente" ya lista para mostrar (el repositorio no formatea texto de interfaz).</summary>
public record GuardadoRecienteFila(DateTime Hora, string RutaFinal)
{
    public string Resumen => $"{Hora:HH:mm:ss} — {Path.GetFileName(RutaFinal)} → {RutaFinal}";
}

/// <summary>Fila de "Pendientes de distribuir": un archivo dañado lleva el aviso a la vista, para no confundirlo con un PDF sin texto (Caso-11, punto 5).</summary>
public record PendienteDistribucionFila(ArchivoPendiente Pendiente)
{
    public string Texto => Pendiente.Motivo == MotivoPendiente.ArchivoDanado
        ? $"⚠ {Pendiente.NombreArchivo} — no se pudo leer este archivo"
        : Pendiente.NombreArchivo;
}

/// <summary>Fila de "Pendientes por reconocer": un documento que solo espera el nombre lo dice, para no confundirlo con uno sin identificar (Caso-11, punto 1).</summary>
public record PendienteReconocerFila(ArchivoPendiente Pendiente)
{
    public string Texto => Pendiente.Motivo == MotivoPendiente.NombrePorConfirmar
        ? $"{Pendiente.NombreArchivo} — falta confirmar el nombre"
        : Pendiente.NombreArchivo;
}

public partial class MainWindow : Window
{
    private readonly PendienteRepository _pendientes = new();
    private readonly ConfiguracionDocumentoRepository _configuraciones = new();
    private readonly ConfiguracionRepository _configuracion = new();
    private readonly CarpetaObservadaService _servicioCarpeta;
    private readonly GuardadoRecienteRepository _guardadosRecientes = new();
    private VigilanciaCarpetaService _vigilancia;
    private string _carpetaObservada;
    private readonly TrayIconService _bandeja = new();
    private bool _permitirCierre;

    public MainWindow(string carpetaObservada, VigilanciaCarpetaService vigilancia)
    {
        InitializeComponent();
        _servicioCarpeta = new CarpetaObservadaService(_configuracion);
        _carpetaObservada = carpetaObservada;
        TxtCarpetaObservada.Text = $"Carpeta observada: {carpetaObservada}";

        _vigilancia = vigilancia;
        SuscribirEventosVigilancia();

        _bandeja.MostrarVentanaSolicitado += () => Dispatcher.Invoke(RestaurarVentana);
        _bandeja.SalirSolicitado += () => Dispatcher.Invoke(SalirDeVerdad);

        CargarPendientes();
        CargarGuardadosRecientes();
        CargarTiempoAhorrado();
    }

    private void SuscribirEventosVigilancia()
    {
        _vigilancia.ArchivoPendienteDetectado += _ => Dispatcher.Invoke(CargarPendientes);
        _vigilancia.ArchivoPendienteEliminado += _ => Dispatcher.Invoke(CargarPendientes);
        _vigilancia.ArchivoRequiereAtencion += (_, _) => Dispatcher.Invoke(CargarPendientes);
        _vigilancia.ArchivoGuardadoAutomaticamente += (_, rutaFinal) => Dispatcher.Invoke(() => AgregarAGuardadosRecientes(rutaFinal));
        _vigilancia.CarpetaObservadaNoDisponible += () => Dispatcher.Invoke(AvisarCarpetaNoDisponible);
    }

    private void BtnCambiarCarpetaObservada_Click(object sender, RoutedEventArgs e)
    {
        var ventana = new CambiarCarpetaObservadaWindow(_servicioCarpeta, _carpetaObservada) { Owner = this };
        if (ventana.ShowDialog() != true || ventana.CarpetaNueva is null)
        {
            return;
        }

        AuditoriaService.Registrar("CARPETA_OBSERVADA_CAMBIADA", $"Anterior={_carpetaObservada}; Nueva={ventana.CarpetaNueva}");

        _vigilancia.Dispose();
        _carpetaObservada = ventana.CarpetaNueva;
        TxtCarpetaObservada.Text = $"Carpeta observada: {_carpetaObservada}";

        _vigilancia = new VigilanciaCarpetaService(_carpetaObservada);
        SuscribirEventosVigilancia();
        _vigilancia.Iniciar();

        CargarPendientes();
    }

    private void CargarPendientes()
    {
        var pendientes = _pendientes.ObtenerTodos();

        // Caso-4, punto 1: los PDF sin texto extraible tienen su propia lista ("Pendientes de
        // distribuir"), separada de "Pendientes por reconocer" -- nunca se mezclan.
        ListaPendientes.ItemsSource = pendientes.Where(p => !p.Motivo.EsPendienteDeDistribuir())
            .Select(p => new PendienteReconocerFila(p))
            .ToList();
        ListaPendientesDistribucion.ItemsSource = pendientes.Where(p => p.Motivo.EsPendienteDeDistribuir())
            .Select(p => new PendienteDistribucionFila(p))
            .ToList();

        _bandeja.ActualizarPendientes(pendientes.Count > 0);
    }

    private void AgregarAGuardadosRecientes(string rutaFinal)
    {
        _guardadosRecientes.Agregar(rutaFinal);
        // Caso-8: el contador de tiempo ahorrado se lleva aparte de GuardadosRecientes (que se
        // recorta a los últimos 20 -- Caso-6, punto 2), justo acá, en el único punto real donde
        // un documento se archivó solo (REQ-002; nunca el flujo manual de PDFs sin texto).
        TiempoAhorradoService.RegistrarDocumentoArchivado(_configuracion);
        CargarGuardadosRecientes();
        CargarTiempoAhorrado();
    }

    private void CargarGuardadosRecientes()
    {
        ListaGuardados.ItemsSource = _guardadosRecientes.ObtenerTodos()
            .Select(g => new GuardadoRecienteFila(g.Hora, g.RutaFinal))
            .ToList();
    }

    private void CargarTiempoAhorrado()
    {
        var total = TiempoAhorradoService.ObtenerTotalDocumentos(_configuracion);
        var (titulo, aclaracion) = TiempoAhorradoService.FormatearResumen(total);
        TxtTiempoAhorrado.Text = titulo;
        TxtTiempoAhorradoAclaracion.Text = aclaracion;
    }

    private void ListaGuardados_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ListaGuardados.SelectedItem is not GuardadoRecienteFila guardado)
        {
            return;
        }

        var ventana = new VerGuardadoWindow(guardado.RutaFinal) { Owner = this };
        ventana.ShowDialog();

        if (ventana.ConfiguracionEditada)
        {
            _ = ReprocesarPendientesAsync();
        }
    }

    private void AbrirAsistenteIdentificacion(string rutaArchivo)
    {
        var asistente = new IdentificarDocumentoWindow(rutaArchivo) { Owner = this };
        if (asistente.ShowDialog() == true)
        {
            _ = ReprocesarPendientesAsync();
        }
    }

    private void BtnReprocesarPendientes_Click(object sender, RoutedEventArgs e) => _ = ReprocesarPendientesAsync();

    /// <summary>
    /// Caso-11, punto 3: se llama sola apenas se crea o se edita una configuración (y a mano con
    /// el botón), para que un documento que ya estaba esperando en pendientes se clasifique sin
    /// tener que sacarlo y volver a meterlo en la carpeta. Corre fuera del hilo de la interfaz
    /// porque lee cada PDF pendiente; VigilanciaCarpetaService lo serializa con el watcher.
    /// </summary>
    private async Task ReprocesarPendientesAsync()
    {
        BtnReprocesarPendientes.IsEnabled = false;
        try
        {
            var vigilancia = _vigilancia;
            await Task.Run(vigilancia.ReprocesarPendientes);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"No se pudieron reprocesar los pendientes: {ex.Message}", "Archivero",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            BtnReprocesarPendientes.IsEnabled = true;
            CargarPendientes();
        }
    }

    private void AvisarCarpetaNoDisponible()
    {
        System.Windows.MessageBox.Show(
            this,
            $"La carpeta observada ya no está disponible (se movió o se borró):\n{_carpetaObservada}\n\n" +
            "Archivero no puede seguir vigilándola hasta que vuelva a estar accesible. Podés recrearla con ese mismo nombre y ruta, " +
            "o usar el botón \"Cambiar…\" para elegir otra.",
            "Archivero", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void RestaurarVentana()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void SalirDeVerdad()
    {
        _permitirCierre = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_permitirCierre)
        {
            // Cerrar la ventana no apaga Archivero: se minimiza a la bandeja y sigue
            // observando la carpeta en silencio (REQ-005). "Salir" desde la bandeja es la
            // unica forma de terminar el proceso de verdad.
            e.Cancel = true;
            Hide();
            return;
        }

        _bandeja.Dispose();
        base.OnClosing(e);
        System.Windows.Application.Current.Shutdown();
    }

    private void ListaPendientes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ListaPendientes.SelectedItem is not PendienteReconocerFila { Pendiente: var pendiente })
        {
            return;
        }

        if (pendiente.Motivo == MotivoPendiente.NombrePorConfirmar)
        {
            AbrirConfirmacionDeNombre(pendiente.RutaArchivo);
        }
        else if (pendiente.Motivo == MotivoPendiente.Duplicado)
        {
            AbrirResolucionDeDuplicado(pendiente.RutaArchivo);
        }
        else if (pendiente.Motivo == MotivoPendiente.PeriodoNuevo)
        {
            AbrirCreacionDePeriodo(pendiente.RutaArchivo);
        }
        else if (pendiente.Motivo is MotivoPendiente.TextoConCaracteresInvalidos or MotivoPendiente.NombreReservadoPorWindows
            or MotivoPendiente.RutaFueraDeCarpetaConfigurada or MotivoPendiente.NombreORutaDemasiadoLarga)
        {
            // Caso-9, mejora 1: no tiene sentido reabrir el asistente -- el dato extraído es el
            // mismo texto problemático de siempre. Se explica el motivo y se deja para revisar a mano.
            System.Windows.MessageBox.Show(
                this,
                $"Este documento no se puede guardar automáticamente:\n\n{pendiente.Motivo.DescripcionLegible()}\n\n" +
                "Revisalo a mano; si corresponde, movelo vos mismo a su carpeta.",
                "Archivero", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            AbrirAsistenteIdentificacion(pendiente.RutaArchivo);
        }

        CargarPendientes();
    }

    private void BtnRecargarPendientesDistribucion_Click(object sender, RoutedEventArgs e) => CargarPendientes();

    private void ListaPendientesDistribucion_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ListaPendientesDistribucion.SelectedItem is not PendienteDistribucionFila { Pendiente: var pendiente })
        {
            return;
        }

        var identificarSinTexto = new IdentificarSinTextoWindow(pendiente.RutaArchivo, pendiente.Motivo == MotivoPendiente.ArchivoDanado) { Owner = this };
        identificarSinTexto.ShowDialog();

        CargarPendientes();
    }

    private void AbrirResolucionDeDuplicado(string rutaArchivo)
    {
        if (!File.Exists(rutaArchivo))
        {
            return;
        }

        // Se recalcula todo en el momento (coincidencia + campos extraidos) en vez de guardarlo
        // en la base: es el mismo documento y la misma configuracion, asi que da lo mismo, y
        // evita duplicar el estado en Pendientes.
        var configuraciones = _configuraciones.ObtenerTodasConPatrones();
        var coincidencia = CoincidenciaAutomaticaService.BuscarConfiguracionQueCoincide(rutaArchivo, configuraciones);
        if (coincidencia is null)
        {
            // Ya no coincide con ninguna configuracion (por ejemplo, se borro) -> tratarlo
            // como documento nuevo en vez de romper.
            AbrirAsistenteIdentificacion(rutaArchivo);
            return;
        }

        var (campos, error) = GuardadoAutomaticoService.ExtraerCamposParaClasificar(rutaArchivo, coincidencia);
        if (campos is null)
        {
            System.Windows.MessageBox.Show(this,
                $"No se pudo volver a leer los datos de este documento ({error}). Probá abrirlo desde \"Administrar clasificaciones\" para revisar el patrón.",
                "Archivero", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var rutaDestinoConflicto = ClasificadorService.CalcularRutaDestino(rutaArchivo, coincidencia, campos.Fecha, campos.NombreExtraido);

        var resolver = new ResolverDuplicadoWindow(rutaArchivo, rutaDestinoConflicto) { Owner = this };
        resolver.ShowDialog();
    }

    private void AbrirConfirmacionDeNombre(string rutaArchivo)
    {
        if (!File.Exists(rutaArchivo))
        {
            return;
        }

        // Igual que duplicado y período nuevo: se recalcula en el momento, no se guarda en la base.
        var configuraciones = _configuraciones.ObtenerTodasConPatrones();
        var coincidencia = CoincidenciaAutomaticaService.BuscarConfiguracionQueCoincide(rutaArchivo, configuraciones);
        if (coincidencia is null)
        {
            AbrirAsistenteIdentificacion(rutaArchivo);
            return;
        }

        var (campos, error) = GuardadoAutomaticoService.ExtraerCamposParaClasificar(rutaArchivo, coincidencia with { Renombrar = false });
        if (campos is null)
        {
            System.Windows.MessageBox.Show(this,
                $"No se pudo volver a leer los datos de este documento ({error}). Revisar el patrón desde \"Administrar clasificaciones\".",
                "Archivero", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var ventana = new ConfirmarNombreWindow(rutaArchivo, coincidencia, campos) { Owner = this };
        ventana.ShowDialog();
    }

    private void AbrirCreacionDePeriodo(string rutaArchivo)
    {
        if (!File.Exists(rutaArchivo))
        {
            return;
        }

        var configuraciones = _configuraciones.ObtenerTodasConPatrones();
        var coincidencia = CoincidenciaAutomaticaService.BuscarConfiguracionQueCoincide(rutaArchivo, configuraciones);
        if (coincidencia is null)
        {
            AbrirAsistenteIdentificacion(rutaArchivo);
            return;
        }

        var (campos, error) = GuardadoAutomaticoService.ExtraerCamposParaClasificar(rutaArchivo, coincidencia);
        if (campos?.Fecha is null)
        {
            System.Windows.MessageBox.Show(this,
                $"No se pudo volver a leer la fecha de este documento ({error}). Probá abrirlo desde \"Administrar clasificaciones\" para revisar el patrón.",
                "Archivero", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var carpetaPeriodo = Path.GetDirectoryName(
            ClasificadorService.CalcularRutaDestino(rutaArchivo, coincidencia, campos.Fecha, campos.NombreExtraido))!;

        var ventana = new CrearPeriodoWindow(rutaArchivo, coincidencia, campos.Fecha.Value, campos.NombreExtraido, carpetaPeriodo) { Owner = this };
        ventana.ShowDialog();
    }

    private void BtnAdministrarClasificaciones_Click(object sender, RoutedEventArgs e)
    {
        var ventana = new AdministrarClasificacionesWindow { Owner = this };
        ventana.ShowDialog();

        if (ventana.HuboConfiguracionesEditadas)
        {
            _ = ReprocesarPendientesAsync();
        }
    }
}
