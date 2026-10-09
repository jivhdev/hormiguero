using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Buscadero.Core.Alertas;
using Hormiguero.Nucleo.Datos;

namespace Buscadero.App;

public sealed record LugarAlertaVm(int Orden, string Nombre, long Id = 0);

public sealed record ModoAlertaVm(string? Nombre, string Texto);

public sealed record ReglaAlertaBorrador(
    int LugarOrden,
    string? Modo,
    string Tipo,
    string Json,
    string Frase
);

public partial class DialogoReglaEsquema : Window
{
    private readonly IReadOnlyList<LugarAlertaVm> _lugares;
    private readonly IReadOnlyList<string> _modos;
    private readonly IReadOnlyList<CalendarioFeriados> _calendarios;
    private int _tipoSeleccionado;
    private string _textoSugeridoActual = "";
    public ReglaAlertaBorrador? Resultado { get; private set; }

    public DialogoReglaEsquema(
        IReadOnlyList<LugarAlertaVm> lugares,
        IReadOnlyList<string> modos,
        IReadOnlyList<CalendarioFeriados> calendarios,
        ReglaAlertaBorrador? regla = null
    )
    {
        InitializeComponent();
        _lugares = lugares;
        _modos = modos;
        _calendarios = calendarios;
        ModoPanel.Visibility = modos.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        var opcionesModo = new[] { new ModoAlertaVm(null, "Todos los modos") }
            .Concat(modos.Select(m => new ModoAlertaVm(m, m)))
            .ToArray();
        Modo.ItemsSource = opcionesModo;
        Modo.SelectedIndex = 0;
        LugarRegla.ItemsSource =
            DesdeLugar.ItemsSource =
            Hasta.ItemsSource =
            Cuando.ItemsSource =
            HastaListo.ItemsSource =
                lugares;
        Condicion.ItemsSource = new[] { new LugarAlertaVm(-1, "Ninguno") }
            .Concat(lugares)
            .ToArray();
        Calendario.ItemsSource = calendarios;
        Calendario.SelectedItem = calendarios.FirstOrDefault(c => c.Predeterminado);
        if (lugares.Count > 0)
        {
            LugarRegla.SelectedIndex = DesdeLugar.SelectedIndex = Cuando.SelectedIndex = 0;
            Hasta.SelectedIndex = Hasta.Items.Count > 1 ? 1 : 0;
        }
        Condicion.SelectedIndex = 0;
        CondicionListo.ItemsSource = Condicion.ItemsSource;
        CondicionListo.SelectedIndex = 0;
        MostrarCampos();
        ActualizarTextoSugerido();
        if (regla is not null)
            CargarRegla(regla);
        Actualizar();
    }

