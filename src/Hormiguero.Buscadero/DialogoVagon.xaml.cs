using System.Windows;
using Buscadero.Core.Lineas;

namespace Buscadero.App;

public partial class DialogoVagon : Window
{
    private const long CrearNuevo = -1;

    public sealed class OpcionModelo
    {
        public required long Id { get; init; }
        public required string Etiqueta { get; init; }
    }

    public DialogoVagon()
    {
        InitializeComponent();
        Loaded += (_, _) => TextoNombre.Focus();
    }

    public string Nombre => TextoNombre.Text.Trim();

    public bool EsMultiple => CheckMultiple.IsChecked == true;

    public bool EsAnexo => CheckAnexo.IsChecked == true;

    public static bool Pedir(
        Window propietario,
        string titulo,
        string nombreInicial,
        bool multipleInicial,
        bool anexoInicial,
        long? modeloHijoInicialId,
        IReadOnlyList<PlantillaLinea> modelos,
        out string nombre,
        out bool esMultiple,
        out bool esAnexo,
        out long? modeloHijoId,
        out bool crearModeloNuevo
    )
    {
        var dialogo = new DialogoVagon { Owner = propietario, Title = titulo };
        dialogo.TextoNombre.Text = nombreInicial;
        dialogo.CheckMultiple.IsChecked = multipleInicial;
        dialogo.CheckAnexo.IsChecked = anexoInicial;

        var opciones = modelos
            .Select(m => new OpcionModelo { Id = m.Id, Etiqueta = m.Nombre })
            .ToList();
        opciones.Add(new OpcionModelo { Id = CrearNuevo, Etiqueta = "(Crear modelo nuevo...)" });
        dialogo.ComboModeloHijo.ItemsSource = opciones;
        dialogo.ComboModeloHijo.SelectedItem =
            opciones.FirstOrDefault(o => o.Id == modeloHijoInicialId) ?? opciones[0];
        dialogo.PanelModeloHijo.IsEnabled = multipleInicial;

        var ok = dialogo.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialogo.Nombre);
        var seleccionado = dialogo.ComboModeloHijo.SelectedItem as OpcionModelo;

        nombre = dialogo.Nombre;
        esMultiple = dialogo.EsMultiple;
        esAnexo = dialogo.EsAnexo;
        crearModeloNuevo = esMultiple && seleccionado?.Id == CrearNuevo;
        modeloHijoId =
            seleccionado is null || seleccionado.Id == CrearNuevo ? null : seleccionado.Id;
        return ok;
    }

    private void CheckMultiple_Changed(object sender, RoutedEventArgs e) =>
        PanelModeloHijo.IsEnabled = CheckMultiple.IsChecked == true;

    private void Aceptar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            MessageBox.Show(
                this,
                "Ingrese un nombre para el documento.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }

        if (EsMultiple && ComboModeloHijo.SelectedItem is null)
        {
            MessageBox.Show(
                this,
                "Elija el modelo de cadena hija del documento ramificado.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }

        DialogResult = true;
    }
}
