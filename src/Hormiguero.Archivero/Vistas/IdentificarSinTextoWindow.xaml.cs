using System.IO;
using System.Windows;
using System.Windows.Controls;
using Archivero.Datos;
using Archivero.Servicios;

namespace Archivero.Vistas;

/// <summary>
/// Identificación de un PDF sin texto extraíble (Caso-4, reemplaza por completo el flujo del
/// Caso-1 punto 1): no hay Emisor/Tipo ni reconocimiento automático futuro -- solo elegir dónde
/// guardar, reutilizando el asistente de tipo/patrón de Caso-3 (<see cref="OrganizacionCarpetaControl"/>),
/// o volviendo a una ubicación ya usada antes.
///
/// Nota de implementación: Caso-4 pide "marcar la fecha sobre el PDF de la misma forma que ya se
/// hace para otros campos", pero un documento sin texto extraíble no tiene NADA que extraer de
/// una coordenada (por definición: <see cref="Archivero.Servicios.Pdf.LectorPdf.TieneTextoExtraible"/>
/// ya descartó el documento entero). Marcar un rectángulo ahí no puede producir un valor real, así
/// que la fecha se escribe a mano en un campo de texto en vez de marcarse por coordenadas.
/// </summary>
public partial class IdentificarSinTextoWindow : Window
{
    private class FilaUbicacion(UbicacionSinTexto ubicacion)
    {
        public UbicacionSinTexto Ubicacion { get; } = ubicacion;
        public string Texto { get; } = ubicacion.Formato == FormatoCarpeta.Directo
            ? ubicacion.CarpetaMadre
            : $"{ubicacion.CarpetaMadre} — {OrganizacionCarpetaService.NombreDe(ubicacion.Formato)}";
    }

    private class FilaAtajo(AtajoGuardadoRapido atajo)
    {
        public AtajoGuardadoRapido Atajo { get; } = atajo;
        public string Texto { get; } = atajo.Nombre;
        public string Detalle { get; } = atajo.Formato == FormatoCarpeta.Directo
            ? atajo.CarpetaMadre
            : $"{atajo.CarpetaMadre} — {OrganizacionCarpetaService.NombreDe(atajo.Formato)}";
    }

    /// <summary>Por qué camino se llegó al paso final: define si se ofrece guardar un atajo y si hace falta pedir la fecha ahí.</summary>
    private enum OrigenDestino
    {
        CrearNueva,
        UbicacionExistente,
        Atajo
    }

    private readonly string _rutaArchivo;
    private readonly PendienteRepository _pendientes = new();
    private readonly UbicacionSinTextoRepository _ubicaciones = new();
    private readonly AtajoGuardadoRapidoRepository _atajos = new();

    private string _carpetaMadre = string.Empty;
    private string? _carpetaDestinoFinal;
    private FormatoCarpeta _formato = FormatoCarpeta.Directo;
    private string? _patron;

    private OrigenDestino _origen;
    private string? _atajoUsado;
    private readonly List<OperacionNombre> _reglaNombre = [];
    private bool _preguntaAtajoHecha;
    private string? _nombreAtajoAGuardar;

    private int _nivelesTotales;
    private int _nivelActual;
    private string _carpetaNavegacionActual = string.Empty;

    public IdentificarSinTextoWindow(string rutaArchivo, bool esArchivoDanado = false)
    {
        InitializeComponent();
        _rutaArchivo = rutaArchivo;

        if (esArchivoDanado)
        {
            TxtTitulo.Text = "No se pudo leer este archivo";
            TxtInstruccion.Text = "Puede estar dañado, vacío o incompleto. Igual se puede elegir dónde guardarlo, " +
                                  "con lo que se sepa por el nombre del archivo.";
        }

        try
        {
            Visor.CargarPdf(rutaArchivo);
        }
        catch (Exception)
        {
            // Caso-11, punto 5: sin vista previa, pero el ruteo manual sigue disponible.
            Visor.Visibility = Visibility.Collapsed;
            AvisoSinVista.Visibility = Visibility.Visible;
            TxtAvisoSinVista.Text = $"{Path.GetFileName(rutaArchivo)}\n\nNo se puede mostrar el documento, " +
                                    "pero se puede guardar igual eligiendo una ubicación a la derecha.";
        }

        ControlOrganizacion.ConfigurarProveedorDeFecha(LeerFechaReferencia);

        CargarAtajos();
        MostrarPanel(PanelElegir);
    }

