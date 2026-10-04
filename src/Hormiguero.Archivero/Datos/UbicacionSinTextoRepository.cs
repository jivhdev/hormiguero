namespace Archivero.Datos;

/// <summary>Ubicaciones guardadas desde el flujo de PDFs sin texto extraíble (Caso-4, punto 3).</summary>
public class UbicacionSinTextoRepository
{
    public List<UbicacionSinTexto> ObtenerTodas()
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT Id, CarpetaMadre, FormatoCarpeta, PatronCarpeta FROM UbicacionesSinTexto ORDER BY Id DESC;";

        var resultado = new List<UbicacionSinTexto>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            resultado.Add(new UbicacionSinTexto(
                lector.GetInt32(0),
                lector.GetString(1),
                Enum.Parse<FormatoCarpeta>(lector.GetString(2)),
                lector.IsDBNull(3) ? null : lector.GetString(3)));
        }

        return resultado;
    }

    /// <summary>
    /// Devuelve la ubicación existente si ya hay una exactamente igual (misma carpeta, formato y
    /// patrón), o crea una nueva. Evita que "Crear ubicación nueva" acumule entradas repetidas
    /// en "Ver ubicaciones disponibles" cada vez que se reutiliza el mismo lugar.
    /// </summary>
    public UbicacionSinTexto ObtenerOCrear(string carpetaMadre, FormatoCarpeta formato, string? patron)
    {
        using var conexion = BaseDeDatos.CrearConexion();

        using (var buscar = conexion.CreateCommand())
        {
            buscar.CommandText =
                """
                SELECT Id FROM UbicacionesSinTexto
                WHERE CarpetaMadre = $carpeta AND FormatoCarpeta = $formato
                    AND (PatronCarpeta = $patron OR (PatronCarpeta IS NULL AND $patron IS NULL));
                """;
            buscar.Parameters.AddWithValue("$carpeta", carpetaMadre);
            buscar.Parameters.AddWithValue("$formato", formato.ToString());
            buscar.Parameters.AddWithValue("$patron", (object?)patron ?? DBNull.Value);

            if (buscar.ExecuteScalar() is long idExistente)
            {
                return new UbicacionSinTexto((int)idExistente, carpetaMadre, formato, patron);
            }
        }

        using var insertar = conexion.CreateCommand();
        insertar.CommandText =
            """
            INSERT INTO UbicacionesSinTexto (CarpetaMadre, FormatoCarpeta, PatronCarpeta)
            VALUES ($carpeta, $formato, $patron);
            SELECT last_insert_rowid();
            """;
        insertar.Parameters.AddWithValue("$carpeta", carpetaMadre);
        insertar.Parameters.AddWithValue("$formato", formato.ToString());
        insertar.Parameters.AddWithValue("$patron", (object?)patron ?? DBNull.Value);

        var id = (int)(long)insertar.ExecuteScalar()!;
        return new UbicacionSinTexto(id, carpetaMadre, formato, patron);
    }
}
