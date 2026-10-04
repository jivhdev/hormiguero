using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public record Movimiento(
    DateTime Fecha,
    string App,
    string Accion,
    string Origen,
    string? Destino,
    string? Huella,
    string Resultado
);

// Registro de cada movimiento de archivos (Archivero REQ-004). Solo se agregan
// filas: nunca se cambian ni se borran (ADR-001 de Archivero).
public sealed class Auditoria(SqliteConnection conexion)
{
    public void Registrar(
        string app,
        string accion,
        string origen,
        string? destino,
        string? huella,
        string resultado
    )
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "INSERT INTO auditoria(fecha, app, accion, origen, destino, huella, resultado) "
            + "VALUES ($fecha, $app, $accion, $origen, $destino, $huella, $resultado);";
        comando.Parameters.AddWithValue("$fecha", DateTime.Now.ToString("o"));
        comando.Parameters.AddWithValue("$app", app);
        comando.Parameters.AddWithValue("$accion", accion);
        comando.Parameters.AddWithValue("$origen", origen);
        comando.Parameters.AddWithValue("$destino", (object?)destino ?? DBNull.Value);
        comando.Parameters.AddWithValue("$huella", (object?)huella ?? DBNull.Value);
        comando.Parameters.AddWithValue("$resultado", resultado);
        comando.ExecuteNonQuery();
    }

    // Los más nuevos primero.
    public IReadOnlyList<Movimiento> Recientes(string app, int cantidad)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT fecha, app, accion, origen, destino, huella, resultado FROM auditoria "
            + "WHERE app = $app ORDER BY id DESC LIMIT $cantidad;";
        comando.Parameters.AddWithValue("$app", app);
        comando.Parameters.AddWithValue("$cantidad", cantidad);
        using var lector = comando.ExecuteReader();
        var lista = new List<Movimiento>();
        while (lector.Read())
        {
            lista.Add(
                new Movimiento(
                    DateTime.Parse(lector.GetString(0)),
                    lector.GetString(1),
                    lector.GetString(2),
                    lector.GetString(3),
                    lector.IsDBNull(4) ? null : lector.GetString(4),
                    lector.IsDBNull(5) ? null : lector.GetString(5),
                    lector.GetString(6)
                )
            );
        }
        return lista;
    }
}
