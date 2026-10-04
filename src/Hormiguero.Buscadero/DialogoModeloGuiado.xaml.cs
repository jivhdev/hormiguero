using System.Collections.ObjectModel;
using System.Windows;
using Buscadero.Core.Lineas;

namespace Buscadero.App;

public sealed class ItemListaModelo
{
    public required string Texto { get; init; }
}

public partial class DialogoModeloGuiado : Window
{
    private const long CrearNuevo = -1;
    private const int MinimoDocumentos = 2;

    private sealed class Contexto
    {
        public required PlantillaLinea Modelo { get; init; }
        public required string BreadcrumbBase { get; init; }
        public required string Subtitulo { get; init; }
        public Contexto? Padre { get; init; }
        public string? DocumentoRamificadoNombre { get; init; }
        public string? SubtituloPersonalizado { get; set; }
        public (string Nombre, long ModeloHijoId)? Pendiente { get; set; }
    }

    private readonly ServicioLineas _lineas;
    private readonly ObservableCollection<ItemListaModelo> _documentos = new();
    private readonly List<Contexto> _pila = new();
    private readonly List<long> _modelosCreados = new();

    private bool _completado;

    public DialogoModeloGuiado(ServicioLineas lineas)
    {
        InitializeComponent();
        _lineas = lineas;
        Lista.ItemsSource = _documentos;
        Loaded += (_, _) => TextoModelo.Focus();
        Closing += (_, _) =>
        {
            if (!_completado)
            {
                foreach (var modeloId in _modelosCreados)
                {
                    _lineas.BorrarPlantilla(modeloId);
                }
            }
        };
    }

    public PlantillaLinea? ModeloCreado { get; private set; }

    private Contexto Actual => _pila[^1];

