using System.Windows;
using System.Windows.Controls;
using Buscadero.Core.Alertas;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Utilidades;

namespace Buscadero.App;

public sealed class AlertaVista
{
    public required Alerta Alerta { get; init; }
    public required string Texto { get; init; }
    public required string Cadena { get; init; }
    public required string ParaCuando { get; init; }
    public required string OrigenPlazo { get; init; }
    public required string EstadoVisible { get; init; }
}

public partial class DialogoAlertas : Window
{
    private readonly string _ruta;
    private readonly Action<long> _verCadena;

    public DialogoAlertas(string ruta, Action<long> verCadena)
    {
        InitializeComponent();
        _ruta = ruta;
        _verCadena = verCadena;
        Cargar();
    }

    private void Cargar()
    {
        try
        {
            using var conexion = BaseComun.Abrir(_ruta);
            var cadenasSimples = new RepositorioCadenas(conexion)
                .ListarCadenasSimples()
                .Select(c => c.Id)
                .ToHashSet();
            var alertas = new RepositorioAlertas(conexion)
                .Listar(limite: 10000)
                .Alertas.Where(a =>
                    a.ReglaId is null
                    && (a.CadenaId is null || cadenasSimples.Contains(a.CadenaId.Value))
                )
                .ToArray();
            var filtro = ((Filtro.SelectedItem as ComboBoxItem)?.Content as string) ?? "Pendientes";
            var visibles = PresentacionAlertas.Filtrar(alertas, filtro);
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            int anioInicial =
                visibles.Count == 0
                    ? hoy.Year
                    : Math.Min(hoy.Year, visibles.Min(a => a.FechaObjetivo.Year));
            int anioFinal =
                visibles.Count == 0
                    ? hoy.Year
                    : Math.Max(hoy.Year, visibles.Max(a => a.FechaObjetivo.Year));
            var repositorioFeriados = new RepositorioCalendariosFeriados(conexion);
            var feriadosPorCalendario = visibles
                .Where(a => a.CalendarioId is not null)
                .Select(a => a.CalendarioId!.Value)
                .Distinct()
                .ToDictionary(
                    calendario => calendario,
                    calendario =>
                        repositorioFeriados.ObtenerFeriadosActivos(
                            calendario,
                            anioInicial,
                            anioFinal
                        )
                );
            Lista.ItemsSource = visibles
                .Select(a => new AlertaVista
                {
                    Alerta = a,
                    Texto = a.Texto,
                    Cadena = a.CadenaId is long id ? $"Cadena {id}" : "Documento",
                    ParaCuando = PresentacionAlertas.ParaCuando(
                        a.FechaObjetivo,
                        hoy,
                        a.ModoDias,
                        a.CalendarioId is long calendario ? feriadosPorCalendario[calendario] : null
                    ),
                    OrigenPlazo = PresentacionAlertas.OrigenDelPlazo(a),
                    EstadoVisible = a.Estado switch
                    {
                        "pendiente" => "Pendiente",
                        "vencida" => "Vencida",
                        "resuelta" => "Resuelta",
                        _ => "Descartada",
                    },
                })
                .ToList();
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

    private Alerta? Seleccionada => (Lista.SelectedItem as AlertaVista)?.Alerta;

    private void Filtro_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized)
            Cargar();
    }

    private void Revisar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var conexion = BaseComun.Abrir(_ruta);
            new EvaluadorAlertas(conexion).Evaluar();
            Cargar();
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

    private void VerCadena_Click(object sender, RoutedEventArgs e)
    {
        if (Seleccionada?.CadenaId is long id)
            _verCadena(id);
        else
            MessageBox.Show(
                this,
                "Este aviso no tiene una cadena asociada.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
    }

    private void Resolver_Click(object sender, RoutedEventArgs e) => Cambiar("resolver");

    private void Descartar_Click(object sender, RoutedEventArgs e) => Cambiar("descartar");

    private void Cambiar(string accion)
    {
        if (Seleccionada is not { } alerta)
            return;
        string? motivo = DialogoTexto.Pedir(
            this,
            accion == "resolver" ? "Resolver aviso" : "Descartar aviso",
            "Motivo corto:"
        );
        if (string.IsNullOrWhiteSpace(motivo))
            return;
        try
        {
            using var conexion = BaseComun.Abrir(_ruta);
            var repo = new RepositorioAlertas(conexion);
            if (accion == "resolver")
                repo.Resolver(alerta.Id, motivo);
            else
                repo.Descartar(alerta.Id, motivo);
            Cargar();
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

    private void Reabrir_Click(object sender, RoutedEventArgs e)
    {
        if (Seleccionada is not { } alerta)
            return;
        try
        {
            using var conexion = BaseComun.Abrir(_ruta);
            new RepositorioAlertas(conexion).Reabrir(alerta.Id);
            Cargar();
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

    private void Cerrar_Click(object sender, RoutedEventArgs e) => Close();
}
