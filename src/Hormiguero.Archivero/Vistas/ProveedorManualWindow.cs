using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Archivero.Datos;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;

namespace Archivero.Vistas;

public sealed class ProveedorManualWindow : Window
{
    private readonly EntidadRepository _entidades = new();
    private readonly ComboBox _proveedor = new()
    {
        IsEditable = true,
        MinWidth = 330,
        Margin = new Thickness(0, 8, 0, 14),
    };

    public string Proveedor { get; private set; } = string.Empty;

    public ProveedorManualWindow()
    {
        Title = "Proveedor";
        Width = 410;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var contenido = new StackPanel { Margin = new Thickness(18) };
        contenido.Children.Add(
            new TextBlock
            {
                Text = "¿A qué proveedor corresponde este documento?",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
            }
        );
        contenido.Children.Add(
            new TextBlock
            {
                Text = "Elige un proveedor conocido o escribe uno nuevo.",
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            }
        );
        _proveedor.ItemsSource = _entidades.Buscar(CategoriaEntidad.Emisor, string.Empty);
        _proveedor.AddHandler(
            TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler(
                (_, _) =>
                    _proveedor.ItemsSource = _entidades.Buscar(
                        CategoriaEntidad.Emisor,
                        _proveedor.Text
                    )
            )
        );
        contenido.Children.Add(_proveedor);

        var botones = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
        };
        var cancelar = new Button
        {
            Content = "Cancelar",
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 0, 8, 0),
        };
        cancelar.Click += (_, _) => DialogResult = false;
        var guardar = new Button { Content = "Continuar", Padding = new Thickness(10, 5, 10, 5) };
        guardar.Click += (_, _) => GuardarProveedor();
        botones.Children.Add(cancelar);
        botones.Children.Add(guardar);
        contenido.Children.Add(botones);
        Content = contenido;
    }

    private void GuardarProveedor()
    {
        var nombre = _proveedor.Text.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            MessageBox.Show(
                this,
                "Escribe o elige el proveedor.",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }

        Proveedor = nombre;
        _entidades.ObtenerOCrear(CategoriaEntidad.Emisor, nombre);
        DialogResult = true;
    }
}
