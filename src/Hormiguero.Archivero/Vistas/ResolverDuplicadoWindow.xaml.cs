using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Archivero.Datos;
using Archivero.Servicios;
using Archivero.Servicios.Pdf;

namespace Archivero.Vistas;

/// <summary>
/// Resolución de un documento duplicado con comparación visual y acciones reversibles.
/// </summary>
public partial class ResolverDuplicadoWindow : Window
{
    private readonly string _rutaArchivoNuevo;
    private readonly string _rutaDestinoConflicto;
    private readonly PendienteRepository _pendientes = new();

    public ResolverDuplicadoWindow(string rutaArchivoNuevo, string rutaDestinoConflicto)
    {
        InitializeComponent();
        _rutaArchivoNuevo = rutaArchivoNuevo;
        _rutaDestinoConflicto = rutaDestinoConflicto;

        TxtRutaConflicto.Text =
            $"Documento nuevo: {Path.GetFileName(rutaArchivoNuevo)}\n"
            + $"Ya existe en: {rutaDestinoConflicto}";
    }

    private void BtnRevisar_Click(object sender, RoutedEventArgs e)
    {
        if (PanelComparacion.Visibility == Visibility.Visible)
        {
            PanelComparacion.Visibility = Visibility.Collapsed;
            BtnRevisar.Content = "Revisar lado a lado";
            return;
        }

        try
        {
            ImagenExistente.Source = RenderizarPrimeraPagina(_rutaDestinoConflicto);
            ImagenNuevo.Source = RenderizarPrimeraPagina(_rutaArchivoNuevo);
            PanelComparacion.Visibility = Visibility.Visible;
            BtnRevisar.Content = "Ocultar comparación";

            // Caso-5: "Eliminar duplicado" solo se puede habilitar despues de usar "Revisar" al
            // menos una vez -- la confirmacion (el checkbox) no aparece antes de eso.
            ChkConfirmarMismoDocumento.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            AuditoriaService.Registrar(
                "COMPARACION_DUPLICADO_FALLIDA",
                $"{_rutaArchivoNuevo}: {ex}"
            );
            System.Windows.MessageBox.Show(
                this,
                $"No se pudo mostrar la comparación: {ex.Message}",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private System.Windows.Controls.ScrollViewer? _scrollMovidoPorSincronizacion;

    private void SliderZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // El slider dispara ValueChanged durante InitializeComponent, antes de que existan las escalas.
        if (EscalaExistente is null || EscalaNuevo is null || TxtZoom is null)
        {
            return;
        }

        var escala = e.NewValue / 100.0;
        EscalaExistente.ScaleX = EscalaExistente.ScaleY = escala;
        EscalaNuevo.ScaleX = EscalaNuevo.ScaleY = escala;
        TxtZoom.Text = $"{e.NewValue:0}%";
    }

    /// <summary>
    /// Mueve el otro documento a la misma posición relativa (no en píxeles: los dos documentos
    /// pueden tener tamaños distintos). ScrollToXxxOffset se aplica recién en el próximo pase de
    /// layout, así que el ScrollChanged que provoca llega después: se marca ese ScrollViewer para
    /// ignorar ese único evento y no rebotar el movimiento de vuelta al original.
    /// </summary>
    private void Scroll_ScrollChanged(
        object sender,
        System.Windows.Controls.ScrollChangedEventArgs e
    )
    {
        var origen = (System.Windows.Controls.ScrollViewer)sender;

        if (origen == _scrollMovidoPorSincronizacion)
        {
            _scrollMovidoPorSincronizacion = null;
            return;
        }

        if (
            ChkSincronizarScroll?.IsChecked != true
            || (e.HorizontalChange == 0 && e.VerticalChange == 0)
        )
        {
            return;
        }

        var destino = origen == ScrollExistente ? ScrollNuevo : ScrollExistente;
        var horizontal =
            origen.ScrollableWidth > 0
                ? origen.HorizontalOffset / origen.ScrollableWidth * destino.ScrollableWidth
                : destino.HorizontalOffset;
        var vertical =
            origen.ScrollableHeight > 0
                ? origen.VerticalOffset / origen.ScrollableHeight * destino.ScrollableHeight
                : destino.VerticalOffset;

        if (
            Math.Abs(horizontal - destino.HorizontalOffset) < 0.5
            && Math.Abs(vertical - destino.VerticalOffset) < 0.5
        )
        {
            return;
        }

        _scrollMovidoPorSincronizacion = destino;
        destino.ScrollToHorizontalOffset(horizontal);
        destino.ScrollToVerticalOffset(vertical);
    }

    private void ChkConfirmarMismoDocumento_Changed(object sender, RoutedEventArgs e)
    {
        if (BtnEliminarDuplicado is not null && ChkConfirmarMismoDocumento is not null)
        {
            BtnEliminarDuplicado.IsEnabled = ChkConfirmarMismoDocumento.IsChecked == true;
        }
    }

    /// <summary>El nuevo documento se conserva en Descartados; nunca se elimina definitivamente.</summary>
    private void BtnEliminarDuplicado_Click(object sender, RoutedEventArgs e)
    {
        var confirmar = System.Windows.MessageBox.Show(
            this,
            $"¿Mover el documento nuevo a la carpeta Descartados?\n\n{_rutaArchivoNuevo}\n\n"
                + "El que ya estaba guardado no se toca.",
            "Archivero",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (confirmar != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var rutaDescartada = ClasificadorService.DescartarDuplicado(
                _rutaArchivoNuevo,
                _rutaDestinoConflicto
            );
            _pendientes.Quitar(_rutaArchivoNuevo);
            AuditoriaService.Registrar(
                "DUPLICADO_DESCARTADO",
                $"{_rutaArchivoNuevo} -> {rutaDescartada}"
            );

            System.Windows.MessageBox.Show(
                this,
                $"Documento nuevo movido a:\n{rutaDescartada}\nSe mantuvo el que ya estaba guardado.",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            AuditoriaService.Registrar("DESCARTE_DUPLICADO_FALLIDO", $"{_rutaArchivoNuevo}: {ex}");
            System.Windows.MessageBox.Show(
                this,
                $"No se pudo mover el documento a Descartados: {ex.Message}",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private static BitmapSource RenderizarPrimeraPagina(string rutaPdf)
    {
        var pagina = LectorPdf.RenderizarPagina(rutaPdf, 0);
        var bitmap = BitmapSource.Create(
            pagina.Ancho,
            pagina.Alto,
            96,
            96,
            System.Windows.Media.PixelFormats.Bgra32,
            null,
            pagina.PixelesBgra,
            pagina.Ancho * 4
        );
        bitmap.Freeze();
        return bitmap;
    }

    private void BtnReemplazar_Click(object sender, RoutedEventArgs e)
    {
        var confirmar = System.Windows.MessageBox.Show(
            this,
            $"¿Reemplazar el archivo guardado? Se conservará una copia de respaldo.\n\n{_rutaDestinoConflicto}",
            "Archivero",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );
        if (confirmar != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            ClasificadorService.ReemplazarYClasificar(_rutaArchivoNuevo, _rutaDestinoConflicto);
            _pendientes.Quitar(_rutaArchivoNuevo);
            AuditoriaService.Registrar(
                "DUPLICADO_REEMPLAZADO",
                $"{_rutaDestinoConflicto} por {_rutaArchivoNuevo}"
            );

            System.Windows.MessageBox.Show(
                this,
                $"Reemplazado:\n{_rutaDestinoConflicto}",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            AuditoriaService.Registrar("REEMPLAZO_DUPLICADO_FALLIDO", $"{_rutaArchivoNuevo}: {ex}");
            System.Windows.MessageBox.Show(
                this,
                $"No se pudo reemplazar: {ex.Message}",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void BtnGuardarAmbos_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var ruta = ClasificadorService.GuardarAmbos(_rutaArchivoNuevo, _rutaDestinoConflicto);
            _pendientes.Quitar(_rutaArchivoNuevo);
            AuditoriaService.Registrar(
                "DUPLICADO_GUARDADO_AMBOS",
                $"{_rutaArchivoNuevo} -> {ruta}"
            );
            System.Windows.MessageBox.Show(
                this,
                $"Se conservaron ambos documentos.\n\nNuevo archivo:\n{ruta}",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            AuditoriaService.Registrar(
                "GUARDAR_AMBOS_DUPLICADO_FALLIDO",
                $"{_rutaArchivoNuevo}: {ex}"
            );
            System.Windows.MessageBox.Show(
                this,
                $"No se pudieron conservar ambos documentos: {ex.Message}",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void BtnDejarPendiente_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnExcepcion_Click(object sender, RoutedEventArgs e)
    {
        using var dialogo = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Elegir dónde guardar este documento como excepción",
        };

        if (dialogo.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var nombreArchivo = Path.GetFileName(_rutaDestinoConflicto);
        var rutaExcepcion = Path.Combine(dialogo.SelectedPath, nombreArchivo);

        try
        {
            ClasificadorService.GuardarComoExcepcion(_rutaArchivoNuevo, rutaExcepcion);
            _pendientes.Quitar(_rutaArchivoNuevo);

            System.Windows.MessageBox.Show(
                this,
                $"Guardado como excepción en:\n{rutaExcepcion}",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            DialogResult = true;
            Close();
        }
        catch (ArchivoDuplicadoException)
        {
            System.Windows.MessageBox.Show(
                this,
                $"También existe un archivo con ese nombre ahí:\n{rutaExcepcion}\nElige otra carpeta.",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
        catch (Exception ex)
        {
            AuditoriaService.Registrar(
                "GUARDAR_EXCEPCION_DUPLICADO_FALLIDO",
                $"{_rutaArchivoNuevo}: {ex}"
            );
            System.Windows.MessageBox.Show(
                this,
                $"No se pudo guardar: {ex.Message}",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
}
