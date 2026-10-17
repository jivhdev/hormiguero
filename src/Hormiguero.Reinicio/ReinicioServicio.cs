using System.Globalization;
using System.IO;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Reinicio;

public sealed record ResultadoReinicio(bool Exitoso, string RutaRespaldo, string Mensaje);

public sealed class ReinicioServicio
{
    private static readonly HashSet<string> TablasHormigueroConservadas = new(
        [
            "diccionario_datos",
            "migraciones",
            "calendarios_feriados",
            "feriados",
            "identificaciones",
            "campos_documento",
            "tipos_documento_datos",
            "tipos_documento_informativos",
            "esquemas_cadena",
            "lugares_esquema",
            "parejas_esquema",
            "modos_esquema",
            "reglas_alerta_esquema",
            "reconocimientos_esquema",
            "reglas_propuesta_modo_esquema",
            "esquemas_diccionario",
            "lugares_esquema_diccionario",
            "enlaces_esquema_diccionario",
            "modos_esquema_diccionario",
            "reglas_alerta_esquema_diccionario",
            "reconocimientos_esquema_diccionario",
            "reglas_propuesta_modo_esquema_diccionario",
            "datos_ingresados_esquema_diccionario",
        ],
        StringComparer.OrdinalIgnoreCase
    );

    private static readonly HashSet<string> TablasArchiveroConservadas = new(
        [
            "Configuraciones",
            "PatronesReconocimiento",
            "Marcas",
            "EntidadesConocidas",
            "CamposPropios",
            "AtajosGuardadoRapido",
            "UbicacionesSinTexto",
            "UbicacionesSinTextoCadena",
            "Configuracion",
        ],
        StringComparer.OrdinalIgnoreCase
    );

    private static readonly HashSet<string> TablasBuscaderoConservadas = new(
        ["Carpetas", "CarpetasIndexadas"],
        StringComparer.OrdinalIgnoreCase
    );

    public static string RutaRespaldo(string carpeta, DateTime fecha) =>
        carpeta.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        + "-respaldo-reinicio-"
        + fecha.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

    public ResultadoReinicio Ejecutar(
        string carpeta,
        DateTime fecha,
        bool borrarClasificaciones = false
    )
    {
        string respaldo = RutaRespaldo(carpeta, fecha);
        try
        {
            if (Directory.Exists(respaldo) || File.Exists(respaldo))
                throw new IOException("Ya existe una carpeta de respaldo con ese nombre.");
            if (Directory.Exists(carpeta))
                CopiarCarpeta(carpeta, respaldo);
            else
                Directory.CreateDirectory(respaldo);

            SqliteConnection.ClearAllPools();
            Limpiar(
                Path.Combine(carpeta, "hormiguero.db"),
                TablasHormigueroConservadas,
                borrarClasificaciones
                    ?
                    [
                        "identificaciones",
                        "campos_documento",
                        "tipos_documento_datos",
                        "tipos_documento_informativos",
                    ]
                    : []
            );
            Limpiar(
                Path.Combine(carpeta, "archivero.db"),
                TablasArchiveroConservadas,
                borrarClasificaciones
                    ?
                    [
                        "PatronesReconocimiento",
                        "Marcas",
                        "EntidadesConocidas",
                        "CamposPropios",
                        "AtajosGuardadoRapido",
                        "UbicacionesSinTexto",
                        "UbicacionesSinTextoCadena",
                    ]
                    : []
            );
            Limpiar(Path.Combine(carpeta, "buscadero.db"), TablasBuscaderoConservadas, []);
            DatosDeApp.MarcarReinicio(carpeta, fecha);
            return new(true, respaldo, "");
        }
        catch (Exception error)
        {
            return new(false, respaldo, $"No se pudo reiniciar Hormiguero: {error.Message}");
        }
    }

    private static void CopiarCarpeta(string origen, string destino)
    {
        Directory.CreateDirectory(destino);
        foreach (string archivo in Directory.EnumerateFiles(origen))
        {
            string ruta = Path.Combine(destino, Path.GetFileName(archivo));
            File.Copy(archivo, ruta, overwrite: false);
            if (!File.ReadAllBytes(archivo).AsSpan().SequenceEqual(File.ReadAllBytes(ruta)))
                throw new IOException($"No se pudo verificar la copia de {archivo}.");
        }
        foreach (string subcarpeta in Directory.EnumerateDirectories(origen))
            CopiarCarpeta(subcarpeta, Path.Combine(destino, Path.GetFileName(subcarpeta)));
    }

    private static void Limpiar(string ruta, HashSet<string> conservar, IEnumerable<string> vaciar)
    {
        if (!File.Exists(ruta))
            return;
        using var conexion = Abrir(ruta);
        using (var clavesForaneas = conexion.CreateCommand())
        {
            clavesForaneas.CommandText = "PRAGMA foreign_keys=OFF;";
            clavesForaneas.ExecuteNonQuery();
        }
        List<string> tablas = [];
        using (var listar = conexion.CreateCommand())
        {
            listar.CommandText =
                "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
            using var lector = listar.ExecuteReader();
            while (lector.Read())
                tablas.Add(lector.GetString(0));
        }
        using (var transaccion = conexion.BeginTransaction())
        {
            foreach (string tabla in tablas)
            {
                if (conservar.Contains(tabla))
                    continue;
                using var borrar = conexion.CreateCommand();
                borrar.Transaction = transaccion;
                borrar.CommandText = $"DELETE FROM {Identificador(tabla)};";
                borrar.ExecuteNonQuery();
            }
            foreach (string tabla in vaciar)
            {
                if (!tablas.Contains(tabla, StringComparer.OrdinalIgnoreCase))
                    continue;
                using var borrar = conexion.CreateCommand();
                borrar.Transaction = transaccion;
                borrar.CommandText = $"DELETE FROM {Identificador(tabla)};";
                borrar.ExecuteNonQuery();
            }
            transaccion.Commit();
        }
        using var vacuum = conexion.CreateCommand();
        vacuum.CommandText = "VACUUM;";
        vacuum.ExecuteNonQuery();
    }

    private static string Identificador(string valor) => "\"" + valor.Replace("\"", "\"\"") + "\"";

    private static SqliteConnection Abrir(string ruta)
    {
        var conexion = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = ruta, Pooling = false }.ToString()
        );
        conexion.Open();
        return conexion;
    }
}
