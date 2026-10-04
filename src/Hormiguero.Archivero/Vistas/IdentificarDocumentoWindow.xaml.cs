using System.Windows;
using System.Windows.Controls;
using Archivero.Datos;
using Archivero.Servicios;
using Archivero.Servicios.Pdf;
using Button = System.Windows.Controls.Button;
using RadioButton = System.Windows.Controls.RadioButton;

namespace Archivero.Vistas;

public partial class IdentificarDocumentoWindow : Window
{
    private enum Paso { EmisorTipo, Carpeta, Organizacion, NombreArchivo, Confirmar }

    private readonly string _rutaArchivo;
    private readonly EntidadRepository _entidades = new();
    private readonly ConfiguracionDocumentoRepository _configuraciones = new();
    private readonly PendienteRepository _pendientes = new();
    private readonly BorradorRepository _borradores = new();
    private readonly Dictionary<CampoMarca, Marca> _marcas = new();
    private readonly Stack<Paso> _pasosRecorridos = new();

    private Paso _paso;
    private Paso _pasoInicial = Paso.EmisorTipo;
    private CampoMarca? _campoActivoParaMarcar;
    private bool _draftYaResuelto;

    private string _emisor = string.Empty;
    private string _tipo = string.Empty;
    private string _carpetaDestino = string.Empty;
    private FormatoCarpeta _formato;
    private string? _patronCarpeta;
    private bool _renombrar;
    private bool _preguntarNombre;
    private bool _abrirDespuesDeGuardar;
    private ConfiguracionDocumento? _configuracionExistente;
    private readonly (ConfiguracionDocumento Configuracion, int PatronId)? _edicion;

    // Precarga para ControlOrganizacion (edicion o borrador restaurado): se aplica una sola vez,
    // la primera vez que se llega al Paso.Organizacion (ver PrepararVistaOrganizacion).
    private bool _organizacionInicializada;
    private FormatoCarpeta? _tipoOrganizacionPrecargado;
    private string? _patronPrecargadoParaControl;

