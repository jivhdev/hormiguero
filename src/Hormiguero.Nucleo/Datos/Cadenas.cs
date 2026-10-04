using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed record ModeloCadena(
    long Id,
    string Nombre,
    DateTime FechaCreacion,
    bool EsModeloHijo,
    int PreferenciaNombre,
    long? VagonNombreId
);

public sealed record VagonModelo(
    long Id,
    long ModeloId,
    long? PadreId,
    int Orden,
    string Nombre,
    bool EsMultiple,
    bool EsAnexo,
    long? ModeloCadenaHijaId
);

public sealed record NodoVagonModelo(VagonModelo Vagon, IReadOnlyList<NodoVagonModelo> Hijos);

public sealed record Cadena(
    long Id,
    long? ModeloId,
    string? NombreModeloOrigen,
    string Nombre,
    DateTime FechaCreacion,
    string EstructuraJson,
    long? CadenaMadreId,
    long? VagonPadreId,
    string Estado
);

public sealed record VagonCadena(
    long Id,
    long CadenaId,
    long? PadreId,
    long? VagonModeloId,
    int Orden,
    string Nombre,
    bool EsMultiple,
    bool EsAnexo,
    long? VersionId,
    string Estado
);

public sealed record NodoVagonCadena(VagonCadena Vagon, IReadOnlyList<NodoVagonCadena> Hijos);

