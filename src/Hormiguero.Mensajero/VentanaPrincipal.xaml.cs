using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Hormiguero.Mensajero.Core;
using Microsoft.Win32;

namespace Hormiguero.Mensajero.App;

public partial class VentanaPrincipal : Window
{
    private readonly AlmacenMensajero almacen;
    private readonly VigilanteCarpeta vigilante = new();
    private readonly DispatcherTimer temporizadorToast;
    private IReadOnlyList<string> clientes;
    private string carpetaOcc;
    private string? rutaUltimoPdf;
    private string? rutaEncontradaExtractor;
    private string? rutaPdfRetiro;
    private string? rutaPdfGuia;
    private bool editorAbierto;

    public VentanaPrincipal()
    {
        InitializeComponent();
        almacen = AlmacenMensajero.AbrirComun();
        carpetaOcc = almacen.LeerCarpetaOcc();
        clientes = almacen.LeerClientesNvv();
        if (clientes.Count == 0)
        {
            clientes = ClientesNvv.PorDefecto;
            almacen.GuardarClientesNvv(clientes);
        }

        temporizadorToast = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        temporizadorToast.Tick += (_, _) =>
        {
            temporizadorToast.Stop();
            TextoToast.Text = "● Listo";
            TextoToast.SetResourceReference(TextBlock.ForegroundProperty, "Hormiguero.Texto");
        };
        // Ofisuiza abre en 1050 x 780; en pantallas más bajas (1366 x 768) se ajusta al área útil.
        Height = Math.Min(Height, SystemParameters.WorkArea.Height);
        Width = Math.Min(Width, SystemParameters.WorkArea.Width);
        vigilante.NuevoPdf += ruta =>
            Dispatcher.BeginInvoke(() =>
            {
                rutaUltimoPdf = ruta;
                PrecargarPdf(ruta);
                MostrarToast("🟢 Nuevo PDF detectado", "success");
            });
        vigilante.Error += mensaje => Dispatcher.BeginInvoke(() => MostrarToast(mensaje, "error"));
        Closed += (_, _) =>
        {
            vigilante.Dispose();
            almacen.Dispose();
        };

        TextoCarpeta.Text = NombreCarpeta(carpetaOcc);
        RefrescarClientes();
        if (!string.IsNullOrWhiteSpace(carpetaOcc) && Directory.Exists(carpetaOcc))
        {
            rutaUltimoPdf = CarpetaOcc.UltimoPdf(carpetaOcc);
            if (rutaUltimoPdf is not null)
            {
                PrecargarPdf(rutaUltimoPdf);
                MostrarToast("✅ PDF precargado y listo", "success");
            }
        }
    }

    private static string NombreCarpeta(string ruta) =>
        string.IsNullOrWhiteSpace(ruta)
            ? "Sin carpeta configurada"
            : $"📂 {Path.GetFileName(ruta.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}";

