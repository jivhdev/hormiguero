using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using Archivero.Datos;
using Archivero.Servicios;
using Hormiguero.Nucleo.Datos;

namespace Archivero.Vistas;

public class FilaClasificacion(
    ConfiguracionDocumento configuracion,
    bool disponible,
    ConfiguracionCadena? cadena = null,
    string? estadoCadena = null
)
{
    public ConfiguracionDocumento Configuracion { get; } = configuracion;
    public string Emisor => Configuracion.Emisor;
    public string Tipo => Configuracion.Tipo;
    public string GrupoDocumento =>
        Configuracion.GrupoDocumento == "Emitido" ? "Emitidos" : "Recibidos";
    public string NombreEstandar =>
        string.IsNullOrWhiteSpace(Configuracion.NombreEstandar)
            ? $"{Tipo} · {Emisor}"
            : Configuracion.NombreEstandar;
    public string CarpetaDestino => Configuracion.CarpetaDestino;
    public string DisponibleTexto => disponible ? "✅ Disponible" : "❌ No disponible";
    public string AlcanceCadenaTexto =>
        cadena is null ? ""
        : cadena.Alcance == "todas" ? "Todas"
        : $"Solo {cadena.Proveedor}";
    public string EstadoCadenaTexto => estadoCadena ?? "";
    public bool EsIncompleta => estadoCadena is not null;
    public Visibility CompletarVisibility =>
        EsIncompleta ? Visibility.Visible : Visibility.Collapsed;
}

public partial class AdministrarClasificacionesWindow : Window
{
    private readonly ConfiguracionDocumentoRepository _configuraciones = new();
    private readonly EntidadRepository _entidades = new();
    private readonly ConfiguracionCadenaRepository _configuracionesCadena = new();
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
        _todas = _configuraciones
            .ObtenerTodas()
            .Select(c => CrearFila(c))
            .OrderBy(f => f.GrupoDocumento, StringComparer.CurrentCulture)
            .ThenBy(f => f.NombreEstandar, StringComparer.CurrentCulture)
            .ToList();

