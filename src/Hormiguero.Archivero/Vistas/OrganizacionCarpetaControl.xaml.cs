using System.Windows;
using System.Windows.Controls;
using Archivero.Datos;
using Archivero.Servicios;
using Button = System.Windows.Controls.Button;
using RadioButton = System.Windows.Controls.RadioButton;
using UserControl = System.Windows.Controls.UserControl;

namespace Archivero.Vistas;

/// <summary>
/// Paso 3 de Caso-3 (tipo de organización de subcarpetas + patrón con ejemplos concretos),
/// extraído a un control propio para que Caso-4 lo reutilice tal cual en el flujo de PDFs sin
/// texto extraíble ("no se reimplementa nada nuevo, se reutiliza el mismo componente/flujo").
///
/// Este control NO incluye el marcado de la fecha sobre el PDF (3d): cada ventana que lo usa
/// tiene su propio visor/mecanismo de marcado, así que solo expone <see cref="FechaEsAplicable"/>
/// y <see cref="FechaEsOpcional"/> para que la ventana dueña decida cuándo mostrar su propio
/// control de "marcar fecha", y <see cref="ConfigurarProveedorDeFecha"/> para que este control
/// sepa qué fecha de referencia usar en los ejemplos.
/// </summary>
public partial class OrganizacionCarpetaControl : UserControl
{
    private readonly AccesoRapidoRepository _accesosRapidos = new();

    private FormatoCarpeta? _tipoOrganizacion;
    private string? _patronSeleccionado;
    private FormatoCarpeta _tipoPendienteDeEnlazar;
    private bool _bloqueado;
    private Func<(DateTime Fecha, bool EsSupuesta)> _proveedorFecha = () => (DateTime.Now, true);

    /// <summary>Se dispara cada vez que cambia el tipo u patrón elegido, para que la ventana dueña actualice su propia vista previa.</summary>
    public event Action? SeleccionCambiada;

    public FormatoCarpeta? FormatoElegido => _tipoOrganizacion;

    public string? PatronElegido => LeerPatronElegido();

    public bool FechaEsAplicable => _tipoOrganizacion is not (null or FormatoCarpeta.Directo);

    public bool FechaEsOpcional => _tipoOrganizacion is { } tipo && OrganizacionCarpetaService.FechaEsOpcional(tipo);

    public OrganizacionCarpetaControl()
    {
        InitializeComponent();
    }

    /// <summary>La ventana dueña provee la fecha de referencia (la marcada en el documento, o la de hoy) para calcular los ejemplos.</summary>
    public void ConfigurarProveedorDeFecha(Func<(DateTime Fecha, bool EsSupuesta)> proveedor)
    {
        _proveedorFecha = proveedor;
    }

    /// <summary>
    /// Arranca el control: si ya hay un tipo elegido (edición, vinculación, o borrador
    /// restaurado), va directo a mostrar sus ejemplos; si no, arranca en los accesos rápidos.
    /// </summary>
    public void Iniciar(FormatoCarpeta? formatoInicial, string? patronInicial, bool bloqueado)
    {
        _bloqueado = bloqueado;
        _tipoOrganizacion = formatoInicial;
        _patronSeleccionado = patronInicial;

        if (_tipoOrganizacion is not null)
        {
            if (_tipoOrganizacion == FormatoCarpeta.Personalizado)
            {
                TxtPatronPersonalizado.Text = patronInicial ?? string.Empty;
            }

            RenderPanelPatron();
            MostrarSubpanel(mostrarPatron: true);
        }
        else
        {
            CargarAccesosRapidos();
            MostrarSubpanel(mostrarPatron: false);
        }
    }

    /// <summary>Recalcula los ejemplos con la fecha de referencia actual (Caso-3, punto 3d: al marcar la fecha del documento).</summary>
    public void RefrescarPorCambioDeFecha()
    {
        if (_tipoOrganizacion is not null)
        {
            RenderPanelPatron();
        }
    }

    public bool Validar(out string? error)
    {
        error = null;

        if (_tipoOrganizacion is null)
        {
            error = "Elegir un tipo de organización para las subcarpetas.";
            return false;
        }

        if (_tipoOrganizacion == FormatoCarpeta.Directo)
        {
            return true;
        }

        var patron = LeerPatronElegido();
        if (string.IsNullOrWhiteSpace(patron))
        {
            error = _tipoOrganizacion == FormatoCarpeta.Personalizado
                ? "Escribir el patrón personalizado."
                : "Elegir cuál de los ejemplos de patrón se parece a tus carpetas.";
            return false;
        }

        if (_tipoOrganizacion == FormatoCarpeta.Personalizado)
        {
            var (fechaReferencia, _) = _proveedorFecha();
            if (!OrganizacionCarpetaService.EsPatronValido(patron, fechaReferencia, out error))
            {
                return false;
            }
        }

        return true;
    }

