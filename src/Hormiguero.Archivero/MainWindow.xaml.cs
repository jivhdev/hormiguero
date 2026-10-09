using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Archivero.Datos;
using Archivero.Servicios;
using Archivero.Vistas;
using Hormiguero.Diseno;
using Hormiguero.Nucleo.Datos;

namespace Archivero;

/// <summary>Fila de "Guardados automáticamente" ya lista para mostrar (el repositorio no formatea texto de interfaz).</summary>
public record GuardadoRecienteFila(DateTime Hora, string RutaFinal)
{
    public string Resumen => $"{Hora:HH:mm:ss} — {Path.GetFileName(RutaFinal)} → {RutaFinal}";
}

/// <summary>Fila de "Pendientes de distribuir": un archivo dañado lleva el aviso a la vista, para no confundirlo con un PDF sin texto (Caso-11, punto 5).</summary>
public record PendienteDistribucionFila(ArchivoPendiente Pendiente)
{
    public string Texto =>
        Pendiente.Motivo == MotivoPendiente.ArchivoDanado
            ? $"⚠ {Pendiente.NombreArchivo} — no se pudo leer este archivo"
            : Pendiente.NombreArchivo;
}

/// <summary>Fila de "Pendientes por reconocer": un documento que solo espera el nombre lo dice, para no confundirlo con uno sin identificar (Caso-11, punto 1).</summary>
public record PendienteReconocerFila(ArchivoPendiente Pendiente)
{
    public string Texto =>
        Pendiente.Motivo switch
        {
            MotivoPendiente.NombrePorConfirmar =>
                $"{Pendiente.NombreArchivo} — falta confirmar el nombre",
            MotivoPendiente.Duplicado => $"{Pendiente.NombreArchivo} — Duplicado: revisar",
            _ => Pendiente.NombreArchivo,
        };
}

public sealed class DecisionPendienteFila : INotifyPropertyChanged
{
    private Queue<string> _datosPendientes = new();
    private DateTime? _fechaDato;

    public DecisionPendienteFila(DecisionPendienteCadena decision, DecisionModoPendiente? modo)
    {
        Decision = decision;
        Modo = modo;
    }

    public DecisionPendienteCadena Decision { get; }
    public DecisionModoPendiente? Modo { get; }
    public long VersionId => Decision.VersionId;
    public string Documento => Decision.Documento;
    public string TipoProveedor => $"{Decision.Tipo} · {Decision.Proveedor}";
    public string Etiqueta => Modo is null ? "Sin origen" : "Modo";
    public string Pregunta =>
        Modo is null
            ? string.Empty
            : $"{Decision.Documento} · {Decision.Proveedor} · ¿Qué modo tiene este proceso?";
    public IReadOnlyList<string> OpcionesModo => Modo?.Opciones ?? [];
    public bool EsModo => Modo is not null;
    public bool MostrarAccionesOrigen => Modo is null;
    public bool MostrarOpcionesModo => EsModo && !ModoElegido;
    public bool ModoElegido { get; private set; }
    public string? DatoPendiente { get; private set; }
    public bool MostrarCapturaDato => ModoElegido && DatoPendiente is not null;
    public DateTime? FechaDato
    {
        get => _fechaDato;
        set
        {
            if (_fechaDato == value)
                return;
            _fechaDato = value;
            PropertyChanged?.Invoke(this, new(nameof(FechaDato)));
        }
    }
    public string Explicacion =>
        Modo is null
            ? $"Pertenece al esquema de {Decision.Proveedor} pero no se encontró su documento de origen (menciona {Decision.DatoMencionado} {Decision.NumeroMencionado})."
            : string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void MarcarModoElegido(IEnumerable<string> datosPendientes)
    {
        ModoElegido = true;
        _datosPendientes = new Queue<string>(datosPendientes);
        DatoPendiente = _datosPendientes.TryDequeue(out var dato) ? dato : null;
        PropertyChanged?.Invoke(this, new(nameof(ModoElegido)));
        PropertyChanged?.Invoke(this, new(nameof(DatoPendiente)));
        PropertyChanged?.Invoke(this, new(nameof(MostrarCapturaDato)));
        PropertyChanged?.Invoke(this, new(nameof(MostrarOpcionesModo)));
    }

    public void AvanzarDatoPendiente()
    {
        DatoPendiente = _datosPendientes.TryDequeue(out var dato) ? dato : null;
        FechaDato = null;
        PropertyChanged?.Invoke(this, new(nameof(DatoPendiente)));
        PropertyChanged?.Invoke(this, new(nameof(MostrarCapturaDato)));
    }
}

