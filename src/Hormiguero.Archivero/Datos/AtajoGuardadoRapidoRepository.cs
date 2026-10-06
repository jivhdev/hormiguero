namespace Archivero.Datos;

/// <summary>Atajos de guardado rápido del flujo de "distribuir" (Caso-11, punto 4).</summary>
public class AtajoGuardadoRapidoRepository
{
    public List<AtajoGuardadoRapido> ObtenerTodos()
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT Id, Nombre, CarpetaMadre, FormatoCarpeta, PatronCarpeta, ReglaNombre, Periodo, AnioFijo, Orden FROM AtajosGuardadoRapido ORDER BY Orden, Id;";

        var resultado = new List<AtajoGuardadoRapido>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            resultado.Add(
                new AtajoGuardadoRapido(
                    lector.GetInt32(0),
                    lector.GetString(1),
                    lector.GetString(2),
                    Enum.Parse<FormatoCarpeta>(lector.GetString(3)),
                    lector.IsDBNull(4) ? null : lector.GetString(4),
                    LeerRegla(lector.GetString(5)),
                    Enum.TryParse<PeriodoAtajo>(lector.GetString(6), out var periodo)
                        ? periodo
                        : PeriodoAtajo.PreguntarFechaCadaVez,
                    lector.IsDBNull(7) ? null : lector.GetInt32(7),
                    lector.GetInt32(8)
                )
            );
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
    public void Guardar(
        string nombre,
        string carpetaMadre,
        FormatoCarpeta formato,
        string? patron,
        IEnumerable<OperacionNombre> reglaNombre,
        PeriodoAtajo periodo = PeriodoAtajo.PreguntarFechaCadaVez,
        int? anioFijo = null
    )
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            INSERT INTO AtajosGuardadoRapido (Nombre, CarpetaMadre, FormatoCarpeta, PatronCarpeta, ReglaNombre, Periodo, AnioFijo, Orden)
            VALUES ($nombre, $carpeta, $formato, $patron, $regla, $periodo, $anio, (SELECT COALESCE(MAX(Orden), -1) + 1 FROM AtajosGuardadoRapido))
            ON CONFLICT(Nombre) DO UPDATE SET
                CarpetaMadre = excluded.CarpetaMadre,
                FormatoCarpeta = excluded.FormatoCarpeta,
                PatronCarpeta = excluded.PatronCarpeta,
                ReglaNombre = excluded.ReglaNombre,
                Periodo = excluded.Periodo,
                AnioFijo = excluded.AnioFijo;
            """;
        comando.Parameters.AddWithValue("$nombre", nombre);
        comando.Parameters.AddWithValue("$carpeta", carpetaMadre);
        comando.Parameters.AddWithValue("$formato", formato.ToString());
        comando.Parameters.AddWithValue("$patron", (object?)patron ?? DBNull.Value);
        comando.Parameters.AddWithValue("$regla", string.Join(",", reglaNombre));
        comando.Parameters.AddWithValue("$periodo", periodo.ToString());
        comando.Parameters.AddWithValue("$anio", (object?)anioFijo ?? DBNull.Value);
        comando.ExecuteNonQuery();
    }

    public void Actualizar(AtajoGuardadoRapido atajo)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "UPDATE AtajosGuardadoRapido SET Nombre=$nombre, CarpetaMadre=$carpeta, FormatoCarpeta=$formato, PatronCarpeta=$patron, ReglaNombre=$regla, Periodo=$periodo, AnioFijo=$anio WHERE Id=$id;";
        comando.Parameters.AddWithValue("$id", atajo.Id);
        comando.Parameters.AddWithValue("$nombre", atajo.Nombre.Trim());
        comando.Parameters.AddWithValue("$carpeta", atajo.CarpetaMadre);
        comando.Parameters.AddWithValue("$formato", atajo.Formato.ToString());
        comando.Parameters.AddWithValue("$patron", (object?)atajo.Patron ?? DBNull.Value);
        comando.Parameters.AddWithValue("$regla", string.Join(",", atajo.ReglaNombre));
        comando.Parameters.AddWithValue("$periodo", atajo.Periodo.ToString());
        comando.Parameters.AddWithValue("$anio", (object?)atajo.AnioFijo ?? DBNull.Value);
        comando.ExecuteNonQuery();
    }

    public void Eliminar(int id)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "DELETE FROM AtajosGuardadoRapido WHERE Id=$id;";
        comando.Parameters.AddWithValue("$id", id);
        comando.ExecuteNonQuery();
    }

    public void Mover(int id, int desplazamiento)
    {
        var atajos = ObtenerTodos();
        var indice = atajos.FindIndex(a => a.Id == id);
        var destino = indice + Math.Sign(desplazamiento);
        if (indice < 0 || destino < 0 || destino >= atajos.Count)
            return;

        (atajos[indice], atajos[destino]) = (atajos[destino], atajos[indice]);
        using var conexion = BaseDeDatos.CrearConexion();
        using var transaccion = conexion.BeginTransaction();
        for (var orden = 0; orden < atajos.Count; orden++)
        {
            using var comando = conexion.CreateCommand();
            comando.Transaction = transaccion;
            comando.CommandText = "UPDATE AtajosGuardadoRapido SET Orden=$orden WHERE Id=$id;";
            comando.Parameters.AddWithValue("$orden", orden);
            comando.Parameters.AddWithValue("$id", atajos[orden].Id);
            comando.ExecuteNonQuery();
        }
        transaccion.Commit();
    }

    // Un valor desconocido (ej. de una versión futura) se ignora en vez de romper la lista de atajos.
    private static List<OperacionNombre> LeerRegla(string texto) =>
        texto
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p =>
                Enum.TryParse<OperacionNombre>(p, out var op) ? op : (OperacionNombre?)null
            )
            .Where(op => op is not null)
            .Select(op => op!.Value)
            .ToList();
}