    private void MostrarSubpanel(bool mostrarPatron)
    {
        PanelAccesosRapidos.Visibility = mostrarPatron ? Visibility.Collapsed : Visibility.Visible;
        PanelListaCompleta.Visibility = Visibility.Collapsed;
        PanelEnlazar.Visibility = Visibility.Collapsed;
        PanelPatron.Visibility = mostrarPatron ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Caso-6, punto 3: volver a elegir el tipo sin cancelar el asistente entero.</summary>
    private void BtnCambiarTipo_Click(object sender, RoutedEventArgs e)
    {
        CargarAccesosRapidos();
        MostrarSubpanel(mostrarPatron: false);
    }

    private void CargarAccesosRapidos()
    {
        ContenedorAccesosRapidos.Children.Clear();

        var accesos = _accesosRapidos.Obtener();
        TxtSinAccesosRapidos.Visibility = accesos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var acceso in accesos)
        {
            var boton = new Button
            {
                Content = OrganizacionCarpetaService.NombreDe(acceso),
                Tag = acceso,
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 6)
            };
            boton.Click += (_, _) => SeleccionarTipo((FormatoCarpeta)boton.Tag);
            ContenedorAccesosRapidos.Children.Add(boton);
        }
    }

    private void BtnVerTodas_Click(object sender, RoutedEventArgs e)
    {
        ContenedorListaCompleta.Children.Clear();

        foreach (var tipo in OrganizacionCarpetaService.TodosLosTipos.Concat([OrganizacionCarpetaService.OpcionPersonalizada]))
        {
            var boton = new Button
            {
                Content = tipo.Nombre,
                Tag = tipo.Formato,
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 6)
            };
            boton.Click += (_, _) => IniciarEnlazado((FormatoCarpeta)boton.Tag);
            ContenedorListaCompleta.Children.Add(boton);
        }

        PanelAccesosRapidos.Visibility = Visibility.Collapsed;
        PanelListaCompleta.Visibility = Visibility.Visible;
        PanelEnlazar.Visibility = Visibility.Collapsed;
        PanelPatron.Visibility = Visibility.Collapsed;
    }

    private void BtnCancelarListaCompleta_Click(object sender, RoutedEventArgs e)
    {
        PanelAccesosRapidos.Visibility = Visibility.Visible;
        PanelListaCompleta.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Caso-3, punto 3b: al elegir un tipo de la lista completa, preguntar a qué acceso rápido
    /// lo quiere enlazar (reemplazar uno actual, o agregarlo como nuevo — sin máximo, decisión
    /// de Javier). Si el tipo ya está enlazado, se continúa directo sin preguntar nada.
    /// </summary>
    private void IniciarEnlazado(FormatoCarpeta tipo)
    {
        var accesos = _accesosRapidos.Obtener();
        if (accesos.Contains(tipo))
        {
            SeleccionarTipo(tipo);
            return;
        }

        _tipoPendienteDeEnlazar = tipo;

        ContenedorReemplazos.Children.Clear();
        for (var i = 0; i < accesos.Count; i++)
        {
            var indice = i;
            var boton = new Button
            {
                Content = $"Reemplazar \"{OrganizacionCarpetaService.NombreDe(accesos[indice])}\"",
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 6)
            };
            boton.Click += (_, _) => EnlazarReemplazando(indice);
            ContenedorReemplazos.Children.Add(boton);
        }

        TxtTipoPendienteEnlazar.Text = $"Elegiste: {OrganizacionCarpetaService.NombreDe(tipo)}";
        TxtPreguntaEnlazar.Text = accesos.Count > 0
            ? "¿Con cuál de tus accesos rápidos actuales querés reemplazar esta opción?"
            : "¿Querés dejarla como acceso rápido para las próximas veces?";

        PanelListaCompleta.Visibility = Visibility.Collapsed;
        PanelEnlazar.Visibility = Visibility.Visible;
    }

    private void EnlazarReemplazando(int indice)
    {
        var accesos = _accesosRapidos.Obtener();
        if (indice >= 0 && indice < accesos.Count)
        {
            accesos[indice] = _tipoPendienteDeEnlazar;
            _accesosRapidos.Guardar(accesos);
        }

        SeleccionarTipo(_tipoPendienteDeEnlazar);
    }

    private void BtnAgregarAccesoNuevo_Click(object sender, RoutedEventArgs e)
    {
        var accesos = _accesosRapidos.Obtener();
        if (!accesos.Contains(_tipoPendienteDeEnlazar))
        {
            accesos.Add(_tipoPendienteDeEnlazar);
            _accesosRapidos.Guardar(accesos);
        }

        SeleccionarTipo(_tipoPendienteDeEnlazar);
    }

    private void BtnCancelarEnlazar_Click(object sender, RoutedEventArgs e)
    {
        PanelEnlazar.Visibility = Visibility.Collapsed;
        PanelListaCompleta.Visibility = Visibility.Visible;
    }

    private void SeleccionarTipo(FormatoCarpeta tipo)
    {
        if (_tipoOrganizacion != tipo)
        {
            _patronSeleccionado = null;
        }

        _tipoOrganizacion = tipo;
        RenderPanelPatron();
        MostrarSubpanel(mostrarPatron: true);
        SeleccionCambiada?.Invoke();
    }

    /// <summary>
    /// Arma la vista del punto 3c/3d: ejemplos concretos del tipo elegido (generados con la
    /// fecha de referencia real — la del documento si ya se marcó, o la de hoy mientras tanto),
    /// y el campo de patrón personalizado si corresponde.
    /// </summary>
    private void RenderPanelPatron()
    {
        var tipo = _tipoOrganizacion;
        if (tipo is null)
        {
            return;
        }

        var (fechaReferencia, fechaEsSupuesta) = _proveedorFecha();

        BtnCambiarTipo.Visibility = _bloqueado ? Visibility.Collapsed : Visibility.Visible;
        ContenedorEjemplos.Children.Clear();
        PanelPatronPersonalizado.Visibility = tipo == FormatoCarpeta.Personalizado ? Visibility.Visible : Visibility.Collapsed;
        TxtAvisoFechaHoy.Visibility = tipo != FormatoCarpeta.Directo && fechaEsSupuesta ? Visibility.Visible : Visibility.Collapsed;

        if (tipo == FormatoCarpeta.Directo)
        {
            TxtTipoElegido.Text = "Directo en la carpeta madre — sin subcarpetas.";
        }
        else if (tipo == FormatoCarpeta.Personalizado)
        {
            TxtTipoElegido.Text = "Patrón personalizado — escribilo vos a mano.";
            ActualizarEjemploPersonalizado();
        }
        else
        {
            TxtTipoElegido.Text = $"{OrganizacionCarpetaService.NombreDe(tipo.Value)} — ¿cuál de estos ejemplos se parece a lo que ya usás?";

            var ejemplos = OrganizacionCarpetaService.ObtenerEjemplos(tipo.Value, fechaReferencia);

            // En modo edición el patrón guardado puede ser uno que ya no está entre los ejemplos
            // (ej. uno detectado por evidencia antes de Caso-3, como "'Año 'yyyy"): se conserva
            // como opción adicional para no obligar a cambiarlo.
            if (_patronSeleccionado is not null && ejemplos.All(e => e.Patron != _patronSeleccionado))
            {
                ejemplos.Insert(0, new EjemploPatron(
                    _patronSeleccionado,
                    OrganizacionCarpetaService.FormatearEjemplo(_patronSeleccionado, fechaReferencia) + " (el actual)"));
            }

            foreach (var ejemplo in ejemplos)
            {
                var radio = new RadioButton
                {
                    Content = ejemplo.Texto,
                    Tag = ejemplo.Patron,
                    GroupName = "EjemplosPatron" + GetHashCode(),
                    Margin = new Thickness(0, 0, 0, 6),
                    IsEnabled = !_bloqueado
                };
                radio.Checked += Ejemplo_Checked;
                ContenedorEjemplos.Children.Add(radio);

                if (ejemplo.Patron == _patronSeleccionado || ejemplos.Count == 1)
                {
                    radio.IsChecked = true;
                }
            }
        }

        TxtPatronPersonalizado.IsEnabled = !_bloqueado;
    }

    private void Ejemplo_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string patron })
        {
            _patronSeleccionado = patron;
        }

        SeleccionCambiada?.Invoke();
    }

    private void TxtPatronPersonalizado_TextChanged(object sender, TextChangedEventArgs e)
    {
        ActualizarEjemploPersonalizado();
        SeleccionCambiada?.Invoke();
    }

    private void ActualizarEjemploPersonalizado()
    {
        var texto = TxtPatronPersonalizado.Text.Trim();
        if (texto.Length == 0)
        {
            TxtEjemploPersonalizado.Text = string.Empty;
            return;
        }

        var (fechaReferencia, _) = _proveedorFecha();
        var patron = OrganizacionCarpetaService.NormalizarPatronPersonalizado(texto);

        TxtEjemploPersonalizado.Text = OrganizacionCarpetaService.EsPatronValido(patron, fechaReferencia, out var error)
            ? "Ejemplo: " + OrganizacionCarpetaService.FormatearEjemplo(patron, fechaReferencia)
            : error;
    }

    private string? LeerPatronElegido()
    {
        if (_tipoOrganizacion is null or FormatoCarpeta.Directo)
        {
            return null;
        }

        if (_tipoOrganizacion == FormatoCarpeta.Personalizado)
        {
            var texto = TxtPatronPersonalizado.Text.Trim();
            return texto.Length == 0 ? null : OrganizacionCarpetaService.NormalizarPatronPersonalizado(texto);
        }

        return _patronSeleccionado;
    }
}
