using System.Globalization;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Mensajero.Core;

/// <summary>
/// Datos propios de Mensajero (fase A, D-65 y D-68): viven en la carpeta común de
/// Hormiguero (<c>mensajero.db</c>, con respaldo diario). Lo que traía Ofisuiza en archivos
/// sueltos junto al programa (carpeta de OCC en <c>settings.json</c>, clientes NVV en
/// <c>clientes.txt</c>) y los clientes de ClickFactura (<c>clickfactura.db</c>) pasan aquí,
/// sin borrar ni modificar los originales.
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
                + "CREATE TABLE IF NOT EXISTS clientes_nvv(orden INTEGER PRIMARY KEY, linea TEXT NOT NULL); "
                // Mismas columnas que la tabla clientes de ClickFactura (D-68: se conservan tal cual).
                + "CREATE TABLE IF NOT EXISTS clientes_factura(rut TEXT PRIMARY KEY, razon_social TEXT NOT NULL, "
                + "correo TEXT NOT NULL, activo INTEGER NOT NULL DEFAULT 1, creado TEXT NOT NULL, actualizado TEXT NOT NULL);"
        );
    }

    /// <summary>Abre los datos de Mensajero en la carpeta común (respeta HORMIGUERO_DATOS).</summary>
    public static AlmacenMensajero AbrirComun() =>
        new(DatosDeApp.Preparar("mensajero", rutaAnterior: null));

    public string LeerCarpetaOcc() => LeerValor(ClaveCarpetaOcc);

    public void GuardarCarpetaOcc(string carpeta) => GuardarValor(ClaveCarpetaOcc, carpeta);

    /// <summary>Valor de configuración guardado con esa clave ("" si no hay).</summary>
    public string LeerValor(string clave)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT valor FROM configuracion WHERE clave = $clave;";
        comando.Parameters.AddWithValue("$clave", clave);
        return comando.ExecuteScalar() as string ?? "";
    }

    /// <summary>Guarda (o reemplaza) un valor de configuración, p. ej. una carpeta elegida.</summary>
    public void GuardarValor(string clave, string valor)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "INSERT INTO configuracion(clave, valor) VALUES ($clave, $valor) "
            + "ON CONFLICT(clave) DO UPDATE SET valor = excluded.valor;";
        comando.Parameters.AddWithValue("$clave", clave);
        comando.Parameters.AddWithValue("$valor", valor);
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

    /// <summary>Clientes activos de ClickFactura ordenados por razón social (como el original).</summary>
    public IReadOnlyList<ClickFactura.ClienteFactura> LeerClientesFactura() =>
        ConsultarClientesFactura(
            "SELECT rut, razon_social, correo FROM clientes_factura WHERE activo = 1 ORDER BY razon_social;",
            null
        );

    /// <summary>Cliente activo con ese RUT (ya normalizado), o null.</summary>
    public ClickFactura.ClienteFactura? BuscarClienteFactura(string rut) =>
        ConsultarClientesFactura(
                "SELECT rut, razon_social, correo FROM clientes_factura WHERE rut = $rut AND activo = 1;",
                rut
            )
            .FirstOrDefault();

    /// <summary>
    /// Agrega un cliente o actualiza nombre y correo si el RUT ya existe; no cambia si está
    /// activo ni su fecha de creación (igual que <c>insertar_o_actualizar</c> de ClickFactura).
    /// </summary>
    public void GuardarClienteFactura(string rut, string razonSocial, string correo, DateTime ahora)
    {
        string fecha = ahora.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "INSERT INTO clientes_factura(rut, razon_social, correo, activo, creado, actualizado) "
            + "VALUES ($rut, $razon, $correo, 1, $fecha, $fecha) "
            + "ON CONFLICT(rut) DO UPDATE SET razon_social = excluded.razon_social, "
            + "correo = excluded.correo, actualizado = excluded.actualizado;";
        comando.Parameters.AddWithValue("$rut", rut);
        comando.Parameters.AddWithValue("$razon", razonSocial);
        comando.Parameters.AddWithValue("$correo", correo);
        comando.Parameters.AddWithValue("$fecha", fecha);
        comando.ExecuteNonQuery();
    }

    /// <summary>
    /// Trae los clientes desde la base de ClickFactura (<c>clickfactura.db</c>), solo si
    /// Mensajero todavía no tiene ninguno: nunca pisa datos existentes. Se lee una copia
    /// temporal de la base (y de su <c>-wal</c> si existe), así el original no se abre ni se
    /// toca. Conserva todo: también los inactivos y las fechas. Devuelve cuántos importó.
    /// </summary>
    public int ImportarClientesFactura(string rutaBaseClickFactura)
    {
        if (ContarClientesFactura() > 0 || !File.Exists(rutaBaseClickFactura))
        {
            return 0;
        }

        string temporal = Path.Combine(
            Path.GetTempPath(),
            "hormiguero-importar-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(temporal);
        try
        {
            string copia = Path.Combine(temporal, "clickfactura.db");
            File.Copy(rutaBaseClickFactura, copia);
            if (File.Exists(rutaBaseClickFactura + "-wal"))
            {
                File.Copy(rutaBaseClickFactura + "-wal", copia + "-wal");
            }

            var filas = new List<(string, string, string, long, string, string)>();
            using (
                var origen = new SqliteConnection(
                    new SqliteConnectionStringBuilder
                    {
                        DataSource = copia,
                        Pooling = false,
                    }.ToString()
                )
            )
            {
                origen.Open();
                using var leer = origen.CreateCommand();
                leer.CommandText =
                    "SELECT rut, razon_social, correo, activo, created_at, updated_at FROM clientes;";
                using var lector = leer.ExecuteReader();
                while (lector.Read())
                {
                    filas.Add(
                        (
                            lector.GetString(0),
                            lector.GetString(1),
                            lector.GetString(2),
                            lector.GetInt64(3),
                            lector.GetString(4),
                            lector.GetString(5)
                        )
                    );
                }
            }

            using var transaccion = conexion.BeginTransaction();
            foreach (var (rut, razon, correo, activo, creado, actualizado) in filas)
            {
                using var insertar = conexion.CreateCommand();
                insertar.Transaction = transaccion;
                insertar.CommandText =
                    "INSERT INTO clientes_factura(rut, razon_social, correo, activo, creado, actualizado) "
                    + "VALUES ($rut, $razon, $correo, $activo, $creado, $actualizado);";
                insertar.Parameters.AddWithValue("$rut", rut);
                insertar.Parameters.AddWithValue("$razon", razon);
                insertar.Parameters.AddWithValue("$correo", correo);
                insertar.Parameters.AddWithValue("$activo", activo);
                insertar.Parameters.AddWithValue("$creado", creado);
                insertar.Parameters.AddWithValue("$actualizado", actualizado);
                insertar.ExecuteNonQuery();
            }
            transaccion.Commit();
            return filas.Count;
        }
        finally
        {
            Directory.Delete(temporal, recursive: true);
        }
    }

    private long ContarClientesFactura()
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM clientes_factura;";
        return (long)comando.ExecuteScalar()!;
    }

    private List<ClickFactura.ClienteFactura> ConsultarClientesFactura(string sql, string? rut)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        if (rut is not null)
        {
            comando.Parameters.AddWithValue("$rut", rut);
        }
        using var lector = comando.ExecuteReader();
        var lista = new List<ClickFactura.ClienteFactura>();
        while (lector.Read())
        {
            lista.Add(new(lector.GetString(0), lector.GetString(1), lector.GetString(2)));
        }
        return lista;
    }

    public void Dispose() => conexion.Dispose();

    private void Ejecutar(string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        comando.ExecuteNonQuery();
    }
}
