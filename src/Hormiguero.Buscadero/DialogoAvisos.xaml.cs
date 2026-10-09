using System.Windows;
using System.Windows.Controls;
using Buscadero.Core.Alertas;
using Buscadero.Core.Lineas;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Utilidades;

namespace Buscadero.App;

public partial class DialogoAvisos : Window
{
    private readonly string _ruta;
    private readonly Action<long> _abrirCadena;
    private IReadOnlyList<AvisoVm> _avisos = [];

    public DialogoAvisos(string ruta, Action<long> abrirCadena)
    {
        InitializeComponent();
        _ruta = ruta;
        _abrirCadena = abrirCadena;
        Cargar();
    }

    private void Cargar()
    {
        try
        {
            using var conexion = BaseComun.Abrir(_ruta);
            var evaluador = new EvaluadorAlertasEsquema(conexion);
            evaluador.Evaluar();
            var cadenas = AsistenteEsquemaCadena.ListarCadenas(conexion).ToDictionary(c => c.Id);
            _avisos = evaluador
                .ListarAbiertas()
                .Where(a =>
                    a.Proveedor.Contains(Proveedor.Text.Trim(), StringComparison.OrdinalIgnoreCase)
                    && (
                        string.IsNullOrWhiteSpace(Cliente.Text)
                        || (
                            a.Cliente?.Contains(
                                Cliente.Text.Trim(),
                                StringComparison.OrdinalIgnoreCase
                            )
                            ?? false
                        )
                    )
                    && (
                        string.IsNullOrWhiteSpace(Cadena.Text)
                        || (long.TryParse(Cadena.Text.Trim(), out var id) && a.CadenaId == id)
                        || (
                            cadenas
                                .GetValueOrDefault(a.CadenaId)
                                ?.Nombre.Contains(
                                    Cadena.Text.Trim(),
                                    StringComparison.OrdinalIgnoreCase
                                )
                            ?? false
                        )
                    )
                    && (
                        Tipo.SelectedIndex == 0
                        || (
                            Tipo.SelectedIndex == 1 ? a.Tipo == "falta_dato"
                            : Tipo.SelectedIndex == 2 ? a.Tipo == "plazo"
                            : a.Tipo == "listo_para"
                        )
                    )
                )
                .Select(a => new AvisoVm(
                    a,
                    cadenas.GetValueOrDefault(a.CadenaId)?.Nombre ?? $"Cadena {a.CadenaId}",
                    FormatoVencimiento(conexion, a)
                ))
                .ToArray();
            Lista.ItemsSource = _avisos;
            Listos.ItemsSource = evaluador
                .ListarListos()
                .Where(a =>
                    a.Proveedor.Contains(Proveedor.Text.Trim(), StringComparison.OrdinalIgnoreCase)
                    && (
                        string.IsNullOrWhiteSpace(Cliente.Text)
                        || (
                            a.Cliente?.Contains(
                                Cliente.Text.Trim(),
                                StringComparison.OrdinalIgnoreCase
                            )
                            ?? false
                        )
                    )
                )
                .Select(a => new ListoVm(
                    a.Lista,
                    a.CadenaId,
                    a.Proveedor,
                    a.Cliente,
                    cadenas.GetValueOrDefault(a.CadenaId)?.Nombre ?? $"Cadena {a.CadenaId}",
                    a.Alcance
                ))
                .ToArray();
            Estado.Text = $"{_avisos.Count} aviso(s) abiertos.";
        }
        catch (Exception error)
        {
            Estado.Text = $"No se pudieron cargar los avisos: {error.Message}";
        }
    }

    private void Filtro_Changed(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
            Cargar();
    }

    private void Actualizar_Click(object sender, RoutedEventArgs e) => Cargar();

    private static string FormatoVencimiento(
        Microsoft.Data.Sqlite.SqliteConnection conexion,
        AlertaEsquema alerta
    )
    {
        if (alerta.FechaVencimiento is not DateOnly vence)
            return "";
        TipoDias tipoDias = TipoDias.Corridos;
        long? calendarioId = null;
        using (var q = conexion.CreateCommand())
        {
            q.CommandText = "SELECT parametros_json FROM reglas_alerta_esquema WHERE id=$id";
            q.Parameters.AddWithValue("$id", alerta.ReglaId);
            if (q.ExecuteScalar() is string json)
            {
                using var documento = System.Text.Json.JsonDocument.Parse(json);
                var raiz = documento.RootElement;
                if (raiz.TryGetProperty("tipoDias", out var dias) && dias.GetString() == "habiles")
                    tipoDias = TipoDias.Habiles;
                if (
                    raiz.TryGetProperty("calendarioId", out var calendario)
                    && calendario.TryGetInt64(out long id)
                )
                    calendarioId = id;
            }
        }
        IReadOnlySet<DateOnly>? feriados = calendarioId is long calendarioSeleccionado
            ? new RepositorioCalendariosFeriados(conexion)
                .ListarFeriados(calendarioSeleccionado, false)
                .Select(f => f.Fecha)
                .ToHashSet()
            : null;
        DateOnly hoy = DateOnly.FromDateTime(DateTime.Today);
        string relativo = PresentacionAlertas.ParaCuando(vence, hoy, tipoDias, feriados);
        return $"{(vence <= hoy ? "venció" : "vence")} el {vence:dd-MM} ({relativo.Replace("vence ", "").Replace("venció ", "")})";
    }

    private void Lista_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Lista.SelectedItem is AvisoVm vm)
            _abrirCadena(vm.Alerta.CadenaId);
    }

    private void Listos_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Listos.SelectedItem is ListoVm vm)
            _abrirCadena(vm.CadenaId);
    }

    private void Descartar_Click(object sender, RoutedEventArgs e)
    {
        if (Lista.SelectedItem is not AvisoVm vm)
        {
            Estado.Text = "Seleccione un aviso para descartarlo.";
            return;
        }
        var motivo = DialogoTexto.Pedir(this, "Descartar aviso", "Motivo corto:");
        if (string.IsNullOrWhiteSpace(motivo))
            return;
        try
        {
            using var conexion = BaseComun.Abrir(_ruta);
            new EvaluadorAlertasEsquema(conexion).Descartar(vm.Alerta.Id, motivo);
            Cargar();
        }
        catch (Exception error)
        {
            Estado.Text = $"No se pudo descartar el aviso: {error.Message}";
        }
    }

    public sealed record AvisoVm(AlertaEsquema Alerta, string CadenaTexto, string Vence)
    {
        public string Texto => Alerta.Texto;
        public string Proveedor => Alerta.Proveedor;
        public string Cliente => Alerta.Cliente ?? "";
        public string UrgenciaTexto =>
            Alerta.Urgencia switch
            {
                "vencido" => "Vencido",
                "por_vencer" => "Vence pronto",
                _ => "Normal",
            };
        public System.Windows.Media.Brush ColorUrgencia =>
            (System.Windows.Media.Brush)
                Application.Current.FindResource(
                    Alerta.Urgencia == "vencido" ? "Hormiguero.Error"
                    : Alerta.Urgencia == "por_vencer" ? "Hormiguero.Aviso"
                    : "Hormiguero.Texto"
                );
    }

    public sealed record ListoVm(
        string Lista,
        long CadenaId,
        string Proveedor,
        string? Cliente,
        string CadenaTexto,
        string Alcance
    );
}
