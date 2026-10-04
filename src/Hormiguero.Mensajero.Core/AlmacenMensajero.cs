using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Mensajero.Core;

/// <summary>
/// Datos propios de Mensajero (fase A, D-65 y D-68): viven en la carpeta común de
/// Hormiguero (<c>mensajero.db</c>, con respaldo diario). Lo que traía Ofisuiza en archivos
/// sueltos junto al programa (carpeta de OCC en <c>settings.json</c>, clientes NVV en
/// <c>clientes.txt</c>) pasa aquí, sin borrar ni modificar los originales.
/// </summary>
public sealed class AlmacenMensajero : IDisposable
{
    private const string ClaveCarpetaOcc = "carpeta_occ";

    private readonly SqliteConnection conexion;

    public AlmacenMensajero(string rutaBase)
    {
        conexion = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = rutaBase }.ToString()
        );
        conexion.Open();
        Ejecutar(
            "PRAGMA journal_mode=WAL; "
                + "CREATE TABLE IF NOT EXISTS configuracion(clave TEXT PRIMARY KEY, valor TEXT NOT NULL); "
                + "CREATE TABLE IF NOT EXISTS clientes_nvv(orden INTEGER PRIMARY KEY, linea TEXT NOT NULL);"
        );
    }

    /// <summary>Abre los datos de Mensajero en la carpeta común (respeta HORMIGUERO_DATOS).</summary>
    public static AlmacenMensajero AbrirComun() =>
        new(DatosDeApp.Preparar("mensajero", rutaAnterior: null));

    public string LeerCarpetaOcc()
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT valor FROM configuracion WHERE clave = $clave;";
        comando.Parameters.AddWithValue("$clave", ClaveCarpetaOcc);
        return comando.ExecuteScalar() as string ?? "";
    }

    public void GuardarCarpetaOcc(string carpeta)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "INSERT INTO configuracion(clave, valor) VALUES ($clave, $valor) "
            + "ON CONFLICT(clave) DO UPDATE SET valor = excluded.valor;";
        comando.Parameters.AddWithValue("$clave", ClaveCarpetaOcc);
        comando.Parameters.AddWithValue("$valor", carpeta);
        comando.ExecuteNonQuery();
    }

    /// <summary>Clientes NVV en su orden. Vacío si todavía no hay ninguno guardado.</summary>
    public IReadOnlyList<string> LeerClientesNvv()
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT linea FROM clientes_nvv ORDER BY orden;";
        using var lector = comando.ExecuteReader();
        var lista = new List<string>();
        while (lector.Read())
        {
            lista.Add(lector.GetString(0));
        }
        return lista;
    }

    /// <summary>Reemplaza la lista completa (como el editor de Ofisuiza): sin líneas vacías ni espacios sobrantes.</summary>
    public void GuardarClientesNvv(IEnumerable<string> lineas)
    {
        using var transaccion = conexion.BeginTransaction();
        using (var borrar = conexion.CreateCommand())
        {
            borrar.Transaction = transaccion;
            borrar.CommandText = "DELETE FROM clientes_nvv;";
            borrar.ExecuteNonQuery();
        }

        int orden = 0;
        foreach (string linea in lineas.Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            using var insertar = conexion.CreateCommand();
            insertar.Transaction = transaccion;
            insertar.CommandText =
                "INSERT INTO clientes_nvv(orden, linea) VALUES ($orden, $linea);";
            insertar.Parameters.AddWithValue("$orden", orden++);
            insertar.Parameters.AddWithValue("$linea", linea);
            insertar.ExecuteNonQuery();
        }
        transaccion.Commit();
    }

    /// <summary>
    /// Trae los clientes NVV desde el <c>clientes.txt</c> de Ofisuiza, solo si Mensajero
    /// todavía no tiene ninguno (nunca pisa una lista existente). El archivo original no se
    /// toca. Devuelve cuántos importó (0 si ya había lista o el archivo no existe).
    /// </summary>
    public int ImportarClientesNvv(string rutaClientesTxt)
    {
        if (LeerClientesNvv().Count > 0 || !File.Exists(rutaClientesTxt))
        {
            return 0;
        }

        string[] lineas = File.ReadAllLines(rutaClientesTxt, System.Text.Encoding.UTF8)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
        GuardarClientesNvv(lineas);
        return lineas.Length;
    }

    public void Dispose() => conexion.Dispose();

    private void Ejecutar(string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        comando.ExecuteNonQuery();
    }
}