public partial class MainWindow : Window
{
    private readonly PendienteRepository _pendientes = new();
    private readonly ConfiguracionDocumentoRepository _configuraciones = new();
    private readonly ConfiguracionRepository _configuracion = new();
    private readonly CarpetaObservadaService _servicioCarpeta;
    private readonly GuardadoRecienteRepository _guardadosRecientes = new();
    private readonly CarpetasObservadasRepository _carpetasObservadas = new();
    private readonly ObservadorCarpetasService _observador;
    private VigilanciaCarpetaService _vigilancia;
    private string _carpetaObservada;
    private readonly TrayIconService _bandeja = new();
    private bool _permitirCierre;
    private bool _inicializandoTema = true;

    public MainWindow(string carpetaObservada, VigilanciaCarpetaService vigilancia)
    {
        InitializeComponent();
        _observador = new ObservadorCarpetasService(
            _carpetasObservadas,
            new ImpresionAlArchivarService(new AccionImpresionWindows())
        );
        _observador.DocumentoActualizado += documento => Dispatcher.Invoke(CargarObservados);
        _observador.ProgresoActualizado += (nombre, procesados, total) =>
            Dispatcher.Invoke(() =>
            {
                TxtProgresoObservador.Text =
                    procesados < total
                        ? $"Poniéndose al día: {procesados} de {total} ({nombre})"
                        : "Al día";
            });
        _observador.DocumentoRequiereAtencion += _ => Dispatcher.Invoke(CargarPorAtender);
        _observador.ErrorVisible += mensaje =>
            Dispatcher.BeginInvoke(() =>
                System.Windows.MessageBox.Show(
                    this,
                    mensaje,
                    "Carpetas observadas",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                )
            );
        ComboTema.SelectedIndex = IndiceTema(DatosDeApp.LeerPreferencia("tema.archivero"));
        _inicializandoTema = false;
        PublicadorDatosDocumentoService.RevisionEnlacesFallida += (_, mensaje) =>
            Dispatcher.Invoke(() =>
                System.Windows.MessageBox.Show(
                    this,
                    mensaje,
                    "Archivero",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                )
            );
        _servicioCarpeta = new CarpetaObservadaService(_configuracion);
        _carpetaObservada = carpetaObservada;
        TxtCarpetaObservada.Text = $"Carpeta observada: {carpetaObservada}";

        _vigilancia = vigilancia;
        SuscribirEventosVigilancia();

        _bandeja.MostrarVentanaSolicitado += () => Dispatcher.Invoke(RestaurarVentana);
        _bandeja.AtencionSolicitada += () =>
            Dispatcher.Invoke(() =>
            {
                RestaurarVentana();
                ListaPorAtender.Focus();
            });
        _bandeja.SalirSolicitado += () => Dispatcher.Invoke(SalirDeVerdad);

        CargarPendientes();
        CargarDecisionesPendientes();
        CargarGuardadosRecientes();
        CargarObservados();
        CargarPorAtender();
        CargarTiempoAhorrado();
        _observador.Iniciar();
    }

    private static int IndiceTema(string? preferencia) =>
        Enum.TryParse<ModoTema>(preferencia, out var modo) ? (int)modo : (int)ModoTema.Sistema;

