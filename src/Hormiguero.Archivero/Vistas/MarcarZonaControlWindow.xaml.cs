using System.Windows;
using Archivero.Datos;
using Archivero.Servicios.Pdf;

namespace Archivero.Vistas;

public partial class MarcarZonaControlWindow : Window
{
    private readonly string _rutaPdf;
    private int? _pagina;
    private RectanguloFraccion? _rectangulo;

    public ZonaControlCarpeta? Zona { get; private set; }

    public MarcarZonaControlWindow(string rutaPdf)
    {
        InitializeComponent();
        _rutaPdf = rutaPdf;
        Visor.MarcaRealizada += MarcarZona;
        Visor.CargarPdf(rutaPdf);
        Visor.IniciarMarcado();
    }

    private void MarcarZona(int pagina, RectanguloFraccion rectangulo)
    {
        _pagina = pagina;
        _rectangulo = rectangulo;
        Visor.TerminarMarcado();
        Visor.MostrarCamposPropios([("Control", pagina, rectangulo)]);
        string texto = LectorPdf.ExtraerTexto(_rutaPdf, pagina, rectangulo);
        TxtLeido.Text = texto;
        TxtEsperado.Text = texto;
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (_pagina is null || _rectangulo is null || string.IsNullOrWhiteSpace(TxtEsperado.Text))
        {
            System.Windows.MessageBox.Show(
                this,
                "Marca una zona y escribe el texto que debe contener.",
                "Control",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }
        Zona = new(
            _pagina.Value + 1,
            _rectangulo.X,
            _rectangulo.Y,
            _rectangulo.Ancho,
            _rectangulo.Alto,
            TxtEsperado.Text.Trim()
        );
        DialogResult = true;
    }
}
