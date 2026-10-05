using System.Windows;
using System.Windows.Controls;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Utilidades;

namespace Buscadero.App;

public partial class DialogoCalculadoraFechas : Window
{
    public DialogoCalculadoraFechas()
    {
        InitializeComponent();
        Fecha.SelectedDate = DateTime.Today;
        try
        {
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            Calendario.ItemsSource = new RepositorioCalendariosFeriados(
                conexion
            ).ListarCalendarios();
            Calendario.SelectedItem = (
                Calendario.ItemsSource as IReadOnlyList<CalendarioFeriados>
            )?.FirstOrDefault(c => c.Predeterminado);
        }
        catch (Exception error)
        {
            Resultado.Text = $"No se cargaron los calendarios: {error.Message}";
        }
        Loaded += (_, _) => Calcular();
    }

    private void Cambiar_Click(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
            Calcular();
    }

    private void Calcular()
    {
        if (
            Fecha.SelectedDate is not DateTime fecha
            || !int.TryParse(Cantidad.Text, out int cantidad)
            || Modo.SelectedIndex < 0
        )
            return;
        try
        {
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            var feriados = Calendario.SelectedItem is CalendarioFeriados calendario
                ? new RepositorioCalendariosFeriados(conexion).ObtenerFeriadosActivos(
                    calendario.Id,
                    1,
                    9999
                )
                : null;
            var tipo = Modo.SelectedIndex == 0 ? TipoDias.Habiles : TipoDias.Corridos;
            var resultado = CalculoFechas.Sumar(
                DateOnly.FromDateTime(fecha),
                cantidad,
                tipo,
                feriados
            );
            Resultado.Text = $"Resultado: {resultado:dd/MM/yyyy}";
        }
        catch (Exception error)
        {
            Resultado.Text = $"No se pudo calcular: {error.Message}";
        }
    }
}
