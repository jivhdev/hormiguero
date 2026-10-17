using Microsoft.Data.Sqlite;

namespace Archivero.Datos;

public sealed record ConfiguracionCadena(string Alcance, string? Proveedor);

public sealed class ConfiguracionCadenaRepository
{
    public void Eliminar(int configuracionId)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "DELETE FROM ConfiguracionCadenas WHERE ConfiguracionId=$id;";
        comando.Parameters.AddWithValue("$id", configuracionId);
        comando.ExecuteNonQuery();
    }

    public void Guardar(int configuracionId, string alcance, string? proveedor)
    {
        if (alcance is not ("todas" or "esquema"))
            throw new ArgumentException("El alcance de cadena no es válido.", nameof(alcance));
        if (alcance == "esquema" && string.IsNullOrWhiteSpace(proveedor))
            throw new ArgumentException("Indica el proveedor del esquema.", nameof(proveedor));
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "INSERT INTO ConfiguracionCadenas(ConfiguracionId,Alcance,Proveedor) VALUES($id,$a,$p) ON CONFLICT(ConfiguracionId) DO UPDATE SET Alcance=excluded.Alcance,Proveedor=excluded.Proveedor;";
        comando.Parameters.AddWithValue("$id", configuracionId);
        comando.Parameters.AddWithValue("$a", alcance);
        comando.Parameters.AddWithValue(
            "$p",
            alcance == "todas" ? DBNull.Value : proveedor!.Trim()
        );
        comando.ExecuteNonQuery();
    }

    public ConfiguracionCadena? Obtener(int configuracionId)
    {
        using var conexion = BaseDeDatos.CrearConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT Alcance,Proveedor FROM ConfiguracionCadenas WHERE ConfiguracionId=$id;";
        comando.Parameters.AddWithValue("$id", configuracionId);
        using var lector = comando.ExecuteReader();
        return lector.Read()
            ? new ConfiguracionCadena(
                lector.GetString(0),
                lector.IsDBNull(1) ? null : lector.GetString(1)
            )
            : null;
    }
}