    private void CargarAtajos()
    {
        var atajos = _atajos.ObtenerTodos();
        TxtSinAtajos.Visibility = atajos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ListaAtajos.ItemsSource = atajos.Select(a => new FilaAtajo(a)).ToList();
    }

    private void MostrarPanel(FrameworkElement panel)
    {
        foreach (var p in new FrameworkElement[] { PanelElegir, PanelCarpetaMadre, PanelOrganizacion, PanelVerUbicaciones, PanelNavegar, PanelNombreArchivo })
        {
            p.Visibility = p == panel ? Visibility.Visible : Visibility.Collapsed;
        }

        TxtError.Visibility = Visibility.Collapsed;
    }

    // ----- 3a: Crear ubicación nueva -----

    private void BtnCrearUbicacionNueva_Click(object sender, RoutedEventArgs e) => MostrarPanel(PanelCarpetaMadre);

    private void BtnElegirCarpetaMadre_Click(object sender, RoutedEventArgs e)
    {
        using var dialogo = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Elegir la carpeta madre: la raíz donde va a vivir todo lo de este tipo de documento"
        };

        if (dialogo.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        _carpetaMadre = dialogo.SelectedPath;
        TxtCarpetaMadre.Text = _carpetaMadre;

        var tieneSubcarpetas = Directory.GetDirectories(_carpetaMadre).Length > 0;
        if (!tieneSubcarpetas)
        {
            var directo = System.Windows.MessageBox.Show(
                this,
                "Esta carpeta madre está vacía. ¿Guardar el documento directo ahí, sin subcarpetas?",
                "Archivero", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (directo == MessageBoxResult.Yes)
            {
                _carpetaDestinoFinal = _carpetaMadre;
                _formato = FormatoCarpeta.Directo;
                _patron = null;
                _ubicaciones.ObtenerOCrear(_carpetaMadre, FormatoCarpeta.Directo, null);
                IrANombreArchivo(OrigenDestino.CrearNueva);
                return;
            }
        }

        ControlOrganizacion.Iniciar(null, null, bloqueado: false);
        ActualizarVisibilidadMarcarFecha();
        MostrarPanel(PanelOrganizacion);
    }

    private void ControlOrganizacion_SeleccionCambiada() => ActualizarVisibilidadMarcarFecha();

    private void ActualizarVisibilidadMarcarFecha()
    {
        PanelMarcarFecha.Visibility = ControlOrganizacion.FechaEsAplicable ? Visibility.Visible : Visibility.Collapsed;
        TxtFechaOpcional.Visibility = ControlOrganizacion.FechaEsOpcional ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TxtFechaManual_TextChanged(object sender, TextChangedEventArgs e) =>
        ControlOrganizacion.RefrescarPorCambioDeFecha();

    private (DateTime Fecha, bool EsSupuesta) LeerFechaReferencia()
    {
        if (!string.IsNullOrWhiteSpace(TxtFechaManual.Text) && FechaExtraidaService.TryParsear(TxtFechaManual.Text, out var fecha))
        {
            return (fecha, false);
        }

        return (DateTime.Now, true);
    }

    private void BtnContinuarOrganizacion_Click(object sender, RoutedEventArgs e)
    {
        if (!ControlOrganizacion.Validar(out var error))
        {
            MostrarError(error!);
            return;
        }

        var formato = ControlOrganizacion.FormatoElegido!.Value;
        var patron = formato == FormatoCarpeta.Directo ? null : ControlOrganizacion.PatronElegido;

        if (ControlOrganizacion.FechaEsAplicable && !ControlOrganizacion.FechaEsOpcional
            && string.IsNullOrWhiteSpace(TxtFechaManual.Text))
        {
            MostrarError("Escribir la fecha del documento.");
            return;
        }

        var (fecha, _) = LeerFechaReferencia();
        var subcarpeta = FormatoCarpetaService.ConstruirSubcarpeta(formato, patron, fecha);
        _carpetaDestinoFinal = string.IsNullOrEmpty(subcarpeta) ? _carpetaMadre : Path.Combine(_carpetaMadre, subcarpeta);

        _ubicaciones.ObtenerOCrear(_carpetaMadre, formato, patron);

        _formato = formato;
        _patron = patron;
        IrANombreArchivo(OrigenDestino.CrearNueva);
    }

    // ----- 3b: Ver ubicaciones disponibles -----

    private void BtnVerUbicaciones_Click(object sender, RoutedEventArgs e)
    {
        var ubicaciones = _ubicaciones.ObtenerTodas();
        TxtSinUbicaciones.Visibility = ubicaciones.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ListaUbicaciones.ItemsSource = ubicaciones.Select(u => new FilaUbicacion(u)).ToList();
        MostrarPanel(PanelVerUbicaciones);
    }

    private void BtnVolverAElegir_Click(object sender, RoutedEventArgs e) => MostrarPanel(PanelElegir);

    private void ListaUbicaciones_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ListaUbicaciones.SelectedItem is not FilaUbicacion fila)
        {
            return;
        }

        var ubicacion = fila.Ubicacion;

        if (ubicacion.Formato == FormatoCarpeta.Directo)
        {
            var confirmar = System.Windows.MessageBox.Show(
                this, $"¿Guardar este documento en:\n{ubicacion.CarpetaMadre}?",
                "Archivero", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirmar == MessageBoxResult.Yes)
            {
                _carpetaDestinoFinal = ubicacion.CarpetaMadre;
                IrANombreArchivo(OrigenDestino.UbicacionExistente);
            }

            return;
        }

        _nivelesTotales = ubicacion.Patron!.Split('\\').Length;
        _nivelActual = 0;
        _carpetaNavegacionActual = ubicacion.CarpetaMadre;
        MostrarNivelNavegacion();
    }

