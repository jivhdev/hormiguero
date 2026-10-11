using System.Windows;
using Hormiguero.Nucleo.Datos;

namespace Buscadero.App;

public partial class DialogoListaEsquemas : Window
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conexion;
    private readonly RepositorioEsquemasDiccionario _repositorio;

    public DialogoListaEsquemas()
    {
        InitializeComponent();
        _conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        _repositorio = new(_conexion);
        Recargar();
    }

    private void Recargar() =>
        Esquemas.ItemsSource = _repositorio.Listar().Where(e => e.Activo).ToArray();

    private void Nuevo_Click(object sender, RoutedEventArgs e) =>
        new DialogoEsquemaDiccionario { Owner = this }.ShowDialog();

    private void Editar_Click(object sender, RoutedEventArgs e)
    {
        if (Esquemas.SelectedItem is not EsquemaDiccionario esquema)
        {
            Estado.Text = "Seleccione un esquema para editar.";
            return;
        }
        new DialogoEsquemaDiccionario(esquema.Proveedor) { Owner = this }.ShowDialog();
        Recargar();
    }

    private void Desactivar_Click(object sender, RoutedEventArgs e)
    {
        if (Esquemas.SelectedItem is not EsquemaDiccionario esquema)
        {
            Estado.Text = "Seleccione un esquema para desactivar.";
            return;
        }
        if (
            MessageBox.Show(
                this,
                $"¿Desactivar el esquema de {esquema.Proveedor}?",
                "Desactivar esquema",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            ) != MessageBoxResult.Yes
        )
            return;
        try
        {
            _repositorio.Desactivar(esquema.Id);
            Estado.Text = "Esquema desactivado.";
            Recargar();
        }
        catch (Exception ex)
        {
            Estado.Text = $"No se pudo desactivar el esquema: {ex.Message}";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _conexion.Dispose();
        base.OnClosed(e);
    }
}
