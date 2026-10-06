using System.Windows;
using Archivero.Datos;

namespace Archivero.Vistas;

/// <summary>
/// Pregunta del final de "Crear ubicación nueva" (Caso-11, punto 4). Solo elige el nombre: el
/// atajo se persiste recién después de que el documento se guardó bien, no aquí.
/// </summary>
public partial class GuardarAtajoWindow : Window
{
    private readonly AtajoGuardadoRapidoRepository _atajos;
    private string _nombreBase = string.Empty;
    private string _nombreSugeridoActual = string.Empty;

    public string? NombreElegido { get; private set; }
    public PeriodoAtajo PeriodoElegido => (PeriodoAtajo)CmbPeriodo.SelectedValue;
    public int? AnioFijoElegido =>
        PeriodoElegido == PeriodoAtajo.AnioFijo && int.TryParse(TxtAnio.Text, out var anio)
            ? anio
            : null;

    public GuardarAtajoWindow(string nombreSugerido, AtajoGuardadoRapidoRepository atajos)
    {
        InitializeComponent();
        _atajos = atajos;
        _nombreBase = nombreSugerido;
        _nombreSugeridoActual = nombreSugerido;
        TxtNombreAtajo.Text = nombreSugerido;
        CmbPeriodo.ItemsSource = new[]
        {
            new OpcionPeriodo(PeriodoAtajo.PreguntarFechaCadaVez, "Preguntar fecha cada vez"),
            new OpcionPeriodo(PeriodoAtajo.AnioEnCurso, "Año en curso"),
            new OpcionPeriodo(PeriodoAtajo.MesEnCurso, "Mes en curso"),
            new OpcionPeriodo(PeriodoAtajo.AnioFijo, "Año fijo"),
        };
        CmbPeriodo.DisplayMemberPath = nameof(OpcionPeriodo.Nombre);
        CmbPeriodo.SelectedValuePath = nameof(OpcionPeriodo.Valor);
        CmbPeriodo.SelectedValue = PeriodoAtajo.PreguntarFechaCadaVez;
        Loaded += (_, _) =>
        {
            TxtNombreAtajo.Focus();
            TxtNombreAtajo.SelectAll();
        };
    }

    private void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        var nombre = TxtNombreAtajo.Text.Trim();
        if (nombre.Length == 0)
        {
            TxtError.Text = "Escribir un nombre para el acceso rápido.";
            TxtError.Visibility = Visibility.Visible;
            return;
        }

        var anioValido = int.TryParse(TxtAnio.Text, out var anioFijo);
        if (PeriodoElegido == PeriodoAtajo.AnioFijo && !anioValido)
        {
            TxtError.Text = "Escribir un año fijo válido.";
            TxtError.Visibility = Visibility.Visible;
            return;
        }
        else if (PeriodoElegido == PeriodoAtajo.AnioFijo && (anioFijo is < 1 or > 9999))
        {
            TxtError.Text = "Escribir un año entre 1 y 9999.";
            TxtError.Visibility = Visibility.Visible;
            return;
        }

        if (_atajos.ExisteNombre(nombre))
        {
            var reemplazar = System.Windows.MessageBox.Show(
                this,
                $"Ya existe un acceso rápido llamado \"{nombre}\". ¿Reemplazarlo por este?",
                "Archivero",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (reemplazar != MessageBoxResult.Yes)
            {
                return;
            }
        }

        NombreElegido = nombre;
        DialogResult = true;
    }

    private void CmbPeriodo_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e
    )
    {
        if (PanelAnio is not null)
            PanelAnio.Visibility =
                CmbPeriodo.SelectedValue is PeriodoAtajo.AnioFijo
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        ActualizarNombreSugerido();
    }

    private void TxtAnio_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e
    ) => ActualizarNombreSugerido();

    private void ActualizarNombreSugerido()
    {
        if (CmbPeriodo.SelectedValue is not PeriodoAtajo periodo || TxtNombreAtajo is null)
            return;
        if (TxtNombreAtajo.Text != _nombreSugeridoActual)
            return;

        var sufijo = periodo switch
        {
            PeriodoAtajo.AnioEnCurso => "Año en curso",
            PeriodoAtajo.MesEnCurso => "Mes en curso",
            PeriodoAtajo.AnioFijo when int.TryParse(TxtAnio.Text, out var anio) => $"Año {anio}",
            _ => null,
        };
        _nombreSugeridoActual = sufijo is null ? _nombreBase : $"{_nombreBase} · {sufijo}";
        TxtNombreAtajo.Text = _nombreSugeridoActual;
    }

    private record OpcionPeriodo(PeriodoAtajo Valor, string Nombre);
}