    /// <summary>
    /// Navegación por niveles dentro de una ubicación organizada (Caso-4, punto 3b): solo lista
    /// subcarpetas ya existentes, nivel por nivel -- no hace falta más que eso para ahorrarle al
    /// usuario el paso de navegar a mano por el explorador de Windows.
    /// </summary>
    private void MostrarNivelNavegacion()
    {
        TxtRutaNavegacion.Text = _carpetaNavegacionActual;

        var subcarpetas = Directory.GetDirectories(_carpetaNavegacionActual)
            .Select(Path.GetFileName)
            .OrderDescending(StringComparer.OrdinalIgnoreCase)
            .ToList();

        TxtSinSubcarpetas.Visibility = subcarpetas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ListaNavegacion.ItemsSource = subcarpetas;
        MostrarPanel(PanelNavegar);
    }

    private void ListaNavegacion_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ListaNavegacion.SelectedItem is not string nombreCarpeta)
        {
            return;
        }

        _carpetaNavegacionActual = Path.Combine(_carpetaNavegacionActual, nombreCarpeta);
        _nivelActual++;

        if (_nivelActual >= _nivelesTotales)
        {
            _carpetaDestinoFinal = _carpetaNavegacionActual;
            IrANombreArchivo(OrigenDestino.UbicacionExistente);
            return;
        }

        MostrarNivelNavegacion();
    }

    private void BtnVolverNavegacion_Click(object sender, RoutedEventArgs e) => MostrarPanel(PanelVerUbicaciones);

    // ----- 3c: Accesos rápidos (Caso-11, punto 4) -----

    private void BtnAtajo_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not AtajoGuardadoRapido atajo)
        {
            return;
        }

        _carpetaMadre = atajo.CarpetaMadre;
        _formato = atajo.Formato;
        _patron = atajo.Patron;
        _carpetaDestinoFinal = atajo.Formato == FormatoCarpeta.Directo ? atajo.CarpetaMadre : null;
        _atajoUsado = atajo.Nombre;

        IrANombreArchivo(OrigenDestino.Atajo);
        TxtNombreArchivo.Text = LimpiezaNombreService.AplicarRegla(TxtNombreArchivo.Text, atajo.ReglaNombre);
    }

    private bool AtajoNecesitaFecha => _origen == OrigenDestino.Atajo && _formato != FormatoCarpeta.Directo;

    private void TxtFechaAtajo_TextChanged(object sender, TextChangedEventArgs e) => ActualizarTextoDestino();

    /// <summary>
    /// Misma regla que el paso de organización del flujo manual: fecha obligatoria salvo en los
    /// tipos que no dependen del año. Una fecha escrita que no se reconoce es error (no se cae a
    /// "hoy" en silencio), para no guardar en una subcarpeta equivocada sin que se note.
    /// </summary>
    private bool TryCalcularDestinoAtajo(out string destino, out string? error)
    {
        destino = string.Empty;
        error = null;

        DateTime fecha;
        if (string.IsNullOrWhiteSpace(TxtFechaAtajo.Text))
        {
            if (!OrganizacionCarpetaService.FechaEsOpcional(_formato))
            {
                error = "Escribir la fecha del documento.";
                return false;
            }

            fecha = DateTime.Now;
        }
        else if (!FechaExtraidaService.TryParsear(TxtFechaAtajo.Text, out fecha))
        {
            error = "No se reconoce la fecha escrita (ej. 15/03/2026).";
            return false;
        }

        var subcarpeta = FormatoCarpetaService.ConstruirSubcarpeta(_formato, _patron, fecha);
        destino = string.IsNullOrEmpty(subcarpeta) ? _carpetaMadre : Path.Combine(_carpetaMadre, subcarpeta);
        return true;
    }

    // ----- Paso final: nombre de archivo y guardado -----

    private void IrANombreArchivo(OrigenDestino origen)
    {
        _origen = origen;
        if (origen != OrigenDestino.Atajo)
        {
            _atajoUsado = null;
        }

        _reglaNombre.Clear();
        TxtNombreArchivo.Text = Path.GetFileNameWithoutExtension(_rutaArchivo);

        PanelFechaAtajo.Visibility = AtajoNecesitaFecha ? Visibility.Visible : Visibility.Collapsed;
        if (AtajoNecesitaFecha)
        {
            TxtAyudaFechaAtajo.Text = OrganizacionCarpetaService.FechaEsOpcional(_formato)
                ? "Opcional para este tipo de organización: si se deja vacía, se usa la fecha de hoy."
                : "Escribir la fecha a mano (ej. 15/03/2026): define la subcarpeta donde se guarda.";
            TxtFechaAtajo.Clear();
        }

        ActualizarTextoDestino();
        MostrarPanel(PanelNombreArchivo);
    }

    private void ActualizarTextoDestino()
    {
        if (!AtajoNecesitaFecha)
        {
            TxtCarpetaDestinoFinal.Text = $"Se va a guardar en: {_carpetaDestinoFinal}";
            return;
        }

        TxtCarpetaDestinoFinal.Text = TryCalcularDestinoAtajo(out var destino, out _)
            ? $"Se va a guardar en: {destino}"
            : $"Se va a guardar en: {_carpetaMadre}, en la subcarpeta que corresponda a la fecha.";
    }

    // Cada botón queda anotado en orden: esa secuencia es la "regla de nombre" de un acceso rápido.
    private void BtnBorrarNombre_Click(object sender, RoutedEventArgs e)
    {
        TxtNombreArchivo.Clear();
        _reglaNombre.Add(OperacionNombre.Borrar);
    }

    private void BtnSoloNumeros_Click(object sender, RoutedEventArgs e)
    {
        TxtNombreArchivo.Text = LimpiezaNombreService.DejarSoloNumeros(TxtNombreArchivo.Text);
        _reglaNombre.Add(OperacionNombre.DejarSoloNumeros);
    }

    private void BtnQuitarCeros_Click(object sender, RoutedEventArgs e)
    {
        TxtNombreArchivo.Text = LimpiezaNombreService.QuitarCerosIzquierda(TxtNombreArchivo.Text.Trim());
        _reglaNombre.Add(OperacionNombre.QuitarCerosIzquierda);
    }

    private void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        var nombre = TxtNombreArchivo.Text.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            MostrarError("Escribir un nombre de archivo.");
            return;
        }

        if (AtajoNecesitaFecha)
        {
            if (!TryCalcularDestinoAtajo(out var destino, out var errorFecha))
            {
                MostrarError(errorFecha!);
                return;
            }

            _carpetaDestinoFinal = destino;
        }

        // Se pregunta una sola vez: si el guardado falla (ej. duplicado) y se reintenta, no se repite.
        if (_origen == OrigenDestino.CrearNueva && !_preguntaAtajoHecha)
        {
            var dialogo = new GuardarAtajoWindow(LimpiezaNombreService.SugerirNombreAtajo(_carpetaMadre, _formato), _atajos)
            {
                Owner = this
            };
            _nombreAtajoAGuardar = dialogo.ShowDialog() == true ? dialogo.NombreElegido : null;
            _preguntaAtajoHecha = true;
        }

        try
        {
            // Para este punto _carpetaDestinoFinal ya es la carpeta exacta (con cualquier
            // subcarpeta de fecha ya resuelta, o la carpeta navegada a mano): se guarda
            // "Directo" ahí, renombrando siempre al nombre que el usuario dejó en este paso.
            var configuracionTemporal = new ConfiguracionDocumento
            {
                Emisor = "(sin texto)",
                Tipo = "(sin texto)",
                CarpetaDestino = _carpetaDestinoFinal!,
                FormatoCarpeta = FormatoCarpeta.Directo,
                PatronCarpeta = null,
                Renombrar = true,
                Patrones = []
            };

            string rutaFinal;
            try
            {
                rutaFinal = ClasificadorService.Clasificar(_rutaArchivo, configuracionTemporal, null, nombre);
            }
            catch (ArchivoDuplicadoException ex)
            {
                var resolver = new ResolverDuplicadoWindow(_rutaArchivo, ex.RutaDestino) { Owner = this };
                if (resolver.ShowDialog() != true)
                {
                    MostrarError("Documento dejado pendiente por nombre duplicado. Se puede intentar de nuevo o cerrar.");
                    return;
                }

                rutaFinal = ex.RutaDestino;
            }

            _pendientes.Quitar(_rutaArchivo);
            AuditoriaService.Registrar("GUARDADO_MANUAL_SIN_TEXTO",
                _atajoUsado is null ? $"Ruta={rutaFinal}" : $"Ruta={rutaFinal}; AccesoRapido={_atajoUsado}");

            var mensaje = $"Documento guardado en:\n{rutaFinal}";
            if (_nombreAtajoAGuardar is not null)
            {
                mensaje += "\n\n" + GuardarAtajo(_nombreAtajoAGuardar);
            }

            System.Windows.MessageBox.Show(this, mensaje, "Archivero",
                MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MostrarError($"No se pudo guardar: {ex.Message}");
        }
    }

    /// <summary>
    /// Se persiste recién con el documento ya guardado: si el guardado falló, no queda un acceso
    /// rápido a una combinación que nunca funcionó. Un error acá no deshace el guardado del documento.
    /// </summary>
    private string GuardarAtajo(string nombreAtajo)
    {
        try
        {
            _atajos.Guardar(nombreAtajo, _carpetaMadre, _formato, _patron, _reglaNombre);
            AuditoriaService.Registrar("ACCESO_RAPIDO_GUARDADO",
                $"Nombre={nombreAtajo}; Carpeta={_carpetaMadre}; Formato={_formato}; Regla={string.Join(",", _reglaNombre)}");
            return $"Acceso rápido guardado: \"{nombreAtajo}\".";
        }
        catch (Exception ex)
        {
            return $"El documento se guardó, pero no se pudo guardar el acceso rápido: {ex.Message}";
        }
    }

    private void BtnCerrar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void MostrarError(string mensaje)
    {
        TxtError.Text = mensaje;
        TxtError.Visibility = Visibility.Visible;
    }
}