    private void CargarRegla(ReglaAlertaBorrador regla)
    {
        _tipoSeleccionado = regla.Tipo switch
        {
            "falta_dato" => 0,
            "plazo" => 1,
            "listo_para" => 2,
            _ => 0,
        };
        MostrarCampos();
        LugarRegla.SelectedItem = _lugares.FirstOrDefault(l => l.Orden == regla.LugarOrden);
        Modo.SelectedItem = Modo
            .Items.Cast<ModoAlertaVm>()
            .FirstOrDefault(m => m.Nombre == regla.Modo);
        using var documento = JsonDocument.Parse(regla.Json);
        var json = documento.RootElement;
        LugarAlertaVm? Lugar(string campo) =>
            json.TryGetProperty(campo, out var valor) && valor.TryGetInt64(out long id)
                ? _lugares.FirstOrDefault(l => l.Id == id || l.Id == 0 && l.Orden + 1 == id)
                : null;
        LugarAlertaVm? LugarCondicion()
        {
            var lugar = Lugar("condicionLugarId");
            return lugar ?? Condicion.Items.Cast<LugarAlertaVm>().First();
        }
        if (regla.Tipo == "falta_dato")
        {
            Dato.Text = json.GetProperty("dato").GetString() ?? "";
            TextoFalta.Text = json.GetProperty("texto").GetString() ?? "";
        }
        else if (regla.Tipo == "plazo")
        {
            DesdeLugar.SelectedItem = Lugar("desdeLugarId");
            DesdeDato.Text = json.TryGetProperty("desdeDato", out var desde)
                ? desde.GetString() ?? ""
                : "";
            Hasta.SelectedItem = Lugar("hastaLugarId");
            if (json.TryGetProperty("dias", out var dias))
                Dias.Text = dias.GetInt32().ToString();
            TipoDias.SelectedIndex =
                json.TryGetProperty("tipoDias", out var tipo) && tipo.GetString() == "habiles"
                    ? 0
                    : 1;
            if (
                json.TryGetProperty("calendarioId", out var calendario)
                && calendario.TryGetInt64(out var calendarioId)
            )
                Calendario.SelectedItem = _calendarios.FirstOrDefault(c => c.Id == calendarioId);
            if (json.TryGetProperty("porLinea", out var porLinea))
                PorLinea.IsChecked = porLinea.GetBoolean();
            TextoPlazo.Text = json.GetProperty("texto").GetString() ?? "";
            Condicion.SelectedItem = LugarCondicion();
        }
        else if (regla.Tipo == "listo_para")
        {
            Cuando.SelectedItem = Lugar("cuandoLugarId");
            HastaListo.SelectedItem = Lugar("hastaLugarId");
            Lista.Text = json.GetProperty("lista").GetString() ?? "";
            CondicionListo.SelectedItem = LugarCondicion();
        }
    }

    private void Actualizar_Click(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
            Actualizar();
    }

    private object? Parametros(out string tipo, out int orden)
    {
        tipo = TipoActual();
        orden = LugarRegla.SelectedItem is LugarAlertaVm l ? l.Orden : 0;
        long Id(LugarAlertaVm? lugar) =>
            lugar is null ? 0
            : lugar.Id > 0 ? lugar.Id
            : lugar.Orden + 1;
        long? condicion = Condicion.SelectedItem is LugarAlertaVm c && c.Orden >= 0 ? Id(c) : null;
        if (tipo == "falta_dato")
            return new ParametrosFaltaDato(Dato.Text.Trim(), TextoFalta.Text.Trim());
        if (tipo == "plazo")
        {
            if (!int.TryParse(Dias.Text, out int dias))
                dias = -1;
            string diasTipo = TipoDias.SelectedIndex == 0 ? "habiles" : "corridos";
            return new ParametrosPlazo(
                string.IsNullOrWhiteSpace(DesdeDato.Text)
                && DesdeLugar.SelectedItem is LugarAlertaVm origen
                    ? Id(origen)
                    : null,
                string.IsNullOrWhiteSpace(DesdeDato.Text) ? null : DesdeDato.Text.Trim(),
                Id(Hasta.SelectedItem as LugarAlertaVm),
                dias,
                diasTipo,
                (Calendario.SelectedItem as CalendarioFeriados)?.Id,
                condicion,
                TextoPlazo.Text.Trim(),
                PorLinea.IsChecked == true
            );
        }
        long? condicionListo =
            CondicionListo.SelectedItem is LugarAlertaVm listoCondicion && listoCondicion.Orden >= 0
                ? Id(listoCondicion)
                : null;
        return new ParametrosListoPara(
            Id(Cuando.SelectedItem as LugarAlertaVm),
            condicionListo,
            Id(HastaListo.SelectedItem as LugarAlertaVm),
            Lista.Text.Trim()
        );
    }

    private void Actualizar()
    {
        try
        {
            var parametros = Parametros(out string tipo, out _);
            if (parametros is null)
            {
                Frase.Text = "Complete los datos del aviso.";
                return;
            }
            string? error = EditorReglasEsquema.Validar(tipo, parametros);
            Frase.Text =
                error
                ?? EditorReglasEsquema.CrearFrase(
                    tipo,
                    parametros,
                    _lugares.ToDictionary(l => l.Id > 0 ? l.Id : l.Orden + 1, l => l.Nombre)
                );
        }
        catch
        {
            Frase.Text = "Complete los datos del aviso.";
        }
    }

