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
    private readonly RepositorioEsquemasDiccionario _esquemasDiccionario;
    private readonly RepositorioCadenas _cadenas;
    private long? _seleccionado;
    private long? _versionSeleccionada;
    private IReadOnlyList<FilaArbolCadena> _filas = [];

    public DialogoVistaCadenas(long? cadenaInicial = null)
    {
        InitializeComponent();
        _conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        _esquemas = new(_conexion);
        _esquemasDiccionario = new(_conexion);
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
                .Concat(_esquemasDiccionario.Listar().Where(e => e.Activo).Select(e => e.Proveedor))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            EstadoSinEsquemas.Visibility =
                proveedoresActivos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            var filas = AsistenteEsquemaCadena
                .ListarCadenas(_conexion)
                .Where(c => proveedoresActivos.Contains(c.Proveedor))
                .Select(c => new CadenaVm(
                    c.Id,
                    c.Proveedor,
                    c.Nombre,
                    c.Estado,
                    c.Cliente,
                    c.EsDiccionario
                ))
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
                    f.EsDiccionario ? 0 : conteos.GetValueOrDefault(f.Id)?.Cantidad ?? 0,
                    f.EsDiccionario ? null : conteos.GetValueOrDefault(f.Id)?.Urgencia
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
            if (fila.Cadena.EsDiccionario)
            {
                var esquemaDiccionario = _esquemasDiccionario.ObtenerPorProveedor(
                    fila.Cadena.Proveedor
                );
                if (esquemaDiccionario is null)
                {
                    Estado.Text = "Este proveedor no tiene esquema de documentos.";
                    return;
                }
                var docsDiccionario = LeerDocumentosDiccionario(fila.Cadena.Id);
                var versionesDiccionario = docsDiccionario
                    .Select(d => d.VersionId)
                    .Distinct()
                    .Select(id => _cadenas.ObtenerVersionDocumento(id))
                    .Where(v => v is not null)
                    .ToDictionary(v => v!.VersionId, v => v!);
                _filas = AsistenteEsquemaCadena.ArmarArbol(
                    esquemaDiccionario,
                    docsDiccionario,
                    versionesDiccionario
                );
                PanelModo.Visibility = Visibility.Collapsed;
                DatosCadena.Visibility = Visibility.Collapsed;
                PrepararArbol(fila.Cadena.Nombre, null);
                Estado.Text = "";
                return;
            }
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
            PrepararArbol(fila.Cadena.Nombre, conteo);
            Estado.Text = "";
        }
        catch (Exception ex)
        {
            Estado.Text = $"No se pudo cargar el árbol de la cadena: {ex.Message}";
        }
    }

    private void PrepararArbol(string nombre, ConteoAlertasCadena? conteo)
    {
        TituloArbol.Text = conteo is null ? nombre : $"{nombre}   ⚠ {conteo.Cantidad}";
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
    }

    private IReadOnlyList<DocumentoLugarCadena> LeerDocumentosDiccionario(long cadenaId)
    {
        using var comando = _conexion.CreateCommand();
        comando.CommandText =
            "SELECT u.id,u.version_id,u.lugar_id,l.nombre,l.orden,u.linea_id,p.version_id,d.nombre FROM ubicaciones_cadena_diccionario u JOIN lugares_esquema_diccionario l ON l.id=u.lugar_id JOIN versiones_documento v ON v.id=u.version_id JOIN documentos d ON d.id=v.documento_id LEFT JOIN ubicaciones_cadena_diccionario p ON p.id=u.padre_id WHERE u.cadena_id=$c AND u.estado='activo' ORDER BY l.orden,u.linea_id,u.orden;";
        comando.Parameters.AddWithValue("$c", cadenaId);
        using var lector = comando.ExecuteReader();
        var documentos = new List<DocumentoLugarCadena>();
        while (lector.Read())
            documentos.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    lector.GetInt64(2),
                    lector.GetString(3),
                    lector.GetInt32(4),
                    lector.IsDBNull(5) ? null : lector.GetInt64(5),
                    lector.IsDBNull(6) ? null : lector.GetInt64(6),
                    lector.GetString(7)
                )
            );
        return documentos;
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
            if (fila.Cadena.EsDiccionario)
            {
                using var tx = _conexion.BeginTransaction();
                using (var hijos = _conexion.CreateCommand())
                {
                    hijos.Transaction = tx;
                    hijos.CommandText =
                        "UPDATE ubicaciones_cadena_diccionario SET padre_id=(SELECT padre_id FROM ubicaciones_cadena_diccionario WHERE version_id=$v AND cadena_id=$c) WHERE padre_id=(SELECT id FROM ubicaciones_cadena_diccionario WHERE version_id=$v AND cadena_id=$c);";
                    hijos.Parameters.AddWithValue("$v", versionId);
                    hijos.Parameters.AddWithValue("$c", fila.Cadena.Id);
                    hijos.ExecuteNonQuery();
                }
                using var quitar = _conexion.CreateCommand();
                quitar.Transaction = tx;
                quitar.CommandText =
                    "DELETE FROM ubicaciones_cadena_diccionario WHERE version_id=$v AND cadena_id=$c;";
                quitar.Parameters.AddWithValue("$v", versionId);
                quitar.Parameters.AddWithValue("$c", fila.Cadena.Id);
                if (quitar.ExecuteNonQuery() == 0)
                {
                    tx.Rollback();
                    Estado.Text = "El documento ya no pertenece a esta cadena.";
                    return;
                }
                tx.Commit();
                _versionSeleccionada = null;
                CargarCadenas(fila.Cadena.Id);
                Estado.Text = "Documento quitado de la cadena.";
                return;
            }
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
        string? Cliente,
        bool EsDiccionario
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
