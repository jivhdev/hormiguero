using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Buscadero.Core.Lineas;

public sealed record TipoDatoCompartido(
    long IdentificacionA,
    long IdentificacionB,
    string TipoA,
    string EmisorA,
    string TipoB,
    string EmisorB,
    string DatoId,
    string DatoNombre
);

public sealed record CadenaVista(
    long Id,
    string Proveedor,
    string Nombre,
    string Estado,
    string? Cliente
);

public sealed record DudosoCadenaVista(
    long EnlaceId,
    long VersionId,
    string Nombre,
    string Ruta,
    string Motivo
);

public sealed record FilaArbolCadena(
    string Lugar,
    int Orden,
    IReadOnlyList<DocumentoArbolCadena> Documentos,
    bool EsPareja
);

public sealed record DocumentoArbolCadena(
    long? VersionId,
    string Texto,
    string? Ruta,
    string? Numero,
    DateTime? Fecha,
    long? LineaId,
    bool Falta
);

public static class AsistenteEsquemaCadena
{
    public static IReadOnlyList<DudosoCadenaVista> ListarDudosos(
        SqliteConnection conexion,
        long cadenaId
    )
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT e.id,e.version_id,d.nombre,d.ruta,COALESCE(e.motivo,'Revisar coincidencia') FROM enlaces_cadena e JOIN vagones_cadena v ON v.id=e.vagon_cadena_id JOIN versiones_documento ver ON ver.id=e.version_id JOIN documentos d ON d.id=ver.documento_id WHERE e.estado='dudoso' AND v.cadena_id=$c ORDER BY e.id;";
        comando.Parameters.AddWithValue("$c", cadenaId);
        using var lector = comando.ExecuteReader();
        var resultados = new List<DudosoCadenaVista>();
        while (lector.Read())
            resultados.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    lector.GetString(2),
                    lector.GetString(3),
                    lector.GetString(4)
                )
            );
        return resultados;
    }

    public static IReadOnlyList<CadenaVista> ListarCadenas(SqliteConnection conexion)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,proveedor,nombre,estado,cliente FROM cadenas WHERE esquema_id IS NOT NULL ORDER BY proveedor,cliente,nombre,id;";
        using var lector = comando.ExecuteReader();
        var resultados = new List<CadenaVista>();
        while (lector.Read())
            resultados.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetString(1),
                    lector.GetString(2),
                    lector.GetString(3),
                    lector.IsDBNull(4) ? null : lector.GetString(4)
                )
            );
        return resultados;
    }

    public static IReadOnlyList<TipoDatoCompartido> TiposQueCompartenDatos(
        SqliteConnection conexion
    )
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT DISTINCT a.identificacion_id,b.identificacion_id,ia.tipo,ia.emisor,ib.tipo,ib.emisor,a.dato_diccionario_id,d.nombre FROM tipos_documento_datos a JOIN tipos_documento_datos b ON b.dato_diccionario_id=a.dato_diccionario_id AND b.identificacion_id>a.identificacion_id AND b.activo=1 AND b.enlazable=1 JOIN identificaciones ia ON ia.id=a.identificacion_id JOIN identificaciones ib ON ib.id=b.identificacion_id JOIN diccionario_datos d ON d.id=a.dato_diccionario_id WHERE a.activo=1 AND a.enlazable=1 ORDER BY ia.emisor,ia.tipo,ib.emisor,ib.tipo,d.orden;";
        using var lector = comando.ExecuteReader();
        var resultados = new List<TipoDatoCompartido>();
        while (lector.Read())
            resultados.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    lector.GetString(2),
                    lector.GetString(3),
                    lector.GetString(4),
                    lector.GetString(5),
                    lector.GetString(6),
                    lector.GetString(7)
                )
            );
        return resultados;
    }

    public static string? Validar(
        string proveedor,
        IReadOnlyList<DefinicionLugarEsquema> lugares,
        IReadOnlyList<DefinicionParejaEsquema> parejas,
        IReadOnlyList<TipoDatoCompartido> compartidos
    )
    {
        if (string.IsNullOrWhiteSpace(proveedor))
            return "Escriba o seleccione un proveedor.";
        if (lugares.Count == 0)
            return "Agregue al menos un documento del proceso.";
        if (lugares.All(l => !l.IniciaCadena))
            return "Marque al menos un documento que inicie la cadena.";
        var porOrden = lugares.ToDictionary(l => l.Orden);
        var ordenes = porOrden.Keys.ToHashSet();
        foreach (var pareja in parejas)
        {
            if (
                !ordenes.Contains(pareja.OrdenA)
                || !ordenes.Contains(pareja.OrdenB)
                || pareja.OrdenA == pareja.OrdenB
            )
                return "Cada pareja debe unir dos lugares distintos del esquema.";
            bool existe = compartidos.Any(d =>
                d.DatoId == pareja.DatoDiccionarioId
                && (
                    (
                        porOrden[pareja.OrdenA].IdentificacionId == d.IdentificacionA
                        && porOrden[pareja.OrdenB].IdentificacionId == d.IdentificacionB
                    )
                    || (
                        porOrden[pareja.OrdenA].IdentificacionId == d.IdentificacionB
                        && porOrden[pareja.OrdenB].IdentificacionId == d.IdentificacionA
                    )
                )
            );
            if (!existe)
                return "La pareja debe usar un dato que compartan ambos tipos de documento.";
        }
        return null;
    }

    public static IReadOnlyList<FilaArbolCadena> ArmarArbol(
        EsquemaCadena esquema,
        IReadOnlyList<DocumentoLugarCadena> documentos,
        IReadOnlyDictionary<long, VersionDocumentoCadena> versiones
    )
    {
        var filas = new List<FilaArbolCadena>();
        foreach (var lugar in esquema.Lugares.OrderBy(l => l.Orden))
        {
            var pareja = esquema.Parejas.FirstOrDefault(p =>
                p.LugarA == lugar.Id || p.LugarB == lugar.Id
            );
            var asociados = documentos.Where(d => d.LugarId == lugar.Id).ToArray();
            if (asociados.Length == 0)
            {
                filas.Add(
                    new(
                        lugar.Nombre,
                        lugar.Orden,
                        [new(null, "Falta", null, null, null, null, true)],
                        pareja is not null
                    )
                );
                continue;
            }
            filas.Add(
                new(
                    lugar.Nombre,
                    lugar.Orden,
                    asociados
                        .Select(d =>
                        {
                            versiones.TryGetValue(d.VersionId, out var v);
                            var numero = v?.Numero ?? "";
                            string linea = d.LineaId is long lineaId ? $"Línea {lineaId} · " : "";
                            return new DocumentoArbolCadena(
                                d.VersionId,
                                $"{linea}{v?.Tipo ?? d.Lugar} · {numero} · {v?.Fecha:yyyy-MM-dd}",
                                v?.Ruta,
                                numero,
                                v?.Fecha,
                                d.LineaId,
                                false
                            );
                        })
                        .ToArray(),
                    pareja is not null
                )
            );
        }
        return filas;
    }
}
