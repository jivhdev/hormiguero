using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public record DocumentoGuardado(long Id, string Ruta, string App, DateTime GuardadoEn);

/// <summary>
/// Fase B-4 (D-66): aviso entre apps de cada documento guardado. Una app escribe
/// (Archivero, al guardar) y otra lee lo nuevo desde el último que vio (Buscadero, para
/// agregarlo a su índice al instante). Solo se agregan filas; el Id sirve de marcador.
/// </summary>
public sealed class DocumentosGuardados(SqliteConnection conexion)
{
    public long Registrar(string ruta, string app)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        ArgumentException.ThrowIfNullOrWhiteSpace(app);

        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "INSERT INTO documentos_guardados(ruta, app, guardado_en) "
            + "VALUES ($ruta, $app, $ahora) RETURNING id;";
        comando.Parameters.AddWithValue("$ruta", Path.GetFullPath(ruta));
        comando.Parameters.AddWithValue("$app", app);
        comando.Parameters.AddWithValue("$ahora", DateTime.Now.ToString("o"));
        return (long)comando.ExecuteScalar()!;
    }

    /// <summary>Los registrados después de <paramref name="ultimoVisto"/>, del más viejo al más nuevo.</summary>
    public IReadOnlyList<DocumentoGuardado> Despues(long ultimoVisto, int maximo = 1000)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id, ruta, app, guardado_en FROM documentos_guardados "
            + "WHERE id > $ultimo ORDER BY id LIMIT $maximo;";
        comando.Parameters.AddWithValue("$ultimo", ultimoVisto);
        comando.Parameters.AddWithValue("$maximo", maximo);
        using var lector = comando.ExecuteReader();
        var lista = new List<DocumentoGuardado>();
        while (lector.Read())
        {
            lista.Add(
                new DocumentoGuardado(
                    lector.GetInt64(0),
                    lector.GetString(1),
                    lector.GetString(2),
                    DateTime.Parse(lector.GetString(3))
                )
            );
        }
        return lista;
    }

    /// <summary>
    /// Ruta de la base común según la carpeta de datos activa (respeta HORMIGUERO_DATOS,
    /// así las pruebas con datos sintéticos no tocan la base real).
    /// </summary>
    public static string RutaBaseComun => Path.Combine(DatosDeApp.Carpeta, "hormiguero.db");
}
