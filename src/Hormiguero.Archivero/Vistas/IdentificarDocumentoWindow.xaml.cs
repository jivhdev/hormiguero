using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Archivero.Datos;
using Archivero.Servicios;
using Archivero.Servicios.Pdf;
using Button = System.Windows.Controls.Button;
using RadioButton = System.Windows.Controls.RadioButton;

namespace Archivero.Vistas;

public sealed class CampoPropioEdicion : INotifyPropertyChanged
{
    private string _nombre;
    private string _estado;
    private string? _datoIdSeleccionado;
    private IReadOnlyList<KeyValuePair<string, string>> _opcionesDatos = [];

    public CampoPropioEdicion(
        string nombre,
        string nombreEstable,
        string estado = "Todavía no marcado."
    )
    {
        _nombre = nombre;
        NombreEstable = nombreEstable;
        _estado = estado;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Nombre
    {
        get => _nombre;
        set
        {
            _nombre = value;
            AvisarCambio();
        }
    }
    public string NombreEstable { get; }
    public string Estado
    {
        get => _estado;
        set
        {
            _estado = value;
            AvisarCambio();
        }
    }
    public bool Marcado { get; set; }
    public IReadOnlyList<KeyValuePair<string, string>> OpcionesDatos
    {
        get => _opcionesDatos;
        set
        {
            _opcionesDatos = value;
            AvisarCambio();
        }
    }
    public string? DatoIdSeleccionado
    {
        get => _datoIdSeleccionado;
        set
        {
            _datoIdSeleccionado = value;
            AvisarCambio();
        }
    }
    public int Pagina { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Ancho { get; set; }
    public double Alto { get; set; }

    private void AvisarCambio([CallerMemberName] string? propiedad = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
}

public partial class IdentificarDocumentoWindow : Window
{
    private enum Paso
    {
        QueDocumento,
        Emisor,
        Numero,
        OtrosDatos,
        Guardar,
        Resumen,
    }

    private readonly string _rutaArchivo;
    private readonly EntidadRepository _entidades = new();
    private readonly ConfiguracionDocumentoRepository _configuraciones = new();
    private readonly PendienteRepository _pendientes = new();
    private readonly BorradorRepository _borradores = new();
    private readonly Dictionary<CampoMarca, Marca> _marcas = new();
    private readonly ObservableCollection<CampoPropioEdicion> _camposPropios = [];
    private readonly ObservableCollection<DatoEnlazanteEdicion> _datosEnlazantes = [];
    private readonly ObservableCollection<DatoInformativoEdicion> _datosInformativos = [];
    private readonly Stack<Paso> _pasosRecorridos = new();

    private Paso _paso;
    private Paso _pasoInicial = Paso.QueDocumento;
    private CampoMarca? _campoActivoParaMarcar;
    private CampoPropioEdicion? _campoPropioActivoParaMarcar;
    private DatoEnlazanteEdicion? _datoEnlazanteActivoParaMarcar;
    private DatoInformativoEdicion? _datoInformativoActivoParaMarcar;
    private bool _draftYaResuelto;
    private bool _nombreEstandarEditado;
    private bool _actualizandoNombreEstandar;

    private string _emisor = string.Empty;
    private string _tipo = string.Empty;
    private string _carpetaDestino = string.Empty;
    private FormatoCarpeta _formato;
    private string? _patronCarpeta;
    private bool _renombrar;
    private bool _preguntarNombre;
    private bool _abrirDespuesDeGuardar;
    private ModoImpresion _modoImpresion;
    private string? _impresora;
    private bool _inicializandoImpresion;
    private string? _avisoImpresion;
    private readonly ConfiguracionImpresionRepository _configuracionImpresion = new();
    private ConfiguracionDocumento? _configuracionExistente;
    private readonly (ConfiguracionDocumento Configuracion, int PatronId)? _edicion;
    private readonly bool _modoObservador;
    private readonly string? _carpetaObservada;

    public int? ConfiguracionDocumentoId { get; private set; }

    // Precarga para ControlOrganizacion (edicion o borrador restaurado): se aplica una sola vez,
    // la primera vez que se llega al Paso.Organizacion (ver PrepararVistaOrganizacion).
    private bool _organizacionInicializada;
    private FormatoCarpeta? _tipoOrganizacionPrecargado;
    private string? _patronPrecargadoParaControl;

    public IdentificarDocumentoWindow(
        string rutaArchivo,
        bool modoObservador = false,
        string? carpetaObservada = null
    )
    {
        InitializeComponent();
        InicializarOpcionesImpresion();
        TxtNombreEstandar.TextChanged += (_, _) =>
        {
            if (!_actualizandoNombreEstandar)
                _nombreEstandarEditado = true;
        };
        ListaCamposPropios.ItemsSource = _camposPropios;
        ConfigurarListaDatosEnlazantes();
        ConfigurarListaDatosInformativos();
        CmbCategoriaDocumento.ItemsSource = AsistenteClasificacionService.Categorias;
        CmbCategoriaOtrosDatos.ItemsSource = AsistenteClasificacionService.Categorias;
        _rutaArchivo = rutaArchivo;
        _modoObservador = modoObservador;
        _carpetaObservada = carpetaObservada;
        CargarDatosEnlazantes(string.Empty, string.Empty, 0);
        ControlOrganizacion.ConfigurarProveedorDeFecha(LeerFechaPreview);

        Visor.CargarPdf(rutaArchivo);
        Visor.MarcaRealizada += Visor_MarcaRealizada;

        CmbEmisor.ItemsSource = _entidades.Buscar(CategoriaEntidad.Emisor, string.Empty);

        var borrador = _modoObservador ? null : _borradores.Obtener(_rutaArchivo);
        if (borrador is not null)
        {
            AplicarBorrador(borrador);
        }

        ActualizarEstadosDeMarca();
        ActualizarMarcasEnVisor();
        MostrarPaso(Paso.QueDocumento);
        if (!_modoObservador)
            Closing += IdentificarDocumentoWindow_Closing;

        if (_modoObservador)
        {
            BtnPosponer.Visibility = Visibility.Collapsed;
            BtnCambiarGuardar.Visibility = Visibility.Collapsed;
            RbGuardarDirecto.IsChecked = true;
            TxtCarpetaDestino.Text = _carpetaObservada ?? string.Empty;
        }

        if (borrador is not null)
        {
            System.Windows.MessageBox.Show(
                this,
                "Se restauró el progreso que habías dejado sin terminar para este documento.",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
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
    public IdentificarDocumentoWindow(
        string rutaArchivo,
        ConfiguracionDocumento configuracion,
        PatronReconocimiento patron,
        bool comenzarEnPasoCarpeta = false
    )
    {
        InitializeComponent();
        TxtNombreEstandar.TextChanged += (_, _) =>
        {
            if (!_actualizandoNombreEstandar)
                _nombreEstandarEditado = true;
        };
        ListaCamposPropios.ItemsSource = _camposPropios;
        ConfigurarListaDatosEnlazantes();
        ConfigurarListaDatosInformativos();
        CmbCategoriaDocumento.ItemsSource = AsistenteClasificacionService.Categorias;
        CmbCategoriaOtrosDatos.ItemsSource = AsistenteClasificacionService.Categorias;
        _rutaArchivo = rutaArchivo;
        _edicion = (configuracion, patron.Id);
        CargarCamposPropios(configuracion.CamposPropios);
        CargarDatosEnlazantes(configuracion.Emisor, configuracion.Tipo, patron.Id);
        ActualizarOpcionesMigracion();
        BtnPosponer.Visibility = Visibility.Collapsed;
        ControlOrganizacion.ConfigurarProveedorDeFecha(LeerFechaPreview);

        Visor.CargarPdf(rutaArchivo);
        Visor.MarcaRealizada += Visor_MarcaRealizada;

        PrecargarParaEdicion(configuracion, patron);

        ActualizarEstadosDeMarca();
        ActualizarMarcasEnVisor();
        _pasoInicial = comenzarEnPasoCarpeta ? Paso.Guardar : Paso.QueDocumento;
        MostrarPaso(_pasoInicial);
    }

    private void PrecargarParaEdicion(
        ConfiguracionDocumento configuracion,
        PatronReconocimiento patron
    )
    {
        _emisor = configuracion.Emisor;
        _tipo = configuracion.Tipo;
        _carpetaDestino = configuracion.CarpetaDestino;
        _formato = configuracion.FormatoCarpeta;
        _patronCarpeta = configuracion.PatronCarpeta;
        _renombrar = configuracion.Renombrar;
        _preguntarNombre = configuracion.PreguntarNombre;
        _abrirDespuesDeGuardar = configuracion.AbrirDespuesDeGuardar;
        RbEmitido.IsChecked = configuracion.GrupoDocumento == "Emitido";
        RbRecibido.IsChecked = configuracion.GrupoDocumento == "Recibido";
        TxtNombreEstandar.Text = string.IsNullOrWhiteSpace(configuracion.NombreEstandar)
            ? $"{configuracion.Tipo} · {configuracion.Emisor}"
            : configuracion.NombreEstandar;
        _nombreEstandarEditado = true;

        CmbEmisor.Text = _emisor;
        CmbEmisor.IsEnabled = false;

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
        TxtCarpetaDestino.Text = borrador.CarpetaDestino;

        if (
            borrador.Formato is not null
            && Enum.TryParse<FormatoCarpeta>(borrador.Formato, out var formato)
        )
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

        var formatoElegido =
            RbGuardarDirecto.IsChecked == true
                ? FormatoCarpeta.Directo
                : ControlOrganizacion.FormatoElegido;

        var renombrarElegido =
            RbMantenerNombre.IsChecked == true || RbPreguntarNombre.IsChecked == true ? false
            : RbExtraerNombre.IsChecked == true ? true
            : (bool?)null;

        var borrador = new BorradorAsistente
        {
            Emisor = CmbEmisor.Text.Trim(),
            Tipo = TipoDerivado(),
            CarpetaDestino = TxtCarpetaDestino.Text,
            Formato = formatoElegido?.ToString(),
            GuardaEnSubcarpetas = RbGuardarSubcarpetas.IsChecked == true,
            PatronCarpeta = LeerPatronElegido(),
            Renombrar = renombrarElegido,
            PreguntarNombre = RbPreguntarNombre.IsChecked == true,
            Marcas = _marcas.Values.ToList(),
        };

        var hayAlgoQueGuardar =
            !string.IsNullOrWhiteSpace(borrador.Emisor)
            || !string.IsNullOrWhiteSpace(borrador.Tipo)
            || borrador.Marcas.Count > 0
            || !string.IsNullOrWhiteSpace(borrador.CarpetaDestino);

        if (hayAlgoQueGuardar)
        {
            _borradores.Guardar(_rutaArchivo, borrador);
        }
    }

    private void IdentificarDocumentoWindow_Closing(
        object? sender,
        System.ComponentModel.CancelEventArgs e
    )
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

        var esPasoIdentificacion =
            nuevoPaso is Paso.QueDocumento or Paso.Emisor or Paso.Numero or Paso.OtrosDatos;
        PanelEmisorTipo.Visibility = esPasoIdentificacion
            ? Visibility.Visible
            : Visibility.Collapsed;
        PanelCategoriaDocumento.Visibility =
            nuevoPaso == Paso.QueDocumento ? Visibility.Visible : Visibility.Collapsed;
        PanelEmisor.Visibility =
            nuevoPaso == Paso.Emisor ? Visibility.Visible : Visibility.Collapsed;
        PanelDatosDiccionario.Visibility =
            nuevoPaso == Paso.OtrosDatos ? Visibility.Visible : Visibility.Collapsed;
        PanelNumeroDato.Visibility =
            nuevoPaso == Paso.Numero ? Visibility.Visible : Visibility.Collapsed;
        PanelDatosDiccionario.IsExpanded = nuevoPaso == Paso.OtrosDatos;
        PanelInformativos.Visibility =
            nuevoPaso == Paso.OtrosDatos ? Visibility.Visible : Visibility.Collapsed;
        PanelDatosAnteriores.Visibility =
            nuevoPaso == Paso.OtrosDatos && _camposPropios.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        ChkSinNumero.Visibility =
            nuevoPaso == Paso.Numero ? Visibility.Visible : Visibility.Collapsed;
        if (nuevoPaso == Paso.Numero)
            ActualizarVistaNumero();
        if (nuevoPaso == Paso.OtrosDatos)
            CollectionViewSource.GetDefaultView(_datosEnlazantes)?.Refresh();
        PanelCarpeta.Visibility =
            nuevoPaso == Paso.Guardar && !_modoObservador
                ? Visibility.Visible
                : Visibility.Collapsed;
        PanelOrganizacion.Visibility =
            nuevoPaso == Paso.Guardar && RbGuardarSubcarpetas.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        PanelNombreArchivo.Visibility =
            nuevoPaso == Paso.Guardar && !_modoObservador
                ? Visibility.Visible
                : Visibility.Collapsed;
        PanelConfirmar.Visibility =
            nuevoPaso == Paso.Resumen ? Visibility.Visible : Visibility.Collapsed;

        if (nuevoPaso == Paso.Guardar)
        {
            PrepararVistaOrganizacion();
        }

        // Preview obligatorio de anterior/actual/futuro (Caso-1, punto 3): visible desde que hay
        // carpeta+organización elegidas hasta confirmar, para que sea imposible llegar a "Guardar y
        // clasificar" sin haberlo visto.
        var mostrarPreview = nuevoPaso == Paso.Guardar && !_modoObservador;
        PanelPreview.Visibility = mostrarPreview ? Visibility.Visible : Visibility.Collapsed;
        if (mostrarPreview)
        {
            ActualizarPreview();
        }

        TxtError.Visibility = Visibility.Collapsed;

        var vinculando = _configuracionExistente is not null;

        (TxtTituloPaso.Text, TxtInstruccionPaso.Text) = nuevoPaso switch
        {
            Paso.QueDocumento => (
                "Paso 1 de 6 — ¿Qué documento es?",
                "Elige una categoría y luego el documento que aparece en la lista."
            ),
            Paso.Emisor => (
                "Paso 2 de 6 — ¿Quién lo emite?",
                "Escribe o elige el emisor y marca su ubicación en el PDF."
            ),
            Paso.Numero => (
                "Paso 3 de 6 — Marca el número del documento",
                "Marca el número que identifica el documento. Si no tiene número, indícalo aquí."
            ),
            Paso.OtrosDatos => (
                "Paso 4 de 6 — Otros datos (opcional)",
                "Marca solo los datos que te serviría buscar después. Puedes saltar este paso."
            ),
            Paso.Guardar => (
                "Paso 5 de 6 — ¿Dónde se guarda?",
                vinculando
                    ? "La carpeta y el formato ya pertenecen a la configuración elegida."
                    : "Elige carpeta, organización, fecha y nombre del archivo."
            ),
            Paso.Resumen => (
                _modoObservador
                    ? "Paso 5 de 5 — Revisa el diseño"
                    : "Paso 6 de 6 — Al llegar y resumen",
                _modoObservador
                    ? "Se queda en la carpeta del proveedor; Archivero solo lo registra en Hormiguero."
                    : "Revisa las opciones antes de guardar y clasificar."
            ),
            _ => (string.Empty, string.Empty),
        };

        BtnSiguiente.Content = nuevoPaso switch
        {
            Paso.Resumen => _modoObservador ? "Guardar diseño"
            : _edicion is not null ? "Guardar cambios"
            : "Guardar y clasificar",
            Paso.OtrosDatos => "Saltar",
            _ => "Siguiente",
        };

        // Se puede retroceder un paso, pero nunca saltar hacia adelante -- sigue siendo
        // estrictamente paso a paso.
        BtnAtras.IsEnabled = true;
    }

    private void BtnAtras_Click(object sender, RoutedEventArgs e)
    {
        if (_pasosRecorridos.Count > 0)
        {
            MostrarPaso(_pasosRecorridos.Pop());
        }
    }

    private void BtnCambiarPaso_Click(object sender, RoutedEventArgs e)
    {
        if (
            sender is Button { Tag: string etiqueta }
            && Enum.TryParse<Paso>(etiqueta, out var paso)
        )
            IrA(paso);
    }

    private void BtnMarcarEmisor_Click(object sender, RoutedEventArgs e) =>
        ArmarMarca(CampoMarca.Emisor);

    private void BtnMarcarTitulo_Click(object sender, RoutedEventArgs e) =>
        ArmarMarca(CampoMarca.Tipo);

    private void BtnMarcarFecha_Click(object sender, RoutedEventArgs e) =>
        ArmarMarca(CampoMarca.Fecha);

    private void BtnMarcarNombreArchivo_Click(object sender, RoutedEventArgs e) =>
        ArmarMarca(CampoMarca.NombreArchivo);

    private void ArmarMarca(CampoMarca campo)
    {
        Visor.IniciarMarcado();
        _campoPropioActivoParaMarcar = null;
        _campoActivoParaMarcar = campo;
        TxtInstruccionPaso.Text =
            $"Dibujar un rectángulo sobre el PDF donde aparece: {NombreCampo(campo)}.";
        ResaltarBotonActivo(campo);
    }

    private void ResaltarBotonActivo(CampoMarca? campoActivo)
    {
        var botones = new[]
        {
            BtnMarcarEmisor,
            BtnMarcarTitulo,
            BtnMarcarFecha,
            BtnMarcarNombreArchivo,
        };
        var camposEnOrden = new[]
        {
            CampoMarca.Emisor,
            CampoMarca.Tipo,
            CampoMarca.Fecha,
            CampoMarca.NombreArchivo,
        };

        for (var i = 0; i < botones.Length; i++)
        {
            if (camposEnOrden[i] == campoActivo)
            {
                botones[i].SetResourceReference(BackgroundProperty, "Hormiguero.AvisoSuave");
                botones[i].FontWeight = FontWeights.Bold;
                botones[i].SetResourceReference(BorderBrushProperty, "Hormiguero.Aviso");
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

    private static string NombreCampo(CampoMarca campo) =>
        campo switch
        {
            CampoMarca.Emisor => "Emisor",
            CampoMarca.Tipo => "Título del documento",
            CampoMarca.Fecha => "Fecha",
            CampoMarca.NombreArchivo => "Campo para el nombre de archivo",
            _ => campo.ToString(),
        };

    private void Visor_MarcaRealizada(int pagina, RectanguloFraccion fraccion)
    {
        Visor.TerminarMarcado();
        if (_datoInformativoActivoParaMarcar is { } datoInformativo)
        {
            string textoInformativo = LectorPdf.ExtraerTexto(_rutaArchivo, pagina, fraccion);
            datoInformativo.Marcado = true;
            datoInformativo.Pagina = pagina;
            datoInformativo.X = fraccion.X;
            datoInformativo.Y = fraccion.Y;
            datoInformativo.Ancho = fraccion.Ancho;
            datoInformativo.Alto = fraccion.Alto;
            datoInformativo.Estado = string.IsNullOrWhiteSpace(textoInformativo)
                ? "Zona marcada; no se pudo leer el ejemplo."
                : $"Valor de ejemplo: {textoInformativo}";
            _datoInformativoActivoParaMarcar = null;
            ActualizarMarcasEnVisor();
            return;
        }
        if (_datoEnlazanteActivoParaMarcar is { } datoEnlazante)
        {
            string texto = LectorPdf.ExtraerTexto(_rutaArchivo, pagina, fraccion);
            datoEnlazante.Incluido = true;
            datoEnlazante.Marcado = true;
            datoEnlazante.Pagina = pagina;
            datoEnlazante.X = fraccion.X;
            datoEnlazante.Y = fraccion.Y;
            datoEnlazante.Ancho = fraccion.Ancho;
            datoEnlazante.Alto = fraccion.Alto;
            datoEnlazante.ValorLeido = texto;
            datoEnlazante.Estado = datoEnlazante.DefineTipo
                ? string.IsNullOrWhiteSpace(texto)
                    ? "No se leyó ningún número. Prueba otra vez o indica que no trae número."
                    : $"Leí: {texto} → se enlaza como {Hormiguero.Nucleo.Datos.DiccionarioDatosEnlazantes.ClaveDeEnlace(texto)} ✓"
                : string.IsNullOrWhiteSpace(texto)
                    ? $"Zona marcada en la página {pagina + 1}; no se pudo leer texto."
                    : $"Texto leído: {texto} (página {pagina + 1}).";
            if (datoEnlazante.DefineTipo)
            {
                TxtEstadoNumero.Text = datoEnlazante.Estado;
                BtnProbarNumeroOtraVez.Visibility = string.IsNullOrWhiteSpace(texto)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            ActualizarSugerencia(datoEnlazante);
            _datoEnlazanteActivoParaMarcar = null;
            ActualizarMarcasEnVisor();
            return;
        }
        if (_campoPropioActivoParaMarcar is { } campoPropio)
        {
            ActualizarCampoPropioMarcado(pagina, fraccion, campoPropio);
            _campoPropioActivoParaMarcar = null;
            return;
        }
        if (_campoActivoParaMarcar is not { } campo)
        {
            return;
        }

        var textoExtraido = LectorPdf.ExtraerTexto(_rutaArchivo, pagina, fraccion);
        _marcas[campo] = new Marca(
            campo,
            pagina,
            fraccion.X,
            fraccion.Y,
            fraccion.Ancho,
            fraccion.Alto,
            textoExtraido
        );
        _campoActivoParaMarcar = null;
        ResaltarBotonActivo(null);
        ActualizarEstadosDeMarca();
        ActualizarMarcasEnVisor();

        // Caso-3, punto 3d: apenas se marca la fecha del documento, los ejemplos del patrón y la
        // vista previa dejan de usar la fecha de hoy y pasan a usar la fecha real detectada.
        if (campo == CampoMarca.Fecha && _paso == Paso.Guardar)
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
        var marcas = _marcas.Values.Select(m =>
            (m.Campo, m.Pagina, new RectanguloFraccion(m.X, m.Y, m.Ancho, m.Alto))
        );
        Visor.MostrarMarcas(marcas);
        Visor.MostrarCamposPropios(
            _camposPropios
                .Where(c => c.Marcado)
                .Select(c =>
                    (c.Nombre, c.Pagina, new RectanguloFraccion(c.X, c.Y, c.Ancho, c.Alto))
                )
                .Concat(
                    _datosEnlazantes
                        .Where(d => d.Incluido && d.Marcado)
                        .Select(d =>
                            (d.Nombre, d.Pagina, new RectanguloFraccion(d.X, d.Y, d.Ancho, d.Alto))
                        )
                )
                .Concat(
                    _datosInformativos
                        .Where(d => d.Marcado)
                        .Select(d =>
                            (d.Nombre, d.Pagina, new RectanguloFraccion(d.X, d.Y, d.Ancho, d.Alto))
                        )
                )
        );
    }

    private void CargarDatosEnlazantes(string emisor, string tipo, int patronId)
    {
        _datosEnlazantes.Clear();
        foreach (var dato in DatosEnlazantesConfiguracionService.Leer(emisor, tipo, patronId))
        {
            var edicion = new DatoEnlazanteEdicion(dato);
            if (dato.Marcado)
            {
                var texto = LectorPdf.ExtraerTexto(
                    _rutaArchivo,
                    dato.Pagina,
                    new(dato.X, dato.Y, dato.Ancho, dato.Alto)
                );
                edicion.ValorLeido = texto;
                edicion.Estado = string.IsNullOrWhiteSpace(texto)
                    ? "Zona guardada; no se pudo leer el texto de ejemplo."
                    : $"Texto leído: {texto} (página {dato.Pagina + 1}).";
                if (dato.Enlazable && !string.IsNullOrWhiteSpace(texto))
                {
                    using var conexion = Hormiguero.Nucleo.Datos.BaseComun.Abrir(
                        Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun
                    );
                    var coincidencias = new Hormiguero.Nucleo.Datos.RepositorioDatosEnlazantes(
                        conexion
                    ).SugerirCalce(dato.Id, texto, 4);
                    edicion.Estado +=
                        coincidencias.Count == 0
                            ? " Aún no hay documentos con este número."
                            : " Calzaría con: "
                                + string.Join(
                                    "; ",
                                    coincidencias
                                        .Take(3)
                                        .Select(c =>
                                            $"{c.NombreEstandar} N° {c.Valor}{(c.FechaDocumento is null ? "" : $" ({c.FechaDocumento:dd-MM-yyyy})")}"
                                        )
                                )
                                + (
                                    coincidencias.Count > 3
                                        ? $"; y {coincidencias.Count - 3} más"
                                        : ""
                                );
                }
            }
            _datosEnlazantes.Add(edicion);
        }
        _tipo = TipoDerivado();
        if (_datosEnlazantes.FirstOrDefault(d => d.DefineTipo) is { } definido)
        {
            CmbCategoriaDocumento.SelectedItem = definido.Grupo;
            CmbCategoriaOtrosDatos.SelectedItem = definido.Grupo;
            TxtDocumentoElegido.Text = $"Elegiste: {definido.NombreDocumento}";
        }
        else if (CmbCategoriaDocumento.SelectedIndex < 0 && CmbCategoriaDocumento.Items.Count > 0)
            CmbCategoriaDocumento.SelectedIndex = 0;
        CargarDatosInformativos(emisor, tipo, patronId);
        CollectionViewSource.GetDefaultView(_datosEnlazantes)?.Refresh();
    }

    private void ConfigurarListaDatosEnlazantes()
    {
        var vista = CollectionViewSource.GetDefaultView(_datosEnlazantes);
        vista.GroupDescriptions.Clear();
        vista.Filter = elemento =>
        {
            if (elemento is not DatoEnlazanteEdicion dato)
                return false;
            return _paso switch
            {
                Paso.Numero => dato.DefineTipo,
                Paso.OtrosDatos => !dato.DefineTipo
                    && dato.Grupo == CmbCategoriaOtrosDatos.SelectedItem as string,
                _ => false,
            };
        };
        ListaDatosEnlazantes.ItemsSource = vista;
    }

    private void CmbCategoriaDocumento_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbCategoriaDocumento.SelectedItem is not string categoria)
            return;
        string grupo = AsistenteClasificacionService.GrupoDocumento(categoria);
        RbEmitido.IsChecked = grupo == "Emitido";
        RbRecibido.IsChecked = grupo == "Recibido";
        RbEmitido.Visibility = grupo.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        PanelGrupo.Visibility = grupo.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        RbRecibido.Visibility = grupo.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var vista = CollectionViewSource.GetDefaultView(_datosEnlazantes);
        vista?.Refresh();
        ListaDocumentos.ItemsSource = AsistenteClasificacionService.DocumentosDeCategoria(
            categoria
        );
        if (
            _datosEnlazantes.FirstOrDefault(d => d.DefineTipo) is { } elegido
            && elegido.Grupo == categoria
        )
            ListaDocumentos.SelectedValue = elegido.Id;
        else
            ListaDocumentos.SelectedIndex = -1;
    }

    private void ListaDocumentos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListaDocumentos.SelectedItem is not Hormiguero.Nucleo.Datos.DatoEnlazante documento)
            return;
        var elegido = _datosEnlazantes.Single(d => d.Id == documento.Id);
        foreach (var dato in _datosEnlazantes)
        {
            dato.DefineTipo = dato == elegido;
            if (dato == elegido)
                dato.Incluido = true;
        }
        TxtDocumentoElegido.Text = $"Elegiste: {documento.EtiquetaTipo}";
        _tipo = TipoDerivado();
        ActualizarVistaNumero();
        ActualizarNombreEstandarSugerido();
        CollectionViewSource.GetDefaultView(_datosEnlazantes)?.Refresh();
    }

    private DatoEnlazanteEdicion? DatoNumero => _datosEnlazantes.FirstOrDefault(d => d.DefineTipo);

    private void ActualizarVistaNumero()
    {
        if (DatoNumero is not { } dato)
            return;
        TxtNombreDatoNumero.Text = dato.NombreDocumento;
        TxtEstadoNumero.Text = dato.Estado;
        ChkNumeroEnlazable.IsChecked = dato.Enlazable;
    }

    private void BtnMarcarNumero_Click(object sender, RoutedEventArgs e)
    {
        if (DatoNumero is not { } dato)
            return;
        _campoActivoParaMarcar = null;
        _campoPropioActivoParaMarcar = null;
        _datoEnlazanteActivoParaMarcar = dato;
        Visor.IniciarMarcado();
        TxtInstruccionPaso.Text = "Dibuja un rectángulo sobre el número en el PDF.";
    }

    private void ChkNumeroEnlazable_Changed(object sender, RoutedEventArgs e)
    {
        if (DatoNumero is { } dato)
            dato.Enlazable = ChkNumeroEnlazable.IsChecked == true;
    }

    private void CmbCategoriaOtrosDatos_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e
    ) => CollectionViewSource.GetDefaultView(_datosEnlazantes)?.Refresh();

    private void ActualizarOpcionesMigracion()
    {
        var opciones = _datosEnlazantes
            .Select(d => new KeyValuePair<string, string>(d.Id, d.Etiqueta))
            .ToList();
        foreach (var campo in _camposPropios)
            campo.OpcionesDatos = opciones;
        PanelDatosAnteriores.Visibility =
            _camposPropios.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AplicarVinculosDatosAnteriores()
    {
        var vinculados = _camposPropios
            .Where(c => !string.IsNullOrWhiteSpace(c.DatoIdSeleccionado))
            .ToList();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var campo in vinculados)
        {
            if (!ids.Add(campo.DatoIdSeleccionado!))
                throw new InvalidOperationException(
                    "Vincula cada dato anterior a una entrada distinta."
                );
            var dato =
                _datosEnlazantes.FirstOrDefault(d => d.Id == campo.DatoIdSeleccionado)
                ?? throw new InvalidOperationException(
                    "La entrada elegida ya no está en el diccionario."
                );
            dato.Incluido = true;
            dato.Marcado = true;
            dato.Pagina = campo.Pagina;
            dato.X = campo.X;
            dato.Y = campo.Y;
            dato.Ancho = campo.Ancho;
            dato.Alto = campo.Alto;
            dato.Estado = "Dato anterior vinculado a esta entrada.";
        }
        ActualizarMarcasEnVisor();
    }

    private IReadOnlyList<(string NombreEstable, string DatoId)> LeerVinculosDatosAnteriores() =>
        _camposPropios
            .Where(c => !string.IsNullOrWhiteSpace(c.DatoIdSeleccionado))
            .Select(c => (c.NombreEstable, c.DatoIdSeleccionado!))
            .ToList();

    private void BtnMarcarDatoEnlazante_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: DatoEnlazanteEdicion dato })
            return;
        _campoActivoParaMarcar = null;
        _campoPropioActivoParaMarcar = null;
        _datoEnlazanteActivoParaMarcar = dato;
        Visor.IniciarMarcado();
        dato.Incluido = true;
        TxtInstruccionPaso.Text = $"Dibuja un rectángulo donde aparece: {dato.Nombre}.";
    }

    private void DatoIncluido_Changed(object sender, RoutedEventArgs e)
    {
        if (
            sender is System.Windows.Controls.CheckBox
            {
                DataContext: DatoEnlazanteEdicion dato,
                IsChecked: false
            }
        )
        {
            dato.Marcado = false;
            dato.Estado = "No aparece en este diseño.";
            dato.DefineTipo = false;
            _tipo = TipoDerivado();
            ActualizarNombreEstandarSugerido();
        }
        ActualizarMarcasEnVisor();
    }

    private void CargarCamposPropios(IEnumerable<CampoPropio> campos)
    {
        _camposPropios.Clear();
        foreach (var campo in campos)
        {
            var texto = LectorPdf.ExtraerTexto(
                _rutaArchivo,
                campo.Pagina,
                new(campo.X, campo.Y, campo.Ancho, campo.Alto)
            );
            var edicion = CrearCampoPropioEdicion(
                campo.Nombre,
                campo.NombreEstable,
                string.IsNullOrWhiteSpace(texto)
                    ? "Zona guardada; no se pudo leer un valor de ejemplo."
                    : $"Valor de ejemplo: \"{texto}\" (página {campo.Pagina + 1})."
            );
            edicion.Marcado = true;
            edicion.Pagina = campo.Pagina;
            edicion.X = campo.X;
            edicion.Y = campo.Y;
            edicion.Ancho = campo.Ancho;
            edicion.Alto = campo.Alto;
            _camposPropios.Add(edicion);
        }
    }

    private void BtnAgregarCampoPropio_Click(object sender, RoutedEventArgs e) =>
        _camposPropios.Add(CrearCampoPropioEdicion(string.Empty, $"campo_{Guid.NewGuid():N}"));

    private CampoPropioEdicion CrearCampoPropioEdicion(
        string nombre,
        string nombreEstable,
        string estado = "Todavía no marcado."
    )
    {
        var campo = new CampoPropioEdicion(nombre, nombreEstable, estado);
        campo.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CampoPropioEdicion.Nombre))
                ActualizarMarcasEnVisor();
        };
        return campo;
    }

    private void BtnQuitarCampoPropio_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: CampoPropioEdicion campo })
        {
            _camposPropios.Remove(campo);
            if (ReferenceEquals(_campoPropioActivoParaMarcar, campo))
                _campoPropioActivoParaMarcar = null;
            ActualizarMarcasEnVisor();
        }
    }

    private void BtnMarcarCampoPropio_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CampoPropioEdicion campo })
            return;
        if (string.IsNullOrWhiteSpace(campo.Nombre))
        {
            MostrarError("Escribe el nombre del dato antes de marcar su zona.");
            return;
        }

        _campoActivoParaMarcar = null;
        ResaltarBotonActivo(null);
        _campoPropioActivoParaMarcar = campo;
        Visor.IniciarMarcado();
        TxtInstruccionPaso.Text =
            $"Dibujar un rectángulo sobre el PDF donde aparece: {campo.Nombre.Trim()}.";
    }

    private void ActualizarCampoPropioMarcado(
        int pagina,
        RectanguloFraccion fraccion,
        CampoPropioEdicion campo
    )
    {
        var texto = LectorPdf.ExtraerTexto(_rutaArchivo, pagina, fraccion);
        campo.Marcado = true;
        campo.Pagina = pagina;
        campo.X = fraccion.X;
        campo.Y = fraccion.Y;
        campo.Ancho = fraccion.Ancho;
        campo.Alto = fraccion.Alto;
        campo.Estado = string.IsNullOrWhiteSpace(texto)
            ? $"Zona marcada en la página {pagina + 1}, pero no se pudo leer texto."
            : $"Valor de ejemplo: \"{texto}\" (página {pagina + 1}).";
        ActualizarMarcasEnVisor();
    }

    private List<CampoPropio> LeerCamposPropiosValidados()
    {
        var campos = new List<CampoPropio>();
        var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var campo in _camposPropios)
        {
            var nombre = campo.Nombre.Trim();
            if (string.IsNullOrWhiteSpace(nombre) || !campo.Marcado)
                throw new InvalidOperationException(
                    "Cada dato propio necesita un nombre y una zona marcada en el PDF."
                );
            if (!nombres.Add(nombre))
                throw new InvalidOperationException(
                    $"El nombre de dato \"{nombre}\" está repetido."
                );
            campos.Add(
                new(
                    nombre,
                    campo.NombreEstable,
                    campo.Pagina,
                    campo.X,
                    campo.Y,
                    campo.Ancho,
                    campo.Alto
                )
            );
        }
        return campos;
    }

    private void ActualizarEstadosDeMarca()
    {
        TxtEstadoMarcaEmisor.Text = EstadoTexto(CampoMarca.Emisor);
        TxtEstadoMarcaTitulo.Text = EstadoTexto(CampoMarca.Tipo);
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
        ActualizarNombreEstandarSugerido();
    }

    private void ActualizarNombreEstandarSugerido()
    {
        if (_nombreEstandarEditado || TxtNombreEstandar is null)
            return;
        _actualizandoNombreEstandar = true;
        try
        {
            TxtNombreEstandar.Text = DatoNumero is { } dato
                ? AsistenteClasificacionService.NombreEstandar(dato.Id, CmbEmisor.Text)
                : string.Empty;
        }
        finally
        {
            _actualizandoNombreEstandar = false;
        }
    }

    private void ConfigurarListaDatosInformativos()
    {
        ListaDatosInformativos.ItemsSource = _datosInformativos;
    }

    private void CargarDatosInformativos(string emisor, string tipo, int patronId)
    {
        _datosInformativos.Clear();
        var zonas = DatosEnlazantesConfiguracionService.LeerInformativos(emisor, tipo, patronId);
        foreach (
            var (id, nombre) in new[]
            {
                ("fecha_documento", "Fecha del documento"),
                ("encargado", "Encargado"),
                ("nombre_cliente", "Nombre de cliente"),
            }
        )
        {
            var dato = new DatoInformativoEdicion(
                id,
                nombre,
                zonas.FirstOrDefault(z => z.Dato == id)
            );
            if (dato.Marcado)
            {
                string ejemplo = LectorPdf.ExtraerTexto(
                    _rutaArchivo,
                    dato.Pagina,
                    new(dato.X, dato.Y, dato.Ancho, dato.Alto)
                );
                dato.Estado = string.IsNullOrWhiteSpace(ejemplo)
                    ? "Zona guardada; no se pudo leer el ejemplo."
                    : $"Valor de ejemplo: {ejemplo}";
            }
            _datosInformativos.Add(dato);
        }
    }

    private void BtnMarcarDatoInformativo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: DatoInformativoEdicion dato })
            return;
        _datoEnlazanteActivoParaMarcar = null;
        _campoPropioActivoParaMarcar = null;
        _datoInformativoActivoParaMarcar = dato;
        Visor.IniciarMarcado();
        dato.Marcado = true;
        TxtInstruccionPaso.Text = $"Dibuja un rectángulo donde aparece: {dato.Nombre}.";
    }

    private void DatoInformativo_Changed(object sender, RoutedEventArgs e)
    {
        if (
            sender is System.Windows.Controls.CheckBox
            {
                DataContext: DatoInformativoEdicion dato,
                IsChecked: false
            }
        )
        {
            dato.Marcado = false;
            dato.Estado = "No aparece en este diseño.";
        }
        ActualizarMarcasEnVisor();
    }

    private void DatoEnlazable_Enlazable_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox { DataContext: DatoEnlazanteEdicion dato })
            ActualizarSugerencia(dato);
    }

    private void ActualizarSugerencia(DatoEnlazanteEdicion dato)
    {
        if (!dato.Marcado || !dato.Enlazable)
            return;
        string texto = LectorPdf.ExtraerTexto(
            _rutaArchivo,
            dato.Pagina,
            new(dato.X, dato.Y, dato.Ancho, dato.Alto)
        );
        if (string.IsNullOrWhiteSpace(texto))
            return;
        using var conexion = Hormiguero.Nucleo.Datos.BaseComun.Abrir(
            Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun
        );
        var coincidencias = new Hormiguero.Nucleo.Datos.RepositorioDatosEnlazantes(
            conexion
        ).SugerirCalce(dato.Id, texto, 4);
        dato.Estado =
            $"Texto leído: {texto}. "
            + (
                coincidencias.Count == 0
                    ? "Aún no hay documentos con este número."
                    : "Calzaría con: "
                        + string.Join(
                            "; ",
                            coincidencias
                                .Take(3)
                                .Select(c =>
                                    $"{c.NombreEstandar} N° {c.Valor}{(c.FechaDocumento is null ? "" : $" ({c.FechaDocumento:dd-MM-yyyy})")}"
                                )
                        )
                        + (coincidencias.Count > 3 ? $"; y {coincidencias.Count - 3} más" : "")
            );
    }

    private string TipoDerivado()
    {
        var id = _datosEnlazantes.FirstOrDefault(d => d.Incluido && d.DefineTipo)?.Id;
        return id is null
            ? string.Empty
            : Hormiguero
                .Nucleo.Datos.DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == id)
                .EtiquetaTipo;
    }

    private void BtnElegirCarpeta_Click(object sender, RoutedEventArgs e)
    {
        using var dialogo = new System.Windows.Forms.FolderBrowserDialog
        {
            Description =
                "Elegir la carpeta madre: la raíz donde va a vivir todo lo de este tipo de documento",
        };

        if (dialogo.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtCarpetaDestino.Text = dialogo.SelectedPath;
            if (PanelPreview.Visibility == Visibility.Visible)
                ActualizarPreview();
        }
    }

    // Todo el paso 5 está en una pantalla: la organización de subcarpetas solo se muestra si se
    // eligió guardar en subcarpetas, y la vista previa se actualiza sola.
    private void RbGuardar_Checked(object sender, RoutedEventArgs e)
    {
        if (PanelOrganizacion is null || PanelPreview is null)
            return;
        PanelOrganizacion.Visibility =
            _paso == Paso.Guardar && RbGuardarSubcarpetas.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        if (PanelPreview.Visibility == Visibility.Visible)
            ActualizarPreview();
    }

    // ----- Paso 3 (Caso-3): organización de subcarpetas -----
    // El tipo/patrón (accesos rápidos, lista completa, ejemplos) vive en ControlOrganizacion
    // (Caso-4 lo reutiliza tal cual); aquí solo queda lo propio de esta ventana: cuándo mostrarlo
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
            ControlOrganizacion.Iniciar(
                _configuracionExistente.FormatoCarpeta,
                _configuracionExistente.PatronCarpeta,
                bloqueado: true
            );
        }
        else
        {
            ControlOrganizacion.Iniciar(
                _tipoOrganizacionPrecargado,
                _patronPrecargadoParaControl,
                bloqueado: false
            );
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
        PanelMarcarFecha.Visibility = ControlOrganizacion.FechaEsAplicable
            ? Visibility.Visible
            : Visibility.Collapsed;
        TxtFechaOpcional.Visibility = ControlOrganizacion.FechaEsOpcional
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OpcionNombre_Changed(object sender, RoutedEventArgs e)
    {
        PanelMarcarNombre.Visibility =
            RbExtraerNombre.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

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

    private void InicializarOpcionesImpresion()
    {
        _inicializandoImpresion = true;
        CmbModoImpresion.SelectedIndex = 0;
        CargarImpresoras();
        _inicializandoImpresion = false;
    }

    private void CargarImpresoras()
    {
        CmbImpresora.Items.Clear();
        string predeterminada = new System.Drawing.Printing.PrinterSettings().PrinterName;
        CmbImpresora.Items.Add(
            new ComboBoxItem
            {
                Content = string.IsNullOrWhiteSpace(predeterminada)
                    ? "Predeterminada de Windows"
                    : $"Predeterminada de Windows ({predeterminada})",
                Tag = "",
            }
        );
        foreach (string nombre in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
            CmbImpresora.Items.Add(new ComboBoxItem { Content = nombre, Tag = nombre });
        CmbImpresora.SelectedIndex = 0;
        CmbImpresora.IsEnabled =
            _modoImpresion is ModoImpresion.PrimeraPagina or ModoImpresion.TodoElDocumento;
    }

    private void CmbModoImpresion_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (
            _inicializandoImpresion
            || CmbModoImpresion.SelectedItem is not ComboBoxItem seleccionado
        )
            return;
        _modoImpresion = Enum.Parse<ModoImpresion>((string)seleccionado.Tag);
        CmbImpresora.IsEnabled =
            _modoImpresion is ModoImpresion.PrimeraPagina or ModoImpresion.TodoElDocumento;
    }

    private void AplicarOpcionesImpresion(ModoImpresion modo, string? impresora)
    {
        _inicializandoImpresion = true;
        _modoImpresion = modo;
        CmbModoImpresion.SelectedItem = CmbModoImpresion
            .Items.Cast<ComboBoxItem>()
            .First(item => (string)item.Tag == modo.ToString());
        CargarImpresoras();
        if (!string.IsNullOrWhiteSpace(impresora))
        {
            var opcionGuardada = CmbImpresora
                .Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item =>
                    string.Equals((string)item.Tag, impresora, StringComparison.OrdinalIgnoreCase)
                );
            if (opcionGuardada is null)
            {
                opcionGuardada = new ComboBoxItem
                {
                    Content = $"{impresora} (no disponible)",
                    Tag = impresora,
                };
                CmbImpresora.Items.Add(opcionGuardada);
            }
            CmbImpresora.SelectedItem = opcionGuardada;
            _impresora = impresora;
        }
        else
        {
            _impresora = null;
        }
        _inicializandoImpresion = false;
    }

    private void LeerImpresoraElegida()
    {
        _impresora =
            CmbImpresora.SelectedItem is ComboBoxItem item
            && !string.IsNullOrWhiteSpace((string?)item.Tag)
                ? (string)item.Tag
                : null;
    }

    private void CmbImpresora_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_inicializandoImpresion)
            LeerImpresoraElegida();
    }

    private void AplicarConfiguracionExistente(ConfiguracionDocumento existente)
    {
        _configuracionExistente = existente;
        RbEmitido.IsChecked = existente.GrupoDocumento == "Emitido";
        RbRecibido.IsChecked = existente.GrupoDocumento == "Recibido";
        TxtNombreEstandar.Text = string.IsNullOrWhiteSpace(existente.NombreEstandar)
            ? $"{existente.Tipo} · {existente.Emisor}"
            : existente.NombreEstandar;
        CargarCamposPropios(existente.CamposPropios);
        CargarDatosEnlazantes(string.Empty, string.Empty, 0);
        ActualizarOpcionesMigracion();
        ActualizarMarcasEnVisor();
        _carpetaDestino = existente.CarpetaDestino;
        _formato = existente.FormatoCarpeta;
        _patronCarpeta = existente.PatronCarpeta;
        _renombrar = existente.Renombrar;
        _preguntarNombre = existente.PreguntarNombre;
        _abrirDespuesDeGuardar = existente.AbrirDespuesDeGuardar;
        AplicarOpcionesImpresion(existente.ModoImpresion, existente.Impresora);

        TxtCarpetaDestino.Text = _carpetaDestino;
        BtnElegirCarpeta.IsEnabled = false;

        RbGuardarDirecto.IsChecked = _formato == FormatoCarpeta.Directo;
        RbGuardarSubcarpetas.IsChecked = _formato != FormatoCarpeta.Directo;
        RbGuardarDirecto.IsEnabled = RbGuardarSubcarpetas.IsEnabled = false;

        MarcarOpcionNombre();
        RbMantenerNombre.IsEnabled =
            RbExtraerNombre.IsEnabled =
            RbPreguntarNombre.IsEnabled =
                false;

        ChkAbrirDespuesDeGuardar.IsChecked = _abrirDespuesDeGuardar;
        ChkAbrirDespuesDeGuardar.IsEnabled = false;
    }

    private void BtnActualizarPreview_Click(object sender, RoutedEventArgs e) =>
        ActualizarPreview();

    /// <summary>
    /// Preview obligatorio de anterior/actual/futuro (Caso-1, punto 3): usa el mismo calculo que
    /// se usaria para guardar de verdad, con los datos que ya esten disponibles en este momento
    /// del asistente (el nombre de archivo final recien se conoce en el Paso 4).
    /// </summary>
    private void ActualizarPreview()
    {
        var formato = LeerFormatoElegido() ?? _formato;
        var patron = LeerPatronElegido() ?? (formato == _formato ? _patronCarpeta : null);
        var carpetaDestino =
            _configuracionExistente?.CarpetaDestino
            ?? (
                string.IsNullOrWhiteSpace(_carpetaDestino)
                    ? TxtCarpetaDestino.Text
                    : _carpetaDestino
            );

        if (string.IsNullOrWhiteSpace(carpetaDestino))
        {
            TxtPreviewActual.Text = "(Elige la carpeta madre para ver aquí dónde quedará.)";
            return;
        }

        if (formato != FormatoCarpeta.Directo && string.IsNullOrWhiteSpace(patron))
        {
            TxtPreviewAnterior.Text = "—";
            TxtPreviewActual.Text =
                "(Elige el tipo de organización y un ejemplo de patrón para verlo aquí.)";
            TxtPreviewFuturaTitulo.Visibility = Visibility.Collapsed;
            TxtPreviewFutura.Visibility = Visibility.Collapsed;
            return;
        }

        var (fecha, fechaEsSupuesta) = LeerFechaPreview();
        var nombreArchivo = LeerNombreArchivoPreview();

        var configuracionTemporal = new ConfiguracionDocumento
        {
            Emisor = _emisor,
            Tipo = _tipo,
            CarpetaDestino = carpetaDestino,
            FormatoCarpeta = formato,
            PatronCarpeta = patron,
            Renombrar = false,
            Patrones = [],
        };

        TxtPreviewActual.Text =
            ClasificadorService.CalcularRutaDestino(
                nombreArchivo,
                configuracionTemporal,
                fecha,
                null
            )
            + (
                fechaEsSupuesta
                    ? "\n(usando la fecha de hoy: todavía no se marcó o no se pudo leer la fecha del documento)"
                    : string.Empty
            );

        var anterior = FormatoCarpetaService.BuscarCarpetaAnteriorReal(
            carpetaDestino,
            formato,
            patron
        );
        TxtPreviewAnterior.Text =
            anterior ?? "(Todavía no hay ninguna carpeta así en el disco — esta sería la primera.)";

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
            var subcarpetaFutura = FormatoCarpetaService.ConstruirSubcarpeta(
                formato,
                patron,
                fechaFutura
            );
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
        if (
            _marcas.TryGetValue(CampoMarca.Fecha, out var marca)
            && !string.IsNullOrWhiteSpace(marca.TextoReferencia)
            && FechaExtraidaService.TryParsear(marca.TextoReferencia, out var fecha)
        )
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
        var extrayendoNombre =
            _configuracionExistente?.Renombrar ?? (RbExtraerNombre.IsChecked == true);

        if (
            extrayendoNombre
            && _marcas.TryGetValue(CampoMarca.NombreArchivo, out var marca)
            && !string.IsNullOrWhiteSpace(marca.TextoReferencia)
        )
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
            _ => OrganizacionCarpetaService.NombreDe(_formato),
        };
        var nombreTexto =
            _renombrar ? "se extrae del campo marcado en el PDF"
            : _preguntarNombre ? "se pregunta cada vez, antes de guardar"
            : "se mantiene el nombre original";

        // Al editar no se guarda ningún documento, así que no hay nombre que pedir aquí.
        var pedirNombreAhora = _preguntarNombre && _edicion is null;
        PanelNombreEsteDocumento.Visibility = pedirNombreAhora
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (pedirNombreAhora && string.IsNullOrWhiteSpace(TxtNombreEsteDocumento.Text))
        {
            TxtNombreEsteDocumento.Text = System.IO.Path.GetFileNameWithoutExtension(_rutaArchivo);
        }

        var encabezado =
            _edicion is not null
                ? "Se van a actualizar las marcas y la configuración de este patrón (no se mueve ningún archivo):\n\n"
            : _configuracionExistente is not null
                ? "Este documento se va a vincular a la configuración existente (se agrega como patrón de reconocimiento adicional):\n\n"
            : string.Empty;

        TxtResumen.Text =
            encabezado
            + $"Categoría: {CmbCategoriaDocumento.SelectedItem}\n"
            + $"Emisor: {_emisor}\n"
            + $"Tipo: {_tipo}\n"
            + $"Grupo: {(RbEmitido.IsChecked == true ? "Emitido" : "Recibido")}\n"
            + $"Número: {(_datosEnlazantes.FirstOrDefault(d => d.DefineTipo)?.ValorLeido is { Length: > 0 } numero ? numero : "no informado")}\n"
            + $"Carpeta madre: {_carpetaDestino}\n"
            + $"Subcarpetas: {formatoTexto}\n"
            + $"Nombre de archivo: {nombreTexto}";
    }

    private void BtnSiguiente_Click(object sender, RoutedEventArgs e)
    {
        switch (_paso)
        {
            case Paso.QueDocumento:
                _tipo = TipoDerivado();
                if (AsistenteClasificacionService.ValidarDocumento(_tipo) is not null)
                {
                    MostrarError("Elige un documento de la lista.");
                    return;
                }
                ActualizarNombreEstandarSugerido();
                IrA(Paso.Emisor);
                break;

            case Paso.Emisor:
                _emisor = CmbEmisor.Text.Trim();
                var errorEmisor = AsistenteClasificacionService.ValidarEmisor(
                    _emisor,
                    _marcas.ContainsKey(CampoMarca.Emisor)
                );
                if (errorEmisor is not null)
                {
                    MostrarError(errorEmisor);
                    return;
                }
                // Sin el título marcado, CoincidenciaAutomaticaService nunca reconoce el documento
                // (exige Emisor y Tipo): el tipo ya no se escribe, pero su texto fijo sí se marca.
                if (
                    !_marcas.TryGetValue(CampoMarca.Tipo, out var marcaTitulo)
                    || string.IsNullOrWhiteSpace(marcaTitulo.TextoReferencia)
                )
                {
                    MostrarError(
                        "Marca el título del documento (el texto fijo que dice qué es, por ejemplo FACTURA ELECTRÓNICA). Sin él, Archivero no puede reconocerlo solo."
                    );
                    return;
                }
                if (
                    AsistenteClasificacionService
                        .GrupoDocumento(
                            CmbCategoriaDocumento.SelectedItem as string ?? string.Empty
                        )
                        .Length == 0
                    && RbEmitido.IsChecked != true
                    && RbRecibido.IsChecked != true
                )
                {
                    MostrarError("Indica si este documento se emite o se recibe.");
                    return;
                }

                // Caso-9, mejora 1: Emisor y Tipo pueden terminar formando parte de un nombre o
                // ruta más adelante -- se validan/sanean aquí, apenas se escriben.
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

                if (_modoObservador)
                {
                    var configuracionObservadorExistente = _configuraciones
                        .ObtenerTodasConPatronesParaObservador()
                        .FirstOrDefault(c => c.Emisor == _emisor && c.Tipo == _tipo);
                    var configuracionNormalExistente = _configuraciones.BuscarPorEmisorYTipo(
                        _emisor,
                        _tipo
                    );
                    if (configuracionNormalExistente is not null)
                    {
                        MostrarError(
                            "Ya existe una configuración normal para este emisor y tipo. No se puede usar también como diseño de carpeta observada sin cambiar su uso en Archivero."
                        );
                        return;
                    }
                    _configuracionExistente = configuracionObservadorExistente;
                }

                if (_edicion is null && !_modoObservador)
                {
                    var configuracionExistente = _configuraciones.BuscarPorEmisorYTipo(
                        _emisor,
                        _tipo
                    );
                    if (configuracionExistente is not null)
                    {
                        var vincular = System.Windows.MessageBox.Show(
                            this,
                            $"Ya existe una configuración guardada para \"{_emisor}\" / \"{_tipo}\".\n\n"
                                + "¿Vincular este documento a esa configuración como un patrón de reconocimiento adicional? "
                                + "(útil si el proveedor cambió el diseño del documento). Se va a usar la misma carpeta de destino, "
                                + "formato y regla de nombre de archivo ya definidos.",
                            "Archivero",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question
                        );

                        if (vincular != MessageBoxResult.Yes)
                        {
                            MostrarError(
                                "Corregir el Emisor o el Tipo si no correspondía, o cancelar la identificación."
                            );
                            return;
                        }

                        AplicarConfiguracionExistente(configuracionExistente);
                    }
                    else
                    {
                        _configuracionExistente = null;
                    }
                }

                IrA(Paso.Numero);
                break;

            case Paso.Numero:
                var identificador = _datosEnlazantes.FirstOrDefault(d =>
                    d.DefineTipo && d.Incluido
                );
                if (identificador is null)
                {
                    MostrarError(
                        "Vuelve al primer paso, elige el documento y marca su número en el PDF."
                    );
                    return;
                }
                var errorNumero = AsistenteClasificacionService.ValidarNumero(
                    identificador.ValorLeido,
                    identificador.Marcado,
                    ChkSinNumero.IsChecked == true
                );
                if (errorNumero is not null)
                {
                    MostrarError(errorNumero);
                    return;
                }
                IrA(Paso.OtrosDatos);
                break;

            case Paso.OtrosDatos:
                foreach (var dato in _datosEnlazantes.Where(d => d.Incluido && !d.Marcado))
                {
                    MostrarError($"Marca dónde aparece «{dato.Nombre}» o desmarca ese dato.");
                    return;
                }
                if (_modoObservador)
                {
                    _carpetaDestino = _carpetaObservada ?? string.Empty;
                    _formato = FormatoCarpeta.Directo;
                    _patronCarpeta = null;
                    _renombrar = false;
                    _preguntarNombre = false;
                    MostrarResumen();
                    TxtResumen.Text =
                        $"Categoría: {CmbCategoriaDocumento.SelectedItem}\nEmisor: {_emisor}\nTipo: {_tipo}\nNúmero: {(_datosEnlazantes.FirstOrDefault(d => d.DefineTipo)?.ValorLeido is { Length: > 0 } numero ? numero : "no informado")}\nSe queda en la carpeta del proveedor; Archivero solo lo registra en Hormiguero.";
                    IrA(Paso.Resumen);
                }
                else
                {
                    IrA(Paso.Guardar);
                }
                break;

            case Paso.Guardar:
                if (
                    _configuracionExistente is null
                    && string.IsNullOrWhiteSpace(TxtCarpetaDestino.Text)
                )
                {
                    MostrarError("Elige la carpeta donde se guardarán estos documentos.");
                    return;
                }
                if (_configuracionExistente is null)
                {
                    _carpetaDestino = TxtCarpetaDestino.Text;
                    if (!Directory.Exists(_carpetaDestino))
                    {
                        MostrarError(
                            "La carpeta elegida ya no existe. Elige una carpeta disponible."
                        );
                        return;
                    }
                    if (
                        RbGuardarDirecto.IsChecked != true
                        && RbGuardarSubcarpetas.IsChecked != true
                    )
                    {
                        MostrarError("Elige guardar directo o dentro de subcarpetas.");
                        return;
                    }
                    if (RbGuardarDirecto.IsChecked == true)
                    {
                        _formato = FormatoCarpeta.Directo;
                        _patronCarpeta = null;
                    }
                }
                if (_configuracionExistente is null)
                {
                    if (
                        _formato != FormatoCarpeta.Directo
                        && !ControlOrganizacion.Validar(out var errorOrganizacion)
                    )
                    {
                        MostrarError(errorOrganizacion!);
                        return;
                    }
                    if (_formato != FormatoCarpeta.Directo)
                    {
                        _formato = ControlOrganizacion.FormatoElegido!.Value;
                        _patronCarpeta = ControlOrganizacion.PatronElegido;
                    }
                }

                // La marca de fecha es obligatoria salvo para "directo en la carpeta" (SPEC
                // REQ-003) o para los dos tipos donde Caso-3 (punto 3d) la hace opcional.
                if (
                    _formato != FormatoCarpeta.Directo
                    && _formato is not (FormatoCarpeta.MesSinAnio or FormatoCarpeta.SemanaDelMes)
                    && !_marcas.ContainsKey(CampoMarca.Fecha)
                )
                {
                    MostrarError(
                        "Marca la fecha en el PDF para crear las subcarpetas por período."
                    );
                    return;
                }
                if (_configuracionExistente is null)
                {
                    if (
                        RbMantenerNombre.IsChecked != true
                        && RbExtraerNombre.IsChecked != true
                        && RbPreguntarNombre.IsChecked != true
                    )
                    {
                        MostrarError("Elegir cómo se va a llamar el archivo.");
                        return;
                    }

                    _renombrar = RbExtraerNombre.IsChecked == true;
                    _preguntarNombre = RbPreguntarNombre.IsChecked == true;
                }

                if (_renombrar && !_marcas.ContainsKey(CampoMarca.NombreArchivo))
                {
                    MostrarError(
                        "Marcar en el PDF el campo que se va a usar como nombre de archivo."
                    );
                    return;
                }

                MostrarResumen();
                IrA(Paso.Resumen);
                break;

            case Paso.Resumen:
                GuardarYClasificar();
                break;
        }
    }

    private void GuardarYClasificar()
    {
        try
        {
            AplicarVinculosDatosAnteriores();
            var marcas = _marcas.Values.ToList();
            var grupoDocumento = RbEmitido.IsChecked == true ? "Emitido" : "Recibido";
            var nombreEstandar = string.IsNullOrWhiteSpace(TxtNombreEstandar.Text)
                ? $"{_tipo.Trim()} · {_emisor.Trim()}"
                : TxtNombreEstandar.Text.Trim();

            if (_modoObservador)
            {
                if (
                    string.IsNullOrWhiteSpace(_carpetaObservada)
                    || !Directory.Exists(_carpetaObservada)
                )
                {
                    MostrarError("La carpeta observada ya no está disponible.");
                    return;
                }
                int configuracionId;
                int patronId;
                if (_configuracionExistente is not null)
                {
                    configuracionId = _configuracionExistente.Id;
                    _configuraciones.ActualizarDestino(
                        configuracionId,
                        _carpetaObservada,
                        FormatoCarpeta.Directo,
                        null,
                        false,
                        false,
                        false
                    );
                    _configuraciones.AgregarPatronAConfiguracionExistente(configuracionId, marcas);
                    var recargada = _configuraciones
                        .ObtenerTodasConPatronesParaObservador()
                        .Single(c => c.Id == configuracionId);
                    patronId = recargada.Patrones.Last().Id;
                }
                else
                {
                    configuracionId = _configuraciones.GuardarNueva(
                        _emisor,
                        _tipo,
                        _carpetaObservada,
                        FormatoCarpeta.Directo,
                        null,
                        false,
                        marcas
                    );
                    var guardada = _configuraciones
                        .ObtenerTodasConPatronesParaObservador()
                        .Single(c => c.Id == configuracionId);
                    patronId = guardada.Patrones.Last().Id;
                }
                _configuraciones.ActualizarTipoDocumento(
                    configuracionId,
                    grupoDocumento,
                    nombreEstandar
                );
                DatosEnlazantesConfiguracionService.Guardar(
                    _emisor,
                    _tipo,
                    grupoDocumento,
                    nombreEstandar,
                    patronId,
                    _datosEnlazantes.Select(d => d.AConfigurado()).ToList(),
                    _datosInformativos.Where(d => d.Marcado).Select(d => d.ComoZona()).ToList()
                );
                _configuraciones.MarcarSoloObservador(configuracionId);
                ConfiguracionDocumentoId = configuracionId;
                _draftYaResuelto = true;
                DialogResult = true;
                Close();
                return;
            }

            DateTime? fecha = null;
            if (_marcas.TryGetValue(CampoMarca.Fecha, out var marcaFecha))
            {
                var textoFecha = LectorPdf.ExtraerTexto(
                    _rutaArchivo,
                    marcaFecha.Pagina,
                    new RectanguloFraccion(
                        marcaFecha.X,
                        marcaFecha.Y,
                        marcaFecha.Ancho,
                        marcaFecha.Alto
                    )
                );

                if (!FechaExtraidaService.TryParsear(textoFecha, out var fechaParseada))
                {
                    MostrarError(
                        $"No se pudo interpretar la fecha extraída (\"{textoFecha}\"). Revisar la marca sobre el PDF."
                    );
                    return;
                }

                fecha = fechaParseada;
            }

            string? nombreExtraido = null;
            if (_renombrar && _marcas.TryGetValue(CampoMarca.NombreArchivo, out var marcaNombre))
            {
                nombreExtraido = LectorPdf.ExtraerTexto(
                    _rutaArchivo,
                    marcaNombre.Pagina,
                    new RectanguloFraccion(
                        marcaNombre.X,
                        marcaNombre.Y,
                        marcaNombre.Ancho,
                        marcaNombre.Alto
                    )
                );

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
                _configuraciones.ActualizarDestino(
                    edicion.Configuracion.Id,
                    _carpetaDestino,
                    _formato,
                    _patronCarpeta,
                    _renombrar,
                    _abrirDespuesDeGuardar,
                    _preguntarNombre
                );
                _configuraciones.GuardarCamposPropios(edicion.Configuracion.Id, []);
                _configuraciones.ActualizarTipoDocumento(
                    edicion.Configuracion.Id,
                    grupoDocumento,
                    nombreEstandar
                );
                _configuraciones.ActualizarTipoDerivado(edicion.Configuracion.Id, _tipo);
                DatosEnlazantesConfiguracionService.Guardar(
                    _emisor,
                    _tipo,
                    grupoDocumento,
                    nombreEstandar,
                    edicion.PatronId,
                    _datosEnlazantes.Select(d => d.AConfigurado()).ToList(),
                    _datosInformativos.Where(d => d.Marcado).Select(d => d.ComoZona()).ToList(),
                    edicion.Configuracion.Tipo
                );
                DatosEnlazantesConfiguracionService.VincularCamposAnteriores(
                    _emisor,
                    _tipo,
                    edicion.PatronId,
                    LeerVinculosDatosAnteriores()
                );
                AuditoriaService.Registrar(
                    "CLASIFICACION_EDITADA",
                    $"Emisor={_emisor}; Tipo={_tipo}"
                );

                System.Windows.MessageBox.Show(
                    this,
                    "Cambios guardados.",
                    "Archivero",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                DialogResult = true;
                Close();
                return;
            }

            // Clasificar el archivo ANTES de guardar la configuracion: si algo falla aca
            // (fecha invalida, carpeta no disponible, nombre duplicado), no debe quedar una
            // configuracion a medias que despues choque con la restriccion de Emisor+Tipo
            // unico al reintentar.
            var configuracionParaClasificar =
                _configuracionExistente
                ?? new ConfiguracionDocumento
                {
                    Emisor = _emisor,
                    Tipo = _tipo,
                    CarpetaDestino = _carpetaDestino,
                    FormatoCarpeta = _formato,
                    PatronCarpeta = _patronCarpeta,
                    Renombrar = _renombrar,
                    AbrirDespuesDeGuardar = _abrirDespuesDeGuardar,
                    Patrones = [],
                };
            LeerImpresoraElegida();
            configuracionParaClasificar = configuracionParaClasificar with
            {
                ModoImpresion = _modoImpresion,
                Impresora = _impresora,
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

                configuracionParaClasificar = configuracionParaClasificar with
                {
                    Renombrar = true,
                    PreguntarNombre = false,
                };
                nombreExtraido = nombreEscrito;
            }

            string rutaFinal;
            try
            {
                rutaFinal = ClasificadorService.Clasificar(
                    _rutaArchivo,
                    configuracionParaClasificar,
                    fecha,
                    nombreExtraido
                );
            }
            catch (ArchivoDuplicadoException ex)
            {
                // REQ-002: ofrecer Revisar / Reemplazar / Dejar pendiente / Guardar como
                // excepcion, en vez de solo fallar.
                var resolver = new ResolverDuplicadoWindow(_rutaArchivo, ex.RutaDestino)
                {
                    Owner = this,
                };
                if (resolver.ShowDialog() != true)
                {
                    MostrarError(
                        "Documento dejado pendiente por nombre duplicado. Puedes posponer o cancelar, o intentar de nuevo."
                    );
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
            _configuraciones.AgregarPatronAConfiguracionExistente(
                _configuracionExistente.Id,
                marcas
            );
            var grupo = RbEmitido.IsChecked == true ? "Emitido" : "Recibido";
            var nombre = string.IsNullOrWhiteSpace(TxtNombreEstandar.Text)
                ? $"{_tipo.Trim()} · {_emisor.Trim()}"
                : TxtNombreEstandar.Text.Trim();
            _configuraciones.ActualizarTipoDocumento(_configuracionExistente.Id, grupo, nombre);
            _configuracionImpresion.Guardar(_configuracionExistente.Id, _modoImpresion, _impresora);
            var patronGuardado = _configuraciones
                .BuscarPorEmisorYTipo(_emisor, _tipo)!
                .Patrones.Last();
            DatosEnlazantesConfiguracionService.Guardar(
                _emisor,
                _tipo,
                grupo,
                nombre,
                patronGuardado.Id,
                _datosEnlazantes.Select(d => d.AConfigurado()).ToList(),
                _datosInformativos.Where(d => d.Marcado).Select(d => d.ComoZona()).ToList()
            );
            DatosEnlazantesConfiguracionService.VincularCamposAnteriores(
                _emisor,
                _tipo,
                patronGuardado.Id,
                LeerVinculosDatosAnteriores()
            );
            _configuraciones.GuardarCamposPropios(_configuracionExistente.Id, []);
            AuditoriaService.Registrar(
                "CLASIFICACION_VINCULADA",
                $"Emisor={_emisor}; Tipo={_tipo}"
            );
        }
        else
        {
            var configuracionId = _configuraciones.GuardarNueva(
                _emisor,
                _tipo,
                _carpetaDestino,
                _formato,
                _patronCarpeta,
                _renombrar,
                marcas,
                _abrirDespuesDeGuardar,
                _preguntarNombre
            );
            _configuraciones.GuardarCamposPropios(configuracionId, []);
            _configuracionImpresion.Guardar(configuracionId, _modoImpresion, _impresora);
            _configuraciones.ActualizarTipoDocumento(
                configuracionId,
                RbEmitido.IsChecked == true ? "Emitido" : "Recibido",
                string.IsNullOrWhiteSpace(TxtNombreEstandar.Text)
                    ? $"{_tipo.Trim()} · {_emisor.Trim()}"
                    : TxtNombreEstandar.Text.Trim()
            );
            var guardada =
                _configuraciones.BuscarPorEmisorYTipo(_emisor, _tipo)
                ?? throw new InvalidOperationException("No se encontró la configuración guardada.");
            var patronGuardado =
                guardada.Patrones.LastOrDefault()
                ?? throw new InvalidOperationException("No se encontró el diseño PDF guardado.");
            DatosEnlazantesConfiguracionService.Guardar(
                _emisor,
                _tipo,
                RbEmitido.IsChecked == true ? "Emitido" : "Recibido",
                string.IsNullOrWhiteSpace(TxtNombreEstandar.Text)
                    ? $"{_tipo.Trim()} · {_emisor.Trim()}"
                    : TxtNombreEstandar.Text.Trim(),
                patronGuardado.Id,
                _datosEnlazantes.Select(d => d.AConfigurado()).ToList(),
                _datosInformativos.Where(d => d.Marcado).Select(d => d.ComoZona()).ToList()
            );
            DatosEnlazantesConfiguracionService.VincularCamposAnteriores(
                _emisor,
                _tipo,
                patronGuardado.Id,
                LeerVinculosDatosAnteriores()
            );
            AuditoriaService.Registrar("CLASIFICACION_CREADA", $"Emisor={_emisor}; Tipo={_tipo}");
        }

        if (_edicion is null)
        {
            _avisoImpresion = new ImpresionAlArchivarService(new AccionImpresionWindows()).Procesar(
                rutaFinal,
                (
                    _configuraciones.BuscarPorEmisorYTipo(_emisor, _tipo)
                    ?? throw new InvalidOperationException(
                        "No se encontró la configuración guardada."
                    )
                ) with
                {
                    ModoImpresion = _modoImpresion,
                    Impresora = _impresora,
                }
            );
        }

        string? errorPublicacion = null;
        if (PublicadorDatosDocumentoService.PublicacionAutomaticaActiva)
            try
            {
                var configuracion =
                    _configuraciones.BuscarPorEmisorYTipo(_emisor, _tipo)
                    ?? throw new InvalidOperationException(
                        "No se encontró la configuración guardada."
                    );
                var (campos, error) = GuardadoAutomaticoService.ExtraerCamposParaClasificar(
                    rutaFinal,
                    configuracion
                );
                if (campos is null)
                    throw new InvalidOperationException(error);
                PublicadorDatosDocumentoService.PublicarGuardado(rutaFinal, configuracion, campos);
            }
            catch (Exception error)
            {
                errorPublicacion =
                    $"No se pudieron publicar los datos en Hormiguero: {error.Message}";
                AuditoriaService.Registrar(
                    "PUBLICACION_HORMIGUERO_FALLIDA",
                    $"{rutaFinal}: {error.Message}"
                );
            }

        _pendientes.Quitar(_rutaArchivo);
        _borradores.Eliminar(_rutaArchivo);
        _draftYaResuelto = true;

        System.Windows.MessageBox.Show(
            this,
            errorPublicacion is null && _avisoImpresion is null
                ? $"Documento guardado en:\n{rutaFinal}"
                : $"Documento guardado en:\n{rutaFinal}\n\n{string.Join("\n", new[] { errorPublicacion, _avisoImpresion }.Where(x => !string.IsNullOrWhiteSpace(x)))}",
            "Archivero",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );

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
            this,
            mensaje,
            "Archivero",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

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
