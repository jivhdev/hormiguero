using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Hormiguero.Mensajero.Core;
using Hormiguero.Mensajero.Core.ClickFactura;
using Microsoft.Win32;

namespace Hormiguero.Mensajero.App;

public partial class VentanaClientesFactura : Window
{
    private readonly AlmacenMensajero almacen;

    public VentanaClientesFactura(AlmacenMensajero almacen)
    {
        InitializeComponent();
        this.almacen = almacen;
        Width = Math.Min(Width, SystemParameters.WorkArea.Width);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height);
        CargarClientes();
    }

    private void CargarClientes() =>
        TablaClientes.ItemsSource = almacen.LeerClientesFactura().ToArray();

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        string rut = RutFactura.NormalizarSeguro(CampoRut.Text);
        string razon = CampoRazon.Text.Trim();
        string correo = CampoCorreo.Text.Trim();
        if (
            string.IsNullOrWhiteSpace(rut)
            || string.IsNullOrWhiteSpace(razon)
            || string.IsNullOrWhiteSpace(correo)
        )
        {
            MostrarAviso("Error", "Todos los campos son obligatorios.", false);
            return;
        }
        if (!RutFactura.Validar(rut))
        {
            MostrarAviso("RUT no válido", "Revisa el RUT ingresado.", false);
            return;
        }
        try
        {
            correo = CorreoFactura.NormalizarParaGuardar(correo);
        }
        catch (FormatException excepcion)
        {
            MostrarAviso("Correo no válido", excepcion.Message, false);
            return;
        }
        try
        {
            almacen.GuardarClienteFactura(rut, razon, correo, DateTime.Now);
            MostrarAviso("Éxito", $"Cliente {rut} guardado correctamente.", true);
            CampoRut.Clear();
            CampoRazon.Clear();
            CampoCorreo.Clear();
            CargarClientes();
        }
        catch (Exception excepcion)
        {
            MensajeroLog.RegistrarError("Guardar cliente de facturas", excepcion);
            MostrarAviso("Error", $"No se pudo guardar: {excepcion.Message}", false);
        }
    }

    private void ImportarClientesFactura_Click(object sender, RoutedEventArgs e)
    {
        if (almacen.LeerClientesFactura().Count > 0)
        {
            MostrarAviso("Importar de ClickFactura", "Solo se importa en una lista vacía.", false);
            return;
        }

        string carpetaInicial = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClickFactura",
            "data"
        );
        var dialogo = new OpenFileDialog
        {
            Title = "Seleccionar base de datos de ClickFactura",
            Filter = "Base de datos de ClickFactura (clickfactura.db)|clickfactura.db",
            FileName = "clickfactura.db",
        };
        if (Directory.Exists(carpetaInicial))
            dialogo.InitialDirectory = carpetaInicial;
        if (dialogo.ShowDialog(this) != true)
            return;

        try
        {
            int cantidad = almacen.ImportarClientesFactura(dialogo.FileName);
            CargarClientes();
            MostrarAviso(
                "Importar de ClickFactura",
                $"Se importaron {cantidad} clientes de ClickFactura.",
                true
            );
        }
        catch (Exception excepcion)
        {
            MensajeroLog.RegistrarError("Importar clientes de ClickFactura", excepcion);
            MostrarAviso(
                "Error",
                $"No se pudieron importar los clientes: {excepcion.Message}",
                false
            );
        }
    }

    private void Cliente_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (
            TablaClientes.SelectedItem
            is not Hormiguero.Mensajero.Core.ClickFactura.ClienteFactura cliente
        )
            return;
        CampoRut.Text = cliente.Rut;
        CampoRazon.Text = cliente.RazonSocial;
        CampoCorreo.Text = cliente.Correo;
    }

    private void Cerrar_Click(object sender, RoutedEventArgs e) => Close();

    private void MostrarAviso(string titulo, string mensaje, bool exito)
    {
        if (!exito)
            MensajeroLog.Registrar("AVISO_ERROR", titulo);
        var ventana = new Window
        {
            Title = titulo,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            MinHeight = 150,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        ventana.SetResourceReference(BackgroundProperty, "Hormiguero.Superficie");
        var panel = new StackPanel { Margin = new Thickness(16) };
        var texto = new TextBlock
        {
            Text = mensaje,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
        };
        texto.SetResourceReference(
            TextBlock.ForegroundProperty,
            exito ? "Hormiguero.Exito" : "Hormiguero.Error"
        );
        panel.Children.Add(texto);
        var aceptar = new Button
        {
            Content = "Aceptar",
            MinWidth = 80,
            Padding = new Thickness(10, 5, 10, 5),
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true,
        };
        aceptar.Click += (_, _) => ventana.Close();
        panel.Children.Add(aceptar);
        ventana.Content = panel;
        ventana.ShowDialog();
    }
}
