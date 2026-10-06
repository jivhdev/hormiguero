using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

// Cómo reconocer un tipo de documento de un emisor (ADR-001 de Archivero).
// Datos lleva el resto (zonas, destino, nombre) como JSON que define cada app.
public record Identificacion(
    long Id,
    string Tipo,
    string Emisor,
    string Datos,
    string GrupoDocumento = "Recibido",
    string NombreEstandar = ""
);

// Única puerta a la tabla común "identificaciones" (ADR-001: solo el núcleo toca tablas comunes).
public sealed class Identificaciones(SqliteConnection conexion)
{
    public IReadOnlyList<Identificacion> Listar()
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id, tipo, emisor, datos, grupo_documento, nombre_estandar FROM identificaciones ORDER BY emisor, tipo;";
        using var lector = comando.ExecuteReader();
        var lista = new List<Identificacion>();
        while (lector.Read())
        {
            lista.Add(
                new Identificacion(
                    lector.GetInt64(0),
                    lector.GetString(1),
                    lector.GetString(2),
                    lector.GetString(3),
                    lector.GetString(4),
                    lector.GetString(5)
                )
            );
        }
        return lista;
    }

    // Id 0 agrega una nueva; otro id reemplaza esa. Devuelve el id guardado.
    // Tipo y emisor repetidos en otra fila lanzan InvalidOperationException:
    // dos configuraciones iguales harían ambiguo el reconocimiento.
    public long Guardar(Identificacion identificacion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identificacion.Tipo);
        ArgumentException.ThrowIfNullOrWhiteSpace(identificacion.Emisor);

        using var comando = conexion.CreateCommand();
        comando.CommandText =
            identificacion.Id == 0
                ? "INSERT INTO identificaciones(tipo, emisor, datos, grupo_documento, nombre_estandar, actualizada) "
                    + "VALUES ($tipo, $emisor, $datos, $grupo, $nombre, $ahora) RETURNING id;"
                : "UPDATE identificaciones SET tipo = $tipo, emisor = $emisor, datos = $datos, grupo_documento=$grupo, nombre_estandar=$nombre, "
                    + "actualizada = $ahora WHERE id = $id RETURNING id;";
        comando.Parameters.AddWithValue("$id", identificacion.Id);
        comando.Parameters.AddWithValue("$tipo", identificacion.Tipo.Trim());
        comando.Parameters.AddWithValue("$emisor", identificacion.Emisor.Trim());
        comando.Parameters.AddWithValue("$datos", identificacion.Datos);
        if (identificacion.GrupoDocumento is not ("Emitido" or "Recibido"))
            throw new ArgumentException(
                "El grupo debe ser Emitido o Recibido.",
                nameof(identificacion)
            );
        comando.Parameters.AddWithValue("$grupo", identificacion.GrupoDocumento);
        comando.Parameters.AddWithValue(
            "$nombre",
            string.IsNullOrWhiteSpace(identificacion.NombreEstandar)
                ? $"{identificacion.Tipo.Trim()} · {identificacion.Emisor.Trim()}"
                : identificacion.NombreEstandar.Trim()
        );
        comando.Parameters.AddWithValue("$ahora", DateTime.Now.ToString("o"));

        try
        {
            return comando.ExecuteScalar() is long id
                ? id
                : throw new InvalidOperationException(
                    $"No existe la configuración {identificacion.Id}."
                );
        }
        catch (SqliteException error) when (error.SqliteExtendedErrorCode == 2067)
        {
            // 2067 = SQLITE_CONSTRAINT_UNIQUE
            throw new InvalidOperationException(
                $"Ya existe una configuración de {identificacion.Tipo} para {identificacion.Emisor}.",
                error
            );
        }
    }

    public bool ConfigurarTipoDocumento(long id, string grupo, string nombreEstandar)
    {
        if (grupo is not ("Emitido" or "Recibido"))
            throw new ArgumentException("El grupo debe ser Emitido o Recibido.", nameof(grupo));
        ArgumentNullException.ThrowIfNull(nombreEstandar);
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "UPDATE identificaciones SET grupo_documento=$grupo,nombre_estandar=$nombre,actualizada=$ahora WHERE id=$id;";
        comando.Parameters.AddWithValue("$grupo", grupo);
        comando.Parameters.AddWithValue("$nombre", nombreEstandar.Trim());
        comando.Parameters.AddWithValue("$ahora", DateTime.Now.ToString("o"));
        comando.Parameters.AddWithValue("$id", id);
        return comando.ExecuteNonQuery() > 0;
    }

    public bool Quitar(long id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = "DELETE FROM identificaciones WHERE id = $id;";
        comando.Parameters.AddWithValue("$id", id);
        return comando.ExecuteNonQuery() > 0;
    }
}
