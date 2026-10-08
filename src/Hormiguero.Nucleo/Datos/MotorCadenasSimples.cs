using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed class MotorCadenasSimples(SqliteConnection conexion)
{
    public int Procesar(long? versionId = null)
    {
        int enlaces = 0;
        foreach (long version in LeerVersiones(versionId))
        {
            if (TieneHistorial(version))
                continue;
            var exactas = new Dictionary<(string Dato, string Valor), HashSet<long>>();
            var soloLimpias = new List<(string Dato, string Nuevo, string Anterior, long Cadena)>();
            foreach (var valor in LeerValores(version))
            {
                using var cmd = conexion.CreateCommand();
                cmd.CommandText =
                    "SELECT DISTINCT c.id,x.valor_original FROM cadenas c JOIN vagones_cadena v ON v.cadena_id=c.id JOIN enlaces_cadena e ON e.vagon_cadena_id=v.id JOIN valores_documento x ON x.version_id=e.version_id JOIN campos_documento campo ON campo.id=x.campo_id JOIN versiones_documento ver ON ver.id=e.version_id WHERE c.estado='activa' AND c.modelo_id IS NULL AND v.estado='activo' AND e.estado='activo' AND ver.estado='vigente' AND e.version_id<>$version AND x.estado='vigente' AND x.dato_diccionario_id=$dato AND x.valor_clave=$clave AND EXISTS(SELECT 1 FROM tipos_documento_datos t WHERE t.identificacion_id=campo.identificacion_id AND t.dato_diccionario_id=x.dato_diccionario_id AND t.activo=1 AND t.enlazable=1);";
                cmd.Parameters.AddWithValue("$version", version);
                cmd.Parameters.AddWithValue("$dato", valor.Dato);
                cmd.Parameters.AddWithValue("$clave", valor.Clave);
                var literales = new Dictionary<long, List<string>>();
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        long cadenaId = r.GetInt64(0);
                        if (!literales.TryGetValue(cadenaId, out var valoresLiterales))
                            literales[cadenaId] = valoresLiterales = [];
                        valoresLiterales.Add(r.GetString(1));
                    }
                var literalNormalizado = DiccionarioDatosEnlazantes.ValorLiteralNormalizado(
                    valor.Original
                );
                var mismos = literales
                    .Where(x =>
                        x.Value.Any(v =>
                            DiccionarioDatosEnlazantes.ValorLiteralNormalizado(v)
                            == literalNormalizado
                        )
                    )
                    .Select(x => x.Key)
                    .ToHashSet();
                if (mismos.Count > 0)
                    exactas[(valor.Dato, valor.Original)] = mismos;
                foreach (var anterior in literales.Where(x => !mismos.Contains(x.Key)))
                    soloLimpias.Add((valor.Dato, valor.Original, anterior.Value[0], anterior.Key));
            }
            var cadenas = exactas.Values.SelectMany(x => x).ToHashSet();
            // Un calce solo al limpiar con la misma cadena que ya calza exacto no genera duda.
            soloLimpias.RemoveAll(x => cadenas.Contains(x.Cadena));
            if (soloLimpias.Count > 0 || cadenas.Count > 1)
            {
                string motivo =
                    soloLimpias.Count > 0
                        ? $"Calza solo al limpiar: «{soloLimpias[0].Nuevo}» ↔ «{soloLimpias[0].Anterior}»"
                        : MotivoVariasCadenas(exactas, cadenas);
                long destino = cadenas
                    .Concat(soloLimpias.Select(x => x.Cadena))
                    .DefaultIfEmpty()
                    .Min();
                if (destino == 0)
                    continue;
                long vagonId = CrearVagon(destino, version);
                new RepositorioReglasYEnlaces(conexion).CrearDudoso(vagonId, version, null, motivo);
                continue;
            }
            if (cadenas.Count == 1)
            {
                AgregarYEnlazar(cadenas.Single(), version);
                enlaces++;
            }
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

    private static string MotivoVariasCadenas(
        Dictionary<(string Dato, string Valor), HashSet<long>> coincidencias,
        HashSet<long> cadenas
    )
    {
        var ambiguo = coincidencias.FirstOrDefault(x => x.Value.Count > 1);
        if (!string.IsNullOrEmpty(ambiguo.Key.Dato))
        {
            string nombre = DiccionarioDatosEnlazantes
                .Todos.First(d => d.Id == ambiguo.Key.Dato)
                .Nombre;
            return $"Coincide con {ambiguo.Value.Count} cadenas por {nombre} {ambiguo.Key.Valor}";
        }
        return $"Coincide con {cadenas.Count} cadenas por más de un dato del documento";
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
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM enlaces_cadena WHERE version_id=$v);";
        cmd.Parameters.AddWithValue("$v", versionId);
        return Convert.ToInt32(cmd.ExecuteScalar()) != 0;
    }

    private IReadOnlyList<(string Dato, string Original, string Clave)> LeerValores(long versionId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT DISTINCT x.dato_diccionario_id,x.valor_original,x.valor_clave FROM valores_documento x JOIN campos_documento c ON c.id=x.campo_id WHERE x.version_id=$v AND x.estado='vigente' AND x.dato_diccionario_id IS NOT NULL AND x.valor_clave<>'' AND EXISTS(SELECT 1 FROM tipos_documento_datos t WHERE t.identificacion_id=c.identificacion_id AND t.dato_diccionario_id=x.dato_diccionario_id AND t.activo=1 AND t.enlazable=1) ORDER BY x.dato_diccionario_id;";
        cmd.Parameters.AddWithValue("$v", versionId);
        using var r = cmd.ExecuteReader();
        var resultado = new List<(string, string, string)>();
        while (r.Read())
            resultado.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
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
