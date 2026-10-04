using System.Text.Json;

namespace Archivero.Datos;

public class BorradorAsistente
{
    public string Emisor { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string CarpetaDestino { get; set; } = string.Empty;
    public string? Formato { get; set; }

    /// <summary>Caso-3, Paso 2: la elección explícita Directo/Subcarpetas (independiente del tipo, que puede no estar elegido todavía).</summary>
    public bool? GuardaEnSubcarpetas { get; set; }

    public string? PatronCarpeta { get; set; }
    public bool? Renombrar { get; set; }

    /// <summary>Caso-11, punto 1: la tercera opción de nombre. Null en borradores anteriores (equivale a no elegida).</summary>
    public bool? PreguntarNombre { get; set; }

    public List<Marca> Marcas { get; set; } = [];
}

public class BorradorRepository
{
    public BorradorAsistente? Obtener(string rutaArchivo)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT Datos FROM Borradores WHERE RutaArchivo = $ruta";
        comando.Parameters.AddWithValue("$ruta", rutaArchivo);

        var json = comando.ExecuteScalar() as string;
        return json is null ? null : JsonSerializer.Deserialize<BorradorAsistente>(json);
    }

    public void Guardar(string rutaArchivo, BorradorAsistente borrador)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            INSERT INTO Borradores (RutaArchivo, Datos) VALUES ($ruta, $datos)
            ON CONFLICT(RutaArchivo) DO UPDATE SET Datos = excluded.Datos;
            """;
        comando.Parameters.AddWithValue("$ruta", rutaArchivo);
        comando.Parameters.AddWithValue("$datos", JsonSerializer.Serialize(borrador));
        comando.ExecuteNonQuery();
    }

    public void Eliminar(string rutaArchivo)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "DELETE FROM Borradores WHERE RutaArchivo = $ruta";
        comando.Parameters.AddWithValue("$ruta", rutaArchivo);
        comando.ExecuteNonQuery();
    }
}
