using System.Windows;
using System.Windows.Controls;
using Hormiguero.Nucleo.Datos;

namespace Buscadero.App;

public partial class DialogoEsquemaDiccionario : Window
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conexion;
    private readonly RepositorioEsquemasDiccionario _repositorio;
    private readonly List<LugarVm> _lugares = [];
    private readonly List<EnlaceVm> _enlaces = [];

    public DialogoEsquemaDiccionario(string? proveedor = null)
    {
        InitializeComponent();
        _conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        _repositorio = new(_conexion);
        Tipos.ItemsSource = RepositorioEsquemasDiccionario
            .ListarTiposDocumento(_conexion)
            .Select(t => new TipoVm(t.Id, t.Nombre, t.Grupo))
            .ToArray();
        Proveedor.Text = proveedor ?? "";
        if (proveedor is not null && _repositorio.ObtenerPorProveedor(proveedor) is { } esquema)
        {
            Nombre.Text = esquema.Nombre;
            _lugares.AddRange(
                esquema.Lugares.Select(l =>
                {
                    var tipo = ((IEnumerable<TipoVm>)Tipos.ItemsSource).Single(t =>
                        t.Id == l.DatoTipoId
                    );
                    return new LugarVm(l.Orden, l.DatoTipoId, tipo.Grupo, l.Nombre, l.IniciaCadena);
                })
            );
            _enlaces.AddRange(
                esquema.Enlaces.Select(e => new EnlaceVm(
                    _lugares
                        .Single(l =>
                            l.Id == esquema.Lugares.Single(p => p.Id == e.LugarA).DatoTipoId
                        )
                        .Orden,
                    _lugares
                        .Single(l =>
                            l.Id == esquema.Lugares.Single(p => p.Id == e.LugarB).DatoTipoId
                        )
                        .Orden,
                    e.DatoDiccionarioId,
                    e.Cardinalidad,
                    _lugares
                        .Single(l =>
                            l.Id == esquema.Lugares.Single(p => p.Id == e.LugarA).DatoTipoId
                        )
                        .Nombre,
                    _lugares
                        .Single(l =>
                            l.Id == esquema.Lugares.Single(p => p.Id == e.LugarB).DatoTipoId
                        )
                        .Nombre,
                    NombreDato(e.DatoDiccionarioId)
                ))
            );
        }
        ActualizarLugares();
        ActualizarEnlaces();
    }

    private void Agregar_Click(object sender, RoutedEventArgs e)
    {
        if (Tipos.SelectedItem is not TipoVm tipo || _lugares.Any(l => l.Id == tipo.Id))
            return;
        var lugar = new LugarVm(
            _lugares.Count,
            tipo.Id,
            tipo.Grupo,
            tipo.Nombre,
            _lugares.Count == 0
        );
        _lugares.Add(lugar);
        foreach (var otro in _lugares.Where(l => l != lugar))
        {
            AgregarSugerencia(otro, lugar);
            AgregarSugerencia(lugar, otro);
        }
        ActualizarLugares();
        ActualizarEnlaces();
    }

    private void AgregarSugerencia(LugarVm a, LugarVm b)
    {
        if (_enlaces.Any(e => e.OrdenA == a.Orden && e.OrdenB == b.Orden))
            return;
        var sugeridos = RepositorioEsquemasDiccionario
            .ListarDatos(_conexion, a.Id, b.Id)
            .Where(d => d.Sugerido)
            .Take(1)
            .ToArray();
        if (sugeridos.Length > 0)
        {
            var sugerido = sugeridos[0];
            _enlaces.Add(
                new(a.Orden, b.Orden, sugerido.Id, "uno_a_uno", a.Nombre, b.Nombre, sugerido.Nombre)
            );
        }
    }

    private void QuitarLugar_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LugarVm lugar)
            return;
        _lugares.Remove(lugar);
        _enlaces.RemoveAll(e => e.OrdenA == lugar.Orden || e.OrdenB == lugar.Orden);
        Reordenar();
        ActualizarLugares();
        ActualizarEnlaces();
    }

    private void Lugar_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized)
            return;
        if (LugarA.SelectedItem is not LugarVm a || LugarB.SelectedItem is not LugarVm b || a == b)
        {
            Dato.ItemsSource = null;
            return;
        }
        Dato.ItemsSource = RepositorioEsquemasDiccionario
            .ListarDatos(_conexion, a.Id, b.Id)
            .Select(d => new DatoVm(d.Id, d.Nombre, d.Grupo, d.Sugerido))
            .GroupBy(d => d.Grupo)
            .SelectMany(g => g)
            .OrderByDescending(d => d.Sugerido)
            .ThenBy(d => d.Grupo)
            .ThenBy(d => d.Nombre)
            .ToArray();
        Dato.SelectedIndex = 0;
    }

    private void AgregarEnlace_Click(object sender, RoutedEventArgs e)
    {
        if (
            LugarA.SelectedItem is not LugarVm a
            || LugarB.SelectedItem is not LugarVm b
            || a == b
            || Dato.SelectedItem is not DatoVm dato
            || Cardinalidad.SelectedItem is not ComboBoxItem cardinalidad
        )
        {
            Estado.Text = "Seleccione dos documentos, un dato y su cardinalidad.";
            return;
        }
        if (
            _enlaces.Any(enlace =>
                enlace.OrdenA == a.Orden && enlace.OrdenB == b.Orden && enlace.Dato == dato.Id
            )
        )
        {
            Estado.Text = "Ese enlace ya está en la lista.";
            return;
        }
        _enlaces.Add(
            new(
                a.Orden,
                b.Orden,
                dato.Id,
                (string)cardinalidad.Tag,
                a.Nombre,
                b.Nombre,
                dato.Nombre
            )
        );
        ActualizarEnlaces();
        Estado.Text = "Enlace agregado.";
    }

    private void QuitarEnlace_Click(object sender, RoutedEventArgs e)
    {
        if (Enlaces.SelectedIndex < 0 || Enlaces.SelectedIndex >= _enlaces.Count)
            return;
        _enlaces.RemoveAt(Enlaces.SelectedIndex);
        ActualizarEnlaces();
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Proveedor.Text) || string.IsNullOrWhiteSpace(Nombre.Text))
        {
            Estado.Text = "Escriba el proveedor y el nombre del esquema.";
            return;
        }
        if (_lugares.Count == 0 || _lugares.All(l => !l.Inicia))
        {
            Estado.Text = "Agregue documentos y marque al menos uno como inicio de cadena.";
            return;
        }
        try
        {
            _repositorio.Guardar(
                Proveedor.Text,
                Nombre.Text,
                _lugares
                    .Select(l => new DefinicionLugarDiccionario(l.Orden, l.Id, l.Inicia, l.Nombre))
                    .ToArray(),
                _enlaces
                    .Select(l => new DefinicionEnlace(l.OrdenA, l.OrdenB, l.Dato, l.Cardinalidad))
                    .ToArray()
            );
            DialogResult = true;
            Close();
        }
        catch (Exception error)
        {
            Estado.Text = $"No se pudo guardar el esquema: {error.Message}";
        }
    }

    private string NombreDato(string id) =>
        RepositorioEsquemasDiccionario.NombreDiccionario(_conexion, id);

    private void ActualizarLugares()
    {
        _lugares.Sort((a, b) => a.Orden.CompareTo(b.Orden));
        Lugares.ItemsSource = null;
        Lugares.ItemsSource = _lugares;
        LugarA.ItemsSource = _lugares;
        LugarB.ItemsSource = _lugares;
        LugarA.SelectedIndex = _lugares.Count > 0 ? 0 : -1;
        LugarB.SelectedIndex = _lugares.Count > 1 ? 1 : -1;
    }

    private void ActualizarEnlaces() =>
        Enlaces.ItemsSource = _enlaces.Select(e => e.Texto).ToArray();

    private void Reordenar()
    {
        for (int i = 0; i < _lugares.Count; i++)
            _lugares[i].Orden = i;
    }

    protected override void OnClosed(EventArgs e)
    {
        _conexion.Dispose();
        base.OnClosed(e);
    }

    private sealed record TipoVm(string Id, string Nombre, string Grupo)
    {
        public string Texto => $"{Grupo} · {Nombre}";
    }

    private sealed record DatoVm(string Id, string Nombre, string Grupo, bool Sugerido)
    {
        public string Texto => $"{(Sugerido ? "✓ " : "")}{Grupo} · {Nombre}";
    }

    private sealed class LugarVm(int orden, string id, string grupo, string nombre, bool inicia)
    {
        public int Orden { get; set; } = orden;
        public string Id { get; } = id;
        public string Grupo { get; } = grupo;
        public string Nombre { get; } = nombre;
        public bool Inicia { get; set; } = inicia;
    }

    private sealed record EnlaceVm(
        int OrdenA,
        int OrdenB,
        string Dato,
        string Cardinalidad,
        string NombreA,
        string NombreB,
        string NombreDato
    )
    {
        public string Texto =>
            Cardinalidad == "uno_a_varios"
                ? $"Una {NombreA} puede tener varias {NombreB} (se unen por {NombreDato})"
                : $"Cada {NombreA} se enlaza con {NombreB} (por {NombreDato})";
    }
}
