using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hormiguero.Nucleo.Pdf;

namespace Hormiguero.Diseno.Controles;

public sealed partial class VisorPdf : UserControl
{
    private byte[]? pdfBytes;
    private string? pdfRuta;
    private int totalPaginas;
    private double zoom = 1.0;
    private int rotacionGrados = 0;
    private CancellationTokenSource? ctsDibujo;
    private Window? ventana;

    // Elige la impresora antes de imprimir (Configuración, REQ-006). Nombre null
    // es la predeterminada de Windows; Seguir false cancela (cuadro cerrado).
    public Func<(bool Seguir, string? Nombre)> Impresora { get; set; } = () => (true, null);

    public VisorPdf()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            ventana = Window.GetWindow(this);
            if (ventana is not null)
            {
                ventana.PreviewKeyDown += Atajo;
                ventana.PreviewMouseWheel += RuedaMouse;
            }
        };
        Unloaded += (_, _) =>
        {
            if (ventana is not null)
            {
                ventana.PreviewKeyDown -= Atajo;
                ventana.PreviewMouseWheel -= RuedaMouse;
            }
            CancelarDibujo();
        };
    }

    public async Task AbrirAsync(string ruta)
    {
        CancelarDibujo();
        LimpiarVisor();
        var cts = new CancellationTokenSource();
        ctsDibujo = cts;

        if (!File.Exists(ruta))
        {
            MostrarError("El archivo ya no está en su carpeta");
            return;
        }

        try
        {
            using var fs = new FileStream(
                ruta,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete
            );
            var leidos = new byte[fs.Length];
            await fs.ReadExactlyAsync(leidos, cts.Token);
            pdfBytes = leidos;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            MostrarError($"No se puede leer el archivo: {ex.Message}");
            return;
        }

        // Mientras se leía, el usuario eligió otro documento.
        if (cts.IsCancellationRequested)
        {
            return;
        }

        if (pdfBytes.Length == 0)
        {
            MostrarError("El archivo está vacío");
            return;
        }

        pdfRuta = ruta;

        try
        {
            totalPaginas = DibujoPdf.Paginas(pdfBytes);
        }
        catch (Exception ex)
        {
            MostrarError($"No se puede mostrar este PDF: {ex.Message}");
            return;
        }

        if (totalPaginas == 0)
        {
            MostrarError("El PDF no tiene páginas");
            return;
        }

        zoom = 1.0;
        rotacionGrados = 0;
        ActualizarZoomTexto();
        HabilitarBotones(true);

        await DibujarTodasLasPaginasAsync(cts.Token);
    }

    private async Task DibujarTodasLasPaginasAsync(CancellationToken token)
    {
        if (pdfBytes is not byte[] pdf)
        {
            return;
        }

        double zoomActual = zoom;
        int paginas = totalPaginas;

        try
        {
            // Primero la página 1 (índice 0) para que se vea rápido (RNF-3);
            // luego las demás, una por una.
            for (int i = 0; i < paginas; i++)
            {
                token.ThrowIfCancellationRequested();
                await DibujarPaginaAsync(pdf, i, zoomActual, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelación esperada, no hacer nada
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                Dispatcher.Invoke(() => MostrarError($"Error al dibujar: {ex.Message}"));
            }
        }
    }

    private async Task DibujarPaginaAsync(
        byte[] pdf,
        int pagina,
        double zoomActual,
        CancellationToken token
    )
    {
        ImagenPagina imagen;
        try
        {
            imagen = await Task.Run(() => DibujoPdf.Dibujar(pdf, pagina, zoomActual), token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
                MostrarError($"No se puede mostrar la página {pagina + 1}: {ex.Message}")
            );
            return;
        }

        token.ThrowIfCancellationRequested();

        await Dispatcher.InvokeAsync(() =>
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            var bitmapSource = BitmapSource.Create(
                imagen.Ancho,
                imagen.Alto,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                imagen.PixelesBgra,
                imagen.Ancho * 4
            );
            bitmapSource.Freeze();

            var image = new Image
            {
                Source = bitmapSource,
                Margin = new Thickness(0, 0, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Center,
                LayoutTransform =
                    rotacionGrados != 0 ? new RotateTransform(rotacionGrados) : Transform.Identity,
                Stretch = Stretch.None,
            };

            var border = new Border
            {
                BorderBrush = (Brush)FindResource("Hormiguero.Borde"),
                BorderThickness = new Thickness(1),
                Child = image,
            };

            Paginas.Children.Add(border);
        });
    }

    private void CancelarDibujo()
    {
        if (ctsDibujo is not null)
        {
            // Sin Dispose: una tarea de dibujo en curso aún puede consultar el token.
            ctsDibujo.Cancel();
            ctsDibujo = null;
        }
    }

    private void LimpiarVisor()
    {
        Paginas.Children.Clear();
        pdfBytes = null;
        pdfRuta = null;
        totalPaginas = 0;
        HabilitarBotones(false);
        Aviso.Text = "";
    }

    private void MostrarError(string mensaje)
    {
        LimpiarVisor();
        var textBlock = new TextBlock
        {
            Text = "No se puede mostrar este PDF",
            FontSize = (double)FindResource("Hormiguero.TextoNormal"),
            Foreground = (Brush)FindResource("Hormiguero.Error"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 24, 0, 8),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
        };
        Paginas.Children.Add(new Border { Child = textBlock, Padding = new Thickness(24) });
        Aviso.Text = mensaje;
    }

    private void HabilitarBotones(bool habilitado)
    {
        BtnZoomMenos.IsEnabled = habilitado;
        BtnZoomMas.IsEnabled = habilitado;
        BtnGirar.IsEnabled = habilitado;
        BtnImprimir1.IsEnabled = habilitado;
        BtnImprimir2.IsEnabled = habilitado;
        BtnVerEnCarpeta.IsEnabled = habilitado;
    }

    private void ActualizarZoomTexto()
    {
        TxtZoom.Text = $"{(int)(zoom * 100)} %";
    }

    private void ZoomMenos_Click(object sender, RoutedEventArgs e) => CambiarZoom(-0.25);

    private void ZoomMas_Click(object sender, RoutedEventArgs e) => CambiarZoom(0.25);

    private void CambiarZoom(double delta)
    {
        if (pdfBytes is null)
        {
            return;
        }

        double nuevoZoom = Math.Clamp(zoom + delta, 0.5, 3.0);
        // Redondear a pasos de 25%
        nuevoZoom = Math.Round(nuevoZoom * 4) / 4.0;
        nuevoZoom = Math.Clamp(nuevoZoom, 0.5, 3.0);

        if (Math.Abs(nuevoZoom - zoom) < 0.01)
        {
            return;
        }

        zoom = nuevoZoom;
        ActualizarZoomTexto();
        RedibujarTodas();
    }

    private void RuedaMouse(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && pdfBytes is not null)
        {
            double delta = e.Delta > 0 ? 0.25 : -0.25;
            CambiarZoom(delta);
            e.Handled = true;
        }
    }

    private void RedibujarTodas()
    {
        CancelarDibujo();
        Paginas.Children.Clear();
        ctsDibujo = new CancellationTokenSource();
        _ = DibujarTodasLasPaginasAsync(ctsDibujo.Token);
    }

    private void Girar_Click(object sender, RoutedEventArgs e)
    {
        if (pdfBytes is null)
        {
            return;
        }

        rotacionGrados = (rotacionGrados + 90) % 360;

        foreach (UIElement child in Paginas.Children)
        {
            if (child is Border border && border.Child is Image image)
            {
                image.LayoutTransform =
                    rotacionGrados != 0 ? new RotateTransform(rotacionGrados) : Transform.Identity;
            }
        }
    }

    private async void Imprimir1_Click(object sender, RoutedEventArgs e) => await ImprimirAsync(1);

    private async void Imprimir2_Click(object sender, RoutedEventArgs e) => await ImprimirAsync(2);

    private async Task ImprimirAsync(int cuantas)
    {
        if (pdfBytes is null)
        {
            return;
        }

        byte[] pdf = pdfBytes;
        (bool seguir, string? impresora) = Impresora();
        if (!seguir)
        {
            return;
        }

        Aviso.Text = "Imprimiendo…";

        try
        {
            await Task.Run(() => ImpresionPdf.Imprimir(pdf, cuantas, impresora));
            Aviso.Text = "Enviado a la impresora";
        }
        catch (Exception ex)
        {
            Aviso.Text = $"No se pudo imprimir: {ex.Message}";
        }
    }

    private void VerEnCarpeta_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(pdfRuta) || !File.Exists(pdfRuta))
        {
            Aviso.Text = "El archivo ya no está en su carpeta";
            return;
        }

        try
        {
            Process.Start("explorer.exe", $"/select,\"{pdfRuta}\"");
        }
        catch (Exception ex)
        {
            Aviso.Text = $"No se pudo abrir la carpeta: {ex.Message}";
        }
    }

    private void Atajo(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Alt)
        {
            return;
        }

        if (e.SystemKey == Key.F && BtnImprimir1.IsEnabled)
        {
            _ = ImprimirAsync(1);
            e.Handled = true;
        }
        else if (e.SystemKey == Key.G && BtnImprimir2.IsEnabled)
        {
            _ = ImprimirAsync(2);
            e.Handled = true;
        }
    }
}
