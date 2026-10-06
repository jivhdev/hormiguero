using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed class MotorCadenasSimples(SqliteConnection conexion)
{
    public int Procesar(long? versionId = null)
    {
        var pendientes = LeerVersiones(versionId);
        int enlaces = 0;
        foreach (var version in pendientes)
        {
            if (TieneHistorial(version))
                continue;
            var valores = LeerValores(version);
            var coincidencias = new Dictionary<(string Dato, string Valor), HashSet<long>>();
            foreach (var (dato, valor) in valores)
            {
                using var cmd = conexion.CreateCommand();
                cmd.CommandText =
                    "SELECT DISTINCT c.id FROM cadenas c JOIN vagones_cadena v ON v.cadena_id=c.id JOIN enlaces_cadena e ON e.vagon_cadena_id=v.id JOIN valores_documento x ON x.version_id=e.version_id JOIN versiones_documento ver ON ver.id=e.version_id WHERE c.estado='activa' AND c.modelo_id IS NULL AND v.estado='activo' AND e.estado='activo' AND ver.estado='vigente' AND e.version_id<>$version AND x.estado='vigente' AND x.dato_diccionario_id=$dato AND x.valor_clave=$valor;";
                cmd.Parameters.AddWithValue("$version", version);
                cmd.Parameters.AddWithValue("$dato", dato);
                cmd.Parameters.AddWithValue("$valor", valor);
                using var r = cmd.ExecuteReader();
                var ids = new HashSet<long>();
                while (r.Read())
                    ids.Add(r.GetInt64(0));
                if (ids.Count > 0)
                    coincidencias[(dato, valor)] = ids;
            }
            var cadenas = coincidencias.Values.SelectMany(x => x).ToHashSet();
            if (cadenas.Count == 0)
                continue;
            if (cadenas.Count == 1)
            {
                AgregarYEnlazar(cadenas.Single(), version);
                enlaces++;
                continue;
            }
            var datoAmbiguo = coincidencias.FirstOrDefault(x => x.Value.Count > 1).Key;
            string motivo;
            if (!string.IsNullOrEmpty(datoAmbiguo.Dato))
            {
                string nombre = DiccionarioDatosEnlazantes
                    .Todos.First(d => d.Id == datoAmbiguo.Dato)
                    .Nombre;
                int cantidad = coincidencias[datoAmbiguo].Count;
                motivo = $"Coincide con {cantidad} cadenas por {nombre} {datoAmbiguo.Valor}";
            }
            else
                motivo = $"Coincide con {cadenas.Count} cadenas por más de un dato del documento";
            long destino = cadenas.Min();
            long vagonId = CrearVagon(destino, version);
            new RepositorioReglasYEnlaces(conexion).CrearDudoso(vagonId, version, null, motivo);
        }
        return enlaces;
    }

    public void RegistrarError(Exception error, long? versionId)
    {
        using var tx = conexion.BeginTransaction();
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "error_enlace_cadena_simple",
            $"version:{versionId?.ToString() ?? "pendiente"}",
            error.Message,
            app: "Archivero"
        );
        tx.Commit();
    }

    private IReadOnlyList<long> LeerVersiones(long? versionId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id FROM versiones_documento WHERE estado='vigente'"
            + (versionId is null ? " ORDER BY id;" : " AND id=$id;");
        cmd.Parameters.AddWithValue("$id", (object?)versionId ?? DBNull.Value);
        using var r = cmd.ExecuteReader();
        var resultado = new List<long>();
        while (r.Read())
            resultado.Add(r.GetInt64(0));
        return resultado;
    }

    private bool TieneHistorial(long versionId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM enlaces_cadena WHERE version_id=$v LIMIT 1;";
        cmd.Parameters.AddWithValue("$v", versionId);
        return cmd.ExecuteScalar() is not null;
    }

    private IReadOnlyList<(string Dato, string Valor)> LeerValores(long versionId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT DISTINCT dato_diccionario_id,valor_clave FROM valores_documento WHERE version_id=$v AND estado='vigente' AND dato_diccionario_id IS NOT NULL AND valor_clave<>'' ORDER BY dato_diccionario_id;";
        cmd.Parameters.AddWithValue("$v", versionId);
        using var r = cmd.ExecuteReader();
        var resultado = new List<(string, string)>();
        while (r.Read())
            resultado.Add((r.GetString(0), r.GetString(1)));
        return resultado;
    }

    private void AgregarYEnlazar(long cadenaId, long versionId)
    {
        long vagonId = CrearVagon(cadenaId, versionId);
        new RepositorioReglasYEnlaces(conexion).CrearEnlace(vagonId, versionId, "automatico");
    }

    private long CrearVagon(long cadenaId, long versionId)
    {
        using var tx = conexion.BeginTransaction();
        using var nombre = conexion.CreateCommand();
        nombre.Transaction = tx;
        nombre.CommandText =
            "SELECT d.nombre FROM versiones_documento v JOIN documentos d ON d.id=v.documento_id WHERE v.id=$v;";
        nombre.Parameters.AddWithValue("$v", versionId);
        string documento = Convert.ToString(nombre.ExecuteScalar()) ?? "Documento";
        using var insertar = conexion.CreateCommand();
        insertar.Transaction = tx;
        insertar.CommandText =
            "INSERT INTO vagones_cadena(cadena_id,orden,nombre) SELECT $c,COALESCE(MAX(orden),-1)+1,$n FROM vagones_cadena WHERE cadena_id=$c AND padre_id IS NULL AND estado='activo' RETURNING id;";
        insertar.Parameters.AddWithValue("$c", cadenaId);
        insertar.Parameters.AddWithValue("$n", documento);
        long id = Convert.ToInt64(insertar.ExecuteScalar());
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "agregar_documento_cadena_automatico",
            $"cadena:{cadenaId}",
            $"vagon:{id}",
            app: "Archivero"
        );
        tx.Commit();
        return id;
    }
}
