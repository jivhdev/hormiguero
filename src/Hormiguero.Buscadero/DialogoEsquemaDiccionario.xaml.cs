using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Hormiguero.Nucleo.Datos;

namespace Buscadero.App;

public partial class DialogoEsquemaDiccionario : Window
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conexion;
    private readonly RepositorioEsquemasDiccionario _repositorio;
    private readonly List<LugarVm> _lugares = [];
    private readonly List<EnlaceVm> _enlaces = [];
    private readonly List<AvisoVm> _reglasAviso = [];
    private readonly List<string> _modos = [];
    private int _paso = 1;
    private string _nombreAutogenerado = "";
    private bool _actualizandoNombre;

    public DialogoEsquemaDiccionario(string? proveedor = null)
    {
        InitializeComponent();
        Proveedor.Loaded += (_, _) =>
        {
            if (Proveedor.Template.FindName("PART_EditableTextBox", Proveedor) is TextBox entrada)
                entrada.TextChanged += Proveedor_TextChanged;
        };
        MaxHeight = Math.Max(440, SystemParameters.WorkArea.Height - 40);
        Height = Math.Min(680, MaxHeight);
        _conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        _repositorio = new(_conexion);
        Proveedor.ItemsSource = LeerProveedoresConocidos();
        var tipos = RepositorioEsquemasDiccionario
            .ListarTiposDocumento(_conexion)
            .Where(t => !DiccionarioDatosEnlazantes.EsParte(t.Id))
            .OrderBy(t => OrdenGrupo(t.Grupo))
            .ThenBy(t => t.Nombre)
            .Select(t => new TipoVm(t.Id, NombreTipoVisible(t.Id, t.Nombre), t.Grupo))
            .ToArray();
        Tipos.ItemsSource = tipos;
        CollectionViewSource
            .GetDefaultView(Tipos.ItemsSource)
            .GroupDescriptions.Add(new PropertyGroupDescription(nameof(TipoVm.Grupo)));
        Proveedor.Text = proveedor ?? "";
        Proveedor_TextChanged(Proveedor, null!);
        if (
            !string.IsNullOrWhiteSpace(proveedor)
            && _repositorio.ObtenerPorProveedor(proveedor) is not { } existente
        )
            Reconocimientos.Items.Add("Nombre: " + proveedor.Trim());
        if (proveedor is not null && _repositorio.ObtenerPorProveedor(proveedor) is { } esquema)
        {
            foreach (var reconocimiento in _repositorio.ListarReconocimientos(esquema.Id))
                Reconocimientos.Items.Add(
                    (reconocimiento.Tipo == "rut" ? "RUT: " : "Nombre: ")
                        + reconocimiento.ValorNormalizado
                );
            if (Reconocimientos.Items.Count == 0)
                Reconocimientos.Items.Add("Nombre: " + esquema.Proveedor);
            _actualizandoNombre = true;
            Nombre.Text = esquema.Nombre;
            _actualizandoNombre = false;
            using (var q = _conexion.CreateCommand())
            {
                q.CommandText =
                    "SELECT COALESCE((SELECT l.orden FROM esquemas_diccionario e JOIN lugares_esquema_diccionario l ON l.id=e.lugar_decide_modo_id WHERE e.id=$e),-1) FROM esquemas_diccionario WHERE id=$e;";
                q.Parameters.AddWithValue("$e", esquema.Id);
                LugarDecideModo.Tag = Convert.ToInt32(q.ExecuteScalar());
            }
            _modos.AddRange(
                new RepositorioModosEsquemaDiccionario(_conexion)
                    .Listar(esquema.Id)
                    .Select(m => m.Nombre)
            );
            using (var q = _conexion.CreateCommand())
            {
                q.CommandText =
                    "SELECT l.orden,r.modo_id,r.tipo,r.parametros_json,(SELECT nombre FROM modos_esquema_diccionario WHERE id=r.modo_id) FROM reglas_alerta_esquema_diccionario r JOIN lugares_esquema_diccionario l ON l.id=r.lugar_id WHERE r.esquema_id=$e ORDER BY r.id;";
                q.Parameters.AddWithValue("$e", esquema.Id);
                using var r = q.ExecuteReader();
                var filas =
                    new List<(
                        int Orden,
                        long? ModoId,
                        string Tipo,
                        string Json,
                        string? NombreModo
                    )>();
                while (r.Read())
                    filas.Add(
                        (
                            r.GetInt32(0),
                            r.IsDBNull(1) ? null : r.GetInt64(1),
                            r.GetString(2),
                            r.GetString(3),
                            r.IsDBNull(4) ? null : r.GetString(4)
                        )
                    );
                r.Close();
                _reglasAviso.AddRange(
                    filas.Select(f => CargarAviso(f.Orden, f.ModoId, f.Tipo, f.Json, f.NombreModo))
                );
            }
            _lugares.AddRange(
                esquema.Lugares.Select(l =>
                {
                    var tipo = ((IEnumerable<TipoVm>)Tipos.ItemsSource).Single(t =>
                        t.Id == l.DatoTipoId
                    );
                    return new LugarVm(
                        l.Orden,
                        l.DatoTipoId,
                        tipo.Grupo,
                        tipo.NombreDocumento,
                        l.IniciaCadena
                    );
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
        for (int i = 0; i < _reglasAviso.Count; i++)
            _reglasAviso[i] = _reglasAviso[i] with
            {
                Frase =
                    FraseAviso(_reglasAviso[i].Tipo, _reglasAviso[i].ParametrosJson)
                    + (_reglasAviso[i].ModoNombre is string m ? $" (modo {m})" : ""),
            };
        ModosLista.ItemsSource = _modos;
        ActualizarModos();
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
        Estado.Text = "";
        ActualizarPaso();
    }

    private void ActualizarPaso()
    {
        NumeroPaso.Text = $"Paso {_paso} de 7";
        (TituloPaso.Text, AyudaPaso.Text) = _paso switch
        {
            1 => ("Proveedor", "Indique el proveedor y el nombre editable del proceso."),
            2 => (
                "Documentos del proceso",
                "Agregue los documentos y ordénelos como ocurren en el proceso."
            ),
            3 => ("Inicios de cadena", "Marque qué documentos pueden iniciar una cadena."),
            4 => ("Enlaces", "Relacione documentos por los datos que comparten."),
            5 => ("Modos", "Agregue modos opcionales e indique qué documento determina cada modo."),
            6 => ("Avisos", "Configure avisos para los documentos y plazos del proceso."),
            _ => ("Resumen", "Revise el esquema antes de guardarlo."),
        };
        PanelProveedor.Visibility = _paso == 1 ? Visibility.Visible : Visibility.Collapsed;
        PanelTipos.Visibility = _paso == 2 ? Visibility.Visible : Visibility.Collapsed;
        PanelLugares.Visibility = _paso == 3 ? Visibility.Visible : Visibility.Collapsed;
        PanelEnlaces.Visibility = _paso == 4 ? Visibility.Visible : Visibility.Collapsed;
        PanelModos.Visibility = _paso == 5 ? Visibility.Visible : Visibility.Collapsed;
        PanelAvisos.Visibility = _paso == 6 ? Visibility.Visible : Visibility.Collapsed;
        PanelResumen.Visibility = _paso == 7 ? Visibility.Visible : Visibility.Collapsed;
        PanelResumen.Text =
            $"Proveedor: {Proveedor.Text}\nEsquema: {Nombre.Text}\nDocumentos: {string.Join(", ", _lugares.Select(l => l.Nombre))}\nEnlaces:\n{string.Join("\n", _enlaces.Select(e => "• " + e.Texto))}\nModos: {string.Join(", ", _modos)}{(LugarDecideModo.SelectedItem is LugarVm decide ? $" (decide: {decide.Nombre})" : "")}\nAvisos:\n{string.Join("\n", _reglasAviso.Select(a => "• " + a.Frase))}";
        Anterior.IsEnabled = _paso > 1;
        Siguiente.Visibility = _paso < 7 ? Visibility.Visible : Visibility.Collapsed;
        Guardar.Visibility = _paso == 7 ? Visibility.Visible : Visibility.Collapsed;
        if (_paso == 2)
            ActualizarTiposSeleccionados();
    }

    private void Agregar_Click(object sender, RoutedEventArgs e)
    {
        if (Tipos.SelectedItem is not TipoVm tipo || _lugares.Any(l => l.Id == tipo.Id))
            return;
        AgregarTipoProceso(tipo);
    }

    private void Tipo_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Tipos.SelectedItem is TipoVm tipo)
            AgregarTipoProceso(tipo);
    }

    private void AgregarTipoProceso(TipoVm tipo)
    {
        if (_lugares.Any(l => l.Id == tipo.Id))
            return;
        var lugar = new LugarVm(
            _lugares.Count,
            tipo.Id,
            tipo.Grupo,
            tipo.NombreDocumento,
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
        ActualizarTiposSeleccionados(lugar);
        Estado.Text = $"{lugar.Nombre} agregado al proceso.";
    }

    private void QuitarTipo_Click(object sender, RoutedEventArgs e) => QuitarTipoSeleccionado();

    private void LugarProceso_DoubleClick(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e
    ) => QuitarTipoSeleccionado();

    private void QuitarTipoSeleccionado()
    {
        if (LugaresProceso.SelectedItem is not LugarVm lugar)
            return;
        QuitarLugar(lugar);
        Estado.Text = $"{lugar.Nombre} quitado del proceso.";
    }

    private void SubirTipo_Click(object sender, RoutedEventArgs e) => MoverTipo(-1);

    private void BajarTipo_Click(object sender, RoutedEventArgs e) => MoverTipo(1);

    private void MoverTipo(int desplazamiento)
    {
        int indice = LugaresProceso.SelectedIndex;
        int destino = indice + desplazamiento;
        if (indice < 0 || destino < 0 || destino >= _lugares.Count)
            return;
        (_lugares[indice], _lugares[destino]) = (_lugares[destino], _lugares[indice]);
        Reordenar();
        ActualizarLugares();
        ActualizarEnlaces();
        ActualizarTiposSeleccionados(_lugares[destino]);
        Estado.Text = "Orden de documentos actualizado.";
    }

    private void ActualizarTiposSeleccionados(LugarVm? seleccionado = null)
    {
        LugaresProceso.ItemsSource = null;
        LugaresProceso.ItemsSource = _lugares.OrderBy(l => l.Orden).ToArray();
        if (seleccionado is not null)
            LugaresProceso.SelectedItem = seleccionado;
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
        QuitarLugar(lugar);
    }

    private void QuitarLugar(LugarVm lugar)
    {
        _lugares.Remove(lugar);
        _enlaces.RemoveAll(e => e.OrdenA == lugar.Orden || e.OrdenB == lugar.Orden);
        Reordenar();
        ActualizarLugares();
        ActualizarEnlaces();
        ActualizarTiposSeleccionados();
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
            .OrderByDescending(d => d.Sugerido)
            .ThenBy(d => OrdenGrupo(d.Grupo))
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

    private void AgregarAviso_Click(object sender, RoutedEventArgs e) => EditarAviso(null);

    private void EditarAviso_Click(object sender, RoutedEventArgs e) => EditarAvisoSeleccionado();

    private void EditarAviso_DoubleClick(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e
    ) => EditarAvisoSeleccionado();

    private void EditarAvisoSeleccionado()
    {
        if (ListaAvisos.SelectedItem is AvisoVm aviso)
            EditarAviso(aviso);
    }

    private void QuitarAviso_Click(object sender, RoutedEventArgs e)
    {
        if (ListaAvisos.SelectedItem is AvisoVm aviso)
        {
            _reglasAviso.Remove(aviso);
            ActualizarReglasAviso();
        }
    }

    private void EditarAviso(AvisoVm? existente)
    {
        var actual = existente is null
            ? null
            : new AvisoDefinicion(
                existente.OrdenLugar,
                existente.Tipo,
                existente.ParametrosJson,
                existente.ModoNombre
            );
        var editor = new DialogoEditorAviso(
            _lugares.Select(l => new LugarAvisoOpcion(l.Orden, l.Nombre)).ToArray(),
            LeerDatosAviso()
                .Where(d => d.Id.Length > 0)
                .Select(d => new DatoAvisoOpcion(d.Id, d.Nombre))
                .ToArray(),
            _enlaces.Select(e => (e.OrdenA, e.OrdenB, e.Cardinalidad)).ToArray(),
            _modos,
            actual
        )
        {
            Owner = this,
        };
        if (editor.ShowDialog() != true || editor.Resultado is not { } resultado)
            return;
        if (existente is not null)
            _reglasAviso.Remove(existente);
        _reglasAviso.Add(
            new(
                resultado.OrdenLugar,
                null,
                resultado.Tipo,
                resultado.ParametrosJson,
                true,
                resultado.ModoNombre,
                FraseAviso(resultado.Tipo, resultado.ParametrosJson)
                    + (resultado.ModoNombre is string modo ? $" (modo {modo})" : ""),
                0
            )
        );
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

    private string[] LeerProveedoresConocidos()
    {
        using var consulta = _conexion.CreateCommand();
        consulta.CommandText =
            "SELECT emisor FROM identificaciones WHERE TRIM(emisor)<>'' UNION SELECT proveedor FROM esquemas_diccionario WHERE TRIM(proveedor)<>'' ORDER BY 1 COLLATE NOCASE;";
        using var lector = consulta.ExecuteReader();
        var proveedores = new List<string>();
        while (lector.Read())
            proveedores.Add(lector.GetString(0));
        return proveedores.ToArray();
    }

    private void ActualizarReglasAviso()
    {
        ListaAvisos.ItemsSource = _reglasAviso.ToArray();
        SinAvisos.Visibility = _reglasAviso.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AgregarModo_Click(object sender, RoutedEventArgs e)
    {
        string modo = ModoNuevo.Text.Trim();
        if (modo.Length == 0 || _modos.Contains(modo, StringComparer.OrdinalIgnoreCase))
            return;
        _modos.Add(modo);
        ModosLista.ItemsSource = null;
        ModosLista.ItemsSource = _modos;
        ModoNuevo.Clear();
        ActualizarModos();
    }

    private void QuitarModo_Click(object sender, RoutedEventArgs e)
    {
        if (ModosLista.SelectedItem is not string modo)
            return;
        _modos.Remove(modo);
        ModosLista.ItemsSource = null;
        ModosLista.ItemsSource = _modos;
        ActualizarModos();
    }

    private void ActualizarModos()
    {
        LugarDecideModo.IsEnabled = _modos.Count > 0;
        if (_modos.Count == 0)
            LugarDecideModo.SelectedIndex = -1;
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
            _repositorio.GuardarReconocimientos(
                esquemaId,
                Reconocimientos
                    .Items.Cast<string>()
                    .Select(texto =>
                    {
                        int separador = texto.IndexOf(':');
                        return new ReconocimientoProveedor(
                            separador >= 0 && texto[..separador] == "RUT" ? "rut" : "nombre",
                            separador >= 0 ? texto[(separador + 1)..].Trim() : texto
                        );
                    })
                    .ToArray()
            );
            new RepositorioConfiguracionEsquemaDiccionario(_conexion).GuardarModos(
                esquemaId,
                LugarDecideModo.SelectedItem is LugarVm decide ? decide.Orden : null,
                _modos
            );
            var modosId = LeerIdsModo(esquemaId);
            new RepositorioConfiguracionEsquemaDiccionario(_conexion).GuardarReglas(
                esquemaId,
                _reglasAviso
                    .Select(a =>
                        a.ConModo(
                            a.ModoNombre is string nombre
                            && modosId.TryGetValue(nombre, out long id)
                                ? id
                                : null
                        )
                    )
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

    private void AgregarReconocimiento_Click(object sender, RoutedEventArgs e)
    {
        string valor = ReconocimientoNuevo.Text.Trim();
        if (valor.Length == 0)
            return;
        bool rut = valor.StartsWith("RUT:", StringComparison.OrdinalIgnoreCase);
        valor =
            rut ? valor[4..].Trim()
            : valor.StartsWith("Nombre:", StringComparison.OrdinalIgnoreCase) ? valor[7..].Trim()
            : valor;
        string fila = (rut ? "RUT: " : "Nombre: ") + valor;
        if (!Reconocimientos.Items.Contains(fila))
            Reconocimientos.Items.Add(fila);
        ReconocimientoNuevo.Clear();
    }

    private void QuitarReconocimiento_Click(object sender, RoutedEventArgs e)
    {
        if (Reconocimientos.SelectedItem is string fila)
            Reconocimientos.Items.Remove(fila);
    }

    private string NombreDato(string id) =>
        id switch
        {
            "oc_propia" => "N° OC propia",
            "guia_proveedor" => "N° Guía del proveedor",
            "factura_proveedor" => "N° Factura del proveedor",
            _ => RepositorioEsquemasDiccionario.NombreDiccionario(_conexion, id),
        };

    private static string NombreTipoVisible(string id, string nombre) =>
        id switch
        {
            "nota_venta_propia" => "NVV propia",
            "oc_propia" => "OC propia",
            "guia_proveedor" => "Guía del proveedor",
            "factura_proveedor" => "Factura del proveedor",
            "factura_propia" => "Factura propia",
            _ => nombre,
        };

    private void ActualizarLugares()
    {
        _lugares.Sort((a, b) => a.Orden.CompareTo(b.Orden));
        Lugares.ItemsSource = null;
        Lugares.ItemsSource = _lugares;
        LugarA.ItemsSource = _lugares;
        LugarB.ItemsSource = _lugares;
        LugarDecideModo.ItemsSource = _lugares;
        if (LugarDecideModo.Tag is int orden && orden >= 0)
            LugarDecideModo.SelectedItem = _lugares.FirstOrDefault(l => l.Orden == orden);
        LugarA.SelectedIndex = _lugares.Count > 0 ? 0 : -1;
        LugarB.SelectedIndex = _lugares.Count > 1 ? 1 : -1;
    }

    private void ActualizarEnlaces()
    {
        Enlaces.ItemsSource = _enlaces.Select(e => e.Texto).ToArray();
    }

    private Dictionary<string, long> LeerIdsModo(long esquemaId)
    {
        using var q = _conexion.CreateCommand();
        q.CommandText = "SELECT nombre,id FROM modos_esquema_diccionario WHERE esquema_id=$e;";
        q.Parameters.AddWithValue("$e", esquemaId);
        using var r = q.ExecuteReader();
        var salida = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        while (r.Read())
            salida[r.GetString(0)] = r.GetInt64(1);
        return salida;
    }

    private AvisoVm CargarAviso(
        int orden,
        long? modoId,
        string tipo,
        string json,
        string? modoNombre
    )
    {
        using var doc = JsonDocument.Parse(json);
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        foreach (
            string propiedad in new[]
            {
                "desdeLugarId",
                "hastaLugarId",
                "cuandoLugarId",
                "condicionLugarId",
            }
        )
            if (
                doc.RootElement.TryGetProperty(propiedad, out var id)
                && id.ValueKind == JsonValueKind.Number
                && id.TryGetInt64(out long valor)
            )
            {
                using var q = _conexion.CreateCommand();
                q.CommandText = "SELECT orden FROM lugares_esquema_diccionario WHERE id=$i;";
                q.Parameters.AddWithValue("$i", valor);
                if (q.ExecuteScalar() is long o)
                    node[propiedad] = o;
            }
        string normalizado = node.ToJsonString();
        return new(
            orden,
            modoId,
            tipo,
            normalizado,
            true,
            modoNombre,
            FraseAviso(tipo, normalizado) + (modoNombre is null ? "" : $" (modo {modoNombre})"),
            modoId ?? 0
        );
    }

    private string FraseAviso(string tipo, string json)
    {
        var nombres = _lugares.ToDictionary(l => (long)l.Orden, l => l.Nombre);
        var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        if (tipo == "falta_dato")
        {
            var p = JsonSerializer.Deserialize<ParametrosFaltaDato>(json, opciones)!;
            return $"Mientras falte ingresar {p.Dato}, avisar: «{p.Texto}»";
        }
        if (tipo == "plazo")
        {
            var p = JsonSerializer.Deserialize<ParametrosPlazo>(json, opciones)!;
            string desde = p.DesdeDato is null
                ? nombres.GetValueOrDefault(p.DesdeLugarId ?? 0, "documento")
                    + " (su fecha de emisión)"
                : $"el dato ingresado «{p.DesdeDato}»";
            string tipoDias = p.TipoDias == "habiles" ? "hábiles" : "corridos";
            return $"Si pasan {p.Dias} días {tipoDias} desde {desde} sin {nombres.GetValueOrDefault(p.HastaLugarId, "documento")}, aviso «{p.Texto}»{(p.PorLinea ? " (por línea)" : "")}";
        }
        if (tipo == "listo_para")
        {
            var p = JsonSerializer.Deserialize<ParametrosListoPara>(json, opciones)!;
            string lista = p.Lista.StartsWith("Listos para ", StringComparison.OrdinalIgnoreCase)
                ? p.Lista["Listos para ".Length..]
                : p.Lista;
            return $"Listo para {lista} cuando estén {nombres.GetValueOrDefault(p.CuandoLugarId, "documento")} + {nombres.GetValueOrDefault(p.HastaLugarId, "documento")}";
        }
        return "Aviso";
    }

    private void Reordenar()
    {
        for (int i = 0; i < _lugares.Count; i++)
            _lugares[i].Orden = i;
    }

    private void Proveedor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_actualizandoNombre)
            return;
        string proveedor = Proveedor.Text.Trim();
        string sugerido = string.IsNullOrWhiteSpace(proveedor) ? "" : $"Proceso con {proveedor}";
        if (string.IsNullOrWhiteSpace(Nombre.Text) || Nombre.Text == _nombreAutogenerado)
        {
            _actualizandoNombre = true;
            Nombre.Text = sugerido;
            _actualizandoNombre = false;
        }
        _nombreAutogenerado = sugerido;
    }

    private static int OrdenGrupo(string grupo) =>
        grupo switch
        {
            "Ventas propias" => 0,
            "Compras propias" => 1,
            "Del cliente" => 2,
            "Del proveedor" => 3,
            _ => 4,
        };

    protected override void OnClosed(EventArgs e)
    {
        _conexion.Dispose();
        base.OnClosed(e);
    }

    private sealed record TipoVm(string Id, string Nombre, string Grupo)
    {
        public string NombreDocumento =>
            Nombre
                .Replace("N° ", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Nº ", "", StringComparison.OrdinalIgnoreCase)
                .Replace("No. ", "", StringComparison.OrdinalIgnoreCase);
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
                ? $"Una {NombreA} puede tener varias {Buscadero.Core.Alertas.EditorReglasEsquema.PluralizarTipo(NombreB)} (se unen por {NombreDato})"
                : $"Cada {NombreA} se enlaza con {NombreB} (por {NombreDato})";
    }

    private sealed record AvisoVm(
        int OrdenLugar,
        long? ModoId,
        string Tipo,
        string ParametrosJson,
        bool IdsSonOrdenes,
        string? ModoNombre,
        string Frase,
        long ModoIdOriginal
    )
    {
        public DefinicionReglaAlertaDiccionario ConModo(long? modoId) =>
            new(OrdenLugar, modoId, Tipo, ParametrosJson, IdsSonOrdenes);
    }
}
