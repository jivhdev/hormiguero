using System.Windows;
using System.Windows.Controls;
using Buscadero.Core.Alertas;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Utilidades;

namespace Buscadero.App;

public partial class DialogoReglaAlerta : Window
{
    private readonly List<CriterioAlerta> _criterios = [];

    public DialogoReglaAlerta()
    {
        InitializeComponent();
        try
        {
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            _criterios.AddRange(
                new Identificaciones(conexion)
                    .Listar()
                    .OrderBy(i => i.Tipo)
                    .ThenBy(i => i.Emisor)
                    .Select(i => new CriterioAlerta(
                        $"Tipo de documento: {(
                            string.IsNullOrWhiteSpace(i.NombreEstandar)
                                ? $"{i.Tipo} · {i.Emisor}"
                                : i.NombreEstandar
                        )}",
                        null,
                        i.Id
                    ))
            );
            _criterios.AddRange(
                new RepositorioDatosEnlazantes(conexion)
                    .LeerDiccionario()
                    .Select(d => new CriterioAlerta($"Dato: {d.Nombre}", d.Id, null))
            );
            Origen.ItemsSource = _criterios;
            Destino.ItemsSource = _criterios;
            Origen.SelectedIndex = 0;
            Destino.SelectedIndex = _criterios.Count > 1 ? 1 : 0;
            var calendarios = new RepositorioCalendariosFeriados(conexion).ListarCalendarios();
            Calendario.ItemsSource = calendarios;
            Calendario.SelectedItem = calendarios.FirstOrDefault(c => c.Predeterminado);
        }
        catch (Exception error)
        {
            MessageBox.Show(
                this,
                error.Message,
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
        Aviso.Text = "Avisar si falta el documento esperado";
        ActualizarFrase();
    }

    private void ActualizarFrase_Click(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
            ActualizarFrase();
    }

    private void ActualizarFrase()
    {
        if (
            Origen.SelectedItem is not CriterioAlerta origen
            || Destino.SelectedItem is not CriterioAlerta destino
            || !int.TryParse(Dias.Text, out int dias)
            || Modo.SelectedIndex < 0
        )
        {
            Frase.Text = "Complete los datos del aviso.";
            return;
        }
        string fuente = origen.Nombre.Replace("Tipo de documento: ", "").Replace("Dato: ", "");
        string esperado = destino.Nombre.Replace("Tipo de documento: ", "").Replace("Dato: ", "");
        Frase.Text = PresentacionAlertas.CrearFraseRegla(
            fuente,
            dias,
            Modo.SelectedIndex == 0 ? TipoDias.Habiles : TipoDias.Corridos,
            esperado,
            Aviso.Text.Trim()
        );
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (
            Origen.SelectedItem is not CriterioAlerta origen
            || Destino.SelectedItem is not CriterioAlerta destino
            || (
                origen.DatoId == destino.DatoId
                && origen.IdentificacionId == destino.IdentificacionId
            )
            || !int.TryParse(Dias.Text, out int dias)
            || dias is < 0 or > CalculoFechas.CantidadMaxima
            || string.IsNullOrWhiteSpace(Aviso.Text)
        )
        {
            MessageBox.Show(
                this,
                "Elija dos documentos o datos distintos, una espera válida y escriba el aviso.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }
        try
        {
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            new RepositorioAlertas(conexion).CrearReglaCadenaSimple(
                Aviso.Text.Trim(),
                origen.DatoId,
                origen.IdentificacionId,
                destino.DatoId,
                destino.IdentificacionId,
                dias,
                Modo.SelectedIndex == 0 ? TipoDias.Habiles : TipoDias.Corridos,
                Aviso.Text.Trim(),
                (Calendario.SelectedItem as CalendarioFeriados)?.Id
            );
            DialogResult = true;
        }
        catch (Exception error)
        {
            MessageBox.Show(
                this,
                error.Message,
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();

    private sealed record CriterioAlerta(string Nombre, string? DatoId, long? IdentificacionId);
}