    private void MostrarCampos()
    {
        FaltaCampos.Visibility = _tipoSeleccionado == 0 ? Visibility.Visible : Visibility.Collapsed;
        FaltaTexto.Visibility = _tipoSeleccionado == 0 ? Visibility.Visible : Visibility.Collapsed;
        PlazoCampos.Visibility = _tipoSeleccionado == 1 ? Visibility.Visible : Visibility.Collapsed;
        ListoCampos.Visibility = _tipoSeleccionado == 2 ? Visibility.Visible : Visibility.Collapsed;
        LugarPanel.Visibility = _tipoSeleccionado == 0 ? Visibility.Visible : Visibility.Collapsed;
        ResaltarTarjetas();
        ActualizarTextoSugerido();
        Actualizar();
    }

    private static Brush BordeActivo() =>
        Application.Current?.TryFindResource("Hormiguero.Acento") as Brush ?? Brushes.Transparent;

    private void ResaltarTarjetas()
    {
        var bordeActivo = BordeActivo();
        var tarjetas = new (Button boton, int indice)[]
        {
            (TipoFalta, 0),
            (TipoPlazo, 1),
            (TipoListo, 2),
        };
        foreach (var (boton, indice) in tarjetas)
        {
            if (indice == _tipoSeleccionado)
            {
                boton.BorderBrush = bordeActivo;
                boton.BorderThickness = new Thickness(2);
                boton.FontWeight = FontWeights.Bold;
            }
            else
            {
                boton.ClearValue(Button.BorderBrushProperty);
                boton.ClearValue(Button.BorderThicknessProperty);
                boton.ClearValue(Button.FontWeightProperty);
            }
        }
    }

    private string TipoActual() =>
        _tipoSeleccionado switch
        {
            0 => "falta_dato",
            1 => "plazo",
            _ => "listo_para",
        };

    private void ActualizarTextoSugerido()
    {
        string lugar = (Hasta.SelectedItem as LugarAlertaVm)?.Nombre ?? "";
        string sugerido = EditorReglasEsquema.TextoSugerido(TipoActual(), lugar);
        var campo = _tipoSeleccionado == 0 ? TextoFalta : TextoPlazo;
        if (string.IsNullOrWhiteSpace(campo.Text) || campo.Text == _textoSugeridoActual)
            campo.Text = sugerido;
        _textoSugeridoActual = sugerido;
    }

    private void TipoFalta_Click(object sender, RoutedEventArgs e) => CambiarTipo(0);

    private void TipoPlazo_Click(object sender, RoutedEventArgs e) => CambiarTipo(1);

    private void TipoListo_Click(object sender, RoutedEventArgs e) => CambiarTipo(2);

    private void CambiarTipo(int tipo)
    {
        _tipoSeleccionado = tipo;
        MostrarCampos();
    }

    private void LugarEsperado_SelectionChanged(object sender, RoutedEventArgs e)
    {
        ActualizarTextoSugerido();
        Actualizar();
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var parametros = Parametros(out string tipo, out int orden);
            if (
                LugarRegla.SelectedItem is not LugarAlertaVm
                || Modo.SelectedItem is not ModoAlertaVm modo
            )
            {
                Estado.Text = "Elija el lugar y el modo del aviso.";
                return;
            }
            string? error = EditorReglasEsquema.Validar(tipo, parametros!);
            if (error is not null)
            {
                Estado.Text = error;
                return;
            }
            Resultado = new(
                orden,
                modo.Nombre,
                tipo,
                EditorReglasEsquema.CrearJson(tipo, parametros!),
                EditorReglasEsquema.CrearFrase(
                    tipo,
                    parametros!,
                    _lugares.ToDictionary(l => l.Id > 0 ? l.Id : l.Orden + 1, l => l.Nombre)
                )
            );
            DialogResult = true;
        }
        catch (Exception error)
        {
            Estado.Text = $"No se pudo preparar el aviso: {error.Message}";
        }
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();
}
