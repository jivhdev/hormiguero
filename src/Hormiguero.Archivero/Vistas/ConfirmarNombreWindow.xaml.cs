using System.IO;
using System.Windows;
using Archivero.Datos;
using Archivero.Servicios;

namespace Archivero.Vistas;

/// <summary>
/// Caso-11, punto 1: último paso del guardado automático de una configuración con "Preguntar el
/// nombre cada vez". La carpeta ya está resuelta; acá solo se escribe o confirma el nombre.
/// Cerrar sin guardar deja el documento en pendientes, sin tocarlo.
/// </summary>
public partial class ConfirmarNombreWindow : Window
{
    private readonly string _rutaArchivo;
    private readonly ConfiguracionDocumento _configuracion;
    private readonly CamposExtraidos _campos;
    private readonly PendienteRepository _pendientes = new();

    public ConfirmarNombreWindow(string rutaArchivo, ConfiguracionDocumento configuracionConPatronCoincidente, CamposExtraidos campos)
    {
        InitializeComponent();
        _rutaArchivo = rutaArchivo;
        _configuracion = configuracionConPatronCoincidente;
        _campos = campos;

        Visor.CargarPdf(rutaArchivo);

        TxtClasificacion.Text = $"{_configuracion.Emisor} / {_configuracion.Tipo}";
        try
        {
            TxtCarpeta.Text = Path.GetDirectoryName(
                ClasificadorService.CalcularRutaDestino(rutaArchivo, _configuracion with { Renombrar = false }, campos.Fecha, null));
        }
        catch (ValidacionSeguridadException)
        {
            // El nombre original puede no ser válido (justamente se va a reemplazar): la carpeta
            // exacta se valida igual al guardar.
            TxtCarpeta.Text = _configuracion.CarpetaDestino;
        }
        TxtNombre.Text = Path.GetFileNameWithoutExtension(rutaArchivo);

        Loaded += (_, _) =>
        {
            TxtNombre.Focus();
            TxtNombre.SelectAll();
        };
    }

    private void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        var nombre = TxtNombre.Text.Trim();
        if (nombre.Length == 0)
        {
            MostrarError("Escribir un nombre de archivo.");
            return;
        }

        try
        {
            var resultado = GuardadoAutomaticoService.GuardarConNombreConfirmado(_rutaArchivo, _configuracion, nombre);
            switch (resultado.Resultado)
            {
                case ResultadoGuardadoAutomatico.Guardado:
                    TerminarGuardado(resultado.RutaFinal!);
                    break;

                case ResultadoGuardadoAutomatico.Duplicado:
                    ResolverDuplicado(nombre);
                    break;

                case ResultadoGuardadoAutomatico.PeriodoNuevo:
                    CrearPeriodo(nombre, resultado.Detalle!);
                    break;

                default:
                    MostrarError($"No se pudo guardar: {resultado.Detalle}");
                    break;
            }
        }
        catch (Exception ex)
        {
            MostrarError($"No se pudo guardar: {ex.Message}");
        }
    }

    private ConfiguracionDocumento ConfiguracionConNombre => _configuracion with { Renombrar = true, PreguntarNombre = false };

    private void ResolverDuplicado(string nombre)
    {
        var rutaConflicto = ClasificadorService.CalcularRutaDestino(_rutaArchivo, ConfiguracionConNombre, _campos.Fecha, nombre);
        var resolver = new ResolverDuplicadoWindow(_rutaArchivo, rutaConflicto) { Owner = this };
        if (resolver.ShowDialog() != true)
        {
            MostrarError("Ya existe un archivo con ese nombre en la carpeta. Se puede cambiar el nombre e intentar de nuevo.");
            return;
        }

        TerminarGuardado(rutaConflicto);
    }

    /// <summary>La carpeta del período todavía no existe: misma pantalla de siempre (Caso-1, punto 2), ya con el nombre confirmado.</summary>
    private void CrearPeriodo(string nombre, string carpetaPeriodo)
    {
        var ventana = new CrearPeriodoWindow(_rutaArchivo, ConfiguracionConNombre, _campos.Fecha ?? DateTime.Now, nombre, carpetaPeriodo)
        {
            Owner = this
        };

        if (ventana.ShowDialog() == true)
        {
            DialogResult = true;
        }
    }

    private void TerminarGuardado(string rutaFinal)
    {
        _pendientes.Quitar(_rutaArchivo);
        AuditoriaService.Registrar("NOMBRE_CONFIRMADO",
            $"Emisor={_configuracion.Emisor}; Tipo={_configuracion.Tipo}; Ruta={rutaFinal}");

        System.Windows.MessageBox.Show(this, $"Documento guardado en:\n{rutaFinal}", "Archivero",
            MessageBoxButton.OK, MessageBoxImage.Information);

        DialogResult = true;
    }

    private void MostrarError(string mensaje)
    {
        TxtError.Text = mensaje;
        TxtError.Visibility = Visibility.Visible;
    }
}