    private void ComboTema_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e
    )
    {
        if (_inicializandoTema || ComboTema.SelectedIndex < 0)
        {
            return;
        }

        var modo = (ModoTema)ComboTema.SelectedIndex;
        DatosDeApp.GuardarPreferencia("tema.archivero", modo.ToString());
        Tema.Aplicar(System.Windows.Application.Current, modo);
    }

    private void SuscribirEventosVigilancia()
    {
        _vigilancia.ArchivoPendienteDetectado += _ => Dispatcher.Invoke(CargarPendientes);
        _vigilancia.ArchivoPendienteEliminado += _ => Dispatcher.Invoke(CargarPendientes);
        _vigilancia.ArchivoRequiereAtencion += (ruta, detalle) =>
            Dispatcher.Invoke(() =>
            {
                CargarPendientes();
                System.Windows.MessageBox.Show(
                    this,
                    $"No se pudo completar el procesamiento de {Path.GetFileName(ruta)}.\n\n{detalle}",
                    "Archivero",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            });
        _vigilancia.ArchivoGuardadoAutomaticamente += (_, rutaFinal) =>
            Dispatcher.Invoke(() => AgregarAGuardadosRecientes(rutaFinal));
        _vigilancia.CarpetaObservadaNoDisponible += () =>
            Dispatcher.Invoke(AvisarCarpetaNoDisponible);
    }

    private void BtnCambiarCarpetaObservada_Click(object sender, RoutedEventArgs e)
    {
        var ventana = new CambiarCarpetaObservadaWindow(_servicioCarpeta, _carpetaObservada)
        {
            Owner = this,
        };
        if (ventana.ShowDialog() != true || ventana.CarpetaNueva is null)
        {
            return;
        }

        AuditoriaService.Registrar(
            "CARPETA_OBSERVADA_CAMBIADA",
            $"Anterior={_carpetaObservada}; Nueva={ventana.CarpetaNueva}"
        );

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
        ListaPendientes.ItemsSource = pendientes
            .Where(p => !p.Motivo.EsPendienteDeDistribuir())
            .Select(p => new PendienteReconocerFila(p))
            .ToList();
        ListaPendientesDistribucion.ItemsSource = pendientes
            .Where(p => p.Motivo.EsPendienteDeDistribuir())
            .Select(p => new PendienteDistribucionFila(p))
            .ToList();

        _bandeja.ActualizarPendientes(pendientes.Count > 0);
    }

    private void CargarDecisionesPendientes()
    {
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        var decisiones = new RepositorioCadenas(conexion).ListarDecisionesPendientes();
        var modos = new RepositorioModosEsquema(conexion)
            .ListarPendientes()
            .ToDictionary(m => m.VersionId);
        ListaDecisionesPendientes.ItemsSource = decisiones
            .Select(d => new DecisionPendienteFila(d, modos.GetValueOrDefault(d.VersionId)))
            .ToList();
        TxtDecisionesPendientes.Text = $"Decisiones pendientes ({decisiones.Count})";
        // Solo ocupa espacio cuando hay algo que decidir: "Pendientes por reconocer" es lo principal.
        SeccionDecisionesPendientes.Visibility =
            decisiones.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static DecisionPendienteFila? FilaDecision(object sender) =>
        (sender as FrameworkElement)?.DataContext as DecisionPendienteFila;

    private void BtnFijarModo_Click(object sender, RoutedEventArgs e)
    {
        var fila = FilaDecision(sender);
        if (fila?.Modo is not { } decisionModo || sender is not FrameworkElement elemento)
            return;
        string nombreModo = elemento.Tag as string ?? string.Empty;
        try
        {
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            var esquema =
                new RepositorioEsquemas(conexion).ObtenerPorProveedor(decisionModo.Proveedor)
                ?? throw new InvalidOperationException("No se encontró el esquema del proveedor.");
            var modo =
                esquema.Modos.SingleOrDefault(m => m.Nombre == nombreModo)
                ?? throw new InvalidOperationException("El modo ya no está disponible.");
            new RepositorioModosEsquema(conexion).FijarModo(decisionModo.CadenaId, modo.Id);
            new EvaluadorAlertas(conexion).Evaluar();
            var datos = ObtenerDatosPendientes(conexion, esquema, decisionModo.CadenaId, modo.Id);
            fila.MarcarModoElegido(datos);
            if (datos.Count == 0)
                CargarDecisionesPendientes();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                "Decisiones pendientes",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private static IReadOnlyList<string> ObtenerDatosPendientes(
        Microsoft.Data.Sqlite.SqliteConnection conexion,
        EsquemaCadena esquema,
        long cadenaId,
        long modoId
    )
    {
        var ingresados = new RepositorioDatosCadena(conexion)
            .Listar(cadenaId)
            .Select(d => d.Nombre)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var datos = new List<string>();
        foreach (
            var regla in esquema
                .ReglasAlerta.Where(r =>
                    r.Tipo == "falta_dato" && (r.ModoId == modoId || r.ModoId is null)
                )
                .OrderBy(r => r.ModoId is null)
        )
        {
            var parametros = JsonSerializer.Deserialize<ParametrosFaltaDato>(regla.ParametrosJson);
            if (
                !string.IsNullOrWhiteSpace(parametros?.Dato)
                && !ingresados.Contains(parametros.Dato)
                && !datos.Contains(parametros.Dato, StringComparer.OrdinalIgnoreCase)
            )
                datos.Add(parametros.Dato);
        }
        return datos;
    }

    private void BtnGuardarDatoCadena_Click(object sender, RoutedEventArgs e)
    {
        var fila = FilaDecision(sender);
        if (
            fila?.Modo is not { } modo
            || fila.DatoPendiente is not { } nombre
            || fila.FechaDato is not { } fecha
        )
            return;
        try
        {
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            new RepositorioDatosCadena(conexion).GuardarFecha(
                modo.CadenaId,
                nombre,
                DateOnly.FromDateTime(fecha)
            );
            fila.AvanzarDatoPendiente();
            if (!fila.MostrarCapturaDato)
                CargarDecisionesPendientes();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                "Decisiones pendientes",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void BtnBuscarOrigen_Click(object sender, RoutedEventArgs e)
    {
        var fila = FilaDecision(sender);
        if (fila is null || string.IsNullOrWhiteSpace(fila.Decision.NumeroMencionado))
            return;
        string numero = fila.Decision.NumeroMencionado;
        string buscadero = Path.Combine(AppContext.BaseDirectory, "Buscadero.exe");
        try
        {
            if (File.Exists(buscadero))
            {
                var inicio = new System.Diagnostics.ProcessStartInfo(buscadero)
                {
                    UseShellExecute = true,
                };
                inicio.ArgumentList.Add(numero);
                System.Diagnostics.Process.Start(inicio);
                return;
            }
        }
        catch
        {
            // Si Buscadero no puede abrirse, se facilita la búsqueda manual con el mismo número.
        }

        try
        {
            System.Windows.Clipboard.SetText(numero);
            System.Windows.MessageBox.Show(
                this,
                $"No se pudo abrir Buscadero. Se copió {fila.Decision.DatoMencionado} {numero}; búscalo en Buscadero. La decisión seguirá pendiente hasta que se resuelva.",
                "Decisiones pendientes",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                $"No se pudo abrir Buscadero ni copiar el número {numero}: {ex.Message}",
                "Decisiones pendientes",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void BtnCrearCadenaIgual_Click(object sender, RoutedEventArgs e)
    {
        var fila = FilaDecision(sender);
        if (fila is null)
            return;
        try
        {
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            var resultado = new MotorCadenas(conexion).CrearCadenaIgual(fila.VersionId);
            if (resultado.Estado != "cadena_creada")
                throw new InvalidOperationException(
                    resultado.Motivo ?? "No se pudo crear la cadena."
                );
            CargarDecisionesPendientes();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                "Decisiones pendientes",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void BtnArchivarSinCadena_Click(object sender, RoutedEventArgs e)
    {
        var fila = FilaDecision(sender);
        if (fila is null)
            return;
        try
        {
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            new MotorCadenas(conexion).ArchivarSinCadena(fila.VersionId);
            CargarDecisionesPendientes();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                "Decisiones pendientes",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void AgregarAGuardadosRecientes(string rutaFinal)
    {
        _guardadosRecientes.Agregar(rutaFinal);
        // Caso-8: el contador de tiempo ahorrado se lleva aparte de GuardadosRecientes (que se
        // recorta a los últimos 20 -- Caso-6, punto 2), justo aquí, en el único punto real donde
        // un documento se archivó solo (REQ-002; nunca el flujo manual de PDFs sin texto).
        TiempoAhorradoService.RegistrarDocumentoArchivado(_configuracion);
        CargarGuardadosRecientes();
        CargarTiempoAhorrado();
        _ = Task.Run(async () =>
        {
            try
            {
                await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);
                Dispatcher.Invoke(CargarDecisionesPendientes);
            }
            catch
            {
                // La lista se volverá a cargar al abrir Archivero o resolver otra decisión.
            }
        });
    }

    private void CargarGuardadosRecientes()
    {
        ListaGuardados.ItemsSource = _guardadosRecientes
            .ObtenerTodos()
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

    private void BtnReprocesarPendientes_Click(object sender, RoutedEventArgs e) =>
        _ = ReprocesarPendientesAsync();

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
            AuditoriaService.Registrar("REPROCESO_PENDIENTES_FALLIDO", ex.ToString());
            System.Windows.MessageBox.Show(
                this,
                $"No se pudieron reprocesar los pendientes: {ex.Message}",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
        finally
        {
            BtnReprocesarPendientes.IsEnabled = true;
            CargarPendientes();
            CargarDecisionesPendientes();
        }
    }

    private void AvisarCarpetaNoDisponible()
    {
        System.Windows.MessageBox.Show(
            this,
            $"La carpeta observada ya no está disponible (se movió o se borró):\n{_carpetaObservada}\n\n"
                + "Archivero no puede seguir vigilándola hasta que vuelva a estar accesible. Puedes recrearla con ese mismo nombre y ruta, "
                + "o usar el botón \"Cambiar…\" para elegir otra.",
            "Archivero",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );
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
        _observador.Dispose();
        base.OnClosing(e);
        System.Windows.Application.Current.Shutdown();
    }

    private void ListaPendientes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ListaPendientes.SelectedItem is not PendienteReconocerFila { Pendiente: var pendiente })
        {
            return;
        }

        try
        {
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
            else if (
                pendiente.Motivo
                is MotivoPendiente.TextoConCaracteresInvalidos
                    or MotivoPendiente.NombreReservadoPorWindows
                    or MotivoPendiente.RutaFueraDeCarpetaConfigurada
                    or MotivoPendiente.NombreORutaDemasiadoLarga
            )
            {
                // El mismo dato problemático persistirá al reabrir el asistente; se informa para revisión manual.
                System.Windows.MessageBox.Show(
                    this,
                    $"Este documento no se puede guardar automáticamente:\n\n{pendiente.Motivo.DescripcionLegible()}\n\n"
                        + "Revísalo a mano; si corresponde, muévelo tú mismo a su carpeta.",
                    "Archivero",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            else
            {
                AbrirAsistenteIdentificacion(pendiente.RutaArchivo);
            }

            CargarPendientes();
        }
        catch (Exception ex)
        {
            AuditoriaService.Registrar("ABRIR_PENDIENTE_FALLIDO", $"{pendiente.RutaArchivo}: {ex}");
            System.Windows.MessageBox.Show(
                this,
                $"No se pudo abrir el documento pendiente: {ex.Message}",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void BtnRecargarPendientesDistribucion_Click(object sender, RoutedEventArgs e) =>
        CargarPendientes();

    private void ListaPendientesDistribucion_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (
            ListaPendientesDistribucion.SelectedItem
            is not PendienteDistribucionFila { Pendiente: var pendiente }
        )
        {
            return;
        }

        var identificarSinTexto = new IdentificarSinTextoWindow(
            pendiente.RutaArchivo,
            pendiente.Motivo == MotivoPendiente.ArchivoDanado
        )
        {
            Owner = this,
        };
        identificarSinTexto.ShowDialog();

        CargarPendientes();
    }

    private void AbrirResolucionDeDuplicado(string rutaArchivo)
    {
        if (!File.Exists(rutaArchivo))
        {
            AuditoriaService.Registrar("DUPLICADO_PENDIENTE_NO_ENCONTRADO", rutaArchivo);
            System.Windows.MessageBox.Show(
                this,
                "El documento pendiente ya no está en esa ubicación. Actualiza la lista e inténtalo de nuevo.",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        try
        {
            // Se recalcula todo en el momento (coincidencia + campos extraidos) en vez de guardarlo
            // en la base: es el mismo documento y la misma configuracion, asi que da lo mismo, y
            // evita duplicar el estado en Pendientes.
            var configuraciones = _configuraciones.ObtenerTodasConPatrones();
            var coincidencia = CoincidenciaAutomaticaService.BuscarConfiguracionQueCoincide(
                rutaArchivo,
                configuraciones
            );
            if (coincidencia is null)
            {
                // Ya no coincide con ninguna configuracion (por ejemplo, se borro) -> tratarlo
                // como documento nuevo en vez de romper.
                AbrirAsistenteIdentificacion(rutaArchivo);
                return;
            }

            var (campos, error) = GuardadoAutomaticoService.ExtraerCamposParaClasificar(
                rutaArchivo,
                coincidencia
            );
            if (campos is null)
            {
                System.Windows.MessageBox.Show(
                    this,
                    $"No se pudo volver a leer los datos de este documento ({error}). Prueba abrirlo desde \"Administrar clasificaciones\" para revisar el patrón.",
                    "Archivero",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            var rutaDestinoConflicto = ClasificadorService.CalcularRutaDestino(
                rutaArchivo,
                coincidencia,
                campos.Fecha,
                campos.NombreExtraido
            );

            var resolver = new ResolverDuplicadoWindow(rutaArchivo, rutaDestinoConflicto)
            {
                Owner = this,
            };
            resolver.ShowDialog();
        }
        catch (Exception ex)
        {
            AuditoriaService.Registrar(
                "ABRIR_RESOLUCION_DUPLICADO_FALLIDO",
                $"{rutaArchivo}: {ex}"
            );
            System.Windows.MessageBox.Show(
                this,
                $"No se pudo abrir la resolución del duplicado: {ex.Message}",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void AbrirConfirmacionDeNombre(string rutaArchivo)
    {
        if (!File.Exists(rutaArchivo))
        {
            return;
        }

        // Igual que duplicado y período nuevo: se recalcula en el momento, no se guarda en la base.
        var configuraciones = _configuraciones.ObtenerTodasConPatrones();
        var coincidencia = CoincidenciaAutomaticaService.BuscarConfiguracionQueCoincide(
            rutaArchivo,
            configuraciones
        );
        if (coincidencia is null)
        {
            AbrirAsistenteIdentificacion(rutaArchivo);
            return;
        }

        var (campos, error) = GuardadoAutomaticoService.ExtraerCamposParaClasificar(
            rutaArchivo,
            coincidencia with
            {
                Renombrar = false,
            }
        );
        if (campos is null)
        {
            System.Windows.MessageBox.Show(
                this,
                $"No se pudo volver a leer los datos de este documento ({error}). Revisar el patrón desde \"Administrar clasificaciones\".",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
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
        var coincidencia = CoincidenciaAutomaticaService.BuscarConfiguracionQueCoincide(
            rutaArchivo,
            configuraciones
        );
        if (coincidencia is null)
        {
            AbrirAsistenteIdentificacion(rutaArchivo);
            return;
        }

        var (campos, error) = GuardadoAutomaticoService.ExtraerCamposParaClasificar(
            rutaArchivo,
            coincidencia
        );
        if (campos?.Fecha is null)
        {
            System.Windows.MessageBox.Show(
                this,
                $"No se pudo volver a leer la fecha de este documento ({error}). Prueba abrirlo desde \"Administrar clasificaciones\" para revisar el patrón.",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        var carpetaPeriodo = Path.GetDirectoryName(
            ClasificadorService.CalcularRutaDestino(
                rutaArchivo,
                coincidencia,
                campos.Fecha,
                campos.NombreExtraido
            )
        )!;

        var ventana = new CrearPeriodoWindow(
            rutaArchivo,
            coincidencia,
            campos.Fecha.Value,
            campos.NombreExtraido,
            carpetaPeriodo
        )
        {
            Owner = this,
        };
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

    private void CargarObservados() =>
        ListaObservados.ItemsSource = _carpetasObservadas.LeerActividad();

    private void CargarPorAtender()
    {
        var documentos = _carpetasObservadas.LeerPorAtender();
        ListaPorAtender.ItemsSource = documentos;
        TxtPorAtender.Text = $"Documentos pendientes de ingresar ({documentos.Count})";
        BtnMarcarAtendido.IsEnabled = ListaPorAtender.SelectedItem is DocumentoPorAtender;
    }

    private void BtnMarcarAtendido_Click(object sender, RoutedEventArgs e)
    {
        if (ListaPorAtender.SelectedItem is DocumentoPorAtender documento)
            _carpetasObservadas.MarcarListo(documento.Id);
        CargarPorAtender();
    }

    private void ListaPorAtender_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e
    ) => BtnMarcarAtendido.IsEnabled = ListaPorAtender.SelectedItem is DocumentoPorAtender;

    private void ListaPorAtender_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ListaPorAtender.SelectedItem is not DocumentoPorAtender documento)
            return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(documento.Ruta) { UseShellExecute = true }
            );
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                $"No se pudo abrir el PDF: {ex.Message}",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void BtnCarpetasObservadas_Click(object sender, RoutedEventArgs e)
    {
        var ventana = new AdministrarCarpetasObservadasWindow { Owner = this };
        if (ventana.ShowDialog() == true)
        {
            _observador.ActualizarCarpetas();
            CargarObservados();
        }
    }

    private async void BtnRevisarObservadas_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _observador.RevisarAhoraAsync();
            CargarObservados();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                $"No se pudieron revisar las carpetas observadas: {ex.Message}",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void ListaObservados_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (
            ListaObservados.SelectedItem is not DocumentoObservadoReciente documento
            || documento.Motivo?.Contains("Buscar original", StringComparison.OrdinalIgnoreCase)
                != true
        )
            return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Path.GetDirectoryName(documento.Ruta)!,
                    UseShellExecute = true,
                }
            );
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                $"No se pudo abrir la carpeta del cedible: {ex.Message}",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }
}