    private void CambiarCarpeta_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFolderDialog
        {
            Title = "Seleccionar carpeta de OCCs",
            FolderName = carpetaOcc,
        };
        if (dialogo.ShowDialog(this) != true)
        {
            return;
        }
        carpetaOcc = dialogo.FolderName;
        almacen.GuardarCarpetaOcc(carpetaOcc);
        TextoCarpeta.Text = NombreCarpeta(carpetaOcc);
        BotonUltimoAsunto.IsEnabled =
            BotonUltimoCuerpo.IsEnabled =
            BotonCopiarPdf.IsEnabled =
            BotonActivar.IsEnabled =
                true;
        MostrarToast("✅ Carpeta guardada", "success");
        rutaUltimoPdf = CarpetaOcc.UltimoPdf(carpetaOcc);
        if (rutaUltimoPdf is not null)
        {
            PrecargarPdf(rutaUltimoPdf);
        }
    }

    private void UltimoAsunto_Click(object sender, RoutedEventArgs e) => CopiarUltimoAsunto();

    private void UltimoCuerpo_Click(object sender, RoutedEventArgs e) => CopiarUltimoCuerpo();

    private void AsuntoEncontrado_Click(object sender, RoutedEventArgs e) =>
        CopiarAsunto(rutaEncontradaExtractor);

    private void CuerpoEncontrado_Click(object sender, RoutedEventArgs e) =>
        CopiarCuerpo(rutaEncontradaExtractor);

    private void CopiarPdf_Click(object sender, RoutedEventArgs e) => CopiarUltimoPdf();

    // Copia el archivo (no el texto): se pega como adjunto en el correo o en el Explorador.
    private void CopiarUltimoPdf()
    {
        if (string.IsNullOrWhiteSpace(carpetaOcc))
        {
            MostrarToast("❌ Sin carpeta configurada", "error");
            return;
        }
        string? ruta = CarpetaOcc.UltimoAgregado(carpetaOcc);
        if (ruta is null)
        {
            MostrarToast("❌ No hay PDFs en la carpeta", "error");
            return;
        }
        for (int intento = 0; intento < 3; intento++)
        {
            try
            {
                Clipboard.SetFileDropList([ruta]);
                MostrarToast($"✅ PDF copiado: {Path.GetFileName(ruta)}", "success");
                return;
            }
            catch (ExternalException) when (intento < 2)
            {
                Thread.Sleep(50);
            }
            catch (Exception excepcion)
            {
                MostrarToast($"❌ Error al copiar: {excepcion.Message}", "error");
                return;
            }
        }
    }

    private void CopiarUltimoAsunto()
    {
        string? ruta = ObtenerUltimoPdf();
        if (ruta is null)
            return;
        CopiarAsunto(ruta);
    }

    private void CopiarUltimoCuerpo()
    {
        string? ruta = ObtenerUltimoPdf("❌ No hay PDFs");
        if (ruta is null)
            return;
        CopiarCuerpo(ruta);
    }

    // Ofisuiza avisa "No hay PDFs en la carpeta" al copiar el asunto y "No hay PDFs" al copiar
    // el cuerpo; se conservan los dos textos.
    private string? ObtenerUltimoPdf(string avisoSinPdfs = "❌ No hay PDFs en la carpeta")
    {
        if (string.IsNullOrWhiteSpace(carpetaOcc))
        {
            MostrarToast("❌ Sin carpeta configurada", "error");
            return null;
        }
        string? ruta = CarpetaOcc.UltimoPdf(carpetaOcc);
        if (ruta is null)
        {
            MostrarToast(avisoSinPdfs, "error");
            return null;
        }
        rutaUltimoPdf = ruta;
        return ruta;
    }

    private void CopiarAsunto(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            MostrarToast("❌ Busca un PDF primero", "error");
            return;
        }
        try
        {
            var datos = ExtractorOcc.ExtraerOccNvvOcl(ruta);
            string asunto = ExtractorOcc.FormatearAsunto(datos.Occ, datos.Nvv, datos.Ocl);
            if (asunto.Length == 0)
                MostrarToast("❌ No se pudo extraer", "error");
            else
            {
                Copiar(asunto, $"✅ Asunto copiado: {asunto}");
                PrecargarPdf(ruta);
            }
        }
        catch (Exception excepcion)
        {
            MostrarToast($"❌ Error: {excepcion.Message}", "error");
        }
    }

    private void CopiarCuerpo(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            MostrarToast("❌ Busca un PDF primero", "error");
            return;
        }
        try
        {
            string despacho = ExtractorOcc.ExtraerDespacho(ruta);
            if (despacho.Length == 0)
                MostrarToast("❌ No se encontró despacho", "error");
            else
            {
                Copiar(despacho, "✅ Cuerpo del correo copiado");
                PrecargarPdf(ruta);
            }
        }
        catch (Exception excepcion)
        {
            MostrarToast($"❌ Error: {excepcion.Message}", "error");
        }
    }

    private void BuscarExtractor_Click(object sender, RoutedEventArgs e) => BuscarExtractor();

    private void BuscarExtractor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BuscarExtractor();
            e.Handled = true;
        }
    }

    private void BuscarExtractor()
    {
        string numero = CampoBuscarExtractor.Text.Trim();
        IReadOnlyList<string>? resultados = BuscarPdf(numero, "❌ Ingresa un número de OCC");
        if (resultados is null)
            return;
        if (resultados.Count == 0)
        {
            MostrarToast($"❌ No encontrado: {numero}", "error");
            TextoArchivoEncontrado.Text = "";
            BotonAsuntoEncontrado.IsEnabled = BotonCuerpoEncontrado.IsEnabled = false;
        }
        else if (resultados.Count == 1)
        {
            rutaEncontradaExtractor = resultados[0];
            string nombre = Path.GetFileName(rutaEncontradaExtractor);
            TextoArchivoEncontrado.Text = $"✅ {nombre}";
            BotonAsuntoEncontrado.IsEnabled = BotonCuerpoEncontrado.IsEnabled = true;
            MostrarToast($"✅ PDF encontrado: {nombre}", "success");
            PrecargarPdf(rutaEncontradaExtractor);
        }
        else
            MostrarToast($"⚠️ Múltiples archivos con N°{numero}", "warning");
    }

    private IReadOnlyList<string>? BuscarPdf(
        string numero,
        string avisoNumeroVacio = "❌ Ingresa un número"
    )
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            MostrarToast(avisoNumeroVacio, "error");
            return null;
        }
        if (string.IsNullOrWhiteSpace(carpetaOcc))
        {
            MostrarToast("❌ Sin carpeta", "error");
            return null;
        }
        return CarpetaOcc.BuscarPorNumero(carpetaOcc, numero);
    }

    private void Activar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(carpetaOcc))
        {
            MostrarToast("❌ Sin carpeta configurada", "error");
            return;
        }
        if (vigilante.Iniciar(carpetaOcc))
        {
            TextoEstadoAuto.Text = "🟢 Auto: Activo";
            BotonActivar.IsEnabled = false;
            BotonDetener.IsEnabled = true;
            MostrarToast("🟢 Modo automático activado", "success");
        }
    }

    private void Detener_Click(object sender, RoutedEventArgs e)
    {
        vigilante.Detener();
        TextoEstadoAuto.Text = "⚪ Auto: Inactivo";
        BotonActivar.IsEnabled = true;
        BotonDetener.IsEnabled = false;
        MostrarToast("⚪ Modo automático detenido", "info");
    }

    private void RefrescarClientes(string filtro = "") =>
        ListaClientes.ItemsSource = ClientesNvv.Buscar(filtro, clientes);

    private void BuscarCliente_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ListaClientes is not null)
            RefrescarClientes(CampoBuscarCliente.Text);
    }

    private void BuscarCliente_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (ListaClientes.Items.Count > 0)
                CopiarCliente(ListaClientes.Items[0]?.ToString());
            e.Handled = true;
        }
    }

    private void Cliente_DoubleClick(object sender, MouseButtonEventArgs e) =>
        CopiarClienteSeleccionado();

    private void CopiarCliente_Click(object sender, RoutedEventArgs e) =>
        CopiarClienteSeleccionado();

    private void CopiarClienteSeleccionado()
    {
        if (ListaClientes.SelectedItem is string seleccionado)
            CopiarCliente(seleccionado);
        else
            MostrarToast("❌ Selecciona un cliente", "error");
    }

    private void CopiarCliente(string? cliente)
    {
        if (editorAbierto)
        {
            MostrarToast("❌ Cierra el editor primero", "error");
            return;
        }
        if (string.IsNullOrWhiteSpace(cliente))
            return;
        Copiar(cliente, $"✅ Cliente copiado: {cliente}");
    }

    private void EditarClientes_Click(object sender, RoutedEventArgs e)
    {
        if (editorAbierto)
            return;
        editorAbierto = true;
        var editor = new EditorClientes(clientes, GuardarClientes) { Owner = this };
        editor.ShowDialog();
        editorAbierto = false;
        RefrescarClientes(CampoBuscarCliente.Text);
    }

    private void GuardarClientes(IReadOnlyList<string> nuevos)
    {
        almacen.GuardarClientesNvv(nuevos);
        clientes = nuevos;
        RefrescarClientes(CampoBuscarCliente.Text);
        MostrarToast("✅ Lista de clientes actualizada", "success");
    }

    private void UltimoPdfRetiro_Click(object sender, RoutedEventArgs e)
    {
        // Mismos avisos que Ofisuiza en esta sección (más cortos que en el extractor).
        if (string.IsNullOrWhiteSpace(carpetaOcc))
        {
            MostrarToast("❌ Sin carpeta", "error");
            return;
        }
        string? ruta = CarpetaOcc.UltimoPdf(carpetaOcc);
        if (ruta is null)
        {
            MostrarToast("❌ No hay PDFs", "error");
            return;
        }
        rutaPdfRetiro = ruta;
        MostrarToast($"✅ Cargado: {Path.GetFileName(ruta)}", "success");
        ActualizarRetiro();
    }

    private void BuscarRetiro_Click(object sender, RoutedEventArgs e) => BuscarRetiro();

    private void BuscarRetiro_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BuscarRetiro();
            e.Handled = true;
        }
    }

    private void BuscarRetiro()
    {
        var resultados = BuscarPdf(CampoBuscarRetiro.Text.Trim());
        if (resultados is null)
            return;
        if (resultados.Count == 0)
            MostrarToast("❌ No encontrado", "error");
        else if (resultados.Count == 1)
        {
            rutaPdfRetiro = resultados[0];
            MostrarToast($"✅ Cargado: {Path.GetFileName(rutaPdfRetiro)}", "success");
            ActualizarRetiro();
        }
        else
            MostrarToast("⚠️ Múltiples archivos", "warning");
    }

    private void ActualizarRetiro_Click(object sender, RoutedEventArgs e) => ActualizarRetiro();

    private void ActualizarRetiro()
    {
        if (string.IsNullOrWhiteSpace(rutaPdfRetiro))
        {
            MostrarToast("❌ Carga un PDF primero", "error");
            return;
        }
        try
        {
            var datos = ExtractorOcc.ExtraerOccNvvOcl(rutaPdfRetiro);
            string proveedor = ExtractorOcc.ExtraerProveedor(rutaPdfRetiro);
            TextoProveedor.Text = $"🏢 Proveedor: {proveedor}";
            PanelHoffens.Visibility =
                proveedor == "HOFFENS" ? Visibility.Visible : Visibility.Collapsed;
            string? dia = null;
            string? bloque = null;
            if (proveedor == "HOFFENS")
            {
                dia = string.IsNullOrWhiteSpace(CampoDiaHoffens.Text)
                    ? "_______________"
                    : CampoDiaHoffens.Text;
                bloque =
                    ListaBloques.SelectedItem?.ToString() == "Manual"
                        ? "_______________"
                        : ListaBloques.SelectedItem?.ToString();
            }
            VistaRetiro.Text = MensajesRetiro.Generar(proveedor, datos.Occ, datos.Ocl, dia, bloque);
            MostrarToast($"✅ Vista previa generada - {proveedor}", "success");
        }
        catch (Exception excepcion)
        {
            MostrarToast($"❌ Error: {excepcion.Message}", "error");
        }
    }

    private void CopiarRetiro_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(VistaRetiro.Text))
        {
            MostrarToast("❌ Genera la vista previa primero", "error");
            return;
        }
        Copiar(VistaRetiro.Text.Trim(), "✅ Mensaje de retiro copiado al portapapeles");
    }

    private void BuscarGuia_Click(object sender, RoutedEventArgs e) => BuscarGuia();

    private void BuscarGuia_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BuscarGuia();
            e.Handled = true;
        }
    }

    private void BuscarGuia()
    {
        var resultados = BuscarPdf(CampoBuscarGuia.Text.Trim());
        if (resultados is null)
            return;
        if (resultados.Count == 0)
            MostrarToast($"❌ No encontrado: {CampoBuscarGuia.Text.Trim()}", "error");
        else if (resultados.Count == 1)
        {
            rutaPdfGuia = resultados[0];
            TextoArchivoGuia.Text = $"✅ {Path.GetFileName(rutaPdfGuia)}";
            try
            {
                var (obra, comuna) = MensajeGuia.ExtraerObraYComuna(
                    ExtractorOcc.ExtraerDespacho(rutaPdfGuia)
                );
                CampoObra.Text = string.IsNullOrWhiteSpace(obra) ? "No detectada" : obra;
                CampoComuna.Text = comuna;
                MostrarToast("✅ PDF encontrado", "success");
            }
            catch (Exception excepcion)
            {
                MostrarToast($"❌ Error: {excepcion.Message}", "error");
            }
        }
        else
            MostrarToast("⚠️ Múltiples archivos", "warning");
    }

    // "Hoy" viene marcado en el XAML: este evento llega antes de que existan los demás controles.
    private void DiaGuia_Checked(object sender, RoutedEventArgs e)
    {
        if (CampoDiaGuia is not null)
            CampoDiaGuia.IsEnabled = DiaOtro.IsChecked == true;
    }

    private void GenerarGuia_Click(object sender, RoutedEventArgs e)
    {
        OpcionDia dia =
            DiaAyer.IsChecked == true ? OpcionDia.Ayer
            : DiaOtro.IsChecked == true ? OpcionDia.Otro
            : OpcionDia.Hoy;
        VistaGuia.Text = MensajeGuia.Generar(
            CampoObra.Text.Trim(),
            CampoComuna.Text.Trim(),
            dia,
            CampoDiaGuia.Text
        );
        MostrarToast("✅ Mensaje de guía generado", "success");
    }

    private void CopiarGuia_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(VistaGuia.Text))
        {
            MostrarToast("❌ Genera el mensaje primero", "error");
            return;
        }
        Copiar(VistaGuia.Text.Trim(), "✅ Mensaje de guía copiado al portapapeles");
    }

    private void Ventana_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            switch (e.Key)
            {
                case Key.D1:
                case Key.NumPad1:
                    CopiarUltimoAsunto();
                    e.Handled = true;
                    break;
                case Key.D2:
                case Key.NumPad2:
                    CopiarUltimoCuerpo();
                    e.Handled = true;
                    break;
                case Key.D3:
                case Key.NumPad3:
                    CopiarRetiro_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D4:
                case Key.NumPad4:
                    CopiarGuia_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D5:
                case Key.NumPad5:
                    CopiarUltimoPdf();
                    e.Handled = true;
                    break;
            }
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
        {
            // Con Alt, WPF entrega la tecla en SystemKey (e.Key llega como Key.System).
            switch (e.Key == Key.System ? e.SystemKey : e.Key)
            {
                case Key.S:
                    CopiarUltimoAsunto();
                    e.Handled = true;
                    break;
                case Key.D:
                    CopiarUltimoCuerpo();
                    e.Handled = true;
                    break;
                case Key.F:
                    CopiarRetiro_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.G:
                    CopiarGuia_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.A:
                    CampoBuscarCliente.Focus();
                    e.Handled = true;
                    break;
            }
        }
    }

    private void PrecargarPdf(string ruta)
    {
        rutaUltimoPdf = ruta;
        _ = Task.Run(() =>
        {
            try
            {
                _ = ExtractorOcc.ExtraerOccNvvOcl(ruta);
                _ = ExtractorOcc.ExtraerDespacho(ruta);
                _ = ExtractorOcc.ExtraerProveedor(ruta);
            }
            catch (Exception excepcion)
            {
                Dispatcher.BeginInvoke(() =>
                    MostrarToast($"❌ Error en precarga: {excepcion.Message}", "error")
                );
            }
        });
    }

    private void Copiar(string texto, string mensaje)
    {
        for (int intento = 0; intento < 3; intento++)
        {
            try
            {
                Clipboard.SetText(texto);
                MostrarToast(mensaje, "success");
                return;
            }
            catch (ExternalException) when (intento < 2)
            {
                Thread.Sleep(50);
            }
            catch (Exception excepcion)
            {
                MostrarToast($"❌ Error al copiar: {excepcion.Message}", "error");
                return;
            }
        }
        MostrarToast("❌ No se pudo acceder al portapapeles", "error");
    }

    private void MostrarToast(string mensaje, string tipo)
    {
        TextoToast.Text = mensaje;
        string clave = tipo switch
        {
            "error" => "Hormiguero.Error",
            "warning" => "Hormiguero.Aviso",
            "success" => "Hormiguero.Exito",
            _ => "Hormiguero.Texto",
        };
        TextoToast.SetResourceReference(TextBlock.ForegroundProperty, clave);
        temporizadorToast.Stop();
        temporizadorToast.Start();
    }
}
