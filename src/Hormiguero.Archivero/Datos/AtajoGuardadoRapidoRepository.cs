namespace Archivero.Datos;

/// <summary>Atajos de guardado rápido del flujo de "distribuir" (Caso-11, punto 4).</summary>
public class AtajoGuardadoRapidoRepository
{
    public List<AtajoGuardadoRapido> ObtenerTodos()
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT Id, Nombre, CarpetaMadre, FormatoCarpeta, PatronCarpeta, ReglaNombre FROM AtajosGuardadoRapido ORDER BY Nombre COLLATE NOCASE;";

        var resultado = new List<AtajoGuardadoRapido>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            resultado.Add(new AtajoGuardadoRapido(
                lector.GetInt32(0),
                lector.GetString(1),
                lector.GetString(2),
                Enum.Parse<FormatoCarpeta>(lector.GetString(3)),
                lector.IsDBNull(4) ? null : lector.GetString(4),
                LeerRegla(lector.GetString(5))));
        }

        return resultado;
    }

    public bool ExisteNombre(string nombre)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM AtajosGuardadoRapido WHERE Nombre = $nombre;";
        comando.Parameters.AddWithValue("$nombre", nombre);
        return (long)comando.ExecuteScalar()! > 0;
    }

    /// <summary>Guarda el atajo; si ya hay uno con ese nombre, lo reemplaza (el usuario lo confirma antes en la UI).</summary>
    public void Guardar(string nombre, string carpetaMadre, FormatoCarpeta formato, string? patron, IEnumerable<OperacionNombre> reglaNombre)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            INSERT INTO AtajosGuardadoRapido (Nombre, CarpetaMadre, FormatoCarpeta, PatronCarpeta, ReglaNombre)
            VALUES ($nombre, $carpeta, $formato, $patron, $regla)
            ON CONFLICT(Nombre) DO UPDATE SET
                CarpetaMadre = excluded.CarpetaMadre,
                FormatoCarpeta = excluded.FormatoCarpeta,
                PatronCarpeta = excluded.PatronCarpeta,
                ReglaNombre = excluded.ReglaNombre;
            """;
        comando.Parameters.AddWithValue("$nombre", nombre);
        comando.Parameters.AddWithValue("$carpeta", carpetaMadre);
        comando.Parameters.AddWithValue("$formato", formato.ToString());
        comando.Parameters.AddWithValue("$patron", (object?)patron ?? DBNull.Value);
        comando.Parameters.AddWithValue("$regla", string.Join(",", reglaNombre));
        comando.ExecuteNonQuery();
    }

    // Un valor desconocido (ej. de una versión futura) se ignora en vez de romper la lista de atajos.
    private static List<OperacionNombre> LeerRegla(string texto) =>
        texto.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => Enum.TryParse<OperacionNombre>(p, out var op) ? op : (OperacionNombre?)null)
            .Where(op => op is not null)
            .Select(op => op!.Value)
            .ToList();
}
