using System.Text.Json;
using System.Windows;
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
        var opcionesModo = new[] { new ModoAlertaVm(null, "Todos los modos") }
            .Concat(modos.Select(m => new ModoAlertaVm(m, m)))
            .ToArray();
        Modo.ItemsSource = opcionesModo;
        Modo.SelectedIndex = 0;
        LugarRegla.ItemsSource =
            DesdeLugar.ItemsSource =
            Hasta.ItemsSource =
            Cuando.ItemsSource =
                lugares;
        Condicion.ItemsSource = new[] { new LugarAlertaVm(-1, "Ninguno") }
            .Concat(lugares)
            .ToArray();
        Calendario.ItemsSource = calendarios;
        Calendario.SelectedItem = calendarios.FirstOrDefault(c => c.Predeterminado);
        if (lugares.Count > 0)
        {
            LugarRegla.SelectedIndex =
                DesdeLugar.SelectedIndex =
                Hasta.SelectedIndex =
                Cuando.SelectedIndex =
                    0;
        }
        Condicion.SelectedIndex = 0;
        if (regla is not null)
            CargarRegla(regla);
        Actualizar();
    }

    private void CargarRegla(ReglaAlertaBorrador regla)
    {
        Tipo.SelectedIndex = regla.Tipo switch
        {
            "falta_dato" => 0,
            "plazo" => 1,
            "listo_para" => 2,
            _ => 0,
        };
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
            Texto.Text = json.GetProperty("texto").GetString() ?? "";
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
            Texto.Text = json.GetProperty("texto").GetString() ?? "";
            Condicion.SelectedItem = LugarCondicion();
        }
        else if (regla.Tipo == "listo_para")
        {
            Cuando.SelectedItem = Lugar("cuandoLugarId");
            Hasta.SelectedItem = Lugar("hastaLugarId");
            Lista.Text = json.GetProperty("lista").GetString() ?? "";
            Condicion.SelectedItem = LugarCondicion();
        }
    }

    private void Actualizar_Click(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
            Actualizar();
    }

    private object? Parametros(out string tipo, out int orden)
    {
        tipo = Tipo.SelectedIndex switch
        {
            0 => "falta_dato",
            1 => "plazo",
            _ => "listo_para",
        };
        orden = LugarRegla.SelectedItem is LugarAlertaVm l ? l.Orden : 0;
        long Id(LugarAlertaVm? lugar) =>
            lugar is null ? 0
            : lugar.Id > 0 ? lugar.Id
            : lugar.Orden + 1;
        long? condicion = Condicion.SelectedItem is LugarAlertaVm c && c.Orden >= 0 ? Id(c) : null;
        if (tipo == "falta_dato")
            return new ParametrosFaltaDato(Dato.Text.Trim(), Texto.Text.Trim());
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
                Texto.Text.Trim(),
                PorLinea.IsChecked == true
            );
        }
        return new ParametrosListoPara(
            Id(Cuando.SelectedItem as LugarAlertaVm),
            condicion,
            Id(Hasta.SelectedItem as LugarAlertaVm),
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
