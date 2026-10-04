using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Hormiguero.Mensajero.Core;

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
        string rut = CampoRut.Text.Trim();
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
            MostrarAviso("Error", $"No se pudo guardar: {excepcion.Message}", false);
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
