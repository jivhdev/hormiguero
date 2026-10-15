using System.Diagnostics;
using System.IO;
using System.Windows;
using Hormiguero.Nucleo.Datos;

namespace Buscadero.App;

public partial class DialogoDudosos : Window
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _conexion;
    private readonly long? _cadenaId;
    private IReadOnlyList<DudosoVm> _dudosos = [];

    public DialogoDudosos(long? cadenaId = null)
    {
        InitializeComponent();
        _cadenaId = cadenaId;
        _conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        RecargarCadenas();
        Recargar();
    }

    private DudosoVm? Seleccionado => ListaDudosos.SelectedItem as DudosoVm;

    private void RecargarCadenas()
    {
        using var q = _conexion.CreateCommand();
        q.CommandText =
            "SELECT id,nombre FROM cadenas_diccionario WHERE estado='activa' ORDER BY id;";
        using var r = q.ExecuteReader();
        var filas = new List<CadenaVm>();
        while (r.Read())
            filas.Add(new(r.GetInt64(0), r.GetString(1)));
        ListaCadenas.ItemsSource = filas;
    }

    private void Recargar()
    {
        using var q = _conexion.CreateCommand();
        q.CommandText =
            "SELECT u.id,u.version_id,u.cadena_id,d.ruta,d.nombre,COALESCE(u.motivo,'Coincidencia dudosa') FROM ubicaciones_cadena_diccionario u JOIN versiones_documento v ON v.id=u.version_id JOIN documentos d ON d.id=v.documento_id WHERE u.estado='dudoso' AND ($c IS NULL OR u.cadena_id=$c) AND NOT EXISTS(SELECT 1 FROM decisiones_esquema_diccionario x WHERE x.version_id=u.version_id AND x.tipo='rechazado' AND x.estado='resuelta') ORDER BY u.orden;";
        q.Parameters.AddWithValue("$c", (object?)_cadenaId ?? DBNull.Value);
        using var r = q.ExecuteReader();
        var filas = new List<DudosoVm>();
        while (r.Read())
            filas.Add(
                new(
                    r.GetInt64(0),
                    r.GetInt64(1),
                    r.GetInt64(2),
                    r.GetString(3),
                    r.GetString(4),
                    r.GetString(5)
                )
            );
        _dudosos = filas;
        ListaDudosos.ItemsSource = _dudosos;
        using (var qPendientes = _conexion.CreateCommand())
        {
            qPendientes.CommandText =
                "SELECT d.nombre,p.motivo FROM decisiones_esquema_diccionario p JOIN versiones_documento v ON v.id=p.version_id JOIN documentos d ON d.id=v.documento_id WHERE p.tipo='sin_inicio' AND p.estado='pendiente' ORDER BY p.creada_en;";
            using var lectorPendientes = qPendientes.ExecuteReader();
            var pendientes = new List<DudosoSinInicioVm>();
            while (lectorPendientes.Read())
                pendientes.Add(new(lectorPendientes.GetString(0), lectorPendientes.GetString(1)));
            ListaSinInicio.ItemsSource = pendientes;
        }
        TextoContador.Text = $"{filas.Count} dudosos";
        if (filas.Count == 0)
        {
            TextoDocumentos.Text = "No hay documentos por revisar.";
            TextoDatos.Text = string.Empty;
            TextoMotivo.Text = string.Empty;
            return;
        }
        ListaDudosos.SelectedIndex = 0;
    }

    private void Lista_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e
    )
    {
        if (Seleccionado is not { } dudoso)
            return;
        TextoDocumentos.Text = dudoso.Nombre;
        TextoDatos.Text = $"Cadena sugerida: {dudoso.CadenaId}";
        TextoMotivo.Text = dudoso.Motivo;
        ListaCadenas.SelectedValue = dudoso.CadenaId;
    }

    private void Enlazar_Click(object sender, RoutedEventArgs e)
    {
        if (Seleccionado is not { } dudoso || ListaCadenas.SelectedItem is not CadenaVm cadena)
            return;
        using var q = _conexion.CreateCommand();
        q.CommandText =
            "UPDATE ubicaciones_cadena_diccionario SET cadena_id=$c,estado='activo',motivo=NULL WHERE id=$u AND estado='dudoso';";
        q.Parameters.AddWithValue("$c", cadena.Id);
        q.Parameters.AddWithValue("$u", dudoso.UbicacionId);
        q.ExecuteNonQuery();
        Recargar();
    }

    private void Rechazar_Click(object sender, RoutedEventArgs e)
    {
        if (Seleccionado is not { } dudoso)
            return;
        using var tx = _conexion.BeginTransaction();
        using (var borrar = _conexion.CreateCommand())
        {
            borrar.Transaction = tx;
            borrar.CommandText =
                "DELETE FROM ubicaciones_cadena_diccionario WHERE id=$u AND estado='dudoso';";
            borrar.Parameters.AddWithValue("$u", dudoso.UbicacionId);
            borrar.ExecuteNonQuery();
        }
        using (var registrar = _conexion.CreateCommand())
        {
            registrar.Transaction = tx;
            registrar.CommandText =
                "INSERT INTO decisiones_esquema_diccionario(version_id,proveedor,motivo,creada_en,estado,tipo,opciones_json) SELECT $v,e.proveedor,'Coincidencia dudosa rechazada',$f,'resuelta','rechazado','[]' FROM cadenas_diccionario c JOIN esquemas_diccionario e ON e.id=c.esquema_id WHERE c.id=$c ON CONFLICT(version_id) DO UPDATE SET estado='resuelta',tipo='rechazado',motivo=excluded.motivo;";
            registrar.Parameters.AddWithValue("$v", dudoso.VersionId);
            registrar.Parameters.AddWithValue("$c", dudoso.CadenaId);
            registrar.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
            registrar.ExecuteNonQuery();
        }
        tx.Commit();
        Recargar();
    }

    private void VerArchivo_Click(object sender, RoutedEventArgs e)
    {
        string? ruta = Seleccionado?.Ruta;
        if (ruta is null || !File.Exists(ruta))
        {
            MessageBox.Show(
                this,
                "No se encontró el archivo.",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            MessageBox.Show(
                this,
                $"No se pudo abrir el archivo: {error.Message}",
                "Buscadero",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _conexion.Dispose();
        base.OnClosed(e);
    }

    private sealed record CadenaVm(long Id, string Nombre);

    private sealed record DudosoVm(
        long UbicacionId,
        long VersionId,
        long CadenaId,
        string Ruta,
        string Nombre,
        string Motivo
    );

    private sealed record DudosoSinInicioVm(string Nombre, string Motivo);
}
