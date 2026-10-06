using System.Globalization;
using System.IO;
using Hormiguero.Mensajero.Core;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Reinicio;

public sealed record ResultadoReinicio(
    bool Exitoso,
    int ClientesConservados,
    string RutaRespaldo,
    string Mensaje
);

public sealed class ReinicioServicio
{
    private readonly Func<string, string, int> importarClientes;

    public ReinicioServicio()
        : this(
            (rutaMensajero, rutaClickFactura) =>
            {
                using var almacen = new AlmacenMensajero(rutaMensajero);
                return almacen.ImportarClientesFactura(rutaClickFactura);
            }
        ) { }

    public ReinicioServicio(Func<string, string, int> importarClientes)
    {
        this.importarClientes = importarClientes;
    }

    public static string RutaRespaldo(string carpeta, DateTime fecha) =>
        carpeta.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        + "-respaldo-reinicio-"
        + fecha.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

    public ResultadoReinicio Ejecutar(string carpeta, DateTime fecha)
    {
        string respaldo = RutaRespaldo(carpeta, fecha);
        bool carpetaRenombrada = false;
        try
        {
            if (Directory.Exists(respaldo) || File.Exists(respaldo))
            {
                throw new IOException("Ya existe una carpeta de respaldo con ese nombre.");
            }

            Directory.CreateDirectory(carpeta);
            List<ClienteConservado> clientes = LeerClientes(Path.Combine(carpeta, "mensajero.db"));
            Directory.Move(carpeta, respaldo);
            carpetaRenombrada = true;
            Directory.CreateDirectory(carpeta);

            string rutaClickFactura = Path.Combine(
                Path.GetTempPath(),
                "hormiguero-reinicio-" + Guid.NewGuid().ToString("N") + ".db"
            );
            try
            {
                EscribirClientes(rutaClickFactura, clientes);
                int importados = importarClientes(
                    Path.Combine(carpeta, "mensajero.db"),
                    rutaClickFactura
                );
                if (importados != clientes.Count)
                {
                    throw new InvalidOperationException(
                        $"Se leyeron {clientes.Count} clientes y se restauraron {importados}."
                    );
                }
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(rutaClickFactura))
                {
                    File.Delete(rutaClickFactura);
                }
            }

            DatosDeApp.MarcarReinicio(carpeta, fecha);
            return new(true, clientes.Count, respaldo, "");
        }
        catch (Exception error)
        {
            string mensaje = $"No se pudo reiniciar Hormiguero: {error.Message}";
            if (carpetaRenombrada)
            {
                try
                {
                    SqliteConnection.ClearAllPools();
                    if (Directory.Exists(carpeta))
                    {
                        string fallido = RutaRespaldo(carpeta, fecha) + "-fallido";
                        int sufijo = 1;
                        while (Directory.Exists(fallido) || File.Exists(fallido))
                        {
                            fallido = RutaRespaldo(carpeta, fecha) + $"-fallido-{sufijo++}";
                        }
                        Directory.Move(carpeta, fallido);
                    }
                    Directory.Move(respaldo, carpeta);
                }
                catch (Exception errorReversion)
                {
                    mensaje +=
                        $" No se pudo completar la reversión: {errorReversion.Message}. El respaldo original permanece en: {respaldo}";
                }
            }
            return new(false, 0, respaldo, mensaje);
        }
    }

    private static List<ClienteConservado> LeerClientes(string ruta)
    {
        if (!File.Exists(ruta))
        {
            return [];
        }

        using var conexion = Abrir(ruta, SqliteOpenMode.ReadOnly);
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT rut, razon_social, correo, activo, creado, actualizado FROM clientes_factura;";
        using var lector = comando.ExecuteReader();
        var clientes = new List<ClienteConservado>();
        while (lector.Read())
        {
            clientes.Add(
                new(
                    lector.GetString(0),
                    lector.GetString(1),
                    lector.GetString(2),
                    lector.GetInt64(3),
                    lector.GetString(4),
                    lector.GetString(5)
                )
            );
        }
        return clientes;
    }

    private static void EscribirClientes(string ruta, IEnumerable<ClienteConservado> clientes)
    {
        using var conexion = Abrir(ruta, SqliteOpenMode.ReadWriteCreate);
        using (var esquema = conexion.CreateCommand())
        {
            esquema.CommandText =
                "CREATE TABLE clientes(rut TEXT PRIMARY KEY, razon_social TEXT NOT NULL, correo TEXT NOT NULL, "
                + "activo INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, updated_at TEXT NOT NULL);";
            esquema.ExecuteNonQuery();
        }

        using var transaccion = conexion.BeginTransaction();
        foreach (ClienteConservado cliente in clientes)
        {
            using var insertar = conexion.CreateCommand();
            insertar.Transaction = transaccion;
            insertar.CommandText =
                "INSERT INTO clientes(rut, razon_social, correo, activo, created_at, updated_at) "
                + "VALUES ($rut, $razon, $correo, $activo, $creado, $actualizado);";
            insertar.Parameters.AddWithValue("$rut", cliente.Rut);
            insertar.Parameters.AddWithValue("$razon", cliente.RazonSocial);
            insertar.Parameters.AddWithValue("$correo", cliente.Correo);
            insertar.Parameters.AddWithValue("$activo", cliente.Activo);
            insertar.Parameters.AddWithValue("$creado", cliente.Creado);
            insertar.Parameters.AddWithValue("$actualizado", cliente.Actualizado);
            insertar.ExecuteNonQuery();
        }
        transaccion.Commit();
    }

    private static SqliteConnection Abrir(string ruta, SqliteOpenMode modo)
    {
        var conexion = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = ruta,
                Mode = modo,
                Pooling = false,
            }.ToString()
        );
        conexion.Open();
        return conexion;
    }

    private sealed record ClienteConservado(
        string Rut,
        string RazonSocial,
        string Correo,
        long Activo,
        string Creado,
        string Actualizado
    );
}
