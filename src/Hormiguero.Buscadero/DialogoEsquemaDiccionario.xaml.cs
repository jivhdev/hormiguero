using System.Text.Json;
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
    private readonly List<DefinicionReglaAlertaDiccionario> _reglasAviso = [];
    private int _paso = 1;

    public DialogoEsquemaDiccionario(string? proveedor = null)
    {
        InitializeComponent();
        _conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        _repositorio = new(_conexion);
        DatoAviso.ItemsSource = new[] { new DatoVm("", "Fecha del documento", "Fechas", false) }
            .Concat(LeerDatosAviso())
            .ToArray();
        Tipos.ItemsSource = RepositorioEsquemasDiccionario
            .ListarTiposDocumento(_conexion)
            .Select(t => new TipoVm(t.Id, t.Nombre, t.Grupo))
            .ToArray();
        Proveedor.Text = proveedor ?? "";
        if (proveedor is not null && _repositorio.ObtenerPorProveedor(proveedor) is { } esquema)
        {
            Nombre.Text = esquema.Nombre;
            using (var q = _conexion.CreateCommand())
            {
                q.CommandText =
                    "SELECT COALESCE((SELECT l.orden FROM esquemas_diccionario e JOIN lugares_esquema_diccionario l ON l.id=e.lugar_decide_modo_id WHERE e.id=$e),-1) FROM esquemas_diccionario WHERE id=$e;";
                q.Parameters.AddWithValue("$e", esquema.Id);
                LugarDecideModo.Tag = Convert.ToInt32(q.ExecuteScalar());
            }
            ModosTexto.Text = string.Join(
                ", ",
                new RepositorioModosEsquemaDiccionario(_conexion)
                    .Listar(esquema.Id)
                    .Select(m => m.Nombre)
            );
            using (var q = _conexion.CreateCommand())
            {
                q.CommandText =
                    "SELECT l.orden,r.modo_id,r.tipo,r.parametros_json FROM reglas_alerta_esquema_diccionario r JOIN lugares_esquema_diccionario l ON l.id=r.lugar_id WHERE r.esquema_id=$e ORDER BY r.id;";
                q.Parameters.AddWithValue("$e", esquema.Id);
                using var r = q.ExecuteReader();
                while (r.Read())
                    _reglasAviso.Add(
                        new(
                            r.GetInt32(0),
                            r.IsDBNull(1) ? null : r.GetInt64(1),
                            r.GetString(2),
                            r.GetString(3),
                            false
                        )
                    );
            }
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
        ActualizarReglasAviso();
        ActualizarPaso();
    }

    private void Anterior_Click(object sender, RoutedEventArgs e)
    {
        _paso = Math.Max(1, _paso - 1);
        ActualizarPaso();
    }

    private void Siguiente_Click(object sender, RoutedEventArgs e)
    {
        if (
            _paso == 1
            && (string.IsNullOrWhiteSpace(Proveedor.Text) || string.IsNullOrWhiteSpace(Nombre.Text))
        )
        {
            Estado.Text = "Escriba el proveedor y el nombre del esquema.";
            return;
        }
        if (_paso == 2 && _lugares.Count == 0)
        {
            Estado.Text = "Agregue al menos un tipo de documento.";
            return;
        }
        if (_paso == 3 && !_lugares.Any(l => l.Inicia))
        {
            Estado.Text = "Marque al menos un documento como inicio de cadena.";
            return;
        }
        _paso = Math.Min(7, _paso + 1);
        ActualizarPaso();
    }

    private void ActualizarPaso()
    {
        NumeroPaso.Text = $"Paso {_paso} de 7";
        PanelProveedor.Visibility = _paso == 1 ? Visibility.Visible : Visibility.Collapsed;
        AyudaTipos.Visibility = _paso == 2 ? Visibility.Visible : Visibility.Collapsed;
        PanelTipos.Visibility = _paso == 2 ? Visibility.Visible : Visibility.Collapsed;
        AgregarTipo.Visibility = _paso == 2 ? Visibility.Visible : Visibility.Collapsed;
        PanelLugares.Visibility = _paso == 3 ? Visibility.Visible : Visibility.Collapsed;
        PanelEnlaces.Visibility = _paso == 4 ? Visibility.Visible : Visibility.Collapsed;
        TituloEnlaces.Visibility = _paso == 4 ? Visibility.Visible : Visibility.Collapsed;
        PanelModos.Visibility = _paso == 5 ? Visibility.Visible : Visibility.Collapsed;
        PanelAvisos.Visibility = _paso == 6 ? Visibility.Visible : Visibility.Collapsed;
        ReglasAvisoTexto.Visibility = _paso == 6 ? Visibility.Visible : Visibility.Collapsed;
        PanelResumen.Visibility = _paso == 7 ? Visibility.Visible : Visibility.Collapsed;
        PanelResumen.Text =
            $"Proveedor: {Proveedor.Text}\nEsquema: {Nombre.Text}\nDocumentos: {string.Join(", ", _lugares.Select(l => l.Nombre))}\nEnlaces: {string.Join("; ", _enlaces.Select(e => e.Texto))}\nModos: {ModosTexto.Text}\nAvisos: {_reglasAviso.Count}";
        Anterior.IsEnabled = _paso > 1;
        Siguiente.Visibility = _paso < 7 ? Visibility.Visible : Visibility.Collapsed;
        Guardar.Visibility = _paso == 7 ? Visibility.Visible : Visibility.Collapsed;
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

    private void AgregarAviso_Click(object sender, RoutedEventArgs e)
    {
        if (
            TipoAviso.SelectedItem is not ComboBoxItem tipoItem
            || LugarAvisoA.SelectedItem is not LugarVm lugarA
        )
        {
            Estado.Text = "Seleccione el tipo de aviso y su documento de referencia.";
            return;
        }
        string tipo = (string)tipoItem.Tag;
        string texto = TextoAviso.Text.Trim();
        object parametros;
        if (
            tipo == "falta_dato"
            && DatoAviso.SelectedItem is DatoVm dato
            && !string.IsNullOrWhiteSpace(dato.Id)
        )
            parametros = new
            {
                dato = dato.Id,
                texto = string.IsNullOrWhiteSpace(texto) ? $"Falta {dato.Nombre}" : texto,
            };
        else if (
            tipo == "plazo"
            && LugarAvisoB.SelectedItem is LugarVm destino
            && int.TryParse(DiasAviso.Text, out int dias)
            && dias >= 0
        )
            parametros = new
            {
                desdeLugarId = (long)lugarA.Orden,
                desdeDato = (DatoAviso.SelectedItem as DatoVm)?.Id is { Length: > 0 } fechaDato
                    ? fechaDato
                    : null,
                hastaLugarId = (long)destino.Orden,
                dias,
                tipoDias = "corridos",
                calendarioId = (long?)null,
                condicionLugarId = (long?)null,
                texto = string.IsNullOrWhiteSpace(texto) ? $"Falta {destino.Nombre}" : texto,
                porLinea = PorLinea.IsChecked == true,
            };
        else if (tipo == "listo_para" && LugarAvisoB.SelectedItem is LugarVm listo)
            parametros = new
            {
                cuandoLugarId = (long)lugarA.Orden,
                condicionLugarId = (long?)null,
                hastaLugarId = (long)listo.Orden,
                lista = string.IsNullOrWhiteSpace(texto) ? $"Listo para {listo.Nombre}" : texto,
            };
        else
        {
            Estado.Text = "Complete los campos requeridos para el tipo de aviso.";
            return;
        }
        _reglasAviso.Add(new(lugarA.Orden, null, tipo, JsonSerializer.Serialize(parametros)));
        ActualizarReglasAviso();
    }

    private IEnumerable<DatoVm> LeerDatosAviso()
    {
        using var q = _conexion.CreateCommand();
        q.CommandText = "SELECT id,nombre,grupo FROM diccionario_datos ORDER BY grupo,orden;";
        using var r = q.ExecuteReader();
        var filas = new List<DatoVm>();
        while (r.Read())
            filas.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), false));
        return filas;
    }

    private void ActualizarReglasAviso() =>
        ReglasAvisoTexto.Text = string.Join(
            Environment.NewLine,
            _reglasAviso.Select(r => $"{r.Tipo}: {r.ParametrosJson}")
        );

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
            long esquemaId = _repositorio.Guardar(
                Proveedor.Text,
                Nombre.Text,
                _lugares
                    .Select(l => new DefinicionLugarDiccionario(l.Orden, l.Id, l.Inicia, l.Nombre))
                    .ToArray(),
                _enlaces
                    .Select(l => new DefinicionEnlace(l.OrdenA, l.OrdenB, l.Dato, l.Cardinalidad))
                    .ToArray()
            );
            new RepositorioConfiguracionEsquemaDiccionario(_conexion).GuardarModos(
                esquemaId,
                LugarDecideModo.SelectedItem is LugarVm decide ? decide.Orden : null,
                ModosTexto.Text.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
            );
            new RepositorioConfiguracionEsquemaDiccionario(_conexion).GuardarReglas(
                esquemaId,
                _reglasAviso
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
        LugarDecideModo.ItemsSource = _lugares;
        LugarAvisoA.ItemsSource = _lugares;
        LugarAvisoB.ItemsSource = _lugares;
        if (LugarDecideModo.Tag is int orden && orden >= 0)
            LugarDecideModo.SelectedItem = _lugares.FirstOrDefault(l => l.Orden == orden);
        LugarAvisoA.SelectedIndex = _lugares.Count > 0 ? 0 : -1;
        LugarAvisoB.SelectedIndex = _lugares.Count > 1 ? 1 : -1;
        LugarA.SelectedIndex = _lugares.Count > 0 ? 0 : -1;
        LugarB.SelectedIndex = _lugares.Count > 1 ? 1 : -1;
    }

    private void ActualizarEnlaces()
    {
        Enlaces.ItemsSource = _enlaces.Select(e => e.Texto).ToArray();
        ActualizarPorLinea();
    }

    private void LugarAviso_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ActualizarPorLinea();

    private void ActualizarPorLinea()
    {
        PorLinea.IsEnabled =
            LugarAvisoA.SelectedItem is LugarVm a
            && LugarAvisoB.SelectedItem is LugarVm b
            && _enlaces.Any(enlace =>
                enlace.Cardinalidad is "uno_a_uno" or "uno_a_varios"
                && (
                    (enlace.OrdenA == a.Orden && enlace.OrdenB == b.Orden)
                    || (enlace.OrdenA == b.Orden && enlace.OrdenB == a.Orden)
                )
            );
        if (!PorLinea.IsEnabled)
            PorLinea.IsChecked = false;
    }

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
