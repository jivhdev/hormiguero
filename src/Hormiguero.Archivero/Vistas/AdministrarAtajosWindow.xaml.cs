using System.Windows;
using System.Windows.Controls;
using Archivero.Datos;
using Archivero.Servicios;

namespace Archivero.Vistas;

public partial class AdministrarAtajosWindow : Window
{
    private readonly AtajoGuardadoRapidoRepository _repositorio = new();
    private List<AtajoGuardadoRapido> _atajos = [];

    private sealed record Opcion<T>(T Valor, string Nombre);

    public AdministrarAtajosWindow()
    {
        InitializeComponent();
        CmbFormato.ItemsSource = Enum.GetValues<FormatoCarpeta>()
            .Select(formato => new Opcion<FormatoCarpeta>(
                formato,
                OrganizacionCarpetaService.NombreDe(formato)
            ))
            .ToList();
        CmbFormato.DisplayMemberPath = nameof(Opcion<FormatoCarpeta>.Nombre);
        CmbFormato.SelectedValuePath = nameof(Opcion<FormatoCarpeta>.Valor);
        CmbPeriodo.ItemsSource = new[]
        {
            new Opcion<PeriodoAtajo>(
                PeriodoAtajo.PreguntarFechaCadaVez,
                "Preguntar fecha cada vez"
            ),
            new Opcion<PeriodoAtajo>(PeriodoAtajo.AnioEnCurso, "Año en curso"),
            new Opcion<PeriodoAtajo>(PeriodoAtajo.MesEnCurso, "Mes en curso"),
            new Opcion<PeriodoAtajo>(PeriodoAtajo.AnioFijo, "Año fijo"),
        };
        CmbPeriodo.DisplayMemberPath = nameof(Opcion<PeriodoAtajo>.Nombre);
        CmbPeriodo.SelectedValuePath = nameof(Opcion<PeriodoAtajo>.Valor);
        Cargar();
    }

    private void Cargar()
    {
        _atajos = _repositorio.ObtenerTodos();
        Lista.ItemsSource = _atajos;
        Lista.SelectedIndex = _atajos.Count == 0 ? -1 : 0;
    }

    private void Lista_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Lista.SelectedItem is not AtajoGuardadoRapido atajo)
            return;
        TxtNombre.Text = atajo.Nombre;
        TxtCarpeta.Text = atajo.CarpetaMadre;
        CmbFormato.SelectedValue = atajo.Formato;
        TxtPatron.Text = atajo.Patron ?? string.Empty;
        CmbPeriodo.SelectedValue = atajo.Periodo;
        TxtAnio.Text = atajo.AnioFijo?.ToString() ?? string.Empty;
        TxtError.Visibility = Visibility.Collapsed;
    }

    private void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        if (Lista.SelectedItem is not AtajoGuardadoRapido original)
            return;
        var nombre = TxtNombre.Text.Trim();
        var carpeta = TxtCarpeta.Text.Trim();
        if (
            nombre.Length == 0
            || carpeta.Length == 0
            || CmbFormato.SelectedValue is not FormatoCarpeta formato
            || CmbPeriodo.SelectedValue is not PeriodoAtajo periodo
        )
        {
            MostrarError("Completar el nombre, la carpeta, la organización y el período.");
            return;
        }
        int? anio = null;
        if (periodo == PeriodoAtajo.AnioFijo)
        {
            if (!int.TryParse(TxtAnio.Text, out var valor) || valor is < 1 or > 9999)
            {
                MostrarError("Escribir un año entre 1 y 9999.");
                return;
            }
            anio = valor;
        }
        try
        {
            _repositorio.Actualizar(
                original with
                {
                    Nombre = nombre,
                    CarpetaMadre = carpeta,
                    Formato = formato,
                    Patron = string.IsNullOrWhiteSpace(TxtPatron.Text)
                        ? null
                        : TxtPatron.Text.Trim(),
                    Periodo = periodo,
                    AnioFijo = anio,
                }
            );
            Cargar();
        }
        catch (Exception ex)
        {
            MostrarError($"No se pudieron guardar los cambios: {ex.Message}");
        }
    }

    private void BtnEliminar_Click(object sender, RoutedEventArgs e)
    {
        if (Lista.SelectedItem is not AtajoGuardadoRapido atajo)
            return;
        if (
            System.Windows.MessageBox.Show(
                this,
                $"¿Eliminar el acceso rápido «{atajo.Nombre}»?",
                "Archivero",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            ) != MessageBoxResult.Yes
        )
            return;
        _repositorio.Eliminar(atajo.Id);
        Cargar();
    }

    private void BtnSubir_Click(object sender, RoutedEventArgs e) => Mover(-1);

    private void BtnBajar_Click(object sender, RoutedEventArgs e) => Mover(1);

    private void Mover(int direccion)
    {
        if (Lista.SelectedItem is not AtajoGuardadoRapido atajo)
            return;
        var indice = Lista.SelectedIndex;
        _repositorio.Mover(atajo.Id, direccion);
        Cargar();
        Lista.SelectedIndex = Math.Clamp(indice + direccion, 0, Math.Max(0, Lista.Items.Count - 1));
    }

    private void CmbPeriodo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelAnio is not null)
            PanelAnio.Visibility =
                CmbPeriodo.SelectedValue is PeriodoAtajo.AnioFijo
                    ? Visibility.Visible
                    : Visibility.Collapsed;
    }

    private void MostrarError(string mensaje)
    {
        TxtError.Text = mensaje;
        TxtError.Visibility = Visibility.Visible;
    }
}