public sealed class RepositorioCadenas(SqliteConnection conexion)
{
    public long CrearModelo(string nombre, DateTime fechaCreacion, bool esModeloHijo = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "INSERT INTO modelos_cadena(nombre, fecha_creacion, es_modelo_hijo) VALUES ($n,$f,$h) RETURNING id;";
        comando.Parameters.AddWithValue("$n", nombre.Trim());
        comando.Parameters.AddWithValue("$f", fechaCreacion.ToString("o"));
        comando.Parameters.AddWithValue("$h", esModeloHijo ? 1 : 0);
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    public long AgregarVagonModelo(
        long modeloId,
        long? padreId,
        string nombre,
        bool esMultiple = false,
        bool esAnexo = false,
        long? modeloCadenaHijaId = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        using var transaccion = conexion.BeginTransaction();
        using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText =
            "INSERT INTO vagones_modelo(modelo_id,padre_id,orden,nombre,es_multiple,es_anexo,modelo_cadena_hija_id) VALUES($m,$p,(SELECT COALESCE(MAX(orden),-1)+1 FROM vagones_modelo WHERE modelo_id=$m AND padre_id IS $p),$n,$x,$a,$h) RETURNING id;";
        comando.Parameters.AddWithValue("$m", modeloId);
        comando.Parameters.AddWithValue("$p", (object?)padreId ?? DBNull.Value);
        comando.Parameters.AddWithValue("$n", nombre.Trim());
        comando.Parameters.AddWithValue("$x", esMultiple ? 1 : 0);
        comando.Parameters.AddWithValue("$a", esAnexo ? 1 : 0);
        comando.Parameters.AddWithValue("$h", (object?)modeloCadenaHijaId ?? DBNull.Value);
        long id = Convert.ToInt64(comando.ExecuteScalar());
        transaccion.Commit();
        return id;
    }

    public IReadOnlyList<NodoVagonModelo> ObtenerArbolModelo(long modeloId) =>
        CrearArbol(
            LeerVagones(
                "SELECT id,modelo_id,padre_id,orden,nombre,es_multiple,es_anexo,modelo_cadena_hija_id FROM vagones_modelo WHERE modelo_id=$id ORDER BY orden,id",
                modeloId
            )
        );

    public long CrearCadena(
        long? modeloId,
        string nombre,
        DateTime fechaCreacion,
        long? cadenaMadreId = null,
        long? vagonPadreId = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        using var transaccion = conexion.BeginTransaction();
        List<VagonModelo> vagones = modeloId is null
            ? new()
            : LeerVagones(
                "SELECT id,modelo_id,padre_id,orden,nombre,es_multiple,es_anexo,modelo_cadena_hija_id FROM vagones_modelo WHERE modelo_id=$id ORDER BY orden,id",
                modeloId.Value,
                transaccion
            );
        string nombreModelo = "";
        if (modeloId is not null)
        {
            using var consulta = conexion.CreateCommand();
            consulta.Transaction = transaccion;
            consulta.CommandText = "SELECT nombre FROM modelos_cadena WHERE id=$id;";
            consulta.Parameters.AddWithValue("$id", modeloId.Value);
            nombreModelo =
                (string?)consulta.ExecuteScalar()
                ?? throw new InvalidOperationException("No existe el modelo de cadena.");
        }
        var estructura = vagones
            .Select(v => new
            {
                v.Id,
                v.PadreId,
                v.Orden,
                v.Nombre,
                v.EsMultiple,
                v.EsAnexo,
                v.ModeloCadenaHijaId,
            })
            .ToArray();
        using var crear = conexion.CreateCommand();
        crear.Transaction = transaccion;
        crear.CommandText =
            "INSERT INTO cadenas(modelo_id,nombre_modelo_origen,nombre,fecha_creacion,estructura_json,cadena_madre_id,vagon_padre_id) VALUES($m,$o,$n,$f,$j,$madre,$padre) RETURNING id;";
        crear.Parameters.AddWithValue("$m", (object?)modeloId ?? DBNull.Value);
        crear.Parameters.AddWithValue("$o", (object?)nombreModelo ?? DBNull.Value);
        crear.Parameters.AddWithValue("$n", nombre.Trim());
        crear.Parameters.AddWithValue("$f", fechaCreacion.ToString("o"));
        crear.Parameters.AddWithValue("$j", JsonSerializer.Serialize(estructura));
        crear.Parameters.AddWithValue("$madre", (object?)cadenaMadreId ?? DBNull.Value);
        crear.Parameters.AddWithValue("$padre", (object?)vagonPadreId ?? DBNull.Value);
        long cadenaId = Convert.ToInt64(crear.ExecuteScalar());
        var ids = new Dictionary<long, long>();
        int Profundidad(VagonModelo v) =>
            v.PadreId is null ? 0 : 1 + Profundidad(vagones.Single(p => p.Id == v.PadreId.Value));
        foreach (var v in vagones.OrderBy(Profundidad).ThenBy(v => v.Orden).ThenBy(v => v.Id))
        {
            using var agregar = conexion.CreateCommand();
            agregar.Transaction = transaccion;
            agregar.CommandText =
                "INSERT INTO vagones_cadena(cadena_id,padre_id,vagon_modelo_id,orden,nombre,es_multiple,es_anexo) VALUES($c,$p,$m,$o,$n,$x,$a) RETURNING id;";
            agregar.Parameters.AddWithValue("$c", cadenaId);
            agregar.Parameters.AddWithValue(
                "$p",
                v.PadreId is null ? DBNull.Value : ids[v.PadreId.Value]
            );
            agregar.Parameters.AddWithValue("$m", v.Id);
            agregar.Parameters.AddWithValue("$o", v.Orden);
            agregar.Parameters.AddWithValue("$n", v.Nombre);
            agregar.Parameters.AddWithValue("$x", v.EsMultiple ? 1 : 0);
            agregar.Parameters.AddWithValue("$a", v.EsAnexo ? 1 : 0);
            ids[v.Id] = Convert.ToInt64(agregar.ExecuteScalar());
        }
        transaccion.Commit();
        return cadenaId;
    }

    public IReadOnlyList<NodoVagonCadena> ObtenerArbolCadena(long cadenaId) =>
        CrearArbolCadena(LeerVagonesCadena(cadenaId));

    public Cadena? ObtenerCadena(long id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,modelo_id,nombre_modelo_origen,nombre,fecha_creacion,estructura_json,cadena_madre_id,vagon_padre_id,estado FROM cadenas WHERE id=$id AND estado='activa';";
        comando.Parameters.AddWithValue("$id", id);
        using var lector = comando.ExecuteReader();
        return lector.Read()
            ? new(
                lector.GetInt64(0),
                NuloLong(lector, 1),
                NuloTexto(lector, 2),
                lector.GetString(3),
                DateTime.Parse(lector.GetString(4)),
                lector.GetString(5),
                NuloLong(lector, 6),
                NuloLong(lector, 7),
                lector.GetString(8)
            )
            : null;
    }

    private List<VagonModelo> LeerVagones(string sql, long id, SqliteTransaction? tx = null)
    {
        using var comando = conexion.CreateCommand();
        comando.Transaction = tx;
        comando.CommandText = sql;
        comando.Parameters.AddWithValue("$id", id);
        using var lector = comando.ExecuteReader();
        var lista = new List<VagonModelo>();
        while (lector.Read())
            lista.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    NuloLong(lector, 2),
                    lector.GetInt32(3),
                    lector.GetString(4),
                    lector.GetInt32(5) != 0,
                    lector.GetInt32(6) != 0,
                    NuloLong(lector, 7)
                )
            );
        return lista;
    }

    private static IReadOnlyList<NodoVagonModelo> CrearArbol(IReadOnlyList<VagonModelo> vagones)
    {
        IReadOnlyList<NodoVagonModelo> Construir(long? padreId) =>
            vagones
                .Where(v => v.PadreId == padreId)
                .OrderBy(v => v.Orden)
                .ThenBy(v => v.Id)
                .Select(v => new NodoVagonModelo(v, Construir(v.Id)))
                .ToArray();
        return Construir(null);
    }

    private List<VagonCadena> LeerVagonesCadena(long id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,cadena_id,padre_id,vagon_modelo_id,orden,nombre,es_multiple,es_anexo,version_id,estado FROM vagones_cadena WHERE cadena_id=$id AND estado='activo' ORDER BY orden,id;";
        comando.Parameters.AddWithValue("$id", id);
        using var lector = comando.ExecuteReader();
        var lista = new List<VagonCadena>();
        while (lector.Read())
            lista.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    NuloLong(lector, 2),
                    NuloLong(lector, 3),
                    lector.GetInt32(4),
                    lector.GetString(5),
                    lector.GetInt32(6) != 0,
                    lector.GetInt32(7) != 0,
                    NuloLong(lector, 8),
                    lector.GetString(9)
                )
            );
        return lista;
    }

    private static IReadOnlyList<NodoVagonCadena> CrearArbolCadena(
        IReadOnlyList<VagonCadena> vagones
    )
    {
        IReadOnlyList<NodoVagonCadena> Construir(long? padreId) =>
            vagones
                .Where(v => v.PadreId == padreId)
                .OrderBy(v => v.Orden)
                .ThenBy(v => v.Id)
                .Select(v => new NodoVagonCadena(v, Construir(v.Id)))
                .ToArray();
        return Construir(null);
    }

    private static long? NuloLong(SqliteDataReader l, int i) =>
        l.IsDBNull(i) ? null : l.GetInt64(i);

    private static string? NuloTexto(SqliteDataReader l, int i) =>
        l.IsDBNull(i) ? null : l.GetString(i);
}
