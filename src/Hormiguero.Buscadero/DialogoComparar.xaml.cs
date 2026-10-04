using System.Windows;
using Buscadero.Core.Busqueda;
using Buscadero.Core.Lineas;

namespace Buscadero.App;

public partial class DialogoComparar : Window
{
    private readonly ServicioBusqueda _busqueda;

    public DialogoComparar(ServicioLineas lineas, ServicioBusqueda busqueda, string rutaActual)
    {
        InitializeComponent();
        _busqueda = busqueda;

        var coincidencias = lineas.BuscarCadenasDeDocumento(rutaActual);
        if (coincidencias.Count == 0)
        {
            ComboDocumentos.IsEnabled = false;
            TextoAyuda.Text =
                "El documento observado no está en una cadena; busque el segundo documento.";
            return;
        }

        var documentos = lineas
            .ObtenerTodosLosDocumentos(coincidencias[0].CadenaRaiz.Id)
            .Where(d =>
                d.RutaDocumento is not null
                && !string.Equals(d.RutaDocumento, rutaActual, StringComparison.OrdinalIgnoreCase)
            )
            .Select(d => new DestinoVm
            {
                Documento = d,
                Etiqueta = $"{d.Nombre} — {d.NombreDocumento}",
            })
            .ToList();

        ComboDocumentos.ItemsSource = documentos;
        if (documentos.Count > 0)
        {
            ComboDocumentos.SelectedIndex = 0;
        }
        else
        {
            ComboDocumentos.IsEnabled = false;
            TextoAyuda.Text =
                "La cadena no tiene otros documentos vinculados; busque el segundo documento.";
        }
    }

    public string? RutaSeleccionada { get; private set; }

    private void BotonBuscar_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new DialogoBuscarDocumento(_busqueda) { Owner = this };
        if (dialogo.ShowDialog() == true && dialogo.RutaSeleccionada is not null)
        {
            RutaSeleccionada = dialogo.RutaSeleccionada;
            DialogResult = true;
        }
    }

    private void Aceptar_Click(object sender, RoutedEventArgs e)
    {
        if (ComboDocumentos.SelectedItem is not DestinoVm seleccionado)
        {
            MessageBox.Show(
                this,
                "Elija un documento o use 'Buscar documento...'.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }

        RutaSeleccionada = seleccionado.Documento.RutaDocumento;
        DialogResult = true;
    }
}
