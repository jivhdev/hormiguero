using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Buscadero.Core.Lineas;
using Hormiguero.Nucleo.Datos;
using Microsoft.Win32;

namespace Buscadero.App;

public partial class DialogoVistaCadenas : Window
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conexion;
    private readonly RepositorioEsquemasDiccionario _esquemasDiccionario;
    private readonly RepositorioCadenas _cadenas;
    private long? _seleccionado;
    private long? _versionSeleccionada;
    private bool _cadenaDiccionarioSeleccionada;
    private IReadOnlyList<FilaArbolCadena> _filas = [];

    public DialogoVistaCadenas(long? cadenaInicial = null)
    {
        InitializeComponent();
        _conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        _esquemasDiccionario = new(_conexion);
        _cadenas = new(_conexion);
        CargarCadenas(cadenaInicial);
    }

    private void CargarCadenas(long? cadenaInicial = null)
    {
        try
        {
            var evaluador = new EvaluadorAlertasEsquemaDiccionario(_conexion);
            evaluador.Evaluar();
            var proveedoresActivos = _esquemasDiccionario
                .Listar()
                .Where(e => e.Activo)
                .Select(e => e.Proveedor)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            EstadoSinEsquemas.Visibility =
                proveedoresActivos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            var filas = AsistenteEsquemaCadena
                .ListarCadenas(_conexion)
                .Where(c => c.EsDiccionario && proveedoresActivos.Contains(c.Proveedor))
                .Select(c => new CadenaVm(
                    c.Id,
                    c.Proveedor,
                    c.Nombre,
                    c.Estado,
                    c.Cliente,
                    c.EsDiccionario,
                    c.Situacion
                ))
                .ToArray();
            var conteos = evaluador.Conteos().ToDictionary(c => c.CadenaId);
            var filtradas = filas
                .Where(f =>
                    f.Proveedor.Contains(FiltroProveedor.Text, StringComparison.OrdinalIgnoreCase)
                    && EstadoVisible(f.Situacion)
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
                        ? $"{f.Cliente ?? "Sin cliente"} · {f.Nombre} · {f.Situacion}"
                        : $"{f.Proveedor} · {f.Nombre} · {f.Situacion}",
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
        _cadenaDiccionarioSeleccionada = fila.Cadena.EsDiccionario;
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
                PrepararModoYDatosDiccionario(fila.Cadena.Id, esquemaDiccionario);
                PrepararCotizaciones(fila.Cadena.Id);
                var conteoDiccionario = new EvaluadorAlertasEsquemaDiccionario(_conexion)
                    .Conteos()
                    .FirstOrDefault(c => c.CadenaId == fila.Cadena.Id);
                PrepararArbol(fila.Cadena.Nombre, conteoDiccionario);
                Estado.Text = $"Situación: {fila.Cadena.Situacion}";
                return;
            }
            Estado.Text = "La cadena seleccionada no pertenece al modelo de esquemas actual.";
        }
        catch (Exception ex)
        {
            Estado.Text = $"No se pudo cargar el árbol de la cadena: {ex.Message}";
        }
    }

    private bool EstadoVisible(string situacion) =>
        FiltroEstado.SelectedIndex switch
        {
            1 => situacion == "activa",
            2 => situacion == "completa",
            3 => situacion == "cerrada",
            4 => true,
            _ => situacion is "activa" or "completa",
        };

    private void PrepararCotizaciones(long cadenaId)
    {
        var repositorio = new RepositorioSituacionCadenaDiccionario(_conexion);
        var sugerencias = repositorio.Sugerir(cadenaId, DateTime.Now);
        PrepararListaCotizaciones(
            CotizacionesCliente,
            sugerencias.Where(c => c.Tipo == "cotizacion_propia"),
            cadenaId,
            "Agregar a esta cadena"
        );
        PrepararListaCotizaciones(
            CotizacionesProveedor,
            sugerencias.Where(c => c.Tipo == "cotizacion_proveedor"),
            cadenaId,
            "Agregar a mano"
        );
        var agregadas = repositorio.ListarAgregadas(cadenaId);
        PrepararListaCotizaciones(CotizacionesAgregadas, agregadas, cadenaId, "Quitar", true);
        using var consultaCliente = _conexion.CreateCommand();
        consultaCliente.CommandText =
            "SELECT EXISTS(SELECT 1 FROM cadenas_diccionario WHERE id=$c AND rut_cliente IS NOT NULL AND rut_cliente<>'');";
        consultaCliente.Parameters.AddWithValue("$c", cadenaId);
        TituloCotizacionesCliente.Visibility =
            Convert.ToInt64(consultaCliente.ExecuteScalar()) != 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        TituloCotizacionesProveedor.Visibility =
            sugerencias.Any(c => c.Tipo == "cotizacion_proveedor")
            || agregadas.Any(c => c.Tipo == "cotizacion_proveedor")
                ? Visibility.Visible
                : Visibility.Collapsed;
        TituloCotizacionesAgregadas.Visibility =
            agregadas.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        PanelCotizaciones.Visibility = Visibility.Visible;
    }

    private void PrepararListaCotizaciones(
        ItemsControl destino,
        IEnumerable<CotizacionCadena> cotizaciones,
        long cadenaId,
        string textoBoton,
        bool quitar = false
    )
    {
        destino.ItemsSource = cotizaciones
            .Select(c =>
            {
                var fila = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
                string codigo = c.Tipo == "cotizacion_propia" ? "COV" : "COVP";
                var nombre = new TextBlock
                {
                    Text = $"{codigo} · {c.Numero} · {c.Nombre}",
                    VerticalAlignment = VerticalAlignment.Center,
                };
                DockPanel.SetDock(nombre, Dock.Left);
                fila.Children.Add(nombre);
                var boton = new Button
                {
                    Content = textoBoton,
                    Tag = c.VersionId,
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(8, 0, 0, 0),
                };
                boton.Click += (_, _) =>
                {
                    try
                    {
                        var repo = new RepositorioSituacionCadenaDiccionario(_conexion);
                        if (quitar)
                            repo.Quitar(cadenaId, c.VersionId);
                        else
                            repo.Agregar(cadenaId, c.VersionId);
                        CargarCadenas(cadenaId);
                    }
                    catch (Exception ex)
                    {
                        Estado.Text = $"No se pudo actualizar la cotización: {ex.Message}";
                    }
                };
                DockPanel.SetDock(boton, Dock.Right);
                fila.Children.Add(boton);
                return fila;
            })
            .ToArray();
        destino.Visibility = destino.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
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
            "SELECT u.id,u.version_id,u.lugar_id,l.nombre,l.orden,u.linea_id,p.version_id,d.nombre FROM ubicaciones_diccionario_todas u JOIN lugares_esquema_diccionario l ON l.id=u.lugar_id JOIN versiones_documento v ON v.id=u.version_id JOIN documentos d ON d.id=v.documento_id LEFT JOIN ubicaciones_diccionario_todas p ON p.id=u.padre_id WHERE u.cadena_id=$c AND u.estado='activo' ORDER BY l.orden,u.linea_id,u.orden;";
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

    private void PrepararModoYDatosDiccionario(long cadenaId, EsquemaDiccionario esquema)
    {
        var modos = new RepositorioModosEsquemaDiccionario(_conexion).Listar(esquema.Id);
        PanelModo.Visibility = modos.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ModoCadena.ItemsSource = modos.Select(m => new ModoVm(m.Id, m.Nombre)).ToArray();
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText = "SELECT modo_id FROM cadenas_diccionario WHERE id=$c;";
            q.Parameters.AddWithValue("$c", cadenaId);
            if (q.ExecuteScalar() is long modoId)
                ModoCadena.SelectedValue = modoId;
        }
        var nombres = new List<string>();
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText =
                "SELECT tipo,parametros_json FROM reglas_alerta_esquema_diccionario WHERE esquema_id=$e AND tipo IN ('falta_dato','plazo');";
            q.Parameters.AddWithValue("$e", esquema.Id);
            using var r = q.ExecuteReader();
            while (r.Read())
            {
                using var json = JsonDocument.Parse(r.GetString(1));
                string propiedad = r.GetString(0) == "falta_dato" ? "dato" : "desdeDato";
                if (
                    json.RootElement.TryGetProperty(propiedad, out var dato)
                    && dato.ValueKind == JsonValueKind.String
                )
                    nombres.Add(dato.GetString() ?? "");
            }
        }
        var guardados = new RepositorioDatosCadenaDiccionario(_conexion)
            .Listar(cadenaId)
            .ToDictionary(d => d.Nombre, d => d.Valor, StringComparer.OrdinalIgnoreCase);
        var datosIngresados = new RepositorioEsquemasDiccionario(_conexion)
            .ListarDatosIngresados(esquema.Id)
            .Select(d => d.Nombre)
            .ToArray();
        DatosCadena.Children.Clear();
        foreach (string nombre in nombres.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var fila = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 2),
            };
            fila.Children.Add(
                new TextBlock
                {
                    Text = nombre,
                    Width = 180,
                    VerticalAlignment = VerticalAlignment.Center,
                }
            );
            var valor = new TextBox
            {
                Text = guardados.GetValueOrDefault(nombre) ?? "",
                Width = 150,
                Tag = nombre,
                Padding = new Thickness(5),
            };
            fila.Children.Add(valor);
            var guardar = new Button
            {
                Content = "Guardar",
                Tag = valor,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(8, 3, 8, 3),
            };
            guardar.Click += (_, _) =>
            {
                try
                {
                    var campo = (TextBox)guardar.Tag;
                    new RepositorioDatosCadenaDiccionario(_conexion).Guardar(
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
        if (datosIngresados.Length > 0)
        {
            DatosCadena.Children.Add(
                new TextBlock
                {
                    Text = "Datos que escribo yo",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 8, 0, 4),
                }
            );
            foreach (string nombre in datosIngresados)
            {
                var fila = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 2, 0, 2),
                };
                fila.Children.Add(
                    new TextBlock
                    {
                        Text = nombre,
                        Width = 190,
                        VerticalAlignment = VerticalAlignment.Center,
                    }
                );
                var valor = new TextBox
                {
                    Text = guardados.GetValueOrDefault(nombre) ?? "",
                    Width = 180,
                    Tag = nombre,
                    Padding = new Thickness(5),
                };
                fila.Children.Add(valor);
                var guardar = new Button
                {
                    Content = "Guardar",
                    Tag = valor,
                    Margin = new Thickness(6, 0, 0, 0),
                    Padding = new Thickness(8, 3, 8, 3),
                };
                guardar.Click += (_, _) =>
                {
                    try
                    {
                        var campo = (TextBox)guardar.Tag;
                        new RepositorioDatosCadenaDiccionario(_conexion).Guardar(
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
        }
        DatosCadena.Visibility =
            nombres.Count == 0 && datosIngresados.Length == 0
                ? Visibility.Collapsed
                : Visibility.Visible;
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
            if (_cadenaDiccionarioSeleccionada)
            {
                new RepositorioModosEsquemaDiccionario(_conexion).FijarModo(cadenaId, modo.Id);
                Estado.Text = "Modo actualizado.";
                return;
            }
            Estado.Text = "La cadena ya no pertenece al modelo actual.";
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
            Content = documento.Falta ? "Buscar…" : documento.Texto,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(16, 2, 0, 2),
            Tag = documento,
        };
        boton.Click += (s, e) =>
        {
            var elegido = (DocumentoArbolCadena)((Button)s!).Tag;
            if (elegido.Falta)
            {
                BuscarEslabon(lugar);
                return;
            }
            _versionSeleccionada = elegido.VersionId;
            AbrirEnBuscadero(elegido);
        };
        boton.MouseDoubleClick += (s, e) =>
            AbrirPredeterminado((DocumentoArbolCadena)((Button)s!).Tag);
        return boton;
    }

    private void BuscarEslabon(string nombreLugar)
    {
        if (Cadenas.SelectedItem is not CadenaFila fila)
            return;
        try
        {
            var esquema = _esquemasDiccionario.ObtenerPorProveedor(fila.Cadena.Proveedor);
            var lugar = esquema?.Lugares.FirstOrDefault(l => l.Nombre == nombreLugar);
            if (esquema is null || lugar is null)
                throw new InvalidOperationException("No se encontró el lugar del esquema.");
            string carpeta = lugar.CarpetaBusqueda ?? "";
            var buscador = new Buscadero.Core.Lineas.ServicioBuscarEslabon();
            string? ruta = buscador.BuscarOCargar(
                carpeta,
                NumeroEsperado(esquema, lugar.Id, fila.Cadena.Id),
                encontrado =>
                    MessageBox.Show(
                        this,
                        $"Se encontró {Path.GetFileName(encontrado)}. ¿Agregarlo a la cadena?",
                        "Buscar documento",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    ) == MessageBoxResult.Yes,
                carpetaInicial =>
                {
                    var selector = new OpenFileDialog
                    {
                        Title = "Seleccionar documento",
                        Filter = "Documentos PDF (*.pdf)|*.pdf|Todos los archivos (*.*)|*.*",
                        InitialDirectory = Directory.Exists(carpetaInicial) ? carpetaInicial : "",
                    };
                    return selector.ShowDialog(this) == true ? selector.FileName : null;
                }
            );
            if (ruta is null)
                return;
            string digitos = string.Concat(
                    Path.GetFileNameWithoutExtension(ruta).Where(char.IsAsciiDigit)
                )
                .TrimStart('0');
            string? numero =
                digitos.Length > 0
                    ? null
                    : DialogoTexto.Pedir(
                        this,
                        "Número del documento",
                        "Escriba el número del documento:"
                    );
            if (digitos.Length == 0 && string.IsNullOrWhiteSpace(numero))
                return;
            new RegistroDocumentoEnCadena(_conexion).RegistrarDesdeArchivo(
                ruta,
                lugar.Id,
                esquema,
                fila.Cadena.Id,
                numero
            );
            CargarCadenas(fila.Cadena.Id);
        }
        catch (Exception ex)
        {
            Estado.Text = $"No se pudo agregar el documento: {ex.Message}";
        }
    }

    private string? NumeroEsperado(EsquemaDiccionario esquema, long lugarId, long cadenaId)
    {
        var enlace = esquema.Enlaces.FirstOrDefault(e =>
            e.LugarA == lugarId || e.LugarB == lugarId
        );
        if (enlace is null)
            return null;
        long otroLugar = enlace.LugarA == lugarId ? enlace.LugarB : enlace.LugarA;
        using var q = _conexion.CreateCommand();
        q.CommandText =
            "SELECT x.valor_original FROM ubicaciones_diccionario_todas u JOIN valores_documento x ON x.version_id=u.version_id AND x.dato_diccionario_id=$d AND x.estado='vigente' WHERE u.cadena_id=$c AND u.lugar_id=$l AND u.estado='activo' ORDER BY u.id LIMIT 1;";
        q.Parameters.AddWithValue("$d", enlace.DatoDiccionarioId);
        q.Parameters.AddWithValue("$c", cadenaId);
        q.Parameters.AddWithValue("$l", otroLugar);
        return Convert.ToString(q.ExecuteScalar());
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
                int quitados = quitar.ExecuteNonQuery();
                if (quitados == 0)
                {
                    using var quitarExtra = _conexion.CreateCommand();
                    quitarExtra.Transaction = tx;
                    quitarExtra.CommandText =
                        "DELETE FROM ubicaciones_extra_cadena_diccionario WHERE version_id=$v AND cadena_id=$c;";
                    quitarExtra.Parameters.AddWithValue("$v", versionId);
                    quitarExtra.Parameters.AddWithValue("$c", fila.Cadena.Id);
                    quitados = quitarExtra.ExecuteNonQuery();
                }
                if (quitados == 0)
                {
                    tx.Rollback();
                    Estado.Text = "El documento ya no pertenece a esta cadena.";
                    return;
                }
                tx.Commit();
                new RepositorioSituacionCadenaDiccionario(_conexion).Recalcular(
                    fila.Cadena.Id,
                    DateTime.Now
                );
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
            new DialogoDudosos(fila.Cadena.Id) { Owner = this }.ShowDialog();
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
        bool EsDiccionario,
        string Situacion
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
