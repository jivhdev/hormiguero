using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Buscadero.Core.Lineas;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Buscadero.App;

public partial class DialogoCadenasSimples : Window
{
    private readonly ServicioCadenasSimples _servicio;
    private readonly List<DocumentoDisponibleCadena> _nuevos = [];
    private CoincidenciasPorDato? _sugerencia;

    public DialogoCadenasSimples()
    {
        InitializeComponent();
        var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        _servicio = new ServicioCadenasSimples(conexion);
        // La conexión se conserva mientras vive la ventana para que las operaciones compartan la transacción local.
        _conexion = conexion;
        RecargarCadenas();
    }

    private SqliteConnection? _conexion;

    private void RecargarCadenas()
    {
        ListaCadenas.ItemsSource = _servicio
            .BuscarCadenas(BuscarCadena.Text)
            .Select(c => new FilaCadena(c.Id, c.Nombre))
            .ToArray();
        if (ListaCadenas.Items.Count == 0)
        {
            ListaDocumentos.ItemsSource = _nuevos.Select(FilaDocumento.Desde).ToArray();
            TextoTituloDocumentos.Text = "Documentos para la nueva cadena";
        }
    }

    private void BuscarCadena_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsInitialized)
            RecargarCadenas();
    }

    private void CadenaSeleccionada(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || ListaCadenas.SelectedItem is not FilaCadena fila)
            return;
        RefrescarDocumentos(fila);
    }

    private void RefrescarDocumentos(FilaCadena fila)
    {
        var documentos = _servicio
            .Documentos(fila.Id)
            .Select(v => DocumentoDeVagon(v))
            .Where(d => d is not null)
            .Select(d => FilaDocumento.Desde(d!))
            .ToArray();
        ListaDocumentos.ItemsSource = documentos;
        TextoTituloDocumentos.Text = $"Documentos en {fila.Nombre}";
        NombreCadena.Text = fila.Nombre;
    }

    private DocumentoDisponibleCadena? DocumentoDeVagon(VagonCadena vagon)
    {
        if (vagon.VersionId is not long versionId)
            return null;
        var documento = new RepositorioCadenas(_conexion!).ObtenerVersionDocumento(versionId);
        return documento is null
            ? null
            : new(
                documento.VersionId,
                documento.Ruta,
                documento.Nombre,
                documento.Tipo,
                documento.Emisor,
                documento.Fecha,
                documento.Numero
            );
    }

    private async void Buscar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Consulta.Text))
                throw new ArgumentException("Escriba un número o dato para buscar documentos.");
            var documentos = _servicio
                .BuscarDocumentos(Consulta.Text)
                .Select(FilaDocumento.Desde)
                .ToArray();
            ListaDisponibles.ItemsSource = documentos;
            _sugerencia =
                documentos.Length == 0
                    ? null
                    : _servicio.Sugerir(documentos[0].Documento.VersionId).FirstOrDefault();
            MostrarSugerencia();
            Estado.Text =
                documentos.Length == 0
                    ? "No se encontraron documentos vigentes."
                    : $"{documentos.Length} documentos encontrados.";
            await Task.CompletedTask;
        }
        catch (Exception error)
        {
            Estado.Text = error.Message;
        }
    }

    private void Consulta_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Buscar_Click(sender, e);
    }

    private void MostrarSugerencia()
    {
        if (_sugerencia is null || _sugerencia.Documentos.Count == 0)
        {
            TextoSugerencia.Text = string.Empty;
            return;
        }
        TextoSugerencia.Text =
            $"Estos {_sugerencia.Documentos.Count} documentos comparten {_sugerencia.Dato.Nombre} {_sugerencia.Valor}. ¿Agregar a la misma cadena?";
    }

    private void AgregarSugeridos_Click(object sender, RoutedEventArgs e)
    {
        if (_sugerencia is null)
            return;
        foreach (var sugerido in _sugerencia.Documentos)
        {
            var hallado = _servicio
                .BuscarDocumentos(Path.GetFileName(sugerido.Ruta))
                .FirstOrDefault(d => d.VersionId == sugerido.VersionId);
            if (hallado is not null && _nuevos.All(d => d.VersionId != hallado.VersionId))
                _nuevos.Add(hallado);
        }
        MostrarNuevos();
    }

    private void Revisar_Click(object sender, RoutedEventArgs e)
    {
        if (_sugerencia is null)
            return;
        var ids = _sugerencia.Documentos.Select(d => d.VersionId).ToHashSet();
        ListaDisponibles.SelectedItems.Clear();
        foreach (FilaDocumento fila in ListaDisponibles.Items)
            if (ids.Contains(fila.Documento.VersionId))
                ListaDisponibles.SelectedItems.Add(fila);
    }

    private void NoAgregar_Click(object sender, RoutedEventArgs e)
    {
        _sugerencia = null;
        TextoSugerencia.Text = "";
    }

    private void CrearCadena_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var elegidos = _nuevos
                .Concat(
                    ListaDisponibles.SelectedItems.Cast<FilaDocumento>().Select(f => f.Documento)
                )
                .DistinctBy(d => d.VersionId)
                .ToArray();
            if (elegidos.Length == 0)
                throw new InvalidOperationException("Agregue al menos un documento.");
            string nombre = string.IsNullOrWhiteSpace(NombreCadena.Text)
                ? Path.GetFileNameWithoutExtension(elegidos[0].Nombre)
                : NombreCadena.Text.Trim();
            long id = _servicio.CrearCadena(nombre);
            foreach (var documento in elegidos)
                _servicio.AgregarDocumento(
                    id,
                    documento.VersionId,
                    $"{documento.Tipo} · {documento.Emisor} · {documento.Numero}".Trim(' ', '·')
                );
            _nuevos.Clear();
            RecargarCadenas();
            ListaCadenas.SelectedItem = ListaCadenas
                .Items.Cast<FilaCadena>()
                .FirstOrDefault(c => c.Id == id);
            Estado.Text = "Cadena creada.";
        }
        catch (Exception error)
        {
            Estado.Text = error.Message;
        }
    }

    private void AgregarSeleccionados_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ListaCadenas.SelectedItem is FilaCadena cadena)
            {
                foreach (FilaDocumento documento in ListaDisponibles.SelectedItems)
                    _servicio.AgregarDocumento(
                        cadena.Id,
                        documento.Documento.VersionId,
                        $"{documento.Documento.Tipo} · {documento.Documento.Emisor} · {documento.Documento.Numero}".Trim(
                            ' ',
                            '·'
                        )
                    );
                RefrescarDocumentos(cadena);
                Estado.Text = "Documentos agregados.";
            }
            else
            {
                foreach (FilaDocumento documento in ListaDisponibles.SelectedItems)
                    if (_nuevos.All(d => d.VersionId != documento.Documento.VersionId))
                        _nuevos.Add(documento.Documento);
                MostrarNuevos();
                Estado.Text = "Documentos listos para guardar en una nueva cadena.";
            }
        }
        catch (Exception error)
        {
            Estado.Text = error.Message;
        }
    }

    private void Renombrar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ListaCadenas.SelectedItem is not FilaCadena cadena)
                throw new InvalidOperationException("Seleccione una cadena.");
            _servicio.Renombrar(cadena.Id, NombreCadena.Text);
            RecargarCadenas();
            Estado.Text = "Cadena renombrada.";
        }
        catch (Exception error)
        {
            Estado.Text = error.Message;
        }
    }

    private void Quitar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ListaDocumentos.SelectedItem is not FilaDocumento documento)
                throw new InvalidOperationException("Seleccione un documento.");
            if (ListaCadenas.SelectedItem is not FilaCadena)
            {
                _nuevos.RemoveAll(d => d.VersionId == documento.Documento.VersionId);
                MostrarNuevos();
                return;
            }
            var vagon = _servicio
                .Documentos(((FilaCadena)ListaCadenas.SelectedItem).Id)
                .First(v => v.VersionId == documento.Documento.VersionId);
            _servicio.QuitarDocumento(vagon.Id);
            RefrescarDocumentos((FilaCadena)ListaCadenas.SelectedItem);
            Estado.Text = "Documento quitado de la cadena; el cambio quedó en el historial.";
        }
        catch (Exception error)
        {
            Estado.Text = error.Message;
        }
    }

    private void Mover(int desplazamiento)
    {
        try
        {
            if (ListaDocumentos.SelectedItem is not FilaDocumento documento)
                throw new InvalidOperationException("Seleccione un documento.");
            if (ListaCadenas.SelectedItem is not FilaCadena cadena)
            {
                int indice = _nuevos.FindIndex(d => d.VersionId == documento.Documento.VersionId);
                int destino = indice + desplazamiento;
                if (indice < 0 || destino < 0 || destino >= _nuevos.Count)
                    return;
                (_nuevos[indice], _nuevos[destino]) = (_nuevos[destino], _nuevos[indice]);
                MostrarNuevos();
                ListaDocumentos.SelectedIndex = destino;
                return;
            }
            var vagon = _servicio
                .Documentos(cadena.Id)
                .First(v => v.VersionId == documento.Documento.VersionId);
            _servicio.MoverDocumento(vagon.Id, desplazamiento);
            RefrescarDocumentos(cadena);
        }
        catch (Exception error)
        {
            Estado.Text = error.Message;
        }
    }

    private void Subir_Click(object sender, RoutedEventArgs e) => Mover(-1);

    private void Bajar_Click(object sender, RoutedEventArgs e) => Mover(1);

    private void Abrir_Click(object sender, RoutedEventArgs e)
    {
        if (
            ListaDocumentos.SelectedItem is not FilaDocumento documento
            || !File.Exists(documento.Documento.Ruta)
        )
        {
            Estado.Text = "No se encontró el archivo PDF.";
            return;
        }
        try
        {
            Process.Start(
                new ProcessStartInfo(documento.Documento.Ruta) { UseShellExecute = true }
            );
        }
        catch (Exception error)
        {
            Estado.Text = $"No se pudo abrir el archivo: {error.Message}";
        }
    }

    private void ReglaAlerta_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new DialogoReglaAlerta { Owner = this };
        if (dialogo.ShowDialog() == true)
            Estado.Text = "La regla quedó configurada para las cadenas simples.";
    }

    private void Recordarme_Click(object sender, RoutedEventArgs e)
    {
        if (ListaCadenas.SelectedItem is not FilaCadena cadena)
        {
            Estado.Text = "Seleccione una cadena para crear un recordatorio.";
            return;
        }
        if (new DialogoRecordatorio(cadena.Id) { Owner = this }.ShowDialog() == true)
            Estado.Text = "Recordatorio creado.";
    }

    private void MostrarNuevos() =>
        ListaDocumentos.ItemsSource = _nuevos.Select(FilaDocumento.Desde).ToArray();

    protected override void OnClosed(EventArgs e)
    {
        _conexion?.Dispose();
        base.OnClosed(e);
    }

    private sealed record FilaCadena(long Id, string Nombre)
    {
        public string Texto => Nombre;
    }

    private sealed record FilaDocumento(DocumentoDisponibleCadena Documento)
    {
        public string Texto =>
            $"{Documento.Tipo} · {Documento.Emisor} · {Documento.Numero} · {Documento.Fecha:yyyy-MM-dd} · {Documento.Nombre}";

        public static FilaDocumento Desde(DocumentoDisponibleCadena documento) => new(documento);
    }
}
