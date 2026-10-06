using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

/// <summary>
/// Fase B-3 (D-66): los datos propios de cada app viven en la carpeta común de Hormiguero
/// (%LocalAppData%\Hormiguero\&lt;app&gt;.db), junto a la base común. La primera vez se copian
/// solos los datos que la app tenía en su ubicación anterior; ese archivo queda intacto
/// como respaldo. Cada día se respalda el archivo de la app, igual que la base común.
/// </summary>
public static class DatosDeApp
{
    private const string ArchivoMarcaReinicio = "reiniciado.txt";
    private const string ArchivoPreferencias = "preferencias.json";

    /// <summary>
    /// Carpeta común de datos. HORMIGUERO_DATOS la cambia para probar con datos
    /// sintéticos sin tocar los reales.
    /// </summary>
    public static string Carpeta =>
        Environment.GetEnvironmentVariable("HORMIGUERO_DATOS") is { Length: > 0 } prueba
            ? prueba
            : Path.GetDirectoryName(BaseComun.RutaPorDefecto)!;

    public static string? LeerPreferencia(string clave, string? carpeta = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        try
        {
            string ruta = Path.Combine(carpeta ?? Carpeta, ArchivoPreferencias);
            if (!File.Exists(ruta))
            {
                return null;
            }

            return JsonSerializer
                .Deserialize<Dictionary<string, string>>(File.ReadAllText(ruta))
                ?.GetValueOrDefault(clave);
        }
        catch (Exception error)
            when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static void GuardarPreferencia(string clave, string valor, string? carpeta = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        ArgumentNullException.ThrowIfNull(valor);
        string directorio = carpeta ?? Carpeta;
        Directory.CreateDirectory(directorio);
        string ruta = Path.Combine(directorio, ArchivoPreferencias);
        Dictionary<string, string> preferencias = new(StringComparer.Ordinal);
        try
        {
            if (File.Exists(ruta))
            {
                preferencias =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(ruta))
                    ?? preferencias;
            }
        }
        catch (Exception error)
            when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            // Un archivo dañado no debe impedir guardar una preferencia nueva.
        }

        preferencias[clave] = valor;
        File.WriteAllText(ruta, JsonSerializer.Serialize(preferencias));
    }

    public static string Preparar(string nombreApp, string? rutaAnterior) =>
        Preparar(Carpeta, nombreApp, rutaAnterior, DateTime.Now);

    public static string Preparar(
        string carpeta,
        string nombreApp,
        string? rutaAnterior,
        DateTime ahora
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carpeta);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreApp);

        Directory.CreateDirectory(carpeta);
        string destino = Path.Combine(carpeta, $"{nombreApp}.db");

        if (
            !File.Exists(destino)
            && !File.Exists(Path.Combine(carpeta, ArchivoMarcaReinicio))
            && rutaAnterior is not null
            && File.Exists(rutaAnterior)
        )
        {
            Copiar(rutaAnterior, destino);
        }

        if (File.Exists(destino))
        {
            try
            {
                using var conexion = Abrir(destino, SqliteOpenMode.ReadWrite);
                Respaldo.HacerSiCorresponde(
                    conexion,
                    Path.Combine(carpeta, "respaldos"),
                    ahora,
                    nombreApp
                );
            }
            catch (Exception error) when (error is IOException or SqliteException)
            {
                // Un respaldo que falla no puede impedir usar la app (igual que la base común).
            }
        }

        return destino;
    }

    public static void MarcarReinicio(string carpeta, DateTime fecha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carpeta);
        Directory.CreateDirectory(carpeta);
        File.WriteAllText(
            Path.Combine(carpeta, ArchivoMarcaReinicio),
            fecha.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
        );
    }

    // Copia con la API de respaldo de SQLite: si la base anterior estaba en modo WAL,
    // copiar el archivo a mano dejaría fuera lo que aún no se había volcado.
    private static void Copiar(string origen, string destino)
    {
        string temporal = destino + ".copiando";
        try
        {
            using (var desde = Abrir(origen, SqliteOpenMode.ReadOnly))
            using (var hacia = Abrir(temporal, SqliteOpenMode.ReadWriteCreate))
            {
                desde.BackupDatabase(hacia);
            }
            SqliteConnection.ClearAllPools();
            File.Move(temporal, destino);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(temporal))
            {
                File.Delete(temporal);
            }
        }
    }

    private static SqliteConnection Abrir(string ruta, SqliteOpenMode modo)
    {
        var conexion = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = ruta,
                Mode = modo,
                Pooling = false,
            }.ToString()
        );
        conexion.Open();
        return conexion;
    }
}
