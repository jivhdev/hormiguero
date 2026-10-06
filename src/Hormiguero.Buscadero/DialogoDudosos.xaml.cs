using System.Diagnostics;
using System.IO;
using System.Windows;
using Buscadero.Core.Lineas;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Buscadero.App;

public partial class DialogoDudosos : Window
{
    private readonly ServicioLineas _lineas;
    private readonly SqliteConnection _conexion;
    private readonly ServicioCadenasSimples _simples;

    public DialogoDudosos(ServicioLineas lineas)
    {
        InitializeComponent();
        _lineas = lineas;
        _conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        _simples = new ServicioCadenasSimples(_conexion);
        ListaCadenas.ItemsSource = _simples.ListarCadenas();
        Recargar();
    }

    private DudosoFila? Seleccionado => ListaDudosos.SelectedItem as DudosoFila;

    private void Recargar()
    {
        var simples = _simples.DudososCadenasSimples();
        var idsSimples = simples.Select(d => d.EnlaceId).ToHashSet();
        var filas = _lineas
            .ListarDudosos()
            .Where(d => !idsSimples.Contains(d.Id))
            .Select(d => new DudosoFila(d, null))
            .Concat(simples.Select(d => new DudosoFila(null, d)))
            .ToArray();
        ListaDudosos.ItemsSource = filas;
        TextoContador.Text = $"{filas.Length} dudosos";
        if (filas.Length == 0)
        {
            TextoDocumentos.Text = "No hay documentos por revisar.";
            TextoDatos.Text = string.Empty;
            TextoMotivo.Text = string.Empty;
            return;
        }
        ListaDudosos.SelectedIndex = 0;
    }

    private void Lista_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e
    )
    {
        if (Seleccionado is not { } fila)
            return;
        if (fila.Simple is { } simple)
        {
            TextoDocumentos.Text = simple.Nombre;
            TextoDatos.Text = string.Empty;
            TextoMotivo.Text = simple.Motivo;
            ListaCadenas.Visibility = Visibility.Visible;
        }
        else if (fila.Legado is { } viejo)
        {
            TextoDocumentos.Text = viejo.NombreDocumentoComparado is null
                ? $"{viejo.NombreDocumento} ↔ {viejo.NombreVagon}"
                : $"{viejo.NombreDocumento} ↔ {viejo.NombreDocumentoComparado}";
            TextoDatos.Text =
                $"{viejo.ValorPropuesto ?? "Dato no disponible"} ↔ {viejo.ValorComparado ?? "Dato no disponible"}";
            TextoMotivo.Text = viejo.Motivo;
            ListaCadenas.Visibility = Visibility.Collapsed;
        }
    }

    private void Enlazar_Click(object sender, RoutedEventArgs e) => Resolver(aceptar: true);

    private void Rechazar_Click(object sender, RoutedEventArgs e) => Resolver(aceptar: false);

    private void Resolver(bool aceptar)
    {
        if (Seleccionado is not { } fila)
            return;
        try
        {
            if (fila.Simple is { } simple)
            {
                if (aceptar)
                {
                    if (ListaCadenas.SelectedItem is not Cadena cadena)
                        throw new InvalidOperationException(
                            "Elija la cadena para enlazar el documento."
                        );
                    _simples.VincularDudosoA(simple.EnlaceId, cadena.Id);
                }
                else
                    _simples.NoCorrespondeDudoso(simple.EnlaceId);
            }
            else if (fila.Legado is { } viejo)
            {
                bool cambio = aceptar
                    ? _lineas.AceptarDudoso(viejo.Id)
                    : _lineas.RechazarDudoso(viejo.Id);
                if (!cambio)
                    return;
            }
            Recargar();
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
        }
    }

    private void VerArchivo_Click(object sender, RoutedEventArgs e)
    {
        var ruta = Seleccionado?.RutaDocumento;
        if (ruta is null || !File.Exists(ruta))
        {
            MessageBox.Show(
                this,
                "No se encontró el archivo.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            MessageBox.Show(
                this,
                $"No se pudo abrir el archivo: {error.Message}",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _conexion.Dispose();
        base.OnClosed(e);
    }

    private sealed record DudosoFila(DudosoVagon? Legado, DudosoCadenaSimple? Simple)
    {
        public string NombreDocumento => Simple?.Nombre ?? Legado?.NombreDocumento ?? "Documento";
        public string NombreVagon => Simple?.Motivo ?? Legado?.NombreVagon ?? string.Empty;
        public string? RutaDocumento => Simple?.Ruta ?? Legado?.RutaDocumento;
    }
}
