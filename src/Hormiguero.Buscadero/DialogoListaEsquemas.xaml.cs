using System.Windows;
using Hormiguero.Nucleo.Datos;

namespace Buscadero.App;

public partial class DialogoListaEsquemas : Window
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conexion;
    private readonly RepositorioEsquemas _repositorio;

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
        new DialogoEsquemaCadena { Owner = this }.ShowDialog();

    private void Editar_Click(object sender, RoutedEventArgs e)
    {
        if (Esquemas.SelectedItem is not EsquemaCadena esquema)
        {
            Estado.Text = "Seleccione un esquema para editar.";
            return;
        }
        new DialogoEsquemaCadena(esquema.Proveedor) { Owner = this }.ShowDialog();
        Recargar();
    }

    private void Desactivar_Click(object sender, RoutedEventArgs e)
    {
        if (Esquemas.SelectedItem is not EsquemaCadena esquema)
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
            _repositorio.Guardar(
                esquema.Proveedor,
                esquema.Nombre,
                esquema
                    .Lugares.Select(l => new DefinicionLugarEsquema(
                        l.Orden,
                        l.IdentificacionId,
                        l.IniciaCadena,
                        l.Nombre
                    ))
                    .ToArray(),
                esquema
                    .Parejas.Select(p => new DefinicionParejaEsquema(
                        esquema.Lugares.First(l => l.Id == p.LugarA).Orden,
                        esquema.Lugares.First(l => l.Id == p.LugarB).Orden,
                        p.DatoDiccionarioId
                    ))
                    .ToArray(),
                activo: false
            );
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
