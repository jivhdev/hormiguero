using System.IO;
using System.Windows;
using System.Windows.Controls;
using Archivero.Datos;

namespace Archivero.Vistas;

public sealed record DisenoObservado(int Id, string Resumen);

public partial class AsistenteCarpetaObservadaWindow : Window
{
    private readonly CarpetasObservadasRepository _repositorio = new();
    private readonly EntidadRepository _entidades = new();
    private readonly CarpetaObservadaExterna? _existente;
    private readonly List<int> _configuraciones = [];
    private readonly Stack<int> _pasosAnteriores = new();
    private ZonaControlCarpeta? _zonaIdentificacion;
    private ZonaControlCarpeta? _zonaCedible;
    private string? _rutaEjemplo;
    private string? _rutaCedible;
    private int _paso = 1;

    public CarpetaObservadaExterna? CarpetaGuardada { get; private set; }

    public AsistenteCarpetaObservadaWindow(CarpetaObservadaExterna? existente = null)
    {
        InitializeComponent();
        ComboEntidad.AddHandler(
            System.Windows.Controls.TextBox.TextChangedEvent,
            new TextChangedEventHandler(Entidad_TextChanged)
        );
        _existente = existente;
        if (existente is not null)
        {
            TxtNombre.Text = existente.Nombre;
            TxtRuta.Text = existente.Ruta;
            ChkSubcarpetas.IsChecked = existente.IncluirSubcarpetas;
            ChkPeriodo.IsChecked = existente.SeguirPeriodo;
            ComboFormato.SelectedIndex = existente.FormatoPeriodo switch
            {
                "AAAA_MM" => 1,
                "AAAAMM" => 2,
                "AAAA" => 3,
                _ => 0,
            };
            _zonaIdentificacion = existente.ZonaIdentificacion;
            TxtIdentificacion.Text = existente.IdentificacionEsperada ?? "";
            if (existente.EntidadIdentificacionId is int entidadId)
            {
                var candidatos = _entidades.Buscar(
                    CategoriaEntidad.Emisor,
                    existente.IdentificacionEsperada ?? ""
                );
                ComboEntidad.Text =
                    candidatos.FirstOrDefault(nombre =>
                        _entidades.ObtenerOCrear(CategoriaEntidad.Emisor, nombre) == entidadId
                    ) ?? "";
            }
            ChkCedibles.IsChecked = existente.TieneCedibles;
            _zonaCedible = existente.ZonaCedible;
            TxtCedibleEsperado.Text = existente.CedibleEsperado ?? "";
            _configuraciones.AddRange(existente.ConfiguracionesDocumentoIds ?? []);
            ComboAccion.SelectedIndex = existente.AccionAlLlegar switch
            {
                "SoloRegistrar" => 0,
                "ImprimirPrimeraPagina" => 1,
                "ImprimirTodo" => 2,
                "Avisar" => 3,
                "AvisarImprimirPrimeraPagina" => 4,
                _ => 0,
            };
        }
        Cedibles_Changed(this, new RoutedEventArgs());
        ActualizarDisenos();
        MostrarPaso(1);
    }

