using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

// Única puerta a la tabla común "configuracion" (ADR-001: solo el núcleo toca tablas comunes).
public sealed class Configuracion(SqliteConnection conexion)
{
    public string? Leer(string clave)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT valor FROM configuracion WHERE clave = $clave;";
        comando.Parameters.AddWithValue("$clave", clave);
        return comando.ExecuteScalar() as string;
    }

    public void Guardar(string clave, string valor)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "INSERT INTO configuracion(clave, valor) VALUES ($clave, $valor) "
            + "ON CONFLICT(clave) DO UPDATE SET valor = $valor;";
        comando.Parameters.AddWithValue("$clave", clave);
        comando.Parameters.AddWithValue("$valor", valor);
        comando.ExecuteNonQuery();
    }
}
