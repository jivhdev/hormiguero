using System.Windows;
using Buscadero.Core.Lineas;

namespace Buscadero.App;

public sealed class OpcionDocumentoRegla
{
    public required long Id { get; init; }
    public required string Nombre { get; init; }
}

public partial class DialogoReglaVagon : Window
{
    private sealed class OpcionDestino
    {
        public long Id { get; init; }
        public required string Nombre { get; init; }
    }

    private readonly ServicioLineas _lineas;
    private readonly IReadOnlyList<OpcionDocumentoRegla> _documentos;
    private readonly IReadOnlyList<OpcionReglaVagon> _configuraciones;
    private readonly HashSet<long> _destinosGuardados = new();

    public DialogoReglaVagon(ServicioLineas lineas, IReadOnlyList<OpcionDocumentoRegla> documentos)
    {
        InitializeComponent();
        _lineas = lineas;
        _documentos = documentos;
        _configuraciones = lineas.ListarOpcionesRegla();
        ComboDestino.ItemsSource = new[]
        {
            new OpcionDestino { Id = 0, Nombre = "No completar automáticamente" },
        }.Concat(documentos.Select(d => new OpcionDestino { Id = d.Id, Nombre = d.Nombre })).ToList();
        ComboDestino.SelectedIndex = 0;
        ComboConfiguracion.ItemsSource = _configuraciones;
        ComboConfiguracion.SelectedIndex = _configuraciones.Count == 0 ? -1 : 0;
        CheckEspacios.IsChecked = true;
        TextoSinDatos.Visibility =
            _configuraciones.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Loaded += (_, _) => ActualizarPantalla();
    }

    private OpcionDestino? Destino => ComboDestino.SelectedItem as OpcionDestino;
    private OpcionReglaVagon? Configuracion => ComboConfiguracion.SelectedItem as OpcionReglaVagon;
    private OpcionCampoRegla? CampoOrigen => ComboCampoOrigen.SelectedItem as OpcionCampoRegla;
    private OpcionDocumentoRegla? Comparacion =>
        ComboComparacion.SelectedItem as OpcionDocumentoRegla;
    private OpcionCampoRegla? CampoComparacion =>
        ComboCampoComparacion.SelectedItem as OpcionCampoRegla;

    private void Configuracion_Changed(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e
    )
    {
        if (!IsLoaded)
            return;
        var configuracion = Configuracion;
        ComboCampoOrigen.ItemsSource = configuracion?.Campos ?? Array.Empty<OpcionCampoRegla>();
        ComboCampoOrigen.SelectedIndex = configuracion?.Campos.Count > 0 ? 0 : -1;
        ActualizarPantalla();
    }

    private void Seleccion_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
            ActualizarPantalla();
    }

    private void ActualizarPantalla()
    {
        bool activa = Destino?.Id > 0 && _configuraciones.Count > 0;
        ComboConfiguracion.IsEnabled = activa;
        ComboCampoOrigen.IsEnabled = activa;
        ComboComparacion.IsEnabled = activa;
        ComboCampoComparacion.IsEnabled = activa;
        CheckEspacios.IsEnabled = activa;
        CheckGuiones.IsEnabled = activa;
        CheckCeros.IsEnabled = activa;
        TextoLargo.IsEnabled = activa;

        var comparaciones = _documentos.Where(d => d.Id != Destino?.Id).ToList();
        var comparacionAnterior = Comparacion?.Id;
        ComboComparacion.ItemsSource = comparaciones;
        ComboComparacion.SelectedItem =
            comparaciones.FirstOrDefault(d => d.Id == comparacionAnterior)
            ?? comparaciones.FirstOrDefault();

        var campos = _configuraciones
            .SelectMany(configuracion =>
                configuracion.Campos.Select(campo => new OpcionCampoRegla(
                    campo.Id,
                    campo.Nombre,
                    configuracion.Nombre
                ))
            )
            .DistinctBy(campo => campo.Id)
            .ToList();
        ComboCampoComparacion.ItemsSource = campos;
        if (
            ComboCampoComparacion.SelectedItem is not OpcionCampoRegla campo
            || !campos.Contains(campo)
        )
            ComboCampoComparacion.SelectedIndex = campos.Count > 0 ? 0 : -1;

        if (!activa)
        {
            TextoFrase.Text = "No completar automáticamente";
            return;
        }

        if (CampoOrigen is null || Comparacion is null || CampoComparacion is null)
        {
            TextoFrase.Text = "Elija los datos que se compararán.";
            return;
        }

        TextoFrase.Text = TextoReglaVagon.CrearFrase(
            _documentos.First(d => d.Id == Destino!.Id).Nombre,
            Configuracion!.Nombre,
            CampoOrigen.Nombre,
            Comparacion.Nombre,
            CampoComparacion.Nombre
        );
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (GuardarReglaActual())
            DialogResult = true;
    }

    private void GuardarOtra_Click(object sender, RoutedEventArgs e)
    {
        if (Destino?.Id is not > 0)
        {
            MessageBox.Show(this, "Elija el documento que se completará.", "Buscadero");
            return;
        }
        if (GuardarReglaActual())
            ComboDestino.SelectedIndex = 0;
    }

    private bool GuardarReglaActual()
    {
        if (Destino is null)
        {
            MessageBox.Show(
                this,
                "Elija un documento o no completar automáticamente.",
                "Buscadero"
            );
            return false;
        }
        if (Destino.Id > 0)
        {
            if (_destinosGuardados.Contains(Destino.Id))
            {
                MessageBox.Show(this, "Ya agregó una regla para ese documento.", "Buscadero");
                return false;
            }
            if (
                Configuracion is null
                || CampoOrigen is null
                || Comparacion is null
                || CampoComparacion is null
            )
            {
                MessageBox.Show(
                    this,
                    "Elija la configuración y los datos que se compararán.",
                    "Buscadero"
                );
                return false;
            }
            if (!int.TryParse(TextoLargo.Text, out int largo) || largo < 0)
            {
                MessageBox.Show(
                    this,
                    "Ingrese un largo mínimo igual o mayor que cero.",
                    "Buscadero"
                );
                return false;
            }
            try
            {
                _lineas.GuardarRegla(
                    new ReglaVagonConfigurada(
                        Destino.Id,
                        Configuracion.Id,
                        CampoOrigen.Id,
                        Comparacion.Id,
                        CampoComparacion.Id,
                        CheckEspacios.IsChecked == true,
                        CheckGuiones.IsChecked == true,
                        CheckCeros.IsChecked == true,
                        largo
                    )
                );
                _destinosGuardados.Add(Destino.Id);
            }
            catch (Exception error)
            {
                MessageBox.Show(
                    this,
                    error.Message,
                    "Buscadero",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return false;
            }
        }
        return true;
    }
}