    private void BotonComenzar_Click(object sender, RoutedEventArgs e)
    {
        var nombre = TextoModelo.Text.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            MessageBox.Show(
                this,
                "Ingrese un nombre para el modelo.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }

        try
        {
            var modelo = _lineas.CrearPlantilla(nombre);
            _modelosCreados.Add(modelo.Id);
            _pila.Add(
                new Contexto
                {
                    Modelo = modelo,
                    BreadcrumbBase = modelo.Nombre,
                    Subtitulo = $"Agregando documentos a '{modelo.Nombre}'.",
                }
            );

            BotonComenzar.IsEnabled = false;
            TextoModelo.IsEnabled = false;
            PanelCuerpo.Visibility = Visibility.Visible;
            ActualizarContexto();
        }
        catch (Exception excepcion)
        {
            MessageBox.Show(
                this,
                excepcion.Message,
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void ActualizarContexto()
    {
        var roots = _lineas.ObtenerArbolPlantilla(Actual.Modelo.Id);
        var cantidad = roots.Count;
        TextoBreadcrumb.Visibility = Visibility.Visible;
        TextoSubtitulo.Visibility = Visibility.Visible;
        TextoBreadcrumb.Text = $"{Actual.BreadcrumbBase} > Documento {cantidad + 1}";
        TextoSubtitulo.Text = Actual.SubtituloPersonalizado ?? Actual.Subtitulo;

        _documentos.Clear();
        foreach (var nodo in roots)
        {
            _documentos.Add(
                new ItemListaModelo
                {
                    Texto =
                        nodo.Vagon.Nombre
                        + (nodo.Vagon.EsMultiple ? "  (ramificado)" : string.Empty),
                }
            );
            foreach (var anexo in nodo.Hijos)
            {
                _documentos.Add(
                    new ItemListaModelo { Texto = "    ↳ " + anexo.Vagon.Nombre + "  (anexo)" }
                );
            }
        }

        BotonFinalizar.Content = Actual.Padre is null
            ? "Completado"
            : "Completar modelo hija y volver";
        BotonFinalizar.IsEnabled = cantidad >= MinimoDocumentos;
        RecargarModelosHijos();
    }

    private void RecargarModelosHijos()
    {
        var opciones = _lineas
            .ObtenerModelosIndependientes()
            .Where(m => m.Id != Actual.Modelo.Id)
            .Select(m => new DialogoVagon.OpcionModelo { Id = m.Id, Etiqueta = m.Nombre })
            .ToList();
        opciones.Add(
            new DialogoVagon.OpcionModelo { Id = CrearNuevo, Etiqueta = "(Crear modelo nuevo...)" }
        );
        ComboModeloHijo.ItemsSource = opciones;
        ComboModeloHijo.SelectedIndex = 0;
    }

    private void CheckMultiple_Changed(object sender, RoutedEventArgs e) =>
        PanelModeloHijo.IsEnabled = CheckMultiple.IsChecked == true;

    private void BotonAgregarDocumento_Click(object sender, RoutedEventArgs e)
    {
        if (_pila.Count == 0)
        {
            return;
        }

        var nombre = TextoDocumento.Text.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            MessageBox.Show(
                this,
                "Ingrese el nombre del documento.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }

        var esAnexo = CheckAnexo.IsChecked == true;
        var esMultiple = CheckMultiple.IsChecked == true;
        var ultimoRaiz = _lineas.ObtenerArbolPlantilla(Actual.Modelo.Id).LastOrDefault()?.Vagon;

        if (esAnexo && ultimoRaiz is null)
        {
            MessageBox.Show(
                this,
                "El primer documento no puede ser anexo.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }

        try
        {
            if (esMultiple && !esAnexo)
            {
                var seleccionado = ComboModeloHijo.SelectedItem as DialogoVagon.OpcionModelo;
                if (seleccionado is null)
                {
                    MessageBox.Show(
                        this,
                        "Elija el modelo de cadena hija.",
                        "Buscadero",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    return;
                }

                if (seleccionado.Id == CrearNuevo)
                {
                    var nombreHijo = DialogoTexto.Pedir(
                        this,
                        "Nuevo modelo de cadena hija",
                        "Nombre del modelo de cadena hija:"
                    );
                    if (nombreHijo is null)
                    {
                        return;
                    }

                    var modelohijo = _lineas.CrearPlantilla(nombreHijo, esModeloHijo: true);
                    _modelosCreados.Add(modelohijo.Id);
                    var padre = Actual;
                    padre.Pendiente = (nombre, modelohijo.Id);
                    padre.SubtituloPersonalizado = null;
                    _pila.Add(
                        new Contexto
                        {
                            Modelo = modelohijo,
                            BreadcrumbBase =
                                $"{padre.BreadcrumbBase} > {nombre} (ramificado) > Nuevo modelo de cadena hija",
                            Subtitulo =
                                $"Definiendo qué documentos siguen después de '{nombre}' cuando se ramifica.",
                            Padre = padre,
                            DocumentoRamificadoNombre = nombre,
                        }
                    );

                    TextoDocumento.Clear();
                    CheckMultiple.IsChecked = false;
                    CheckAnexo.IsChecked = false;
                    ActualizarContexto();
                    return;
                }

                _lineas.AgregarVagon(Actual.Modelo.Id, null, nombre, true, false, seleccionado.Id);
            }
            else
            {
                _lineas.AgregarVagon(
                    Actual.Modelo.Id,
                    esAnexo ? ultimoRaiz!.Id : null,
                    nombre,
                    false,
                    esAnexo,
                    null
                );
            }

            TextoDocumento.Clear();
            CheckMultiple.IsChecked = false;
            CheckAnexo.IsChecked = false;
            Actual.SubtituloPersonalizado = null;
            ActualizarContexto();
        }
        catch (Exception excepcion)
        {
            MessageBox.Show(
                this,
                excepcion.Message,
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void BotonFinalizar_Click(object sender, RoutedEventArgs e)
    {
        var roots = _lineas.ObtenerArbolPlantilla(Actual.Modelo.Id);
        if (roots.Count < MinimoDocumentos)
        {
            MessageBox.Show(
                this,
                $"Un modelo necesita al menos {MinimoDocumentos} documentos.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }

        PedirYGuardarPreferenciaNombre(Actual.Modelo);

        if (Actual.Padre is null)
        {
            ModeloCreado = Actual.Modelo;
            _completado = true;
            DialogResult = true;
            return;
        }

        // Cerrar el modelo hijo y volver al padre, agregando el documento ramificado pendiente.
        var hijo = Actual;
        var padre = hijo.Padre!;
        _pila.RemoveAt(_pila.Count - 1);

        var pendiente = padre.Pendiente!.Value;
        padre.Pendiente = null;
        _lineas.AgregarVagon(
            padre.Modelo.Id,
            null,
            pendiente.Nombre,
            true,
            false,
            pendiente.ModeloHijoId
        );

        padre.SubtituloPersonalizado =
            $"'{pendiente.Nombre}' ahora está marcado como ramificado, usando el modelo '{hijo.Modelo.Nombre}'. Seguís agregando documentos a '{padre.Modelo.Nombre}'.";
        ActualizarContexto();
    }

    private void PedirYGuardarPreferenciaNombre(PlantillaLinea modelo)
    {
        var documentos = new List<OpcionDocumentoNombre>();
        foreach (var nodo in _lineas.ObtenerArbolPlantilla(modelo.Id))
        {
            documentos.Add(
                new OpcionDocumentoNombre { Id = nodo.Vagon.Id, Nombre = nodo.Vagon.Nombre }
            );
            foreach (var anexo in nodo.Hijos)
            {
                documentos.Add(
                    new OpcionDocumentoNombre
                    {
                        Id = anexo.Vagon.Id,
                        Nombre = anexo.Vagon.Nombre + " (anexo)",
                    }
                );
            }
        }

        var (preferencia, vagonId) = DialogoPreferenciaNombre.Pedir(this, documentos);
        _lineas.ConfigurarPreferenciaNombre(modelo.Id, preferencia, vagonId);
    }
}
