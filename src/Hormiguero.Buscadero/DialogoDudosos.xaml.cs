using System.Diagnostics;
using System.IO;
using System.Windows;
using Buscadero.Core.Lineas;
using Hormiguero.Nucleo.Datos;

namespace Buscadero.App;

public partial class DialogoDudosos : Window
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conexion;
    private readonly ServicioCadenasSimples _simples;

    public DialogoDudosos()
    {
        InitializeComponent();
        _conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        _simples = new ServicioCadenasSimples(_conexion);
        ListaCadenas.ItemsSource = _simples.ListarCadenas();
        Recargar();
    }

    private DudosoCadenaSimple? Seleccionado => ListaDudosos.SelectedItem as DudosoCadenaSimple;

    private void Recargar()
    {
        var filas = _simples.DudososCadenasSimples();
        ListaDudosos.ItemsSource = filas;
        TextoContador.Text = $"{filas.Count} dudosos";
        if (filas.Count == 0)
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
        TextoDocumentos.Text = dudoso.Nombre;
        TextoDatos.Text = string.Empty;
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
            if (aceptar)
            {
                if (ListaCadenas.SelectedItem is not Cadena cadena)
                    throw new InvalidOperationException(
                        "Elija la cadena para enlazar el documento."
                    );
                _simples.VincularDudosoA(dudoso.EnlaceId, cadena.Id);
            }
            else
                _simples.NoCorrespondeDudoso(dudoso.EnlaceId);
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
        var ruta = Seleccionado?.Ruta;
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
}
