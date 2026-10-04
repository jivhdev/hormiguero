namespace Archivero.Datos;

public enum CategoriaEntidad
{
    Emisor,
    Tipo
}

public class EntidadRepository
{
    public List<string> Buscar(CategoriaEntidad categoria, string textoParcial)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            SELECT Nombre FROM EntidadesConocidas
            WHERE Categoria = $categoria AND Nombre LIKE $filtro
            ORDER BY Nombre
            LIMIT 20;
            """;
        comando.Parameters.AddWithValue("$categoria", categoria.ToString());
        comando.Parameters.AddWithValue("$filtro", $"%{textoParcial}%");

        var resultado = new List<string>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            resultado.Add(lector.GetString(0));
        }

        return resultado;
    }

    public int ObtenerOCrear(CategoriaEntidad categoria, string nombre)
    {
        using var conexion = BaseDeDatos.CrearConexion();

        using (var buscar = conexion.CreateCommand())
        {
            buscar.CommandText = "SELECT Id FROM EntidadesConocidas WHERE Categoria = $categoria AND Nombre = $nombre";
            buscar.Parameters.AddWithValue("$categoria", categoria.ToString());
            buscar.Parameters.AddWithValue("$nombre", nombre);

            var existente = buscar.ExecuteScalar();
            if (existente is long id)
            {
                return (int)id;
            }
        }

        using var insertar = conexion.CreateCommand();
        insertar.CommandText =
            """
            INSERT INTO EntidadesConocidas (Categoria, Nombre) VALUES ($categoria, $nombre);
            SELECT last_insert_rowid();
            """;
        insertar.Parameters.AddWithValue("$categoria", categoria.ToString());
        insertar.Parameters.AddWithValue("$nombre", nombre);

        return (int)(long)insertar.ExecuteScalar()!;
    }
}
