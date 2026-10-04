using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Buscadero.Core.Busqueda;
using Buscadero.Core.Lineas;

namespace Buscadero.App;

public sealed class DestinoVm
{
    public required InstanciaVagon Documento { get; init; }
    public required string Etiqueta { get; init; }
}

public partial class DialogoVincularCadena : Window
{
    private readonly ServicioLineas _lineas;
    private readonly ServicioBusqueda _busqueda;
    private readonly string? _rutaObservada;

    private string? _rutaSegunda;
    private long? _cadenaSeleccionadaId;

    public DialogoVincularCadena(
        ServicioLineas lineas,
        ServicioBusqueda busqueda,
        string? rutaObservada
    )
    {
        InitializeComponent();
        _lineas = lineas;
        _busqueda = busqueda;
        _rutaObservada = rutaObservada;

        ComboFiltroCarpeta.ItemsSource = _busqueda.ObtenerSugerenciasCarpeta();

        var modelos = _lineas.ObtenerModelosIndependientes();
        ComboModelo.ItemsSource = modelos;
        if (modelos.Count == 1)
        {
            ComboModelo.SelectedIndex = 0;
        }
        else if (modelos.Count == 0)
        {
            TextoEstado.Text =
                "Todavía no hay modelos de cadena independientes. Créelos desde 'Crear o editar modelos de cadena documental'.";
        }
    }

    public long? CadenaResultanteId { get; private set; }

    public long? CadenaCreadaId { get; private set; }

    private PlantillaLinea? ModeloSeleccionado => ComboModelo.SelectedItem as PlantillaLinea;