        AplicarFiltro();
    }

    private FilaClasificacion CrearFila(ConfiguracionDocumento configuracion)
    {
        var cadena = _configuracionesCadena.Obtener(configuracion.Id);
        var conPatrones =
            _configuraciones.BuscarPorEmisorYTipo(configuracion.Emisor, configuracion.Tipo)
            ?? configuracion;
        return new(
            configuracion,
            Directory.Exists(configuracion.CarpetaDestino),
            cadena,
            cadena is null ? null : CalcularFaltantes(conPatrones, cadena)
        );
    }

    private static string? CalcularFaltantes(
        ConfiguracionDocumento configuracion,
        ConfiguracionCadena cadena
    )
    {
        string? tipoId = DiccionarioDatosEnlazantes
            .Todos.FirstOrDefault(d => d.EtiquetaTipo == configuracion.Tipo)
            ?.Id;
        if (tipoId is null)
            return null;
        var opciones = OpcionesCadenaService.ParaTipo(tipoId);
        var esquemas =
            cadena.Alcance == "todas"
                ? opciones
                : opciones.Where(o => o.Proveedor == cadena.Proveedor).ToArray();
        var requeridos = esquemas.SelectMany(o => o.DatosRequeridos).Distinct().ToArray();
        if (requeridos.Length == 0)
            return null;
        var patron = configuracion.Patrones.FirstOrDefault();
        if (patron is null)
            return $"Incompleta para la cadena: falta {requeridos[0]}";
        var datosMarcados = DatosEnlazantesConfiguracionService
            .Leer(configuracion.Emisor, configuracion.Tipo, patron.Id)
            .Where(d => d.Marcado)
            .Select(d => d.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> informativosMarcados = [];
        using (var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun))
        {
            var identificacion = new Identificaciones(conexion)
                .Listar()
                .FirstOrDefault(i =>
                    i.Emisor == configuracion.Emisor && i.Tipo == configuracion.Tipo
                );
            if (identificacion is not null)
                informativosMarcados = new RepositorioDatosInformativos(conexion)
                    .LeerZonas(
                        identificacion.Id,
                        patron.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    )
                    .Select(z => z.Dato)
                    .ToHashSet(StringComparer.Ordinal);
        }
        string? falta = requeridos.FirstOrDefault(id =>
            id is "fecha_documento" or "encargado"
                ? !informativosMarcados.Contains(id)
                : !datosMarcados.Contains(id)
        );
        if (falta is null)
            return null;
        string etiqueta = falta switch
        {
            "fecha_documento" => "Fecha del documento",
            "encargado" => "Encargado",
            _ => DiccionarioDatosEnlazantes.Todos.FirstOrDefault(d => d.Id == falta)?.Nombre
                ?? falta,
        };
        string nombreCadena = cadena.Alcance == "todas" ? "" : $" de {cadena.Proveedor}";
        return $"Incompleta para la cadena{nombreCadena}: falta {etiqueta}";
    }

    private void AplicarFiltro()
    {
        var filtro = CmbBusqueda.Text.Trim();

        var filas = string.IsNullOrWhiteSpace(filtro)
            ? _todas
            : _todas
                .Where(f =>
                    f.Emisor.Contains(filtro, StringComparison.OrdinalIgnoreCase)
                    || f.Tipo.Contains(filtro, StringComparison.OrdinalIgnoreCase)
                    || f.NombreEstandar.Contains(filtro, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();
        var vista = CollectionViewSource.GetDefaultView(filas);
        vista.GroupDescriptions.Clear();
        vista.GroupDescriptions.Add(
            new PropertyGroupDescription(nameof(FilaClasificacion.GrupoDocumento))
        );
        ListaClasificaciones.ItemsSource = vista;
    }

    private void CmbBusqueda_TextChanged(object sender, TextChangedEventArgs e)
    {
        var sugerencias = _entidades
            .Buscar(CategoriaEntidad.Emisor, CmbBusqueda.Text)
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

    private void ListaClasificaciones_MouseDoubleClick(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e
    ) => Editar();

    private void BtnEditar_Click(object sender, RoutedEventArgs e) => Editar();

    private void BtnCompletar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { DataContext: FilaClasificacion fila })
            Editar(fila, true);
    }

    private void Editar(FilaClasificacion? seleccion = null, bool completar = false)
    {
        var fila = seleccion ?? ListaClasificaciones.SelectedItem as FilaClasificacion;
        if (fila is null)
        {
            System.Windows.MessageBox.Show(
                this,
                "Selecciona una clasificación de la lista para editar.",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }

        // Necesitamos los patrones (ObtenerTodas no los trae, para no pagar ese costo en la lista).
        var configuracion = _configuraciones.BuscarPorEmisorYTipo(fila.Emisor, fila.Tipo);
        if (configuracion is null || configuracion.Patrones.Count == 0)
        {
            System.Windows.MessageBox.Show(
                this,
                "Esta configuración no tiene ningún patrón de reconocimiento guardado.",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
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
            Filter = "Documentos PDF (*.pdf)|*.pdf",
        };

        if (dialogoArchivo.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var asistente = new IdentificarDocumentoWindow(
            dialogoArchivo.FileName,
            configuracion,
            patron,
            comenzarEnDatos: completar
        )
        {
            Owner = this,
        };
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
            System.Windows.MessageBox.Show(
                this,
                "Selecciona una clasificación de la lista para borrar.",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }

        var confirmar = System.Windows.MessageBox.Show(
            this,
            $"¿Borrar la configuración de \"{fila.Emisor}\" / \"{fila.Tipo}\"?\n\n"
                + "Los documentos que ya se guardaron con ella no se tocan ni se mueven. "
                + "Los próximos documentos de este Emisor y Tipo van a volver a pedir identificación.",
            "Archivero",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (confirmar != MessageBoxResult.Yes)
        {
            return;
        }

        _configuraciones.EliminarConfiguracion(fila.Configuracion.Id);
        Servicios.AuditoriaService.Registrar(
            "CLASIFICACION_BORRADA",
            $"Emisor={fila.Emisor}; Tipo={fila.Tipo}"
        );
        CargarClasificaciones();
    }

    private void BtnExportar_Click(object sender, RoutedEventArgs e)
    {
        using var dialogo = new SaveFileDialog
        {
            Title = "Exportar clasificaciones",
            Filter = "Archivo JSON (*.json)|*.json",
            FileName = $"archivero-clasificaciones-{DateTime.Now:yyyy-MM-dd}.json",
        };

        if (dialogo.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var datos = _configuraciones.ObtenerTodasConPatrones();
        var json = JsonSerializer.Serialize(
            datos,
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(dialogo.FileName, json);

        System.Windows.MessageBox.Show(
            this,
            $"Clasificaciones exportadas a:\n{dialogo.FileName}",
            "Archivero",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );
    }

    private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Close();
}
