using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Buscadero.Core.Alertas;

namespace Buscadero.App;

internal sealed record LugarAvisoOpcion(int Orden, string Nombre);

internal sealed record DatoAvisoOpcion(string Id, string Nombre);

internal sealed record AvisoDefinicion(
    int OrdenLugar,
    string Tipo,
    string ParametrosJson,
    string? ModoNombre
);

internal sealed class DialogoEditorAviso : Window
{
    private readonly IReadOnlyList<LugarAvisoOpcion> _lugares;
    private readonly IReadOnlyList<DatoAvisoOpcion> _datos;
    private readonly IReadOnlyList<(int OrdenA, int OrdenB, string Cardinalidad)> _enlaces;
    private readonly IReadOnlyList<string> _modos;
    private readonly StackPanel _campos = new();
    private readonly TextBlock _frase = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 10, 0, 0),
    };
    private readonly ComboBox _modo = new() { MinWidth = 220 };
    private readonly Button _guardar = new()
    {
        Name = "GuardarAviso",
        Content = "Guardar aviso",
        Padding = new Thickness(12, 6, 12, 6),
        IsDefault = true,
    };
    private readonly Button _cancelar = new()
    {
        Content = "Cancelar",
        Padding = new Thickness(12, 6, 12, 6),
        IsCancel = true,
    };
    private ComboBox? _dato,
        _ubicacion,
        _origen,
        _destino,
        _condicion,
        _tipoDias,
        _cuando;
    private TextBox? _texto,
        _dias;
    private CheckBox? _linea;
    private string _tipo = "falta_dato";
    private AvisoDefinicion? _existente;
    private bool _textoEditado;
    private bool _actualizandoTexto;
    private readonly List<Button> _botonesTipo = [];
    public AvisoDefinicion? Resultado { get; private set; }

    public DialogoEditorAviso(
        IReadOnlyList<LugarAvisoOpcion> lugares,
        IReadOnlyList<DatoAvisoOpcion> datos,
        IReadOnlyList<(int OrdenA, int OrdenB, string Cardinalidad)> enlaces,
        IReadOnlyList<string> modos,
        AvisoDefinicion? existente
    )
    {
        NameScope.SetNameScope(this, new NameScope());
        _lugares = lugares;
        _datos = datos;
        _enlaces = enlaces;
        _modos = modos;
        _existente = existente;
        Title = "Editar aviso";
        Width = 760;
        Height = 700;
        MinWidth = 600;
        MinHeight = 440;
        MaxHeight = Math.Max(440, SystemParameters.WorkArea.Height - 40);
        Height = Math.Min(700, MaxHeight);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(16) };
        var pie = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0),
        };
        _guardar.Click += Guardar_Click;
        _cancelar.Click += (_, _) => Close();
        pie.Children.Add(_guardar);
        pie.Children.Add(_cancelar);
        DockPanel.SetDock(pie, Dock.Bottom);
        root.Children.Add(pie);
        var contenido = new StackPanel();
        var tarjetas = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 12) };
        int indiceTarjeta = 0;
        foreach (
            var opcion in new[]
            {
                ("falta_dato", "Falta ingresar un dato", "Ej. fecha de retiro"),
                ("plazo", "Plazo entre documentos", "Ej. 7 días sin guía firmada"),
                ("listo_para", "Listo para…", "Ej. listos para facturar"),
            }
        )
        {
            var boton = new Button
            {
                Content = $"{opcion.Item2}\n{opcion.Item3}",
                Tag = opcion.Item1,
                Margin = new Thickness(3),
                Padding = new Thickness(8),
                MinHeight = 58,
                BorderThickness = new Thickness(2),
            };
            boton.Name = $"TipoAviso{indiceTarjeta++}";
            RegisterName(boton.Name, boton);
            _botonesTipo.Add(boton);
            boton.Click += (_, _) => ElegirTipo((string)boton.Tag);
            tarjetas.Children.Add(boton);
        }
        contenido.Children.Add(tarjetas);
        _campos.Margin = new Thickness(0, 2, 0, 4);
        contenido.Children.Add(_campos);
        if (_modos.Count > 0)
        {
            contenido.Children.Add(
                new TextBlock
                {
                    Text = "Aplica al modo",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 10, 0, 3),
                }
            );
            _modo.Items.Add("Todos los modos");
            foreach (string modo in _modos)
                _modo.Items.Add(modo);
            _modo.SelectedIndex = 0;
            contenido.Children.Add(_modo);
        }
        contenido.Children.Add(_frase);
        var scroll = new ScrollViewer
        {
            Content = contenido,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        root.Children.Add(scroll);
        Content = root;
        _frase.Name = "FraseAviso";
        RegisterName(_frase.Name, _frase);
        RegisterName(_guardar.Name, _guardar);
        ElegirTipo(existente?.Tipo ?? "falta_dato");
        if (existente is not null)
            CargarExistente(existente);
        ActualizarTarjetas();
    }

    private void ElegirTipo(string tipo)
    {
        _tipo = tipo;
        foreach (var elemento in _campos.Children.OfType<FrameworkElement>())
            if (!string.IsNullOrEmpty(elemento.Name))
                UnregisterName(elemento.Name);
        _campos.Children.Clear();
        if (tipo == "falta_dato")
            CamposFalta();
        else if (tipo == "plazo")
            CamposPlazo();
        else
            CamposListo();
        ActualizarTarjetas();
        ActualizarFrase();
    }

    private ComboBox Selector(string titulo, IEnumerable<object> items, string display = "")
    {
        _campos.Children.Add(
            new TextBlock
            {
                Text = titulo,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 6, 0, 2),
            }
        );
        var c = new ComboBox
        {
            ItemsSource = items,
            MinWidth = 300,
            DisplayMemberPath = display,
        };
        c.Name = titulo switch
        {
            "Dato que falta" => "DatoAviso",
            "Documento del proceso" => "UbicacionAviso",
            "Contar desde" => "OrigenAviso",
            "Tipo de días" => "TipoDiasAviso",
            "Hasta que llegue" => "DestinoAviso",
            "Solo si existe (opcional)" or "Y también esté (opcional)" => "CondicionAviso",
            "Cuando esté" => "CuandoAviso",
            _ => "",
        };
        if (c.Name.Length > 0)
            RegisterName(c.Name, c);
        c.SelectionChanged += (_, _) => ActualizarFrase();
        _campos.Children.Add(c);
        return c;
    }

    private TextBox Texto(string titulo, string inicial = "")
    {
        _campos.Children.Add(
            new TextBlock
            {
                Text = titulo,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 6, 0, 2),
            }
        );
        var t = new TextBox { Text = inicial, MinWidth = 300 };
        t.Name = titulo switch
        {
            "Texto del aviso" or "Nombre de la lista" => "TextoAviso",
            "Cantidad de días" => "DiasAviso",
            _ => "",
        };
        if (t.Name.Length > 0)
            RegisterName(t.Name, t);
        t.TextChanged += (_, _) =>
        {
            if (!_actualizandoTexto)
                _textoEditado = true;
            ActualizarFrase();
        };
        _campos.Children.Add(t);
        return t;
    }

    // Dato del diccionario elegido, o el escrito por el usuario (se guarda con su nombre).
    private DatoAvisoOpcion? DatoElegido()
    {
        if (_dato is null)
            return null;
        if (_dato.SelectedItem is DatoAvisoOpcion elegido && elegido.Nombre == _dato.Text)
            return elegido;
        string texto = _dato.Text.Trim();
        return texto.Length == 0
            ? null
            : _datos.FirstOrDefault(d =>
                string.Equals(d.Nombre, texto, StringComparison.OrdinalIgnoreCase)
            ) ?? new DatoAvisoOpcion(texto, texto);
    }

    private void CamposFalta()
    {
        _dato = Selector("Dato que falta", _datos.Cast<object>(), nameof(DatoAvisoOpcion.Nombre));
        // Se puede elegir un dato del diccionario o escribir uno propio (ej. "Fecha de retiro"),
        // que se ingresa después en la cadena.
        _dato.IsEditable = true;
        _dato.SelectionChanged += (_, _) => ActualizarTextoSugerido();
        _dato.AddHandler(
            System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler((_, _) => ActualizarTextoSugerido())
        );
        if (_datos.Count > 0)
            _dato.SelectedIndex = 0;
        _texto = Texto("Texto del aviso");
        _ubicacion = Selector(
            "Documento del proceso",
            _lugares.Cast<object>(),
            nameof(LugarAvisoOpcion.Nombre)
        );
        if (_lugares.Count > 0)
            _ubicacion.SelectedIndex = 0;
        ActualizarTextoSugerido(true);
    }

    private void CamposPlazo()
    {
        _origen = Selector(
            "Contar desde",
            _lugares.Cast<object>().Concat(_datos.Select(d => (object)d)),
            nameof(LugarAvisoOpcion.Nombre)
        );
        if (_origen.Items.Count > 0)
            _origen.SelectedIndex = 0;
        _dias = Texto("Cantidad de días", "7");
        _tipoDias = Selector(
            "Tipo de días",
            new object[] { new Opcion("hábiles", "habiles"), new Opcion("corridos", "corridos") },
            nameof(Opcion.Nombre)
        );
        _tipoDias.SelectedIndex = 0;
        _destino = Selector(
            "Hasta que llegue",
            _lugares.Cast<object>(),
            nameof(LugarAvisoOpcion.Nombre)
        );
        if (_destino.Items.Count > 1)
            _destino.SelectedIndex = 1;
        _condicion = Selector(
            "Solo si existe (opcional)",
            new object[] { new LugarAvisoOpcion(-1, "Sin condición") }.Concat(_lugares),
            nameof(LugarAvisoOpcion.Nombre)
        );
        _condicion.SelectedIndex = 0;
        _linea = new CheckBox { Content = "Por línea", Margin = new Thickness(0, 8, 0, 0) };
        _linea.Checked += (_, _) => ActualizarFrase();
        _linea.Unchecked += (_, _) => ActualizarFrase();
        _campos.Children.Add(_linea);
        _campos.Children.Add(
            new TextBlock
            {
                Text = "Disponible cuando existe un enlace entre los documentos elegidos.",
                Foreground = System.Windows.Media.Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
            }
        );
        _texto = Texto("Texto del aviso");
        _destino.SelectionChanged += (_, _) => ActualizarPorLinea();
        _destino.SelectionChanged += (_, _) => ActualizarTextoSugerido();
        _origen.SelectionChanged += (_, _) => ActualizarPorLinea();
        ActualizarPorLinea();
        ActualizarTextoSugerido(true);
    }

    private void CamposListo()
    {
        _cuando = Selector("Cuando esté", _lugares.Cast<object>(), nameof(LugarAvisoOpcion.Nombre));
        if (_cuando.Items.Count > 0)
            _cuando.SelectedIndex = 0;
        // "Hasta que llegue" es el documento que saca la cadena de la lista (ej. la factura
        // propia); antes esta casilla decía "Y también esté" y confundía ambos papeles.
        _condicion = Selector(
            "Y también esté (opcional)",
            new object[] { new LugarAvisoOpcion(-1, "Nada más") }.Concat(_lugares),
            nameof(LugarAvisoOpcion.Nombre)
        );
        _condicion.SelectedIndex = 0;
        _destino = Selector(
            "Hasta que llegue",
            _lugares.Cast<object>(),
            nameof(LugarAvisoOpcion.Nombre)
        );
        if (_destino.Items.Count > 1)
            _destino.SelectedIndex = _destino.Items.Count - 1;
        _texto = Texto("Nombre de la lista");
        ActualizarTextoSugerido(true);
    }

    private void ActualizarTextoSugerido(bool reiniciar = false)
    {
        if (_texto is null)
            return;
        if (reiniciar)
            _textoEditado = false;
        if (_textoEditado)
            return;
        string sugerencia = _tipo switch
        {
            "falta_dato" when DatoElegido() is DatoAvisoOpcion dato =>
                $"Ingresar {InicialMinuscula(dato.Nombre)}",
            "plazo" when _destino?.SelectedItem is LugarAvisoOpcion destino =>
                $"Falta {destino.Nombre}",
            "listo_para" => "Listos para facturar",
            _ => "",
        };
        _actualizandoTexto = true;
        _texto.Text = sugerencia;
        _actualizandoTexto = false;
    }

    private static string InicialMinuscula(string texto) => texto.ToLowerInvariant();

    private void ActualizarTarjetas()
    {
        foreach (Button boton in _botonesTipo)
        {
            bool elegida = Equals(boton.Tag, _tipo);
            var acento = (System.Windows.Media.SolidColorBrush)FindResource("Hormiguero.Acento");
            boton.BorderBrush = elegida
                ? acento
                : (System.Windows.Media.Brush)FindResource("Hormiguero.Borde");
            boton.Background = elegida
                ? new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(
                        36,
                        acento.Color.R,
                        acento.Color.G,
                        acento.Color.B
                    )
                )
                : (System.Windows.Media.Brush)FindResource("Hormiguero.Fondo");
            AutomationProperties.SetItemStatus(boton, elegida ? "Elegida" : "No elegida");
            AutomationProperties.SetName(
                boton,
                $"{((string)boton.Content).Replace('\n', ' ')}: {(elegida ? "elegida" : "no elegida")}"
            );
        }
    }

    private void ActualizarPorLinea()
    {
        if (_linea is null)
            return;
        int? a = _origen?.SelectedItem is LugarAvisoOpcion la ? la.Orden : null;
        int? b = _destino?.SelectedItem is LugarAvisoOpcion lb ? lb.Orden : null;
        _linea.IsEnabled =
            a.HasValue
            && b.HasValue
            && EditorReglasEsquema.PermitePorLinea(a.Value, b.Value, _enlaces);
        if (!_linea.IsEnabled)
            _linea.IsChecked = false;
    }

    private void ActualizarFrase()
    {
        string mensaje = _texto?.Text.Trim() ?? "aviso";
        _frase.Text = _tipo switch
        {
            "falta_dato" when DatoElegido() is DatoAvisoOpcion d =>
                $"Mientras falte {d.Nombre}, aviso «{mensaje}».",
            "plazo" when _destino?.SelectedItem is LugarAvisoOpcion h =>
                $"Si pasan {_dias?.Text} días {(_tipoDias?.SelectedItem as Opcion)?.Nombre} desde {(_origen?.SelectedItem is LugarAvisoOpcion o ? o.Nombre + " (su fecha de emisión)" : "el dato ingresado")} sin {h.Nombre}, aviso «{mensaje}»{(_linea?.IsChecked == true ? " (por línea)" : "")}.",
            "listo_para"
                when _cuando?.SelectedItem is LugarAvisoOpcion c
                    && _destino?.SelectedItem is LugarAvisoOpcion l =>
                $"Listo para {mensaje.Replace("Listos para ", "", StringComparison.OrdinalIgnoreCase)} cuando esté{(_condicion?.SelectedItem is LugarAvisoOpcion { Orden: >= 0 } y ? $"n {c.Nombre} + {y.Nombre}" : $" {c.Nombre}")}, hasta que llegue {l.Nombre}.",
            _ => "Complete los campos del aviso.",
        };
    }

    private void CargarExistente(AvisoDefinicion aviso)
    {
        try
        {
            using var d = JsonDocument.Parse(aviso.ParametrosJson);
            var r = d.RootElement;
            if (_modo.Items.Count > 0 && aviso.ModoNombre is string modo)
            {
                int ix = _modo.Items.IndexOf(modo);
                if (ix >= 0)
                    _modo.SelectedIndex = ix;
            }
            if (_tipo == "falta_dato")
            {
                string idDato = r.GetProperty("dato").GetString() ?? "";
                if (_datos.FirstOrDefault(x => x.Id == idDato) is DatoAvisoOpcion conocido)
                    _dato!.SelectedItem = conocido;
                else
                    _dato!.Text = idDato;
                _texto!.Text = r.GetProperty("texto").GetString() ?? "";
                _ubicacion!.SelectedItem = _lugares.FirstOrDefault(x =>
                    x.Orden == aviso.OrdenLugar
                );
            }
            else if (_tipo == "plazo")
            {
                long? origen = r.TryGetProperty("desdeLugarId", out var ol) ? ol.GetInt64() : null;
                string? dato =
                    r.TryGetProperty("desdeDato", out var dd)
                    && dd.ValueKind == JsonValueKind.String
                        ? dd.GetString()
                        : null;
                _origen!.SelectedItem = origen is long oi
                    ? _lugares.FirstOrDefault(x => x.Orden == oi)
                    : _datos.FirstOrDefault(x => x.Id == dato);
                _dias!.Text = r.GetProperty("dias").GetInt32().ToString();
                _tipoDias!.SelectedItem = ((IEnumerable<object>)_tipoDias.ItemsSource)
                    .Cast<Opcion>()
                    .FirstOrDefault(x => x.Valor == r.GetProperty("tipoDias").GetString());
                _destino!.SelectedItem = _lugares.FirstOrDefault(x =>
                    x.Orden == r.GetProperty("hastaLugarId").GetInt32()
                );
                _texto!.Text = r.GetProperty("texto").GetString() ?? "";
                _linea!.IsChecked = r.GetProperty("porLinea").GetBoolean();
                if (
                    r.TryGetProperty("condicionLugarId", out var co)
                    && co.ValueKind == JsonValueKind.Number
                )
                    _condicion!.SelectedItem = _lugares.FirstOrDefault(x =>
                        x.Orden == co.GetInt32()
                    );
            }
            else
            {
                _cuando!.SelectedItem = _lugares.FirstOrDefault(x =>
                    x.Orden == r.GetProperty("cuandoLugarId").GetInt32()
                );
                _destino!.SelectedItem = _lugares.FirstOrDefault(x =>
                    x.Orden == r.GetProperty("hastaLugarId").GetInt32()
                );
                if (
                    r.TryGetProperty("condicionLugarId", out var condicion)
                    && condicion.ValueKind == JsonValueKind.Number
                )
                    _condicion!.SelectedItem = _lugares.FirstOrDefault(x =>
                        x.Orden == condicion.GetInt32()
                    );
                _texto!.Text = r.GetProperty("lista").GetString() ?? "";
            }
        }
        catch (JsonException) { }
        ActualizarFrase();
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            object parametros;
            int orden;
            string? modo = _modo.SelectedIndex > 0 ? _modo.SelectedItem?.ToString() : null;
            if (_tipo == "falta_dato")
            {
                if (
                    DatoElegido() is not DatoAvisoOpcion dato
                    || _ubicacion?.SelectedItem is not LugarAvisoOpcion lugar
                )
                    throw new InvalidOperationException();
                orden = lugar.Orden;
                parametros = new { dato = dato.Id, texto = TextoFinal("Ingresar " + dato.Nombre) };
            }
            else if (_tipo == "plazo")
            {
                if (
                    _origen?.SelectedItem is not object o
                    || _destino?.SelectedItem is not LugarAvisoOpcion h
                    || !int.TryParse(_dias?.Text, out int dias)
                    || dias < 0
                )
                    throw new InvalidOperationException();
                long? desde = o is LugarAvisoOpcion lo ? lo.Orden : null;
                string? desdeDato = o is DatoAvisoOpcion da ? da.Id : null;
                orden = desde is long ordenOrigen ? (int)ordenOrigen : h.Orden;
                parametros = new
                {
                    desdeLugarId = desde,
                    desdeDato,
                    hastaLugarId = (long)h.Orden,
                    dias,
                    tipoDias = (_tipoDias?.SelectedItem as Opcion)?.Valor ?? "habiles",
                    calendarioId = (long?)null,
                    condicionLugarId = (_condicion?.SelectedItem as LugarAvisoOpcion)?.Orden
                        is int co
                    && co >= 0
                        ? (long?)co
                        : null,
                    texto = TextoFinal("Falta " + h.Nombre),
                    porLinea = _linea?.IsChecked == true,
                };
            }
            else
            {
                if (
                    _cuando?.SelectedItem is not LugarAvisoOpcion c
                    || _destino?.SelectedItem is not LugarAvisoOpcion h
                )
                    throw new InvalidOperationException();
                orden = c.Orden;
                parametros = new
                {
                    cuandoLugarId = (long)c.Orden,
                    condicionLugarId = (_condicion?.SelectedItem as LugarAvisoOpcion)?.Orden
                        is int co
                    && co >= 0
                        ? (long?)co
                        : null,
                    hastaLugarId = (long)h.Orden,
                    lista = TextoFinal("Listos para facturar"),
                };
            }
            if (EditorReglasEsquema.Validar(_tipo, parametros) is string error)
                throw new InvalidOperationException(error);
            Resultado = new(orden, _tipo, JsonSerializer.Serialize(parametros), modo);
            DialogResult = true;
            Close();
        }
        catch (InvalidOperationException)
        {
            MessageBox.Show(
                this,
                "Complete los campos requeridos del aviso.",
                "Avisos",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }

    private string TextoFinal(string sugerido) =>
        string.IsNullOrWhiteSpace(_texto?.Text) ? sugerido : _texto.Text.Trim();

    private sealed record Opcion(string Nombre, string Valor);
}