    private void ComboModelo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _cadenaSeleccionadaId = null;
        ComboDestino.ItemsSource = null;
        ComboDestinoSegundo.ItemsSource = null;
        ListaResultados.ItemsSource = null;
    }

    private void CargarDestinos()
    {
        if (_cadenaSeleccionadaId is not long cadenaId)
        {
            ComboDestino.ItemsSource = null;
            ComboDestinoSegundo.ItemsSource = null;
            return;
        }

        var documentos = RecolectarDocumentos(cadenaId)
            .Select(d => new DestinoVm
            {
                Documento = d,
                Etiqueta = $"{d.Nombre} — {(d.NombreDocumento ?? "(vacío)")}",
            })
            .ToList();

        ComboDestino.ItemsSource = documentos;
        ComboDestinoSegundo.ItemsSource = documentos;
    }

    private List<InstanciaVagon> RecolectarDocumentos(long cadenaId)
    {
        var resultado = new List<InstanciaVagon>();
        foreach (var nodo in _lineas.ObtenerArbolInstancia(cadenaId))
        {
            AgregarDocumento(nodo, resultado);
        }

        return resultado;
    }

    private void AgregarDocumento(NodoInstancia nodo, List<InstanciaVagon> resultado)
    {
        resultado.Add(nodo.Vagon);
        foreach (var hijo in nodo.Hijos)
        {
            AgregarDocumento(hijo, resultado);
        }

        foreach (var hija in _lineas.ObtenerCadenasHijas(nodo.Vagon.Id))
        {
            foreach (var nodoHija in _lineas.ObtenerArbolInstancia(hija.Id))
            {
                AgregarDocumento(nodoHija, resultado);
            }
        }
    }

    private void BotonCrearCadena_Click(object sender, RoutedEventArgs e)
    {
        if (ModeloSeleccionado is not PlantillaLinea modelo)
        {
            TextoEstado.Text = "Elija un modelo de cadena.";
            return;
        }

        var cadena = _lineas.CrearCadenaVacia(modelo.Id);
        CadenaCreadaId = cadena.Id;
        DialogResult = true;
    }

    private async void BotonBuscar_Click(object sender, RoutedEventArgs e) => await BuscarAsync();

    private async void TextoBusqueda_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await BuscarAsync();
        }
    }

    private ModoBusqueda ModoSeleccionado() =>
        ComboModo.SelectedIndex switch
        {
            1 => ModoBusqueda.Exacto,
            2 => ModoBusqueda.Alfanumerico,
            3 => ModoBusqueda.SoloNumero,
            4 => ModoBusqueda.SoloLetras,
            _ => ModoBusqueda.Todos,
        };

    private async Task BuscarAsync()
    {
        if (ModeloSeleccionado is null)
        {
            TextoEstado.Text = "Elija primero un modelo de cadena.";
            return;
        }

        var texto = TextoBusqueda.Text.Trim();
        if (string.IsNullOrWhiteSpace(texto))
        {
            TextoEstado.Text = "Ingrese un número para buscar.";
            return;
        }

        BotonBuscar.IsEnabled = false;
        TextoEstado.Text = "Buscando...";
        try
        {
            var filtro = ComboFiltroCarpeta.Text.Trim();
            var modo = ModoSeleccionado();
            var resultados = await Task.Run(() => _busqueda.Buscar(texto, filtro, modo));
            ListaResultados.ItemsSource = resultados
                .Select(r => new ResultadoBusquedaLista
                {
                    Ruta = r.Ruta,
                    Etiqueta = $"{r.Nombre}  —  {r.Carpeta}",
                })
                .ToList();
            TextoEstado.Text =
                resultados.Count == 0
                    ? "Sin coincidencias."
                    : $"{resultados.Count} coincidencias. Doble clic para usar una.";
        }
        catch (Exception excepcion)
        {
            TextoEstado.Text = $"Error al buscar: {excepcion.Message}";
        }
        finally
        {
            BotonBuscar.IsEnabled = true;
        }
    }

    private void ListaResultados_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (
            ListaResultados.SelectedItem is not ResultadoBusquedaLista seleccionado
            || ModeloSeleccionado is not PlantillaLinea modelo
        )
        {
            return;
        }

        var coincidencias = _lineas.BuscarDocumentoEnCadenas(modelo.Id, seleccionado.Ruta);
        if (coincidencias.Count > 0)
        {
            var coincidencia = coincidencias[0];
            _cadenaSeleccionadaId = coincidencia.CadenaRaiz.Id;
            CargarDestinos();
            var ubicacion = System.IO.Path.GetDirectoryName(coincidencia.Documento.RutaDocumento);
            TextoEstado.Text =
                $"Encontrado en la cadena: {coincidencia.Documento.NombreDocumento} — {ubicacion}. Elija el documento destino.";
            return;
        }

        var respuesta = MessageBox.Show(
            this,
            "Este documento no forma parte de ningún documento de ninguna cadena. ¿Desea crear una nueva cadena y enlazar ambos?\n\n"
                + "Sí: crear la cadena y ubicar los dos documentos.\nNo: crear la cadena solo con el documento observado.\nCancelar: reintentar la búsqueda.",
            "Buscadero",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question
        );

        if (respuesta == MessageBoxResult.Cancel)
        {
            return;
        }

        _rutaSegunda = respuesta == MessageBoxResult.Yes ? seleccionado.Ruta : null;
        PanelSegundo.Visibility = _rutaSegunda is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

        var cadena = _lineas.CrearCadenaVacia(modelo.Id);
        _cadenaSeleccionadaId = cadena.Id;
        CargarDestinos();
        TextoEstado.Text =
            "Cadena nueva creada. Elija en qué documento ubicar el documento observado.";
    }

    private void Aceptar_Click(object sender, RoutedEventArgs e)
    {
        if (_rutaObservada is null)
        {
            TextoEstado.Text = "No hay un documento observado para vincular.";
            return;
        }

        if (_cadenaSeleccionadaId is null)
        {
            TextoEstado.Text =
                "Cree una cadena nueva o busque otro documento para ubicar la cadena.";
            return;
        }

        if (ComboDestino.SelectedItem is not DestinoVm destino)
        {
            TextoEstado.Text = "Elija en qué documento de la cadena ubicar el documento observado.";
            return;
        }

        if (destino.Documento.NombreDocumento is not null)
        {
            MessageBox.Show(
                this,
                "Este documento ya tiene un archivo vinculado y no admite documentos anexos.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        try
        {
            _lineas.VincularDocumento(destino.Documento.Id, _rutaObservada);

            if (
                _rutaSegunda is not null
                && ComboDestinoSegundo.SelectedItem is DestinoVm destinoSegundo
            )
            {
                if (destinoSegundo.Documento.NombreDocumento is not null)
                {
                    MessageBox.Show(
                        this,
                        "El documento elegido para el segundo archivo ya tiene un archivo vinculado.",
                        "Buscadero",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }

                _lineas.VincularDocumento(destinoSegundo.Documento.Id, _rutaSegunda);
            }

            CadenaResultanteId = destino.Documento.InstanciaId;
            DialogResult = true;
        }
        catch (Exception excepcion)
        {
            TextoEstado.Text = excepcion.Message;
        }
    }
}
