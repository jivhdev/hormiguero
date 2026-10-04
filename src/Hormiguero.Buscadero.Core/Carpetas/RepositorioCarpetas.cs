using Microsoft.Data.Sqlite;

namespace Buscadero.Core.Carpetas;

public sealed class RepositorioCarpetas
{
    private readonly string _cadenaConexion;

    public RepositorioCarpetas(string rutaBaseDeDatos)
    {
        _cadenaConexion = new SqliteConnectionStringBuilder
        {
            DataSource = rutaBaseDeDatos,
        }.ToString();
        Inicializar();
    }

    private void Inicializar()
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            CREATE TABLE IF NOT EXISTS Carpetas (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Ruta TEXT NOT NULL UNIQUE
            );
            """;
        comando.ExecuteNonQuery();
    }

    private SqliteConnection AbrirConexion()
    {
        var conexion = new SqliteConnection(_cadenaConexion);
        conexion.Open();
        return conexion;
    }

    public IReadOnlyList<Carpeta> ObtenerTodas()
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT Id, Ruta FROM Carpetas ORDER BY Id;";
        using var lector = comando.ExecuteReader();

        var resultado = new List<Carpeta>();
        while (lector.Read())
        {
            resultado.Add(new Carpeta { Id = lector.GetInt32(0), Ruta = lector.GetString(1) });
        }

        return resultado;
    }

    public Carpeta Agregar(string ruta)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "INSERT INTO Carpetas (Ruta) VALUES ($ruta); SELECT last_insert_rowid();";
        comando.Parameters.AddWithValue("$ruta", ruta);
        var id = (long)comando.ExecuteScalar()!;

        return new Carpeta { Id = (int)id, Ruta = ruta };
    }

    public void Quitar(int id)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "DELETE FROM Carpetas WHERE Id = $id;";
        comando.Parameters.AddWithValue("$id", id);
        comando.ExecuteNonQuery();
    }

    public void ActualizarRuta(int id, string nuevaRuta)
    {
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "UPDATE Carpetas SET Ruta = $ruta WHERE Id = $id;";
        comando.Parameters.AddWithValue("$ruta", nuevaRuta);
        comando.Parameters.AddWithValue("$id", id);
        comando.ExecuteNonQuery();
    }
}
