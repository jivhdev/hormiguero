using System.IO;

namespace Archivero.Datos;

public record ArchivoPendiente(int Id, string RutaArchivo, DateTime FechaDetectado, MotivoPendiente Motivo)
{
    public string NombreArchivo => Path.GetFileName(RutaArchivo);
}

public class PendienteRepository
{
    /// <summary>
    /// Agrega el archivo a pendientes, o actualiza el motivo si ya estaba (ej. paso de
    /// "nuevo documento" a "duplicado" tras un reintento). Devuelve true solo si es la
    /// primera vez que se agrega (para no re-notificar en cada reescaneo).
    /// </summary>
    public bool Agregar(string rutaArchivo, MotivoPendiente motivo)
    {
        using var conexion = BaseDeDatos.CrearConexion();

        using (var verificar = conexion.CreateCommand())
        {
            verificar.CommandText = "SELECT Motivo FROM Pendientes WHERE RutaArchivo = $ruta;";
            verificar.Parameters.AddWithValue("$ruta", rutaArchivo);
            var motivoActual = verificar.ExecuteScalar() as string;

            if (motivoActual is not null)
            {
                if (motivoActual != motivo.ToString())
                {
                    using var actualizar = conexion.CreateCommand();
                    actualizar.CommandText = "UPDATE Pendientes SET Motivo = $motivo WHERE RutaArchivo = $ruta;";
                    actualizar.Parameters.AddWithValue("$motivo", motivo.ToString());
                    actualizar.Parameters.AddWithValue("$ruta", rutaArchivo);
                    actualizar.ExecuteNonQuery();
                }

                return false;
            }
        }

        using var insertar = conexion.CreateCommand();
        insertar.CommandText =
            """
            INSERT INTO Pendientes (RutaArchivo, FechaDetectado, Motivo) VALUES ($ruta, $fecha, $motivo);
            """;
        insertar.Parameters.AddWithValue("$ruta", rutaArchivo);
        insertar.Parameters.AddWithValue("$fecha", DateTime.Now.ToString("O"));
        insertar.Parameters.AddWithValue("$motivo", motivo.ToString());
        return insertar.ExecuteNonQuery() > 0;
    }

    public bool Quitar(string rutaArchivo)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "DELETE FROM Pendientes WHERE RutaArchivo = $ruta;";
        comando.Parameters.AddWithValue("$ruta", rutaArchivo);
        return comando.ExecuteNonQuery() > 0;
    }

    public List<ArchivoPendiente> ObtenerTodos()
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT Id, RutaArchivo, FechaDetectado, Motivo FROM Pendientes ORDER BY FechaDetectado;";

        var resultado = new List<ArchivoPendiente>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            resultado.Add(new ArchivoPendiente(
                lector.GetInt32(0),
                lector.GetString(1),
                DateTime.Parse(lector.GetString(2)),
                Enum.Parse<MotivoPendiente>(lector.GetString(3))));
        }

        return resultado;
    }
}
