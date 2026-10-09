using System.Windows;
using System.Windows.Controls;
using Buscadero.Core.Lineas;
using Hormiguero.Nucleo.Datos;

namespace Buscadero.App;

public partial class DialogoDudososCadena : Window
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conexion;
    private readonly RepositorioReglasYEnlaces _repositorio;
    private readonly long _cadenaId;

    public DialogoDudososCadena(long cadenaId, string nombre)
    {
        InitializeComponent();
        _cadenaId = cadenaId;
        Titulo.Text = $"Dudosos de {nombre}";
        _conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        _repositorio = new(_conexion);
        Recargar();
    }

    private DudosoCadenaVista? Seleccionado => Lista.SelectedItem as DudosoCadenaVista;

    private void Recargar()
    {
        Lista.ItemsSource = AsistenteEsquemaCadena.ListarDudosos(_conexion, _cadenaId);
        if (Lista.Items.Count > 0)
            Lista.SelectedIndex = 0;
        else
        {
            Documento.Text = "No hay documentos dudosos en esta cadena.";
            Motivo.Text = "";
        }
    }

    private void Lista_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Seleccionado is not { } dudoso)
            return;
        Documento.Text = dudoso.Nombre;
        Motivo.Text = dudoso.Motivo;
    }

    private void Aceptar_Click(object sender, RoutedEventArgs e) => Resolver(true);

    private void Rechazar_Click(object sender, RoutedEventArgs e) => Resolver(false);

    private void Resolver(bool aceptar)
    {
        if (Seleccionado is not { } dudoso)
        {
            Estado.Text = "Seleccione un documento dudoso.";
            return;
        }
        try
        {
            bool cambio = aceptar
                ? _repositorio.CambiarEstadoEnlace(dudoso.EnlaceId, "activo")
                : _repositorio.RechazarDudoso(dudoso.EnlaceId);
            Estado.Text = cambio ? "Decisión guardada." : "El documento ya no está pendiente.";
            Recargar();
        }
        catch (Exception ex)
        {
            Estado.Text = $"No se pudo guardar la decisión: {ex.Message}";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _conexion.Dispose();
        base.OnClosed(e);
    }
}
