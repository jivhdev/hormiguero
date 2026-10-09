using System.Windows;
using System.Windows.Controls;
using Buscadero.Core.Lineas;
using Hormiguero.Nucleo.Datos;

namespace Buscadero.App;

public partial class DialogoEsquemaCadena : Window
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conexion;
    private readonly RepositorioEsquemas _repositorio;
    private readonly IReadOnlyList<Identificacion> _identificaciones;
    private readonly IReadOnlyList<TipoDatoCompartido> _compartidos;
    private List<DefinicionLugarEsquema> _lugares = [];
    private List<DefinicionParejaEsquema> _parejas = [];
    private readonly List<string> _nombresParejas = [];
    private int _paso = 1;

    public DialogoEsquemaCadena(string? proveedorInicial = null)
    {
        InitializeComponent();
        _conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        _repositorio = new(_conexion);
        _identificaciones = new Identificaciones(_conexion).Listar();
        _compartidos = AsistenteEsquemaCadena.TiposQueCompartenDatos(_conexion);
        Proveedor.ItemsSource = _identificaciones
            .Select(i => i.Emisor)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order()
            .ToArray();
        RefrescarListas();
        Compartidos.Text = GenerarPistas();
        Proveedor.LostFocus += Proveedor_LostFocus;
        if (!string.IsNullOrWhiteSpace(proveedorInicial))
        {
            Proveedor.Text = proveedorInicial;
            CargarEsquemaExistente(proveedorInicial);
        }
        MostrarPaso();
    }

    private void Proveedor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(Proveedor.Text))
            CargarEsquemaExistente(Proveedor.Text);
    }

    private void CargarEsquemaExistente(string proveedor)
    {
        var esquema = _repositorio.ObtenerPorProveedor(proveedor);
        if (esquema is null)
            return;
        _lugares.Clear();
        _lugares.AddRange(
            esquema.Lugares.Select(l => new DefinicionLugarEsquema(
                l.Orden,
                l.IdentificacionId,
                l.IniciaCadena,
                l.Nombre
            ))
        );
        _parejas.Clear();
        _nombresParejas.Clear();
        var byId = esquema.Lugares.ToDictionary(l => l.Id, l => l.Orden);
        foreach (var pareja in esquema.Parejas)
        {
            _parejas.Add(new(byId[pareja.LugarA], byId[pareja.LugarB], pareja.DatoDiccionarioId));
            _nombresParejas.Add(
                TextoPareja(new(byId[pareja.LugarA], byId[pareja.LugarB], pareja.DatoDiccionarioId))
            );
        }
        RefrescarListas();
        Estado.Text = "Se encontró un esquema; sus datos están listos para editar.";
    }

    private void MostrarPaso()
    {
        PasoProveedor.Visibility = _paso == 1 ? Visibility.Visible : Visibility.Collapsed;
        PasoDocumentos.Visibility = _paso == 2 ? Visibility.Visible : Visibility.Collapsed;
        PasoInicio.Visibility = _paso == 3 ? Visibility.Visible : Visibility.Collapsed;
        PasoParejas.Visibility = _paso == 4 ? Visibility.Visible : Visibility.Collapsed;
        PasoResumen.Visibility = _paso == 5 ? Visibility.Visible : Visibility.Collapsed;
        string[] titulos =
        [
            "Proveedor",
            "Documentos del proceso",
            "¿Cuáles inician la cadena?",
            "Parejas 1 a 1",
            "Resumen",
        ];
        string[] ayudas =
        [
            "Elija un emisor conocido o escriba el nombre del proveedor.",
            "Agregue los documentos a la lista derecha y ordene esa lista con Subir y Bajar.",
            "Marque los documentos que pueden iniciar la cadena. Debe marcar al menos uno.",
            "Elija dos documentos y un dato que compartan; Agregar pareja la añade a la lista.",
            "Revise proveedor, inicios, orden y parejas. Use Cambiar para corregir una sección y Guardar para conservar el esquema.",
        ];
        PasoTitulo.Text = titulos[_paso - 1];
        Ayuda.Text = ayudas[_paso - 1];
        NumeroPaso.Text = $"Paso {_paso} de 5";
        if (_paso == 2)
            Compartidos.Text = GenerarPistas();
        Siguiente.Visibility = _paso == 5 ? Visibility.Collapsed : Visibility.Visible;
        Guardar.Visibility = _paso == 5 ? Visibility.Visible : Visibility.Collapsed;
        if (_paso == 5)
            PrepararResumen();
    }

    private void AgregarTipo_Click(object sender, RoutedEventArgs e)
    {
        if (TiposDisponibles.SelectedItem is not TipoVm tipo)
            return;
        AgregarTipo(tipo);
    }

    private void AgregarTipo(TipoVm tipo)
    {
        if (_lugares.Any(l => l.IdentificacionId == tipo.Identificacion.Id))
            return;
        _lugares.Add(
            new(_lugares.Count, tipo.Identificacion.Id, false, tipo.Identificacion.NombreEstandar)
        );
        RefrescarListas();
        TiposProceso.SelectedItem = TiposProceso
            .Items.Cast<TipoVm>()
            .FirstOrDefault(t => t.Identificacion.Id == tipo.Identificacion.Id);
    }

    private void Disponible_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (TiposDisponibles.SelectedItem is TipoVm tipo)
            AgregarTipo(tipo);
    }

    private void QuitarTipo_Click(object sender, RoutedEventArgs e) => QuitarTipoSeleccionado();

    private void Proceso_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        QuitarTipoSeleccionado();

    private void QuitarTipoSeleccionado()
    {
        if (TiposProceso.SelectedItem is not TipoVm tipo)
            return;
        int orden = _lugares.FindIndex(l => l.IdentificacionId == tipo.Identificacion.Id);
        if (orden < 0)
            return;
        _lugares.RemoveAt(orden);
        _lugares = _lugares.Select((l, i) => l with { Orden = i }).ToList();
        var quitadas = _parejas.Where(p => p.OrdenA == orden || p.OrdenB == orden).ToHashSet();
        _parejas.RemoveAll(p => quitadas.Contains(p));
        _parejas = _parejas
            .Select(p => new DefinicionParejaEsquema(
                p.OrdenA > orden ? p.OrdenA - 1 : p.OrdenA,
                p.OrdenB > orden ? p.OrdenB - 1 : p.OrdenB,
                p.DatoDiccionarioId
            ))
            .ToList();
        _nombresParejas.Clear();
        _nombresParejas.AddRange(_parejas.Select(TextoPareja));
        RefrescarListas();
    }

    private void RefrescarListas()
    {
        var seleccionados = _lugares.OrderBy(l => l.Orden).ToArray();
        TiposDisponibles.ItemsSource = _identificaciones
            .Where(i => seleccionados.All(l => l.IdentificacionId != i.Id))
            .Select(i => new TipoVm(i))
            .ToArray();
        TiposProceso.ItemsSource = seleccionados
            .Select(l => _identificaciones.First(i => i.Id == l.IdentificacionId))
            .Select(i => new TipoVm(i))
            .ToArray();
        Inicios.ItemsSource = _lugares
            .Select((l, i) => new InicioVm(i, l.Nombre, l.IniciaCadena))
            .ToArray();
        LugarA.ItemsSource = LugarB.ItemsSource = _lugares
            .Select((l, i) => new LugarVm(i, l.Nombre))
            .ToArray();
        LugarA.DisplayMemberPath = LugarB.DisplayMemberPath = "Texto";
        Parejas.ItemsSource = _nombresParejas.ToArray();
        DatoPareja.ItemsSource = _compartidos
            .Select(d => new DatoVm(d.DatoId, d.DatoNombre))
            .DistinctBy(d => d.Id)
            .ToArray();
        DatoPareja.DisplayMemberPath = "Nombre";
    }

    private string GenerarPistas()
    {
        var grupos = _compartidos
            .GroupBy(d => (d.IdentificacionA, d.IdentificacionB))
            .Select(g =>
            {
                var primero = g.First();
                string tipoA = TipoPista(primero.TipoA, primero.EmisorA);
                string tipoB = TipoPista(primero.TipoB, primero.EmisorB);
                return $"{tipoA} ↔ {tipoB} · {string.Join(", ", g.Select(d => d.DatoNombre).Distinct())}";
            })
            .ToArray();
        return grupos.Length == 0
            ? "Pistas: estos tipos ya comparten datos (pueden ir en la misma cadena). Aún no hay datos compartidos configurados."
            : "Pistas: estos tipos ya comparten datos (pueden ir en la misma cadena)\n"
                + string.Join("\n", grupos);
    }

    private string TipoPista(string tipo, string emisor) =>
        string.Equals(emisor, Proveedor.Text.Trim(), StringComparison.OrdinalIgnoreCase)
        && !tipo.Contains("propia", StringComparison.OrdinalIgnoreCase)
            ? $"{tipo} del proveedor"
            : tipo;

    private string TextoPareja(DefinicionParejaEsquema pareja)
    {
        long identificacionA = _lugares[pareja.OrdenA].IdentificacionId;
        long identificacionB = _lugares[pareja.OrdenB].IdentificacionId;
        string nombreDato =
            _compartidos
                .FirstOrDefault(d =>
                    d.DatoId == pareja.DatoDiccionarioId
                    && (
                        (
                            d.IdentificacionA == identificacionA
                            && d.IdentificacionB == identificacionB
                        )
                        || (
                            d.IdentificacionA == identificacionB
                            && d.IdentificacionB == identificacionA
                        )
                    )
                )
                ?.DatoNombre
            ?? "Dato compartido";
        return $"{_lugares[pareja.OrdenA].Nombre} ↔ {_lugares[pareja.OrdenB].Nombre} · {nombreDato}";
    }

    private void Subir_Click(object sender, RoutedEventArgs e) => Mover(-1);

    private void Bajar_Click(object sender, RoutedEventArgs e) => Mover(1);

    private void Mover(int delta)
    {
        if (TiposProceso.SelectedItem is not TipoVm tipo)
            return;
        int actual = _lugares.FindIndex(l => l.IdentificacionId == tipo.Identificacion.Id),
            destino = actual + delta;
        if (actual < 0 || destino < 0 || destino >= _lugares.Count)
            return;
        var parejasPorTipo = _parejas
            .Select(p =>
                (
                    IdA: _lugares[p.OrdenA].IdentificacionId,
                    IdB: _lugares[p.OrdenB].IdentificacionId,
                    p.DatoDiccionarioId
                )
            )
            .ToArray();
        (_lugares[actual], _lugares[destino]) = (_lugares[destino], _lugares[actual]);
        for (int i = 0; i < _lugares.Count; i++)
            _lugares[i] = _lugares[i] with { Orden = i };
        var ordenPorIdentificacion = _lugares.ToDictionary(l => l.IdentificacionId, l => l.Orden);
        _parejas.Clear();
        _parejas.AddRange(
            parejasPorTipo.Select(p => new DefinicionParejaEsquema(
                ordenPorIdentificacion[p.IdA],
                ordenPorIdentificacion[p.IdB],
                p.DatoDiccionarioId
            ))
        );
        RefrescarListas();
        TiposProceso.SelectedItem = TiposProceso
            .Items.Cast<TipoVm>()
            .FirstOrDefault(t => t.Identificacion.Id == tipo.Identificacion.Id);
    }

    private void AgregarPareja_Click(object sender, RoutedEventArgs e)
    {
        if (
            LugarA.SelectedItem is not LugarVm a
            || LugarB.SelectedItem is not LugarVm b
            || DatoPareja.SelectedItem is not DatoVm dato
        )
        {
            Estado.Text = "Elija dos lugares y un dato compartido.";
            return;
        }
        if (a.Orden == b.Orden)
        {
            Estado.Text = "Elija dos lugares distintos.";
            return;
        }
        _parejas.Add(new(a.Orden, b.Orden, dato.Id));
        _nombresParejas.Add($"{a.Texto} ↔ {b.Texto}: {dato.Nombre}");
        RefrescarListas();
        Parejas.SelectedIndex = Parejas.Items.Count - 1;
        Estado.Text = "Pareja agregada a la lista.";
    }

    private void QuitarPareja_Click(object sender, RoutedEventArgs e)
    {
        int indice = Parejas.SelectedIndex;
        if (indice < 0 || indice >= _parejas.Count)
        {
            Estado.Text = "Seleccione una pareja de la lista para quitarla.";
            return;
        }
        _parejas.RemoveAt(indice);
        _nombresParejas.RemoveAt(indice);
        RefrescarListas();
        Estado.Text = "Pareja quitada de la lista.";
    }

    private void Siguiente_Click(object sender, RoutedEventArgs e)
    {
        Estado.Text = "";
        if (_paso == 1 && string.IsNullOrWhiteSpace(Proveedor.Text))
        {
            Estado.Text = "Escriba o seleccione un proveedor.";
            return;
        }
        if (_paso == 2 && _lugares.Count == 0)
        {
            Estado.Text = "Agregue al menos un documento del proceso.";
            return;
        }
        if (_paso == 3)
        {
            var marcados = Inicios
                .Items.Cast<InicioVm>()
                .Where(x => x.Inicia)
                .Select(x => x.Orden)
                .ToHashSet();
            if (marcados.Count == 0)
            {
                Estado.Text = "Marque al menos un documento que inicie la cadena.";
                return;
            }
            for (int i = 0; i < _lugares.Count; i++)
                _lugares[i] = _lugares[i] with { IniciaCadena = marcados.Contains(i) };
        }
        if (_paso < 5)
            _paso++;
        MostrarPaso();
    }

    private void Atras_Click(object sender, RoutedEventArgs e)
    {
        if (_paso > 1)
            _paso--;
        MostrarPaso();
    }

    private void Cambiar_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string paso && int.TryParse(paso, out int numero))
        {
            _paso = numero;
            MostrarPaso();
        }
    }

    private void PrepararResumen()
    {
        var error = AsistenteEsquemaCadena.Validar(
            Proveedor.Text,
            _lugares,
            _parejas,
            _compartidos
        );
        Guardar.IsEnabled = error is null;
        Estado.Text = error ?? "";
        Resumen.Text =
            $"Proveedor: {Proveedor.Text.Trim()}\nLa cadena puede iniciar con: {string.Join(", ", _lugares.Where(l => l.IniciaCadena).Select(l => l.Nombre))}\nLugares: {string.Join(" → ", _lugares.OrderBy(l => l.Orden).Select(l => l.Nombre))}\nParejas 1 a 1: {(_nombresParejas.Count == 0 ? "ninguna" : string.Join("; ", _nombresParejas))}";
        DibujoArbol.ItemsSource = _lugares
            .OrderBy(l => l.Orden)
            .Select(l => new TextBlock
            {
                Text =
                    $"{l.Nombre}{(_parejas.Any(p => p.OrdenA == l.Orden || p.OrdenB == l.Orden) ? "   ↔ en pareja" : "")}",
                Margin = new Thickness(12, 4, 0, 4),
            })
            .ToArray();
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string? error = AsistenteEsquemaCadena.Validar(
                Proveedor.Text,
                _lugares,
                _parejas,
                _compartidos
            );
            if (error is not null)
            {
                Estado.Text = error;
                return;
            }
            _repositorio.Guardar(Proveedor.Text, Proveedor.Text.Trim(), _lugares, _parejas);
            DialogResult = true;
        }
        catch (Exception error)
        {
            Estado.Text = $"No se pudo guardar el esquema: {error.Message}";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _conexion.Dispose();
        base.OnClosed(e);
    }

    private sealed record TipoVm(Identificacion Identificacion)
    {
        public string Texto => $"{Identificacion.Tipo} · {Identificacion.Emisor}";
    }

    private sealed class InicioVm(int orden, string nombre, bool inicia)
    {
        public int Orden { get; } = orden;
        public string Nombre { get; } = nombre;
        public bool Inicia { get; set; } = inicia;
    }

    private sealed record LugarVm(int Orden, string Texto);

    private sealed record DatoVm(string Id, string Nombre);
}
