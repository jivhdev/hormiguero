using System.Windows;
using System.Windows.Controls;
using Hormiguero.Nucleo.Datos;
using Microsoft.Win32;

namespace Buscadero.App;

public partial class DialogoFeriados : Window
{
    private readonly string _ruta = DocumentosGuardados.RutaBaseComun;

    public DialogoFeriados()
    {
        InitializeComponent();
        Fecha.SelectedDate = DateTime.Today;
        CargarCalendarios();
    }

    private RepositorioCalendariosFeriados Abrir(
        out Microsoft.Data.Sqlite.SqliteConnection conexion
    )
    {
        conexion = BaseComun.Abrir(_ruta);
        return new RepositorioCalendariosFeriados(conexion);
    }

    private void CargarCalendarios()
    {
        try
        {
            var repo = Abrir(out var conexion);
            using (conexion)
                Calendarios.ItemsSource = repo.ListarCalendarios();
            Calendarios.SelectedItem = (
                Calendarios.ItemsSource as IReadOnlyList<CalendarioFeriados>
            )?.FirstOrDefault(c => c.Predeterminado);
            CargarFeriados();
        }
        catch (Exception error)
        {
            Error(error);
        }
    }

    private void CargarFeriados()
    {
        if (Calendarios.SelectedItem is not CalendarioFeriados calendario)
            return;
        try
        {
            var repo = Abrir(out var conexion);
            using (conexion)
                Lista.ItemsSource = repo.ListarFeriados(calendario.Id)
                    .Select(f => new
                    {
                        f.Id,
                        f.Fecha,
                        f.Nombre,
                        Activo = f.Activo ? "Activo" : "Anulado",
                    })
                    .ToList();
        }
        catch (Exception error)
        {
            Error(error);
        }
    }

    private void Calendarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized)
            CargarFeriados();
    }

    private void Agregar_Click(object sender, RoutedEventArgs e)
    {
        if (
            Calendarios.SelectedItem is not CalendarioFeriados calendario
            || Fecha.SelectedDate is not DateTime fecha
            || string.IsNullOrWhiteSpace(Nombre.Text)
        )
        {
            Error("Elija un calendario, una fecha y escriba el nombre del feriado.");
            return;
        }
        try
        {
            var repo = Abrir(out var conexion);
            using (conexion)
                repo.AgregarFeriado(calendario.Id, DateOnly.FromDateTime(fecha), Nombre.Text);
            Nombre.Clear();
            CargarFeriados();
        }
        catch (Exception error)
        {
            Error(error);
        }
    }

    private void Anular_Click(object sender, RoutedEventArgs e)
    {
        if (
            Lista.SelectedItem is not { } seleccionado
            || Calendarios.SelectedItem is not CalendarioFeriados calendario
        )
            return;
        long id = (long)seleccionado.GetType().GetProperty("Id")!.GetValue(seleccionado)!;
        try
        {
            var repo = Abrir(out var conexion);
            using (conexion)
                repo.AnularFeriado(id);
            CargarFeriados();
        }
        catch (Exception error)
        {
            Error(error);
        }
    }

    private void Importar_Click(object sender, RoutedEventArgs e)
    {
        if (Calendarios.SelectedItem is not CalendarioFeriados calendario)
            return;
        var dialogo = new OpenFileDialog { Filter = "Archivo CSV (*.csv)|*.csv" };
        if (dialogo.ShowDialog(this) != true)
            return;
        try
        {
            var repo = Abrir(out var conexion);
            using (conexion)
                repo.ImportarCsv(calendario.Id, dialogo.FileName);
            CargarFeriados();
        }
        catch (Exception error)
        {
            Error(error);
        }
    }

    private void Exportar_Click(object sender, RoutedEventArgs e)
    {
        if (Calendarios.SelectedItem is not CalendarioFeriados calendario)
            return;
        var dialogo = new SaveFileDialog
        {
            Filter = "Archivo CSV (*.csv)|*.csv",
            FileName = "feriados.csv",
        };
        if (dialogo.ShowDialog(this) != true)
            return;
        try
        {
            var repo = Abrir(out var conexion);
            using (conexion)
                repo.ExportarCsv(calendario.Id, dialogo.FileName);
        }
        catch (Exception error)
        {
            Error(error);
        }
    }

    private void Cerrar_Click(object sender, RoutedEventArgs e) => Close();

    private void Error(Exception error) =>
        MessageBox.Show(
            this,
            error.Message,
            "Buscadero",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );

    private void Error(string mensaje) =>
        MessageBox.Show(
            this,
            mensaje,
            "Buscadero",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );
}
