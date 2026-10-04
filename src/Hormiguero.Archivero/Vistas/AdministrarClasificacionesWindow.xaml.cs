using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Archivero.Datos;

namespace Archivero.Vistas;

public class FilaClasificacion(ConfiguracionDocumento configuracion, bool disponible)
{
    public ConfiguracionDocumento Configuracion { get; } = configuracion;
    public string Emisor => Configuracion.Emisor;
    public string Tipo => Configuracion.Tipo;
    public string CarpetaDestino => Configuracion.CarpetaDestino;
    public string DisponibleTexto => disponible ? "✅ Disponible" : "❌ No disponible";
}

public partial class AdministrarClasificacionesWindow : Window
{
    private readonly ConfiguracionDocumentoRepository _configuraciones = new();
    private readonly EntidadRepository _entidades = new();
    private List<FilaClasificacion> _todas = [];

    /// <summary>Caso-11, punto 3: si se editó alguna configuración, la ventana principal reprocesa los pendientes al cerrar.</summary>
    public bool HuboConfiguracionesEditadas { get; private set; }

    public AdministrarClasificacionesWindow()
    {
        InitializeComponent();
        CargarClasificaciones();
    }

    private void CargarClasificaciones()
    {
        _todas = _configuraciones.ObtenerTodas()
            .Select(c => new FilaClasificacion(c, Directory.Exists(c.CarpetaDestino)))
            .ToList();

        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        var filtro = CmbBusqueda.Text.Trim();

        ListaClasificaciones.ItemsSource = string.IsNullOrWhiteSpace(filtro)
            ? _todas
            : _todas.Where(f =>
                f.Emisor.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                f.Tipo.Contains(filtro, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void CmbBusqueda_TextChanged(object sender, TextChangedEventArgs e)
    {
        var sugerencias = _entidades.Buscar(CategoriaEntidad.Emisor, CmbBusqueda.Text)
            .Concat(_entidades.Buscar(CategoriaEntidad.Tipo, CmbBusqueda.Text))
            .Distinct()
            .ToList();

        CmbBusqueda.ItemsSource = sugerencias;
        AplicarFiltro();
    }

    private void BtnRevisarDisponibilidad_Click(object sender, RoutedEventArgs e)
    {
        CargarClasificaciones();
    }

    private void ListaClasificaciones_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => Editar();

    private void BtnEditar_Click(object sender, RoutedEventArgs e) => Editar();

    private void Editar()
    {
        if (ListaClasificaciones.SelectedItem is not FilaClasificacion fila)
        {
            System.Windows.MessageBox.Show(this, "Seleccioná una clasificación de la lista para editar.", "Archivero",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Necesitamos los patrones (ObtenerTodas no los trae, para no pagar ese costo en la lista).
        var configuracion = _configuraciones.BuscarPorEmisorYTipo(fila.Emisor, fila.Tipo);
        if (configuracion is null || configuracion.Patrones.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "Esta configuración no tiene ningún patrón de reconocimiento guardado.", "Archivero",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var patron = configuracion.Patrones[0];
        if (configuracion.Patrones.Count > 1)
        {
            var elegirPatron = new ElegirPatronWindow(configuracion.Patrones) { Owner = this };
            if (elegirPatron.ShowDialog() != true || elegirPatron.PatronElegido is null)
            {
                return;
            }

            patron = elegirPatron.PatronElegido;
        }

        using var dialogoArchivo = new OpenFileDialog
        {
            Title = "Elegir un PDF de ejemplo de este diseño para revisar/corregir las marcas",
            Filter = "Documentos PDF (*.pdf)|*.pdf"
        };

        if (dialogoArchivo.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var asistente = new IdentificarDocumentoWindow(dialogoArchivo.FileName, configuracion, patron) { Owner = this };
        if (asistente.ShowDialog() == true)
        {
            HuboConfiguracionesEditadas = true;
            CargarClasificaciones();
        }
    }

    private void BtnBorrar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaClasificaciones.SelectedItem is not FilaClasificacion fila)
        {
            System.Windows.MessageBox.Show(this, "Seleccioná una clasificación de la lista para borrar.", "Archivero",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmar = System.Windows.MessageBox.Show(
            this,
            $"¿Borrar la configuración de \"{fila.Emisor}\" / \"{fila.Tipo}\"?\n\n" +
            "Los documentos que ya se guardaron con ella no se tocan ni se mueven. " +
            "Los próximos documentos de este Emisor y Tipo van a volver a pedir identificación.",
            "Archivero", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirmar != MessageBoxResult.Yes)
        {
            return;
        }

        _configuraciones.EliminarConfiguracion(fila.Configuracion.Id);
        Servicios.AuditoriaService.Registrar("CLASIFICACION_BORRADA", $"Emisor={fila.Emisor}; Tipo={fila.Tipo}");
        CargarClasificaciones();
    }

    private void BtnExportar_Click(object sender, RoutedEventArgs e)
    {
        using var dialogo = new SaveFileDialog
        {
            Title = "Exportar clasificaciones",
            Filter = "Archivo JSON (*.json)|*.json",
            FileName = $"archivero-clasificaciones-{DateTime.Now:yyyy-MM-dd}.json"
        };

        if (dialogo.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var datos = _configuraciones.ObtenerTodasConPatrones();
        var json = JsonSerializer.Serialize(datos, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(dialogo.FileName, json);

        System.Windows.MessageBox.Show(this, $"Clasificaciones exportadas a:\n{dialogo.FileName}", "Archivero",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Close();
}
