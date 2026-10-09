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
    private readonly List<DefinicionLugarEsquema> _lugares = [];
    private readonly List<DefinicionParejaEsquema> _parejas = [];
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
        Tipos.ItemsSource = _identificaciones.Select(i => new TipoVm(i)).ToArray();
        Compartidos.Text =
            _compartidos.Count == 0
                ? "Estos documentos ya comparten datos: todavía no hay pares configurados."
                : "Estos documentos ya comparten datos:\n"
                    + string.Join(
                        "\n",
                        _compartidos
                            .Select(d =>
                                $"{d.TipoA} de {d.EmisorA} ↔ {d.TipoB} de {d.EmisorB}: {d.DatoNombre}"
                            )
                            .Distinct()
                    );
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
                $"{esquema.Lugares.First(l => l.Id == pareja.LugarA).Nombre} ↔ {esquema.Lugares.First(l => l.Id == pareja.LugarB).Nombre}: {pareja.DatoDiccionarioId}"
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
            "Elija tipos configurados. Puede agregarlos y cambiar su orden.",
            "Marque al menos un documento que pueda iniciar el proceso.",
            "Una pareja representa documentos que se corresponden uno a uno.",
            "Revise el esquema y guarde los cambios.",
        ];
        PasoTitulo.Text = titulos[_paso - 1];
        Ayuda.Text = ayudas[_paso - 1];
        NumeroPaso.Text = $"Paso {_paso} de 5";
        Siguiente.Visibility = _paso == 5 ? Visibility.Collapsed : Visibility.Visible;
        Guardar.Visibility = _paso == 5 ? Visibility.Visible : Visibility.Collapsed;
        if (_paso == 5)
            PrepararResumen();
    }

    private void AgregarTipo_Click(object sender, RoutedEventArgs e)
    {
        if (
            Tipos.SelectedItem is not TipoVm tipo
            || _lugares.Any(l => l.IdentificacionId == tipo.Identificacion.Id)
        )
            return;
        _lugares.Add(
            new(_lugares.Count, tipo.Identificacion.Id, false, tipo.Identificacion.NombreEstandar)
        );
        RefrescarListas();
    }

    private void RefrescarListas()
    {
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

    private void Subir_Click(object sender, RoutedEventArgs e) => Mover(-1);

    private void Bajar_Click(object sender, RoutedEventArgs e) => Mover(1);

    private void Mover(int delta)
    {
        if (Tipos.SelectedItem is not TipoVm tipo)
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
        _nombresParejas.Clear();
        foreach (var pareja in _parejas)
            _nombresParejas.Add(
                $"{_lugares[pareja.OrdenA].Nombre} ↔ {_lugares[pareja.OrdenB].Nombre}: {pareja.DatoDiccionarioId}"
            );
        RefrescarListas();
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
        Estado.Text = "Pareja agregada.";
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
