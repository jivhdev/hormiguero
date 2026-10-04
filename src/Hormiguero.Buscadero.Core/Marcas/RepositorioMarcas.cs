using Buscadero.Core.Indexado;
using Microsoft.Data.Sqlite;

namespace Buscadero.Core.Marcas;

public sealed class RepositorioMarcas
{
    private readonly string _cadenaConexion;

    public RepositorioMarcas(string rutaBaseDeDatos)
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
            CREATE TABLE IF NOT EXISTS Marcas (
                Id TEXT PRIMARY KEY,
                RutaClave TEXT NOT NULL,
                Pagina INTEGER NOT NULL,
                Tipo INTEGER NOT NULL,
                X REAL NOT NULL,
                Y REAL NOT NULL,
                Ancho REAL NOT NULL,
                Alto REAL NOT NULL,
                Texto TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Marcas_Ruta ON Marcas (RutaClave);
            """;
        comando.ExecuteNonQuery();
    }

    private SqliteConnection AbrirConexion()
    {
        var conexion = new SqliteConnection(_cadenaConexion);
        conexion.Open();
        return conexion;
    }

    public IReadOnlyList<Marca> ObtenerPorDocumento(string rutaDocumento)
    {
        var clave = RepositorioIndice.ClaveRuta(rutaDocumento);
        using var conexion = AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT Id, Pagina, Tipo, X, Y, Ancho, Alto, Texto
            FROM Marcas
            WHERE RutaClave = $clave
            ORDER BY Pagina, rowid;
            """;
        comando.Parameters.AddWithValue("$clave", clave);

        using var lector = comando.ExecuteReader();
        var resultado = new List<Marca>();
        while (lector.Read())
        {
            resultado.Add(
                new Marca
                {
                    Id = Guid.Parse(lector.GetString(0)),
                    Pagina = lector.GetInt32(1),
                    Tipo = (TipoMarca)lector.GetInt32(2),
                    X = lector.GetDouble(3),
                    Y = lector.GetDouble(4),
                    Ancho = lector.GetDouble(5),
                    Alto = lector.GetDouble(6),
                    Texto = lector.IsDBNull(7) ? null : lector.GetString(7),
                }
            );
        }

        return resultado;
    }

    public void ReemplazarDelDocumento(string rutaDocumento, IEnumerable<Marca> marcas)
    {
        var clave = RepositorioIndice.ClaveRuta(rutaDocumento);
        using var conexion = AbrirConexion();
        using var transaccion = conexion.BeginTransaction();

        using (var borrar = conexion.CreateCommand())
        {
            borrar.Transaction = transaccion;
            borrar.CommandText = "DELETE FROM Marcas WHERE RutaClave = $clave;";
            borrar.Parameters.AddWithValue("$clave", clave);
            borrar.ExecuteNonQuery();
        }

        foreach (var marca in marcas)
        {
            using var insertar = conexion.CreateCommand();
            insertar.Transaction = transaccion;
            insertar.CommandText = """
                INSERT INTO Marcas (Id, RutaClave, Pagina, Tipo, X, Y, Ancho, Alto, Texto)
                VALUES ($id, $clave, $pagina, $tipo, $x, $y, $ancho, $alto, $texto);
                """;
            insertar.Parameters.AddWithValue("$id", marca.Id.ToString());
            insertar.Parameters.AddWithValue("$clave", clave);
            insertar.Parameters.AddWithValue("$pagina", marca.Pagina);
            insertar.Parameters.AddWithValue("$tipo", (int)marca.Tipo);
            insertar.Parameters.AddWithValue("$x", marca.X);
            insertar.Parameters.AddWithValue("$y", marca.Y);
            insertar.Parameters.AddWithValue("$ancho", marca.Ancho);
            insertar.Parameters.AddWithValue("$alto", marca.Alto);
            insertar.Parameters.AddWithValue("$texto", (object?)marca.Texto ?? DBNull.Value);
            insertar.ExecuteNonQuery();
        }

        transaccion.Commit();
    }
}