    private void MostrarPaso(int paso)
    {
        _paso = paso;
        PasoCarpeta.Visibility = paso == 1 ? Visibility.Visible : Visibility.Collapsed;
        PasoEjemplo.Visibility = paso == 2 ? Visibility.Visible : Visibility.Collapsed;
        PasoIdentificacion.Visibility = paso == 3 ? Visibility.Visible : Visibility.Collapsed;
        PasoCedibles.Visibility = paso == 4 ? Visibility.Visible : Visibility.Collapsed;
        PasoDisenos.Visibility = paso == 5 ? Visibility.Visible : Visibility.Collapsed;
        PasoAccion.Visibility = paso == 6 ? Visibility.Visible : Visibility.Collapsed;
        var titulos = new[]
        {
            "",
            "Paso 1 de 6 — ¿Qué carpeta observar?",
            "Paso 2 de 6 — Elige un documento tuyo de ejemplo.",
            "Paso 3 de 6 — ¿Cómo se sabe que es tuyo?",
            "Paso 4 de 6 — ¿Hay copias cedibles?",
            "Paso 5 de 6 — Enseña el documento a Archivero.",
            "Paso 6 de 6 — ¿Qué hacer cuando llegue un documento nuevo tuyo?",
        };
        var ayudas = new[]
        {
            "",
            "Archivero solo lee esta carpeta; nunca mueve ni borra nada aquí.",
            "Elige un documento que sea de tu empresa; con él le enseñas a Archivero a reconocer los tuyos.",
            "Los documentos que no digan esto en esa zona se ignoran por completo.",
            "Un cedible es otra copia del mismo documento; Archivero se queda con el original.",
            "Configura uno o más diseños de documento para reconocer los documentos de esta carpeta.",
            "Lo que ya está en la carpeta solo se ingresa a Hormiguero; imprimir o avisar es solo para lo que llegue desde hoy.",
        };
        TxtTitulo.Text = titulos[paso];
        TxtAyuda.Text = ayudas[paso];
        BtnAtras.IsEnabled = _pasosAnteriores.Count > 0;
        BtnSiguiente.Content = paso == 6 ? "Guardar" : "Siguiente";
        if (paso == 6)
            ActualizarResumen();
        TxtError.Visibility = Visibility.Collapsed;
    }

    private void Atras_Click(object sender, RoutedEventArgs e)
    {
        if (_pasosAnteriores.Count > 0)
            MostrarPaso(_pasosAnteriores.Pop());
    }

