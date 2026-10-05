using System.Diagnostics;
using System.IO;
using System.Windows;
using Buscadero.Core.Lineas;

namespace Buscadero.App;

public partial class DialogoDudosos : Window
{
    private readonly ServicioLineas _lineas;

    public DialogoDudosos(ServicioLineas lineas)
    {
        InitializeComponent();
        _lineas = lineas;
        Recargar();
    }

    private DudosoVagon? Seleccionado => ListaDudosos.SelectedItem as DudosoVagon;

    private void Recargar()
    {
        var dudosos = _lineas.ListarDudosos();
        ListaDudosos.ItemsSource = dudosos;
        TextoContador.Text = $"{dudosos.Count} dudosos";
        if (dudosos.Count == 0)
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
        if (Seleccionado is not { } dudoso)
            return;
        TextoDocumentos.Text = dudoso.NombreDocumentoComparado is null
            ? $"{dudoso.NombreDocumento} ↔ {dudoso.NombreVagon}"
            : $"{dudoso.NombreDocumento} ↔ {dudoso.NombreDocumentoComparado}";
        TextoDatos.Text =
            $"{dudoso.ValorPropuesto ?? "Dato no disponible"} ↔ {dudoso.ValorComparado ?? "Dato no disponible"}";
        TextoMotivo.Text = dudoso.Motivo;
    }

    private void Enlazar_Click(object sender, RoutedEventArgs e) => Resolver(aceptar: true);

    private void Rechazar_Click(object sender, RoutedEventArgs e) => Resolver(aceptar: false);

    private void Resolver(bool aceptar)
    {
        if (Seleccionado is not { } dudoso)
            return;
        try
        {
            bool cambio = aceptar
                ? _lineas.AceptarDudoso(dudoso.Id)
                : _lineas.RechazarDudoso(dudoso.Id);
            if (cambio)
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
}
