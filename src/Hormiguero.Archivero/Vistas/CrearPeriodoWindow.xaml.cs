using System.IO;
using System.Windows;
using Archivero.Datos;
using Archivero.Servicios;

namespace Archivero.Vistas;

/// <summary>
/// Caso-1, punto 2 (último párrafo): al aplicar un patrón ya confirmado a un documento nuevo
/// cuya carpeta de período todavía no existe, Archivero nunca la crea sola — esto ofrece las
/// opciones paso a paso: guardar en el período actual creándolo (con la opción de crear también
/// el próximo por adelantado), o elegir la subcarpeta manualmente como alternativa.
/// </summary>
public partial class CrearPeriodoWindow : Window
{
    private readonly string _rutaArchivo;
    private readonly ConfiguracionDocumento _configuracion;
    private readonly DateTime _fecha;
    private readonly string? _nombreExtraido;
    private readonly string _carpetaPeriodoActual;
    private readonly string _carpetaPeriodoSiguiente;
    private readonly PendienteRepository _pendientes = new();

    public CrearPeriodoWindow(string rutaArchivo, ConfiguracionDocumento configuracion, DateTime fecha, string? nombreExtraido, string carpetaPeriodoActual)
    {
        InitializeComponent();
        _rutaArchivo = rutaArchivo;
        _configuracion = configuracion;
        _fecha = fecha;
        _nombreExtraido = nombreExtraido;
        _carpetaPeriodoActual = carpetaPeriodoActual;

        var fechaSiguiente = FormatoCarpetaService.SiguientePeriodo(configuracion.FormatoCarpeta, fecha, configuracion.PatronCarpeta);
        var subcarpetaSiguiente = FormatoCarpetaService.ConstruirSubcarpeta(configuracion.FormatoCarpeta, configuracion.PatronCarpeta, fechaSiguiente);
        _carpetaPeriodoSiguiente = Path.Combine(configuracion.CarpetaDestino, subcarpetaSiguiente);

        TxtCarpetaPeriodo.Text = $"Documento: {Path.GetFileName(rutaArchivo)}\nCarpeta que haría falta: {_carpetaPeriodoActual}";
        BtnCrearPeriodoActualYSiguiente.Content = $"Crear esta carpeta y también, por adelantado, \"{Path.GetFileName(_carpetaPeriodoSiguiente)}\"";
    }

    private void BtnCrearPeriodoActual_Click(object sender, RoutedEventArgs e) => CrearYGuardar(crearSiguienteTambien: false);

    private void BtnCrearPeriodoActualYSiguiente_Click(object sender, RoutedEventArgs e) => CrearYGuardar(crearSiguienteTambien: true);

    private void CrearYGuardar(bool crearSiguienteTambien)
    {
        try
        {
            var rutaFinal = ClasificadorService.Clasificar(_rutaArchivo, _configuracion, _fecha, _nombreExtraido);

            if (crearSiguienteTambien)
            {
                Directory.CreateDirectory(_carpetaPeriodoSiguiente);
            }

            _pendientes.Quitar(_rutaArchivo);

            System.Windows.MessageBox.Show(this, $"Documento guardado en:\n{rutaFinal}", "Archivero",
                MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"No se pudo guardar: {ex.Message}", "Archivero",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnElegirManualmente_Click(object sender, RoutedEventArgs e)
    {
        using var dialogo = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Elegir en qué carpeta guardar este documento"
        };

        if (dialogo.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var nombreArchivo = _configuracion.Renombrar && !string.IsNullOrWhiteSpace(_nombreExtraido)
            ? $"{_nombreExtraido}{Path.GetExtension(_rutaArchivo)}"
            : Path.GetFileName(_rutaArchivo);
        var rutaElegida = Path.Combine(dialogo.SelectedPath, nombreArchivo);

        try
        {
            ClasificadorService.GuardarComoExcepcion(_rutaArchivo, rutaElegida);
            _pendientes.Quitar(_rutaArchivo);

            System.Windows.MessageBox.Show(this, $"Guardado en:\n{rutaElegida}", "Archivero",
                MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (ArchivoDuplicadoException)
        {
            System.Windows.MessageBox.Show(this,
                $"Ya existe un archivo con ese nombre ahí:\n{rutaElegida}\nElegí otra carpeta.",
                "Archivero", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"No se pudo guardar: {ex.Message}", "Archivero",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnDejarPendiente_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
