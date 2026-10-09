using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Hormiguero.Mensajero.Core;
using Hormiguero.Mensajero.Core.ClickFactura;

namespace Hormiguero.Mensajero.App;

public partial class VentanaEnvioConjuntoOcc : Window
{
    private readonly string carpetaOcc;
    private readonly AlmacenMensajero almacen;
    private readonly ObservableCollection<OccEnvioConjunto> ordenes = [];
    private string? proveedorDestinatarioActual;

    public VentanaEnvioConjuntoOcc(string carpetaOcc, AlmacenMensajero almacen)
    {
        InitializeComponent();
        this.carpetaOcc = carpetaOcc;
        this.almacen = almacen;
        ListaOrdenes.ItemsSource = ordenes;
        ordenes.CollectionChanged += Ordenes_CollectionChanged;
        ActualizarVista();
    }

    private void UltimoPdf_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(carpetaOcc))
        {
            MostrarAviso("No hay una carpeta de OCC configurada.");
            return;
        }

        string? ruta = CarpetaOcc.UltimoPdf(carpetaOcc);
        if (ruta is null)
        {
            MostrarAviso("No hay PDF en la carpeta.");
            return;
        }
        AgregarPdf(ruta);
    }

    private void Buscar_Click(object sender, RoutedEventArgs e) => BuscarPdf();

    private void Buscar_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BuscarPdf();
            e.Handled = true;
        }
    }

    private void BuscarPdf()
    {
        string numero = CampoBuscar.Text.Trim();
        if (numero.Length == 0)
        {
            MostrarAviso("Ingresa un número de OCC.");
            return;
        }
        if (!Directory.Exists(carpetaOcc))
        {
            MostrarAviso("No hay una carpeta de OCC configurada.");
            return;
        }

        IReadOnlyList<string> resultados = CarpetaOcc.BuscarPorNumero(carpetaOcc, numero);
        if (resultados.Count == 0)
            MostrarAviso($"No se encontró la OCC {numero}.");
        else if (resultados.Count > 1)
            MostrarAviso($"Hay varios PDF para el número {numero}.");
        else
            AgregarPdf(resultados[0]);
    }

    private void AgregarPdf(string ruta)
    {
        try
        {
            var datos = ExtractorOcc.ExtraerOccNvvOcl(ruta);
            if (string.IsNullOrWhiteSpace(datos.Occ))
            {
                MostrarAviso($"No se pudo extraer la OCC de {Path.GetFileName(ruta)}.");
                return;
            }

            string proveedor = ExtractorOcc.ExtraerProveedor(ruta);
            var nueva = new OccEnvioConjunto(datos.Occ, datos.Nvv, datos.Ocl, proveedor, ruta);
            if (ordenes.Count > 0 && ordenes[0].Proveedor != proveedor)
            {
                MostrarAviso(
                    $"La OCC es de {proveedor}; la lista contiene OCC de {ordenes[0].Proveedor}."
                );
                return;
            }
            if (!EnvioConjuntoOcc.PuedeAgregar(ordenes, nueva))
            {
                MostrarAviso($"La OCC {datos.Occ} ya está en la lista.");
                return;
            }

            if (ordenes.Count == 0)
            {
                string clave = AlmacenMensajero.NormalizarProveedor(proveedor);
                if (proveedorDestinatarioActual != clave)
                {
                    CampoDestinatario.Text = almacen.BuscarCorreosProveedor(proveedor);
                    proveedorDestinatarioActual = clave;
                }
            }
            ordenes.Add(nueva);
            MostrarAviso($"OCC {datos.Occ} agregada.");
        }
        catch (Exception excepcion)
        {
            MensajeroLog.RegistrarError("Agregar OCC al envío conjunto", excepcion);
            MostrarAviso($"No se pudo leer el PDF: {excepcion.Message}");
        }
    }

    private void Quitar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: OccEnvioConjunto orden })
            ordenes.Remove(orden);
    }

    private void Limpiar_Click(object sender, RoutedEventArgs e) => ordenes.Clear();

    private void Ordenes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        ActualizarVista();

    private void ActualizarVista()
    {
        if (ordenes.Count == 0)
        {
            TextoProveedor.Text = "Proveedor: —";
            CampoAsunto.Clear();
            CampoCuerpo.Clear();
            return;
        }

        TextoProveedor.Text = $"Proveedor: {ordenes[0].Proveedor}";
        CampoAsunto.Text = EnvioConjuntoOcc.FormatearAsunto(ordenes);
        try
        {
            string despacho = ExtractorOcc.ExtraerDespacho(ordenes[0].RutaPdf);
            CampoCuerpo.Text = EnvioConjuntoOcc.GenerarCuerpo(despacho, ordenes);
        }
        catch (Exception excepcion)
        {
            MensajeroLog.RegistrarError("Preparar cuerpo de envío conjunto", excepcion);
            CampoCuerpo.Clear();
            MostrarAviso($"No se pudo leer el cuerpo del correo: {excepcion.Message}");
        }
    }

    private void CopiarDestinatario_Click(object sender, RoutedEventArgs e)
    {
        if (ordenes.Count == 0 || string.IsNullOrWhiteSpace(CampoDestinatario.Text))
        {
            MostrarAviso("Agrega una OCC e ingresa el correo del proveedor.");
            return;
        }

        try
        {
            string correos = CorreoFactura.NormalizarParaGuardar(CampoDestinatario.Text);
            CopiarTexto(correos, "Destinatario copiado.");
            RecordarCorreoProveedor(correos);
        }
        catch (FormatException excepcion)
        {
            MostrarAviso(excepcion.Message);
        }
    }

    private void CopiarAsunto_Click(object sender, RoutedEventArgs e) =>
        CopiarTexto(CampoAsunto.Text, "Asunto copiado.");

    private void CopiarCuerpo_Click(object sender, RoutedEventArgs e) =>
        CopiarTexto(PrepararCuerpoCorreo(CampoCuerpo.Text), "Cuerpo copiado.");

    private void AbrirEnGmail_Click(object sender, RoutedEventArgs e)
    {
        if (
            ordenes.Count == 0
            || string.IsNullOrWhiteSpace(CampoDestinatario.Text)
            || string.IsNullOrWhiteSpace(CampoAsunto.Text)
        )
        {
            MostrarAviso("Agrega una OCC e ingresa el correo del proveedor.");
            return;
        }

        try
        {
            string correos = CorreoFactura.NormalizarParaGuardar(CampoDestinatario.Text);
            UrlGmailFactura resultado = GeneradorUrlGmailFactura.Generar(
                CorreoFactura.Separar(correos),
                CampoAsunto.Text,
                PrepararCuerpoCorreo(CampoCuerpo.Text)
            );
            Process.Start(new ProcessStartInfo(resultado.Url) { UseShellExecute = true });
            RecordarCorreoProveedor(correos);
            MostrarAviso(
                resultado.OmitioCuerpo
                    ? "Gmail abierto. Copia el cuerpo y los PDF antes de enviar."
                    : "Gmail abierto. Copia los PDF con Ctrl+V antes de enviar."
            );
        }
        catch (Exception excepcion)
        {
            MensajeroLog.RegistrarError("Abrir envío conjunto en Gmail", excepcion);
            MostrarAviso($"No se pudo abrir Gmail: {excepcion.Message}");
        }
    }

    private void CopiarPdfs_Click(object sender, RoutedEventArgs e)
    {
        if (ordenes.Count == 0)
        {
            MostrarAviso("Agrega al menos una OCC.");
            return;
        }

        try
        {
            var rutas = new StringCollection();
            foreach (OccEnvioConjunto orden in ordenes)
            {
                if (!File.Exists(orden.RutaPdf))
                {
                    MostrarAviso($"No se encontró {Path.GetFileName(orden.RutaPdf)}.");
                    return;
                }
                rutas.Add(orden.RutaPdf);
            }
            Clipboard.SetFileDropList(rutas);
            MostrarAviso($"{rutas.Count} PDF listos para pegar.");
        }
        catch (Exception excepcion)
        {
            MensajeroLog.RegistrarError("Copiar PDF del envío conjunto", excepcion);
            MostrarAviso($"No se pudieron copiar los PDF: {excepcion.Message}");
        }
    }

    private static string PrepararCuerpoCorreo(string texto) =>
        string.IsNullOrEmpty(texto) ? string.Empty : texto.TrimEnd() + "\r\n\r\n";

    private void RecordarCorreoProveedor(string correos)
    {
        if (ordenes.Count == 0 || string.IsNullOrWhiteSpace(CampoDestinatario.Text))
            return;
        almacen.GuardarCorreosProveedor(ordenes[0].Proveedor, correos);
        proveedorDestinatarioActual = AlmacenMensajero.NormalizarProveedor(ordenes[0].Proveedor);
    }

    private void CopiarTexto(string texto, string aviso)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            MostrarAviso("Agrega una OCC y genera el contenido primero.");
            return;
        }

        for (int intento = 0; intento < 3; intento++)
        {
            try
            {
                Clipboard.SetText(texto);
                MostrarAviso(aviso);
                return;
            }
            catch (ExternalException) when (intento < 2)
            {
                Thread.Sleep(50);
            }
            catch (Exception excepcion)
            {
                MensajeroLog.RegistrarError("Copiar datos del envío conjunto", excepcion);
                MostrarAviso($"No se pudo acceder al portapapeles: {excepcion.Message}");
                return;
            }
        }
        MostrarAviso("No se pudo acceder al portapapeles.");
    }

    private void MostrarAviso(string mensaje) => TextoAviso.Text = mensaje;
}