    private void Siguiente_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            switch (_paso)
            {
                case 1:
                    if (
                        string.IsNullOrWhiteSpace(TxtNombre.Text)
                        || !Directory.Exists(TxtRuta.Text.Trim())
                    )
                    {
                        MostrarError("Escribe un nombre y elige una carpeta que exista.");
                        return;
                    }
                    break;
                case 2:
                    if (_rutaEjemplo is null || !File.Exists(_rutaEjemplo))
                    {
                        MostrarError("Elige un PDF de ejemplo.");
                        return;
                    }
                    break;
                case 3:
                    if (
                        _zonaIdentificacion is null
                        || string.IsNullOrWhiteSpace(TxtIdentificacion.Text)
                        || (
                            string.IsNullOrWhiteSpace(ComboEntidad.Text)
                            && _existente?.EntidadIdentificacionId is null
                        )
                    )
                    {
                        MostrarError(
                            "Marca la zona, escribe lo que debe decir y elige quién lo emite."
                        );
                        return;
                    }
                    break;
                case 4:
                    if (
                        ChkCedibles.IsChecked == true
                        && (
                            _zonaCedible is null
                            || string.IsNullOrWhiteSpace(TxtCedibleEsperado.Text)
                        )
                    )
                    {
                        MostrarError("Marca la zona cedible y escribe el texto esperado.");
                        return;
                    }
                    break;
                case 5:
                    if (_configuraciones.Count == 0)
                    {
                        MostrarError("Configura al menos un diseño de documento.");
                        return;
                    }
                    break;
                case 6:
                    GuardarCarpeta();
                    return;
            }
            _pasosAnteriores.Push(_paso);
            MostrarPaso(_paso + 1);
        }
        catch (Exception ex)
        {
            MostrarError($"No se pudo continuar: {ex.Message}");
        }
    }

    private void Examinar_Click(object sender, RoutedEventArgs e)
    {
        using var dialogo = new System.Windows.Forms.FolderBrowserDialog
        {
            SelectedPath = TxtRuta.Text,
            ShowNewFolderButton = false,
        };
        if (dialogo.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;
        TxtRuta.Text = dialogo.SelectedPath;
        var periodo = PeriodosCarpetaObservada.Detectar(dialogo.SelectedPath);
        if (
            periodo is not null
            && System.Windows.MessageBox.Show(
                this,
                $"Parece una carpeta por período. ¿Observar «{periodo.Value.RutaBase}» y seguir el período automáticamente?",
                "Carpetas observadas",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            ) == MessageBoxResult.Yes
        )
        {
            TxtRuta.Text = periodo.Value.RutaBase;
            ChkPeriodo.IsChecked = true;
            ComboFormato.SelectedIndex = periodo.Value.Formato switch
            {
                "AAAA_MM" => 1,
                "AAAAMM" => 2,
                "AAAA" => 3,
                _ => 0,
            };
        }
    }

    private void ElegirEjemplo_Click(object sender, RoutedEventArgs e) =>
        ElegirPdf(ruta =>
        {
            _rutaEjemplo = ruta;
            TxtEjemplo.Text = Path.GetFileName(ruta);
        });

    private void ElegirCedible_Click(object sender, RoutedEventArgs e) =>
        ElegirPdf(ruta =>
        {
            _rutaCedible = ruta;
            TxtCedible.Text = Path.GetFileName(ruta);
            _zonaCedible = null;
        });

    private void ElegirPdf(Action<string> elegido)
    {
        string carpeta = _rutaEjemplo is not null
            ? Path.GetDirectoryName(_rutaEjemplo)!
            : TxtRuta.Text.Trim();
        if (Directory.Exists(carpeta) && ChkPeriodo.IsChecked == true)
        {
            var candidato = PeriodosCarpetaObservada
                .Rutas(
                    new CarpetaObservadaExterna(
                        Guid.Empty,
                        "",
                        carpeta,
                        false,
                        true,
                        SeguirPeriodo: true,
                        FormatoPeriodo: LeerFormato()
                    ),
                    DateTime.Now
                )
                .FirstOrDefault();
            if (candidato is not null)
                carpeta = candidato;
        }
        var dialogo = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Documentos PDF (*.pdf)|*.pdf",
            InitialDirectory = Directory.Exists(carpeta)
                ? carpeta
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            CheckFileExists = true,
        };
        if (dialogo.ShowDialog(this) == true)
            elegido(dialogo.FileName);
    }

    private void MarcarIdentificacion_Click(object sender, RoutedEventArgs e)
    {
        if (_rutaEjemplo is null)
        {
            MostrarError("Primero elige el PDF de ejemplo.");
            return;
        }
        var ventana = new MarcarZonaControlWindow(_rutaEjemplo) { Owner = this };
        if (ventana.ShowDialog() == true)
        {
            _zonaIdentificacion = ventana.Zona;
            TxtIdentificacion.Text = ventana.Zona!.TextoEsperado;
            TxtLeidoIdentificacion.Text = $"Texto leído: {ventana.Zona.TextoEsperado}";
        }
    }

    private void MarcarCedible_Click(object sender, RoutedEventArgs e)
    {
        string? ruta = _rutaCedible ?? _rutaEjemplo;
        if (ruta is null)
        {
            MostrarError("Elige un PDF de ejemplo primero.");
            return;
        }
        var ventana = new MarcarZonaControlWindow(ruta) { Owner = this };
        if (ventana.ShowDialog() == true)
        {
            _zonaCedible = ventana.Zona;
            TxtCedibleEsperado.Text = ventana.Zona!.TextoEsperado;
            TxtLeidoCedible.Text = $"Texto leído: {ventana.Zona.TextoEsperado}";
        }
    }

    private void Cedibles_Changed(object sender, RoutedEventArgs e) =>
        PanelCedibles.Visibility =
            ChkCedibles.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private void Entidad_TextChanged(object sender, TextChangedEventArgs e) =>
        ComboEntidad.ItemsSource = _entidades.Buscar(CategoriaEntidad.Emisor, ComboEntidad.Text);

    private void ConfigurarDocumento_Click(object sender, RoutedEventArgs e)
    {
        if (_rutaEjemplo is null)
        {
            MostrarError("Elige el PDF de ejemplo en el paso anterior.");
            return;
        }
        var identificar = new IdentificarDocumentoWindow(
            _rutaEjemplo,
            true,
            Path.GetFullPath(TxtRuta.Text.Trim())
        )
        {
            Owner = this,
        };
        if (
            identificar.ShowDialog() == true
            && identificar.ConfiguracionDocumentoId is int id
            && !_configuraciones.Contains(id)
        )
        {
            _configuraciones.Add(id);
            ActualizarDisenos();
        }
    }

    private void AgregarDiseno_Click(object sender, RoutedEventArgs e)
    {
        if (_rutaEjemplo is null)
        {
            MostrarError("Elige el PDF de ejemplo en el paso 2.");
            return;
        }
        _pasosAnteriores.Push(5);
        MostrarPaso(2);
    }

    private void QuitarDiseno_Click(object sender, RoutedEventArgs e)
    {
        if (ListaDisenos.SelectedItem is DisenoObservado diseno)
        {
            _configuraciones.Remove(diseno.Id);
            ActualizarDisenos();
        }
    }

    private void ActualizarDisenos()
    {
        var configuraciones =
            new ConfiguracionDocumentoRepository().ObtenerTodasConPatronesParaObservador();
        ListaDisenos.ItemsSource = _configuraciones
            .Select(id =>
            {
                var configuracion = configuraciones.FirstOrDefault(c => c.Id == id);
                return new DisenoObservado(
                    id,
                    configuracion is null
                        ? $"Diseño {id}"
                        : $"{configuracion.Tipo} · {configuracion.Emisor}"
                );
            })
            .ToList();
    }

    private void CambiarPaso_Click(object sender, RoutedEventArgs e)
    {
        if (
            sender is System.Windows.Controls.Button { Tag: string paso }
            && int.TryParse(paso, out int numero)
        )
        {
            _pasosAnteriores.Push(_paso);
            MostrarPaso(numero);
        }
    }

    private void ActualizarResumen() =>
        TxtResumen.Text =
            $"Carpeta: {TxtNombre.Text.Trim()} — {TxtRuta.Text.Trim()}\n"
            + $"Documento de ejemplo: {(string.IsNullOrWhiteSpace(_rutaEjemplo) ? "sin elegir" : Path.GetFileName(_rutaEjemplo))}\n"
            + $"Identificación: {TxtIdentificacion.Text.Trim()} — {ComboEntidad.Text.Trim()}\n"
            + $"Cedibles: {(ChkCedibles.IsChecked == true ? TxtCedibleEsperado.Text.Trim() : "No")}\n"
            + $"Diseños: {_configuraciones.Count}\n"
            + $"Acción: {(ComboAccion.SelectedItem as ComboBoxItem)?.Content}";

    private void GuardarCarpeta()
    {
        string ruta = Path.GetFullPath(TxtRuta.Text.Trim());
        int entidadId =
            string.IsNullOrWhiteSpace(ComboEntidad.Text)
            && _existente?.EntidadIdentificacionId is int entidadAnterior
                ? entidadAnterior
                : _entidades.ObtenerOCrear(CategoriaEntidad.Emisor, ComboEntidad.Text.Trim());
        string accion = ComboAccion.SelectedIndex switch
        {
            1 => "ImprimirPrimeraPagina",
            2 => "ImprimirTodo",
            3 => "Avisar",
            4 => "AvisarImprimirPrimeraPagina",
            _ => "SoloRegistrar",
        };
        CarpetaGuardada = new CarpetaObservadaExterna(
            _existente?.Id ?? Guid.NewGuid(),
            TxtNombre.Text.Trim(),
            ruta,
            ChkSubcarpetas.IsChecked == true,
            _existente?.Activa ?? true,
            _existente is null ? DateTime.Now : _existente.Agregada,
            "Configuraciones",
            null,
            "",
            ChkPeriodo.IsChecked == true,
            LeerFormato(),
            accion,
            _zonaIdentificacion,
            TxtIdentificacion.Text.Trim(),
            entidadId,
            ChkCedibles.IsChecked == true,
            ChkCedibles.IsChecked == true ? _zonaCedible : null,
            ChkCedibles.IsChecked == true ? TxtCedibleEsperado.Text.Trim() : null,
            _configuraciones.ToList()
        );
        DialogResult = true;
    }

    private string LeerFormato() =>
        ComboFormato.SelectedIndex switch
        {
            1 => "AAAA_MM",
            2 => "AAAAMM",
            3 => "AAAA",
            _ => "AAAA_AAAAMM",
        };

    private void MostrarError(string mensaje)
    {
        TxtError.Text = mensaje;
        TxtError.Visibility = Visibility.Visible;
    }
}
