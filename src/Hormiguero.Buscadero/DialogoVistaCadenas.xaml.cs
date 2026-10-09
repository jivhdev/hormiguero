using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Buscadero.Core.Lineas;
using Hormiguero.Nucleo.Datos;

namespace Buscadero.App;

public partial class DialogoVistaCadenas : Window
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conexion;
    private readonly RepositorioEsquemas _esquemas;
    private readonly RepositorioCadenas _cadenas;
    private long? _seleccionado;
    private long? _versionSeleccionada;
    private IReadOnlyList<FilaArbolCadena> _filas = [];

    public DialogoVistaCadenas(long? cadenaInicial = null)
    {
        InitializeComponent();
        _conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        _esquemas = new(_conexion);
        _cadenas = new(_conexion);
        CargarCadenas(cadenaInicial);
    }

    private void CargarCadenas(long? cadenaInicial = null)
    {
        try
        {
            var proveedoresActivos = _esquemas
                .Listar()
                .Where(e => e.Activo)
                .Select(e => e.Proveedor)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            EstadoSinEsquemas.Visibility =
                proveedoresActivos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            var filas = AsistenteEsquemaCadena
                .ListarCadenas(_conexion)
                .Where(c => proveedoresActivos.Contains(c.Proveedor))
                .Select(c => new CadenaVm(c.Id, c.Proveedor, c.Nombre, c.Estado, c.Cliente))
                .ToArray();
            var evaluador = new EvaluadorAlertasEsquema(_conexion);
            evaluador.Evaluar();
            var conteos = evaluador.Conteos().ToDictionary(c => c.CadenaId);
            var filtradas = filas
                .Where(f =>
                    f.Proveedor.Contains(FiltroProveedor.Text, StringComparison.OrdinalIgnoreCase)
                    && (FiltroEstado.SelectedIndex == 1 || f.Estado == "activa")
                    && (
                        string.IsNullOrWhiteSpace(FiltroCliente.Text)
                        || (
                            f.Cliente?.Contains(
                                FiltroCliente.Text,
                                StringComparison.OrdinalIgnoreCase
                            ) ?? false
                        )
                    )
                )
                .ToArray();
            Cadenas.ItemsSource = filtradas
                .Select(f => new CadenaFila(
                    f,
                    Agrupar.SelectedIndex == 1
                        ? $"{f.Cliente ?? "Sin cliente"} · {f.Nombre}"
                        : $"{f.Proveedor} · {f.Nombre}",
                    conteos.GetValueOrDefault(f.Id)?.Cantidad ?? 0,
                    conteos.GetValueOrDefault(f.Id)?.Urgencia
                ))
                .ToArray();
            if (cadenaInicial is long id)
                Cadenas.SelectedItem = Cadenas
                    .Items.Cast<CadenaFila>()
                    .FirstOrDefault(x => x.Cadena.Id == id);
            Estado.Text =
                filas.Length == 0
                    ? "Aún no hay cadenas. Aparecerán cuando llegue un documento que inicie un proceso configurado."
                    : "";
        }
        catch (Exception ex)
        {
            Estado.Text = $"No se pudieron cargar las cadenas: {ex.Message}";
        }
    }

    private void Filtro_TextChanged(object sender, RoutedEventArgs e)
    {
        AyudaFiltroProveedor.Visibility = string.IsNullOrEmpty(FiltroProveedor.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        AyudaFiltroCliente.Visibility = string.IsNullOrEmpty(FiltroCliente.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (IsInitialized)
            CargarCadenas(_seleccionado);
    }

    private void Cadena_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Cadenas.SelectedItem is not CadenaFila fila)
            return;
        _seleccionado = fila.Cadena.Id;
        try
        {
            var esquema = _esquemas.ObtenerPorProveedor(fila.Cadena.Proveedor);
            if (esquema is null)
            {
                Estado.Text = "Este proveedor no tiene esquema de cadena.";
                return;
            }
            PrepararModoYDatos(fila.Cadena.Id, esquema);
            var docs = _cadenas.ArbolPorLugares(fila.Cadena.Id);
            var versiones = docs.Select(d => d.VersionId)
                .Distinct()
                .Select(id => _cadenas.ObtenerVersionDocumento(id))
                .Where(v => v is not null)
                .ToDictionary(v => v!.VersionId, v => v!);
            _filas = AsistenteEsquemaCadena.ArmarArbol(esquema, docs, versiones);
            var conteo = new EvaluadorAlertasEsquema(_conexion)
                .Conteos()
                .FirstOrDefault(c => c.CadenaId == fila.Cadena.Id);
            TituloArbol.Text = conteo is null
                ? fila.Cadena.Nombre
                : $"{fila.Cadena.Nombre}   ⚠ {conteo.Cantidad}";
            TituloArbol.Foreground = (System.Windows.Media.Brush)
                Application.Current.FindResource(
                    conteo?.Urgencia == "vencido" ? "Hormiguero.Error"
                    : conteo?.Urgencia == "por_vencer" ? "Hormiguero.Aviso"
                    : "Hormiguero.Texto"
                );
            Arbol.ItemsSource = _filas
                .SelectMany(f =>
                    new object[]
                    {
                        new TextBlock
                        {
                            Text = f.Lugar,
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(0, 10, 0, 3),
                        },
                    }.Concat(f.Documentos.Select(d => CrearTarjeta(d, f.Lugar)))
                )
                .ToArray();
            Estado.Text = "";
        }
        catch (Exception ex)
        {
            Estado.Text = $"No se pudo cargar el árbol de la cadena: {ex.Message}";
        }
    }

    private void PrepararModoYDatos(long cadenaId, EsquemaCadena esquema)
    {
        PanelModo.Visibility = esquema.Modos.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ModoCadena.ItemsSource = esquema.Modos.Select(m => new ModoVm(m.Id, m.Nombre)).ToArray();
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText = "SELECT modo_id FROM cadenas WHERE id=$c";
            q.Parameters.AddWithValue("$c", cadenaId);
            var valor = q.ExecuteScalar();
            if (valor is long modoId)
                ModoCadena.SelectedValue = modoId;
        }
        var nombres = esquema
            .ReglasAlerta.Select(r =>
            {
                using var json = JsonDocument.Parse(r.ParametrosJson);
                string? dato = r.Tipo switch
                {
                    "falta_dato" => json.RootElement.GetProperty("dato").GetString(),
                    "plazo" when json.RootElement.TryGetProperty("desdeDato", out var desde) =>
                        desde.GetString(),
                    _ => null,
                };
                return dato;
            })
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var guardados = new RepositorioDatosCadena(_conexion)
            .Listar(cadenaId)
            .ToDictionary(d => d.Nombre, d => d.Valor, StringComparer.OrdinalIgnoreCase);
        DatosCadena.Children.Clear();
        foreach (string nombre in nombres!)
        {
            var fila = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 3, 0, 3),
            };
            fila.Children.Add(
                new TextBlock
                {
                    Text = nombre,
                    Width = 150,
                    VerticalAlignment = VerticalAlignment.Center,
                }
            );
            var valor = new TextBox
            {
                Text = guardados.GetValueOrDefault(nombre) ?? "",
                Width = 150,
                Padding = new Thickness(5),
                Tag = nombre,
            };
            fila.Children.Add(valor);
            var guardar = new Button
            {
                Content = "Guardar",
                Margin = new Thickness(6, 0, 0, 0),
                Tag = valor,
                Padding = new Thickness(8, 3, 8, 3),
            };
            guardar.Click += (_, _) =>
            {
                try
                {
                    var campo = (TextBox)guardar.Tag;
                    new RepositorioDatosCadena(_conexion).Guardar(
                        cadenaId,
                        (string)campo.Tag,
                        campo.Text
                    );
                    Estado.Text = "Dato de la cadena guardado; los avisos se actualizaron.";
                }
                catch (Exception error)
                {
                    Estado.Text = $"No se pudo guardar el dato: {error.Message}";
                }
            };
            fila.Children.Add(guardar);
            DatosCadena.Children.Add(fila);
        }
        DatosCadena.Visibility = nombres.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void CambiarModo_Click(object sender, RoutedEventArgs e)
    {
        if (_seleccionado is not long cadenaId || ModoCadena.SelectedItem is not ModoVm modo)
        {
            Estado.Text = "Seleccione un modo.";
            return;
        }
        try
        {
            new RepositorioModosEsquema(_conexion).FijarModo(cadenaId, modo.Id);
            Estado.Text = "Modo actualizado.";
        }
        catch (Exception error)
        {
            Estado.Text = $"No se pudo cambiar el modo: {error.Message}";
        }
    }

    private FrameworkElement CrearTarjeta(DocumentoArbolCadena documento, string lugar)
    {
        var boton = new Button
        {
            Content = documento.Falta ? "Falta" : documento.Texto,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(16, 2, 0, 2),
            IsEnabled = !documento.Falta,
            Tag = documento,
        };
        boton.Click += (s, e) =>
        {
            var elegido = (DocumentoArbolCadena)((Button)s!).Tag;
            _versionSeleccionada = elegido.VersionId;
            AbrirEnBuscadero(elegido);
        };
        boton.MouseDoubleClick += (s, e) =>
            AbrirPredeterminado((DocumentoArbolCadena)((Button)s!).Tag);
        return boton;
    }

    private void AbrirEnBuscadero(DocumentoArbolCadena documento)
    {
        if (documento.Ruta is null)
            return;
        if (Owner is MainWindow principal)
            _ = principal.MostrarDocumentoCadenaAsync(documento.Ruta);
    }

    private void AbrirPredeterminado(DocumentoArbolCadena documento)
    {
        if (documento.Ruta is null)
            return;
        try
        {
            Process.Start(new ProcessStartInfo(documento.Ruta) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Estado.Text = $"No se pudo abrir el documento: {ex.Message}";
        }
    }

    private void Quitar_Click(object sender, RoutedEventArgs e)
    {
        if (
            Cadenas.SelectedItem is not CadenaFila fila
            || _versionSeleccionada is not long versionId
        )
        {
            Estado.Text = "Seleccione un documento del árbol.";
            return;
        }
        if (
            MessageBox.Show(
                this,
                "¿Quitar el documento seleccionado de la cadena?",
                "Quitar de la cadena",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            ) != MessageBoxResult.Yes
        )
            return;
        try
        {
            var vagon = _cadenas
                .ArbolPorLugares(fila.Cadena.Id)
                .First(d => d.VersionId == versionId);
            _cadenas.QuitarDocumentoCadena(vagon.VagonId);
            _versionSeleccionada = null;
            CargarCadenas(fila.Cadena.Id);
            Estado.Text = "Documento quitado de la cadena.";
        }
        catch (Exception ex)
        {
            Estado.Text = $"No se pudo quitar el documento: {ex.Message}";
        }
    }

    private void Dudosos_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Cadenas.SelectedItem is not CadenaFila fila)
            {
                Estado.Text = "Seleccione una cadena para revisar sus dudosos.";
                return;
            }
            new DialogoDudososCadena(fila.Cadena.Id, fila.Cadena.Nombre)
            {
                Owner = this,
            }.ShowDialog();
            Cadenas.SelectedItem = null;
            Cadenas.SelectedItem = fila;
        }
        catch (Exception ex)
        {
            Estado.Text = $"No se pudieron abrir los dudosos: {ex.Message}";
        }
    }

    private void Esquemas_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            new DialogoListaEsquemas { Owner = this }.ShowDialog();
            CargarCadenas(_seleccionado);
        }
        catch (Exception ex)
        {
            Estado.Text = $"No se pudieron administrar los esquemas: {ex.Message}";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _conexion.Dispose();
        base.OnClosed(e);
    }

    private sealed record CadenaVm(
        long Id,
        string Proveedor,
        string Nombre,
        string Estado,
        string? Cliente
    );

    private sealed record ModoVm(long Id, string Nombre);

    private sealed record CadenaFila(
        CadenaVm Cadena,
        string BaseTexto,
        int CantidadAvisos,
        string? Urgencia
    )
    {
        public string Texto =>
            CantidadAvisos == 0 ? BaseTexto : $"{BaseTexto}   ⚠ {CantidadAvisos}";
        public System.Windows.Media.Brush ColorAviso =>
            (System.Windows.Media.Brush)
                Application.Current.FindResource(
                    Urgencia == "vencido" ? "Hormiguero.Error"
                    : Urgencia == "por_vencer" ? "Hormiguero.Aviso"
                    : "Hormiguero.Texto"
                );
    }
}