    public IdentificarDocumentoWindow(string rutaArchivo)
    {
        InitializeComponent();
        _rutaArchivo = rutaArchivo;
        ControlOrganizacion.ConfigurarProveedorDeFecha(LeerFechaPreview);

        Visor.CargarPdf(rutaArchivo);
        Visor.MarcaRealizada += Visor_MarcaRealizada;

        CmbEmisor.ItemsSource = _entidades.Buscar(CategoriaEntidad.Emisor, string.Empty);
        CmbTipo.ItemsSource = _entidades.Buscar(CategoriaEntidad.Tipo, string.Empty);

        var borrador = _borradores.Obtener(_rutaArchivo);
        if (borrador is not null)
        {
            AplicarBorrador(borrador);
        }

        ActualizarEstadosDeMarca();
        ActualizarMarcasEnVisor();
        MostrarPaso(Paso.EmisorTipo);
        Closing += IdentificarDocumentoWindow_Closing;

        if (borrador is not null)
        {
            System.Windows.MessageBox.Show(
                this, "Se restauró el progreso que habías dejado sin terminar para este documento.",
                "Archivero", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// Modo edicion (REQ-004): revisa/corrige un patron de reconocimiento ya existente usando
    /// un PDF de ejemplo. No mueve ni renombra ese archivo — solo actualiza las marcas y la
    /// configuracion (carpeta/formato/nombre) en la base.
    ///
    /// <paramref name="comenzarEnPasoCarpeta"/> es para el caso de Caso-1, punto 4: al editar
    /// desde "Guardados automáticamente", ya se conoce exactamente el Emisor/Tipo y se tiene el
    /// documento real (no un ejemplo cualquiera) -- el asistente arranca directo en el paso de
    /// elegir carpeta, sin pasar por un primer paso que no tiene nada para decidir.
    /// </summary>
    public IdentificarDocumentoWindow(string rutaArchivo, ConfiguracionDocumento configuracion, PatronReconocimiento patron, bool comenzarEnPasoCarpeta = false)
    {
        InitializeComponent();
        _rutaArchivo = rutaArchivo;
        _edicion = (configuracion, patron.Id);
        BtnPosponer.Visibility = Visibility.Collapsed;
        ControlOrganizacion.ConfigurarProveedorDeFecha(LeerFechaPreview);

        Visor.CargarPdf(rutaArchivo);
        Visor.MarcaRealizada += Visor_MarcaRealizada;

        PrecargarParaEdicion(configuracion, patron);

        ActualizarEstadosDeMarca();
        ActualizarMarcasEnVisor();
        _pasoInicial = comenzarEnPasoCarpeta ? Paso.Carpeta : Paso.EmisorTipo;
        MostrarPaso(_pasoInicial);
    }

    private void PrecargarParaEdicion(ConfiguracionDocumento configuracion, PatronReconocimiento patron)
    {
        _emisor = configuracion.Emisor;
        _tipo = configuracion.Tipo;
        _carpetaDestino = configuracion.CarpetaDestino;
        _formato = configuracion.FormatoCarpeta;
        _patronCarpeta = configuracion.PatronCarpeta;
        _renombrar = configuracion.Renombrar;
        _preguntarNombre = configuracion.PreguntarNombre;
        _abrirDespuesDeGuardar = configuracion.AbrirDespuesDeGuardar;

        CmbEmisor.Text = _emisor;
        CmbTipo.Text = _tipo;
        CmbEmisor.IsEnabled = false;
        CmbTipo.IsEnabled = false;

        TxtCarpetaDestino.Text = _carpetaDestino;

        RbGuardarDirecto.IsChecked = _formato == FormatoCarpeta.Directo;
        RbGuardarSubcarpetas.IsChecked = _formato != FormatoCarpeta.Directo;
        _tipoOrganizacionPrecargado = _formato == FormatoCarpeta.Directo ? null : _formato;
        _patronPrecargadoParaControl = _patronCarpeta;

        MarcarOpcionNombre();
        ChkAbrirDespuesDeGuardar.IsChecked = _abrirDespuesDeGuardar;

        foreach (var marca in patron.Marcas)
        {
            _marcas[marca.Campo] = marca;
        }
    }

    private void AplicarBorrador(BorradorAsistente borrador)
    {
        CmbEmisor.Text = borrador.Emisor;
        CmbTipo.Text = borrador.Tipo;
        TxtCarpetaDestino.Text = borrador.CarpetaDestino;

        if (borrador.Formato is not null && Enum.TryParse<FormatoCarpeta>(borrador.Formato, out var formato))
        {
            if (formato == FormatoCarpeta.Directo)
            {
                RbGuardarDirecto.IsChecked = true;
            }
            else
            {
                RbGuardarSubcarpetas.IsChecked = true;
                _tipoOrganizacionPrecargado = formato;
            }
        }
        else if (borrador.GuardaEnSubcarpetas == true)
        {
            RbGuardarSubcarpetas.IsChecked = true;
        }

        if (borrador.PatronCarpeta is not null)
        {
            _patronPrecargadoParaControl = borrador.PatronCarpeta;
        }

        if (borrador.Renombrar is not null)
        {
            RbMantenerNombre.IsChecked = borrador.Renombrar == false;
            RbExtraerNombre.IsChecked = borrador.Renombrar == true;
        }

        if (borrador.PreguntarNombre == true)
        {
            RbPreguntarNombre.IsChecked = true;
        }

        foreach (var marca in borrador.Marcas)
        {
            _marcas[marca.Campo] = marca;
        }
    }

    private void GuardarBorradorActual()
    {
        if (_edicion is not null)
        {
            return;
        }

        var formatoElegido = RbGuardarDirecto.IsChecked == true ? FormatoCarpeta.Directo : ControlOrganizacion.FormatoElegido;

        var renombrarElegido = RbMantenerNombre.IsChecked == true || RbPreguntarNombre.IsChecked == true ? false
            : RbExtraerNombre.IsChecked == true ? true
            : (bool?)null;

        var borrador = new BorradorAsistente
        {
            Emisor = CmbEmisor.Text.Trim(),
            Tipo = CmbTipo.Text.Trim(),
            CarpetaDestino = TxtCarpetaDestino.Text,
            Formato = formatoElegido?.ToString(),
            GuardaEnSubcarpetas = RbGuardarSubcarpetas.IsChecked == true,
            PatronCarpeta = LeerPatronElegido(),
            Renombrar = renombrarElegido,
            PreguntarNombre = RbPreguntarNombre.IsChecked == true,
            Marcas = _marcas.Values.ToList()
        };

        var hayAlgoQueGuardar = !string.IsNullOrWhiteSpace(borrador.Emisor)
            || !string.IsNullOrWhiteSpace(borrador.Tipo)
            || borrador.Marcas.Count > 0
            || !string.IsNullOrWhiteSpace(borrador.CarpetaDestino);

        if (hayAlgoQueGuardar)
        {
            _borradores.Guardar(_rutaArchivo, borrador);
        }
    }

    private void IdentificarDocumentoWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Si se cierra la ventana sin pasar por "Guardar y clasificar", "Posponer" o
        // "Cancelar" (por ejemplo, con la X), se trata igual que Posponer: no se pierde
        // nada de lo ya tipeado/marcado.
        if (!_draftYaResuelto)
        {
            GuardarBorradorActual();
        }
    }

    /// <summary>Avanza al próximo paso dejándolo anotado para que "Atrás" vuelva exactamente al paso anterior visible.</summary>
    private void IrA(Paso nuevoPaso)
    {
        if (nuevoPaso != _paso)
        {
            _pasosRecorridos.Push(_paso);
        }

        MostrarPaso(nuevoPaso);
    }

    private void MostrarPaso(Paso nuevoPaso)
    {
        _paso = nuevoPaso;

        PanelEmisorTipo.Visibility = nuevoPaso == Paso.EmisorTipo ? Visibility.Visible : Visibility.Collapsed;
        PanelCarpeta.Visibility = nuevoPaso == Paso.Carpeta ? Visibility.Visible : Visibility.Collapsed;
        PanelOrganizacion.Visibility = nuevoPaso == Paso.Organizacion ? Visibility.Visible : Visibility.Collapsed;
        PanelNombreArchivo.Visibility = nuevoPaso == Paso.NombreArchivo ? Visibility.Visible : Visibility.Collapsed;
        PanelConfirmar.Visibility = nuevoPaso == Paso.Confirmar ? Visibility.Visible : Visibility.Collapsed;

        if (nuevoPaso == Paso.Organizacion)
        {
            PrepararVistaOrganizacion();
        }

        // Preview obligatorio de anterior/actual/futuro (Caso-1, punto 3): visible desde que hay
        // carpeta+organización elegidas hasta confirmar, para que sea imposible llegar a "Guardar y
        // clasificar" sin haberlo visto.
        var mostrarPreview = nuevoPaso is Paso.Organizacion or Paso.NombreArchivo or Paso.Confirmar;
        PanelPreview.Visibility = mostrarPreview ? Visibility.Visible : Visibility.Collapsed;
        if (mostrarPreview)
        {
            ActualizarPreview();
        }

        TxtError.Visibility = Visibility.Collapsed;

        var vinculando = _configuracionExistente is not null;

        (TxtTituloPaso.Text, TxtInstruccionPaso.Text) = nuevoPaso switch
        {
            Paso.EmisorTipo => ("Paso 1 de 5 — Emisor y Tipo",
                "Marcar sobre el PDF dónde aparecen el Emisor y el Tipo de documento, y escribirlos (o elegir uno ya conocido)."),
            Paso.Carpeta => ("Paso 2 de 5 — Carpeta madre",
                vinculando
                    ? "Esta carpeta ya está definida por la configuración existente a la que se va a vincular este documento."
                    : "Elegir la carpeta raíz donde va a vivir todo lo de este tipo de documento, y cómo se va a guardar dentro de ella."),
            Paso.Organizacion => ("Paso 3 de 5 — Organización de las subcarpetas",
                vinculando
                    ? "El tipo de organización ya está definido por la configuración existente. Si corresponde, marcar la fecha en este documento."
                    : "Elegir el tipo de organización y cuál de los ejemplos se parece más a las carpetas que ya usás."),
            Paso.NombreArchivo => ("Paso 4 de 5 — Nombre de archivo",
                vinculando
                    ? "La regla de nombre ya está definida por la configuración existente. Si corresponde, marcar el campo en este documento."
                    : "Elegir cómo se va a llamar el archivo guardado."),
            Paso.Confirmar => ("Paso 5 de 5 — Confirmar", "Revisar los datos antes de guardar."),
            _ => (string.Empty, string.Empty)
        };

        BtnSiguiente.Content = nuevoPaso == Paso.Confirmar
            ? (_edicion is not null ? "Guardar cambios" : "Guardar y clasificar")
            : "Siguiente";

        // Se puede retroceder un paso, pero nunca saltar hacia adelante -- sigue siendo
        // estrictamente paso a paso.
        BtnAtras.IsEnabled = _pasosRecorridos.Count > 0;
    }

    private void BtnAtras_Click(object sender, RoutedEventArgs e)
    {
        if (_pasosRecorridos.Count > 0)
        {
            MostrarPaso(_pasosRecorridos.Pop());
        }
    }

    private void BtnMarcarEmisor_Click(object sender, RoutedEventArgs e) => ArmarMarca(CampoMarca.Emisor);

    private void BtnMarcarTipo_Click(object sender, RoutedEventArgs e) => ArmarMarca(CampoMarca.Tipo);

    private void BtnMarcarFecha_Click(object sender, RoutedEventArgs e) => ArmarMarca(CampoMarca.Fecha);

    private void BtnMarcarNombreArchivo_Click(object sender, RoutedEventArgs e) => ArmarMarca(CampoMarca.NombreArchivo);

    private void ArmarMarca(CampoMarca campo)
    {
        _campoActivoParaMarcar = campo;
        TxtInstruccionPaso.Text = $"Dibujar un rectángulo sobre el PDF donde aparece: {NombreCampo(campo)}.";
        ResaltarBotonActivo(campo);
    }

    private void ResaltarBotonActivo(CampoMarca? campoActivo)
    {
        var botones = new[] { BtnMarcarEmisor, BtnMarcarTipo, BtnMarcarFecha, BtnMarcarNombreArchivo };
        var camposEnOrden = new[] { CampoMarca.Emisor, CampoMarca.Tipo, CampoMarca.Fecha, CampoMarca.NombreArchivo };

        for (var i = 0; i < botones.Length; i++)
        {
            if (camposEnOrden[i] == campoActivo)
            {
                botones[i].Background = System.Windows.Media.Brushes.LightGoldenrodYellow;
                botones[i].FontWeight = FontWeights.Bold;
                botones[i].BorderBrush = System.Windows.Media.Brushes.DarkOrange;
                botones[i].BorderThickness = new Thickness(2);
            }
            else
            {
                botones[i].ClearValue(BackgroundProperty);
                botones[i].ClearValue(FontWeightProperty);
                botones[i].ClearValue(BorderBrushProperty);
                botones[i].ClearValue(BorderThicknessProperty);
            }
        }
    }

    private static string NombreCampo(CampoMarca campo) => campo switch
    {
        CampoMarca.Emisor => "Emisor",
        CampoMarca.Tipo => "Tipo de documento",
        CampoMarca.Fecha => "Fecha",
        CampoMarca.NombreArchivo => "Campo para el nombre de archivo",
        _ => campo.ToString()
    };

    private void Visor_MarcaRealizada(int pagina, RectanguloFraccion fraccion)
    {
        if (_campoActivoParaMarcar is not { } campo)
        {
            return;
        }

        var textoExtraido = LectorPdf.ExtraerTexto(_rutaArchivo, pagina, fraccion);
        _marcas[campo] = new Marca(campo, pagina, fraccion.X, fraccion.Y, fraccion.Ancho, fraccion.Alto, textoExtraido);
        _campoActivoParaMarcar = null;
        ResaltarBotonActivo(null);
        ActualizarEstadosDeMarca();
        ActualizarMarcasEnVisor();

        // Caso-3, punto 3d: apenas se marca la fecha del documento, los ejemplos del patrón y la
        // vista previa dejan de usar la fecha de hoy y pasan a usar la fecha real detectada.
        if (campo == CampoMarca.Fecha && _paso == Paso.Organizacion)
        {
            ControlOrganizacion.RefrescarPorCambioDeFecha();
        }

        if (PanelPreview.Visibility == Visibility.Visible)
        {
            ActualizarPreview();
        }
    }

    private void ActualizarMarcasEnVisor()
    {
        var marcas = _marcas.Values.Select(m => (m.Campo, m.Pagina, new RectanguloFraccion(m.X, m.Y, m.Ancho, m.Alto)));
        Visor.MostrarMarcas(marcas);
    }

    private void ActualizarEstadosDeMarca()
    {
        TxtEstadoMarcaEmisor.Text = EstadoTexto(CampoMarca.Emisor);
        TxtEstadoMarcaTipo.Text = EstadoTexto(CampoMarca.Tipo);
        TxtEstadoMarcaFecha.Text = EstadoTexto(CampoMarca.Fecha);
        TxtEstadoMarcaNombre.Text = EstadoTexto(CampoMarca.NombreArchivo);
    }

    private string EstadoTexto(CampoMarca campo)
    {
        if (!_marcas.TryGetValue(campo, out var marca))
        {
            return "Todavía no marcado.";
        }

        return string.IsNullOrWhiteSpace(marca.TextoReferencia)
            ? $"⚠ Marcado en página {marca.Pagina + 1}, pero no se pudo leer texto ahí. Probar marcar de nuevo, un poco más grande."
            : $"✅ \"{marca.TextoReferencia}\" (página {marca.Pagina + 1})";
    }

    private void CmbEmisor_TextChanged(object sender, TextChangedEventArgs e)
    {
        CmbEmisor.ItemsSource = _entidades.Buscar(CategoriaEntidad.Emisor, CmbEmisor.Text);
    }

    private void CmbTipo_TextChanged(object sender, TextChangedEventArgs e)
    {
        CmbTipo.ItemsSource = _entidades.Buscar(CategoriaEntidad.Tipo, CmbTipo.Text);
    }

    private void BtnElegirCarpeta_Click(object sender, RoutedEventArgs e)
    {
        using var dialogo = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Elegir la carpeta madre: la raíz donde va a vivir todo lo de este tipo de documento"
        };

        if (dialogo.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtCarpetaDestino.Text = dialogo.SelectedPath;
        }
    }

    // ----- Paso 3 (Caso-3): organización de subcarpetas -----
    // El tipo/patrón (accesos rápidos, lista completa, ejemplos) vive en ControlOrganizacion
    // (Caso-4 lo reutiliza tal cual); acá solo queda lo propio de esta ventana: cuándo mostrarlo
    // por primera vez, y el marcado de la fecha sobre el PDF.

    private void PrepararVistaOrganizacion()
    {
        if (_organizacionInicializada)
        {
            return;
        }

        _organizacionInicializada = true;

        if (_configuracionExistente is not null)
        {
            // Vinculando a una configuración existente: el tipo/patrón ya están definidos por
            // ella; solo se muestra (bloqueado) y se marca la fecha en este documento.
            ControlOrganizacion.Iniciar(_configuracionExistente.FormatoCarpeta, _configuracionExistente.PatronCarpeta, bloqueado: true);
        }
        else
        {
            ControlOrganizacion.Iniciar(_tipoOrganizacionPrecargado, _patronPrecargadoParaControl, bloqueado: false);
        }

        ActualizarVisibilidadMarcarFecha();
    }

    private void ControlOrganizacion_SeleccionCambiada()
    {
        ActualizarVisibilidadMarcarFecha();

        if (PanelPreview.Visibility == Visibility.Visible)
        {
            ActualizarPreview();
        }
    }

    private void ActualizarVisibilidadMarcarFecha()
    {
        PanelMarcarFecha.Visibility = ControlOrganizacion.FechaEsAplicable ? Visibility.Visible : Visibility.Collapsed;
        TxtFechaOpcional.Visibility = ControlOrganizacion.FechaEsOpcional ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpcionNombre_Changed(object sender, RoutedEventArgs e)
    {
        PanelMarcarNombre.Visibility = RbExtraerNombre.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

        if (PanelPreview.Visibility == Visibility.Visible)
        {
            ActualizarPreview();
        }
    }

    private void MarcarOpcionNombre()
    {
        RbMantenerNombre.IsChecked = !_renombrar && !_preguntarNombre;
        RbExtraerNombre.IsChecked = _renombrar;
        RbPreguntarNombre.IsChecked = _preguntarNombre;
    }

    private void ChkAbrirDespuesDeGuardar_Changed(object sender, RoutedEventArgs e)
    {
        _abrirDespuesDeGuardar = ChkAbrirDespuesDeGuardar.IsChecked == true;
    }

    private void AplicarConfiguracionExistente(ConfiguracionDocumento existente)
    {
        _configuracionExistente = existente;
        _carpetaDestino = existente.CarpetaDestino;
        _formato = existente.FormatoCarpeta;
        _patronCarpeta = existente.PatronCarpeta;
        _renombrar = existente.Renombrar;
        _preguntarNombre = existente.PreguntarNombre;
        _abrirDespuesDeGuardar = existente.AbrirDespuesDeGuardar;

        TxtCarpetaDestino.Text = _carpetaDestino;
        BtnElegirCarpeta.IsEnabled = false;

        RbGuardarDirecto.IsChecked = _formato == FormatoCarpeta.Directo;
        RbGuardarSubcarpetas.IsChecked = _formato != FormatoCarpeta.Directo;
        RbGuardarDirecto.IsEnabled = RbGuardarSubcarpetas.IsEnabled = false;

        MarcarOpcionNombre();
        RbMantenerNombre.IsEnabled = RbExtraerNombre.IsEnabled = RbPreguntarNombre.IsEnabled = false;

        ChkAbrirDespuesDeGuardar.IsChecked = _abrirDespuesDeGuardar;
        ChkAbrirDespuesDeGuardar.IsEnabled = false;
    }

    private void BtnActualizarPreview_Click(object sender, RoutedEventArgs e) => ActualizarPreview();

    /// <summary>
    /// Preview obligatorio de anterior/actual/futuro (Caso-1, punto 3): usa el mismo calculo que
    /// se usaria para guardar de verdad, con los datos que ya esten disponibles en este momento
    /// del asistente (el nombre de archivo final recien se conoce en el Paso 4).
    /// </summary>
    private void ActualizarPreview()
    {
        var formato = LeerFormatoElegido() ?? _formato;
        var patron = LeerPatronElegido() ?? (formato == _formato ? _patronCarpeta : null);
        var carpetaDestino = _configuracionExistente?.CarpetaDestino ?? _carpetaDestino;

        if (string.IsNullOrWhiteSpace(carpetaDestino))
        {
            return;
        }

        if (formato != FormatoCarpeta.Directo && string.IsNullOrWhiteSpace(patron))
        {
            TxtPreviewAnterior.Text = "—";
            TxtPreviewActual.Text = "(Elegí el tipo de organización y un ejemplo de patrón para verlo acá.)";
            TxtPreviewFuturaTitulo.Visibility = Visibility.Collapsed;
            TxtPreviewFutura.Visibility = Visibility.Collapsed;
            return;
        }

        var (fecha, fechaEsSupuesta) = LeerFechaPreview();
        var nombreArchivo = LeerNombreArchivoPreview();

        var configuracionTemporal = new ConfiguracionDocumento
        {
            Emisor = _emisor, Tipo = _tipo, CarpetaDestino = carpetaDestino,
            FormatoCarpeta = formato, PatronCarpeta = patron, Renombrar = false, Patrones = []
        };

        TxtPreviewActual.Text = ClasificadorService.CalcularRutaDestino(nombreArchivo, configuracionTemporal, fecha, null)
            + (fechaEsSupuesta ? "\n(usando la fecha de hoy: todavía no se marcó o no se pudo leer la fecha del documento)" : string.Empty);

        var anterior = FormatoCarpetaService.BuscarCarpetaAnteriorReal(carpetaDestino, formato, patron);
        TxtPreviewAnterior.Text = anterior ?? "(Todavía no hay ninguna carpeta así en el disco — esta sería la primera.)";

        if (formato == FormatoCarpeta.Directo)
        {
            TxtPreviewFuturaTitulo.Visibility = Visibility.Collapsed;
            TxtPreviewFutura.Visibility = Visibility.Collapsed;
        }
        else
        {
            TxtPreviewFuturaTitulo.Visibility = Visibility.Visible;
            TxtPreviewFutura.Visibility = Visibility.Visible;
            var fechaFutura = FormatoCarpetaService.SiguientePeriodo(formato, fecha, patron);
            var subcarpetaFutura = FormatoCarpetaService.ConstruirSubcarpeta(formato, patron, fechaFutura);
            TxtPreviewFutura.Text = System.IO.Path.Combine(carpetaDestino, subcarpetaFutura);
        }
    }

    private FormatoCarpeta? LeerFormatoElegido()
    {
        if (_configuracionExistente is not null)
        {
            return _configuracionExistente.FormatoCarpeta;
        }

        if (RbGuardarDirecto.IsChecked == true)
        {
            return FormatoCarpeta.Directo;
        }

        if (RbGuardarSubcarpetas.IsChecked == true)
        {
            return ControlOrganizacion.FormatoElegido;
        }

        return null;
    }

    private string? LeerPatronElegido()
    {
        var formato = LeerFormatoElegido();
        if (formato is null or FormatoCarpeta.Directo)
        {
            return null;
        }

        if (_configuracionExistente is not null)
        {
            return _configuracionExistente.PatronCarpeta;
        }

        return ControlOrganizacion.PatronElegido;
    }

    private (DateTime Fecha, bool EsSupuesta) LeerFechaPreview()
    {
        if (_marcas.TryGetValue(CampoMarca.Fecha, out var marca)
            && !string.IsNullOrWhiteSpace(marca.TextoReferencia)
            && FechaExtraidaService.TryParsear(marca.TextoReferencia, out var fecha))
        {
            return (fecha, false);
        }

        return (DateTime.Now, true);
    }

    /// <summary>
    /// El nombre final recien se decide en el Paso 4 (mantener vs extraer): mientras tanto, el
    /// preview usa el nombre original como mejor aproximacion disponible -- se corrige solo
    /// apenas el usuario elige "extraer" y marca el campo.
    /// </summary>
    private string LeerNombreArchivoPreview()
    {
        var extrayendoNombre = _configuracionExistente?.Renombrar
            ?? (RbExtraerNombre.IsChecked == true);

        if (extrayendoNombre && _marcas.TryGetValue(CampoMarca.NombreArchivo, out var marca)
            && !string.IsNullOrWhiteSpace(marca.TextoReferencia))
        {
            return marca.TextoReferencia + System.IO.Path.GetExtension(_rutaArchivo);
        }

        return _rutaArchivo;
    }

    private void MostrarResumen()
    {
        var (fechaReferencia, _) = LeerFechaPreview();

        var formatoTexto = _formato switch
        {
            FormatoCarpeta.Directo => "directo en la carpeta madre",
            _ when _patronCarpeta is not null =>
                $"{OrganizacionCarpetaService.NombreDe(_formato)} — ejemplo de carpeta: {OrganizacionCarpetaService.FormatearEjemplo(_patronCarpeta, fechaReferencia)}",
            _ => OrganizacionCarpetaService.NombreDe(_formato)
        };
        var nombreTexto = _renombrar ? "se extrae del campo marcado en el PDF"
            : _preguntarNombre ? "se pregunta cada vez, antes de guardar"
            : "se mantiene el nombre original";

        // Al editar no se guarda ningún documento, así que no hay nombre que pedir acá.
        var pedirNombreAhora = _preguntarNombre && _edicion is null;
        PanelNombreEsteDocumento.Visibility = pedirNombreAhora ? Visibility.Visible : Visibility.Collapsed;
        if (pedirNombreAhora && string.IsNullOrWhiteSpace(TxtNombreEsteDocumento.Text))
        {
            TxtNombreEsteDocumento.Text = System.IO.Path.GetFileNameWithoutExtension(_rutaArchivo);
        }

        var encabezado = _edicion is not null
            ? "Se van a actualizar las marcas y la configuración de este patrón (no se mueve ningún archivo):\n\n"
            : _configuracionExistente is not null
                ? "Este documento se va a vincular a la configuración existente (se agrega como patrón de reconocimiento adicional):\n\n"
                : string.Empty;

        TxtResumen.Text =
            encabezado +
            $"Emisor: {_emisor}\n" +
            $"Tipo: {_tipo}\n" +
            $"Carpeta madre: {_carpetaDestino}\n" +
            $"Subcarpetas: {formatoTexto}\n" +
            $"Nombre de archivo: {nombreTexto}";
    }

    private void BtnSiguiente_Click(object sender, RoutedEventArgs e)
    {
        switch (_paso)
        {
            case Paso.EmisorTipo:
                _emisor = CmbEmisor.Text.Trim();
                _tipo = CmbTipo.Text.Trim();
                if (string.IsNullOrWhiteSpace(_emisor) || string.IsNullOrWhiteSpace(_tipo))
                {
                    MostrarError("Completar el Emisor y el Tipo.");
                    return;
                }

                // Caso-9, mejora 1: Emisor y Tipo pueden terminar formando parte de un nombre o
                // ruta más adelante -- se validan/sanean acá, apenas se escriben.
                try
                {
                    _emisor = ValidadorRutaService.ValidarYSanearSegmento(_emisor);
                    _tipo = ValidadorRutaService.ValidarYSanearSegmento(_tipo);
                }
                catch (ValidacionSeguridadException ex)
                {
                    MostrarError(ex.Message);
                    return;
                }

                if (!_marcas.ContainsKey(CampoMarca.Emisor) || !_marcas.ContainsKey(CampoMarca.Tipo))
                {
                    MostrarError("Marcar el Emisor y el Tipo sobre el PDF.");
                    return;
                }

                if (_edicion is null)
                {
                    var configuracionExistente = _configuraciones.BuscarPorEmisorYTipo(_emisor, _tipo);
                    if (configuracionExistente is not null)
                    {
                        var vincular = System.Windows.MessageBox.Show(
                            this,
                            $"Ya existe una configuración guardada para \"{_emisor}\" / \"{_tipo}\".\n\n" +
                            "¿Vincular este documento a esa configuración como un patrón de reconocimiento adicional? " +
                            "(útil si el proveedor cambió el diseño del documento). Se va a usar la misma carpeta de destino, " +
                            "formato y regla de nombre de archivo ya definidos.",
                            "Archivero", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (vincular != MessageBoxResult.Yes)
                        {
                            MostrarError("Corregir el Emisor o el Tipo si no correspondía, o cancelar la identificación.");
                            return;
                        }

                        AplicarConfiguracionExistente(configuracionExistente);
                    }
                    else
                    {
                        _configuracionExistente = null;
                    }
                }

                IrA(Paso.Carpeta);
                break;

            case Paso.Carpeta:
                if (_configuracionExistente is null)
                {
                    if (string.IsNullOrWhiteSpace(TxtCarpetaDestino.Text))
                    {
                        MostrarError("Elegir una carpeta de destino.");
                        return;
                    }

                    _carpetaDestino = TxtCarpetaDestino.Text;

                    if (RbGuardarDirecto.IsChecked == true)
                    {
                        _formato = FormatoCarpeta.Directo;
                        _patronCarpeta = null;
                        IrA(Paso.NombreArchivo);
                        return;
                    }

                    if (RbGuardarSubcarpetas.IsChecked != true)
                    {
                        MostrarError("Elegir si el documento se guarda directo en la carpeta o en subcarpetas dentro de ella.");
                        return;
                    }
                }
                else if (_formato == FormatoCarpeta.Directo)
                {
                    IrA(Paso.NombreArchivo);
                    return;
                }

                IrA(Paso.Organizacion);
                break;

            case Paso.Organizacion:
                if (_configuracionExistente is null)
                {
                    if (!ControlOrganizacion.Validar(out var errorOrganizacion))
                    {
                        MostrarError(errorOrganizacion!);
                        return;
                    }

                    _formato = ControlOrganizacion.FormatoElegido!.Value;
                    _patronCarpeta = _formato == FormatoCarpeta.Directo ? null : ControlOrganizacion.PatronElegido;
                }

                // La marca de fecha es obligatoria salvo para "directo en la carpeta" (SPEC
                // REQ-003) o para los dos tipos donde Caso-3 (punto 3d) la hace opcional.
                if (_formato != FormatoCarpeta.Directo
                    && _formato is not (FormatoCarpeta.MesSinAnio or FormatoCarpeta.SemanaDelMes)
                    && !_marcas.ContainsKey(CampoMarca.Fecha))
                {
                    MostrarError("Marcar dónde aparece la fecha en el PDF.");
                    return;
                }

                IrA(Paso.NombreArchivo);
                break;

            case Paso.NombreArchivo:
                if (_configuracionExistente is null)
                {
                    if (RbMantenerNombre.IsChecked != true && RbExtraerNombre.IsChecked != true && RbPreguntarNombre.IsChecked != true)
                    {
                        MostrarError("Elegir cómo se va a llamar el archivo.");
                        return;
                    }

                    _renombrar = RbExtraerNombre.IsChecked == true;
                    _preguntarNombre = RbPreguntarNombre.IsChecked == true;
                }

                if (_renombrar && !_marcas.ContainsKey(CampoMarca.NombreArchivo))
                {
                    MostrarError("Marcar en el PDF el campo que se va a usar como nombre de archivo.");
                    return;
                }

                MostrarResumen();
                IrA(Paso.Confirmar);
                break;

            case Paso.Confirmar:
                GuardarYClasificar();
                break;
        }
    }

    private void GuardarYClasificar()
    {
        try
        {
            var marcas = _marcas.Values.ToList();

            DateTime? fecha = null;
            if (_marcas.TryGetValue(CampoMarca.Fecha, out var marcaFecha))
            {
                var textoFecha = LectorPdf.ExtraerTexto(
                    _rutaArchivo, marcaFecha.Pagina,
                    new RectanguloFraccion(marcaFecha.X, marcaFecha.Y, marcaFecha.Ancho, marcaFecha.Alto));

                if (!FechaExtraidaService.TryParsear(textoFecha, out var fechaParseada))
                {
                    MostrarError($"No se pudo interpretar la fecha extraída (\"{textoFecha}\"). Revisar la marca sobre el PDF.");
                    return;
                }

                fecha = fechaParseada;
            }

            string? nombreExtraido = null;
            if (_renombrar && _marcas.TryGetValue(CampoMarca.NombreArchivo, out var marcaNombre))
            {
                nombreExtraido = LectorPdf.ExtraerTexto(
                    _rutaArchivo, marcaNombre.Pagina,
                    new RectanguloFraccion(marcaNombre.X, marcaNombre.Y, marcaNombre.Ancho, marcaNombre.Alto));

                if (string.IsNullOrWhiteSpace(nombreExtraido))
                {
                    MostrarError("No se pudo extraer un nombre válido de la coordenada marcada.");
                    return;
                }
            }

            if (_edicion is { } edicion)
            {
                // Modo edicion: solo se actualizan las marcas del patron y la configuracion.
                // El PDF de ejemplo elegido para revisar/corregir no se toca ni se mueve.
                _configuraciones.ActualizarPatron(edicion.PatronId, marcas);
                _configuraciones.ActualizarDestino(edicion.Configuracion.Id, _carpetaDestino, _formato, _patronCarpeta, _renombrar, _abrirDespuesDeGuardar, _preguntarNombre);
                AuditoriaService.Registrar("CLASIFICACION_EDITADA", $"Emisor={_emisor}; Tipo={_tipo}");

                System.Windows.MessageBox.Show(this, "Cambios guardados.", "Archivero",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
                return;
            }

            // Clasificar el archivo ANTES de guardar la configuracion: si algo falla aca
            // (fecha invalida, carpeta no disponible, nombre duplicado), no debe quedar una
            // configuracion a medias que despues choque con la restriccion de Emisor+Tipo
            // unico al reintentar.
            var configuracionParaClasificar = _configuracionExistente ?? new ConfiguracionDocumento
            {
                Emisor = _emisor,
                Tipo = _tipo,
                CarpetaDestino = _carpetaDestino,
                FormatoCarpeta = _formato,
                PatronCarpeta = _patronCarpeta,
                Renombrar = _renombrar,
                AbrirDespuesDeGuardar = _abrirDespuesDeGuardar,
                Patrones = []
            };

            // Caso-11, punto 1: el nombre escrito en el paso de confirmar se usa como si fuera
            // un nombre extraído, solo para este documento; la configuración guarda "preguntar".
            if (_preguntarNombre)
            {
                var nombreEscrito = TxtNombreEsteDocumento.Text.Trim();
                if (string.IsNullOrWhiteSpace(nombreEscrito))
                {
                    MostrarError("Escribir el nombre para este documento.");
                    return;
                }

                configuracionParaClasificar = configuracionParaClasificar with { Renombrar = true, PreguntarNombre = false };
                nombreExtraido = nombreEscrito;
            }

            string rutaFinal;
            try
            {
                rutaFinal = ClasificadorService.Clasificar(_rutaArchivo, configuracionParaClasificar, fecha, nombreExtraido);
            }
            catch (ArchivoDuplicadoException ex)
            {
                // REQ-002: ofrecer Revisar / Reemplazar / Dejar pendiente / Guardar como
                // excepcion, en vez de solo fallar.
                var resolver = new ResolverDuplicadoWindow(_rutaArchivo, ex.RutaDestino) { Owner = this };
                if (resolver.ShowDialog() != true)
                {
                    MostrarError("Documento dejado pendiente por nombre duplicado. Podés posponer o cancelar, o intentar de nuevo.");
                    return;
                }

                rutaFinal = ex.RutaDestino;
            }

            GuardarConfiguracionYCerrar(marcas, rutaFinal);
        }
        catch (Exception ex)
        {
            MostrarError($"No se pudo guardar: {ex.Message}");
        }
    }

    private void GuardarConfiguracionYCerrar(List<Marca> marcas, string rutaFinal)
    {
        if (_configuracionExistente is not null)
        {
            _configuraciones.AgregarPatronAConfiguracionExistente(_configuracionExistente.Id, marcas);
            AuditoriaService.Registrar("CLASIFICACION_VINCULADA", $"Emisor={_emisor}; Tipo={_tipo}");
        }
        else
        {
            _configuraciones.GuardarNueva(_emisor, _tipo, _carpetaDestino, _formato, _patronCarpeta, _renombrar, marcas, _abrirDespuesDeGuardar, _preguntarNombre);
            AuditoriaService.Registrar("CLASIFICACION_CREADA", $"Emisor={_emisor}; Tipo={_tipo}");
        }

        _pendientes.Quitar(_rutaArchivo);
        _borradores.Eliminar(_rutaArchivo);
        _draftYaResuelto = true;

        System.Windows.MessageBox.Show(
            this, $"Documento guardado en:\n{rutaFinal}", "Archivero",
            MessageBoxButton.OK, MessageBoxImage.Information);

        DialogResult = true;
        Close();
    }

    private void BtnPosponer_Click(object sender, RoutedEventArgs e)
    {
        GuardarBorradorActual();
        _draftYaResuelto = true;
        DialogResult = false;
        Close();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        var mensaje = _edicion is not null
            ? "¿Cancelar la edición? Se pierden los cambios sin guardar."
            : "¿Cancelar la identificación de este documento? Se pierde lo marcado hasta ahora; el archivo sigue en pendientes.";

        var confirmar = System.Windows.MessageBox.Show(
            this, mensaje, "Archivero", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirmar == MessageBoxResult.Yes)
        {
            _borradores.Eliminar(_rutaArchivo);
            _draftYaResuelto = true;
            DialogResult = false;
            Close();
        }
    }

    private void MostrarError(string mensaje)
    {
        TxtError.Text = mensaje;
        TxtError.Visibility = Visibility.Visible;
    }
}
