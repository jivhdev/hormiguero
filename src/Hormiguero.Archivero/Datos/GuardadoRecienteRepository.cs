namespace Archivero.Datos;

/// <summary>Una entrada del historial de "Guardados automáticamente" (Caso-1, punto 4).</summary>
public record GuardadoReciente(DateTime Hora, string RutaFinal);

/// <summary>
/// Historial de guardados automáticos (Caso-6, punto 2): antes vivía solo en memoria de
/// MainWindow, así que se "olvidaba" apenas se cerraba Archivero de verdad (no solo minimizado
/// a la bandeja). Ahora queda en la base, recortado a un tope razonable para no crecer sin
/// control — Javier pidió recordar al menos los últimos 10; se usa un poco más de margen.
/// </summary>
public class GuardadoRecienteRepository
{
    private const int MaximoEntradas = 20;

    public void Agregar(string rutaFinal)
    {
        using var conexion = BaseDeDatos.CrearConexion();

        using (var insertar = conexion.CreateCommand())
        {
            insertar.CommandText = "INSERT INTO GuardadosRecientes (RutaFinal, FechaHora) VALUES ($ruta, $fecha);";
            insertar.Parameters.AddWithValue("$ruta", rutaFinal);
            insertar.Parameters.AddWithValue("$fecha", DateTime.Now.ToString("O"));
            insertar.ExecuteNonQuery();
        }

        using var recortar = conexion.CreateCommand();
        recortar.CommandText =
            """
            DELETE FROM GuardadosRecientes
            WHERE Id NOT IN (SELECT Id FROM GuardadosRecientes ORDER BY Id DESC LIMIT $maximo);
            """;
        recortar.Parameters.AddWithValue("$maximo", MaximoEntradas);
        recortar.ExecuteNonQuery();
    }

    public List<GuardadoReciente> ObtenerTodos()
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT RutaFinal, FechaHora FROM GuardadosRecientes ORDER BY Id DESC;";

        var resultado = new List<GuardadoReciente>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            resultado.Add(new GuardadoReciente(DateTime.Parse(lector.GetString(1)), lector.GetString(0)));
        }

        return resultado;
    }
}
