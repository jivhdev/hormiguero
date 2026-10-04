namespace Archivero.Datos;

public class ConfiguracionRepository
{
    public string? Obtener(string clave)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT Valor FROM Configuracion WHERE Clave = $clave";
        comando.Parameters.AddWithValue("$clave", clave);
        return comando.ExecuteScalar() as string;
    }

    public void Guardar(string clave, string valor)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            INSERT INTO Configuracion (Clave, Valor) VALUES ($clave, $valor)
            ON CONFLICT(Clave) DO UPDATE SET Valor = excluded.Valor;
            """;
        comando.Parameters.AddWithValue("$clave", clave);
        comando.Parameters.AddWithValue("$valor", valor);
        comando.ExecuteNonQuery();
    }

    /// <summary>Suma 1 a un contador guardado bajo esa clave (arranca en 0 si todavía no existía). Usado por Caso-8 para el total histórico de documentos archivados.</summary>
    public void IncrementarContador(string clave)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            INSERT INTO Configuracion (Clave, Valor) VALUES ($clave, '1')
            ON CONFLICT(Clave) DO UPDATE SET Valor = CAST(CAST(Valor AS INTEGER) + 1 AS TEXT);
            """;
        comando.Parameters.AddWithValue("$clave", clave);
        comando.ExecuteNonQuery();
    }

    public int ObtenerContador(string clave) => int.TryParse(Obtener(clave), out var valor) ? valor : 0;
}
