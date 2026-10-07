using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed record FiltrosBuscadorMaestro(
    string? Numero = null,
    string? Tipo = null,
    string? Emisor = null,
    string? Cliente = null,
    string? Encargado = null,
    string Grupo = "Todos",
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null
);

public sealed record DatoBuscadorMaestro(string Nombre, string Valor, bool Informativo);

public sealed record DocumentoBuscadorMaestro(
    long DocumentoId,
    long VersionId,
    string Ruta,
    string Tipo,
    string Emisor,
    string Grupo,
    string Numeros,
    DateTime? Fecha,
    string Cliente,
    string Encargado,
    long? CadenaId,
    string Cadena,
    IReadOnlyList<DatoBuscadorMaestro> Datos
);

public sealed class RepositorioBuscadorMaestro(SqliteConnection conexion)
{
    public IReadOnlyList<string> Tipos()
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT DISTINCT i.tipo FROM identificaciones i WHERE EXISTS (SELECT 1 FROM campos_documento c JOIN valores_documento v ON v.campo_id=c.id JOIN versiones_documento ver ON ver.id=v.version_id WHERE c.identificacion_id=i.id AND v.estado='vigente' AND ver.estado='vigente') ORDER BY i.tipo COLLATE NOCASE;";
        using var lector = comando.ExecuteReader();
        var tipos = new List<string>();
        while (lector.Read())
            tipos.Add(lector.GetString(0));
        return tipos;
    }

    public IReadOnlyList<DocumentoBuscadorMaestro> Buscar(FiltrosBuscadorMaestro filtros)
    {
        using var comando = conexion.CreateCommand();
        var condiciones = new List<string> { "v.estado='vigente'", "d.estado_baja='activo'" };
        if (!string.IsNullOrWhiteSpace(filtros.Numero))
        {
            condiciones.Add(
                "EXISTS (SELECT 1 FROM valores_documento vn WHERE vn.version_id=v.id AND vn.estado='vigente' AND vn.valor_clave=$numero)"
            );
            comando.Parameters.AddWithValue(
                "$numero",
                DiccionarioDatosEnlazantes.ClaveDeEnlace(filtros.Numero)
            );
        }
        if (!string.IsNullOrWhiteSpace(filtros.Tipo))
        {
            condiciones.Add("i.tipo=$tipo");
            comando.Parameters.AddWithValue("$tipo", filtros.Tipo);
        }
        if (!string.IsNullOrWhiteSpace(filtros.Emisor))
        {
            condiciones.Add("i.emisor LIKE $emisor ESCAPE '\\'");
            comando.Parameters.AddWithValue(
                "$emisor",
                "%" + EscaparLike(filtros.Emisor.Trim()) + "%"
            );
        }
        if (!string.IsNullOrWhiteSpace(filtros.Cliente))
        {
            condiciones.Add(
                "EXISTS (SELECT 1 FROM valores_informativos_documento vc WHERE vc.version_id=v.id AND vc.dato='nombre_cliente' AND vc.valor LIKE $cliente ESCAPE '\\')"
            );
            comando.Parameters.AddWithValue(
                "$cliente",
                "%" + EscaparLike(filtros.Cliente.Trim()) + "%"
            );
        }
        if (!string.IsNullOrWhiteSpace(filtros.Encargado))
        {
            condiciones.Add(
                "EXISTS (SELECT 1 FROM valores_informativos_documento ve WHERE ve.version_id=v.id AND ve.dato='encargado')"
            );
        }
        if (filtros.Grupo is "Emitido" or "Recibido")
        {
            condiciones.Add("i.grupo_documento=$grupo");
            comando.Parameters.AddWithValue("$grupo", filtros.Grupo);
        }
        if (filtros.FechaDesde is DateTime desde)
        {
            condiciones.Add(
                "EXISTS (SELECT 1 FROM valores_informativos_documento vf WHERE vf.version_id=v.id AND vf.dato='fecha_documento' AND vf.fecha_reconocida >= $desde)"
            );
            comando.Parameters.AddWithValue(
                "$desde",
                desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            );
        }
        if (filtros.FechaHasta is DateTime hasta)
        {
            condiciones.Add(
                "EXISTS (SELECT 1 FROM valores_informativos_documento vf WHERE vf.version_id=v.id AND vf.dato='fecha_documento' AND vf.fecha_reconocida < $hasta)"
            );
            comando.Parameters.AddWithValue(
                "$hasta",
                hasta.Date.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            );
        }
        comando.CommandText =
            "SELECT d.id,v.id,d.ruta,i.tipo,i.emisor,i.grupo_documento, "
            + "(SELECT group_concat(x.valor_original, ', ') FROM (SELECT DISTINCT vd.valor_original FROM valores_documento vd WHERE vd.version_id=v.id AND vd.estado='vigente' ORDER BY vd.id) x), "
            + "(SELECT vi.fecha_reconocida FROM valores_informativos_documento vi WHERE vi.version_id=v.id AND vi.dato='fecha_documento'), "
            + "(SELECT vi.valor FROM valores_informativos_documento vi WHERE vi.version_id=v.id AND vi.dato='nombre_cliente'), "
            + "(SELECT vi.valor FROM valores_informativos_documento vi WHERE vi.version_id=v.id AND vi.dato='encargado'), "
            + "(SELECT c.id FROM enlaces_cadena ec JOIN vagones_cadena vc ON vc.id=ec.vagon_cadena_id JOIN cadenas c ON c.id=vc.cadena_id WHERE ec.version_id=v.id AND ec.estado='activo' AND vc.estado='activo' AND c.estado='activa' ORDER BY c.id LIMIT 1), "
            + "(SELECT c.nombre FROM enlaces_cadena ec JOIN vagones_cadena vc ON vc.id=ec.vagon_cadena_id JOIN cadenas c ON c.id=vc.cadena_id WHERE ec.version_id=v.id AND ec.estado='activo' AND vc.estado='activo' AND c.estado='activa' ORDER BY c.id LIMIT 1) "
            + "FROM versiones_documento v JOIN documentos d ON d.id=v.documento_id JOIN identificaciones i ON i.id=(SELECT c.identificacion_id FROM campos_documento c JOIN valores_documento x ON x.campo_id=c.id WHERE x.version_id=v.id AND x.estado='vigente' ORDER BY x.id LIMIT 1) "
            + "WHERE "
            + string.Join(" AND ", condiciones)
            + " ORDER BY COALESCE((SELECT vi.fecha_reconocida FROM valores_informativos_documento vi WHERE vi.version_id=v.id AND vi.dato='fecha_documento'),'') DESC,d.id DESC;";
        var filas = new List<DocumentoBuscadorMaestro>();
        using (var lector = comando.ExecuteReader())
        {
            while (lector.Read())
            {
                string encargado = lector.IsDBNull(9) ? "" : lector.GetString(9);
                if (
                    !string.IsNullOrWhiteSpace(filtros.Encargado)
                    && !ContieneSinTildes(encargado, filtros.Encargado)
                )
                    continue;
                string? fecha = lector.IsDBNull(7) ? null : lector.GetString(7);
                filas.Add(
                    new DocumentoBuscadorMaestro(
                        lector.GetInt64(0),
                        lector.GetInt64(1),
                        lector.GetString(2),
                        lector.GetString(3),
                        lector.GetString(4),
                        lector.GetString(5),
                        lector.IsDBNull(6) ? "" : lector.GetString(6),
                        ParsearFecha(fecha),
                        lector.IsDBNull(8) ? "" : lector.GetString(8),
                        encargado,
                        lector.IsDBNull(10) ? null : lector.GetInt64(10),
                        lector.IsDBNull(11) ? "" : lector.GetString(11),
                        []
                    )
                );
            }
        }
        return filas;
    }

    public IReadOnlyList<DocumentoBuscadorMaestro> ObtenerCadena(long cadenaId)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT v.documento_id,v.id,d.ruta,i.tipo,i.emisor,i.grupo_documento,c.nombre FROM vagones_cadena vc JOIN enlaces_cadena ec ON ec.vagon_cadena_id=vc.id AND ec.estado='activo' JOIN versiones_documento v ON v.id=ec.version_id AND v.estado='vigente' JOIN documentos d ON d.id=v.documento_id JOIN cadenas c ON c.id=vc.cadena_id JOIN campos_documento cd ON cd.identificacion_id=(SELECT identificacion_id FROM campos_documento WHERE id=(SELECT campo_id FROM valores_documento WHERE version_id=v.id AND estado='vigente' ORDER BY id LIMIT 1)) JOIN identificaciones i ON i.id=cd.identificacion_id WHERE vc.cadena_id=$id AND vc.estado='activo' GROUP BY v.id ORDER BY vc.orden,vc.id;";
        comando.Parameters.AddWithValue("$id", cadenaId);
        var resultados = new List<DocumentoBuscadorMaestro>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            long version = lector.GetInt64(1);
            var info = ObtenerInformativos(version);
            resultados.Add(
                new(
                    lector.GetInt64(0),
                    version,
                    lector.GetString(2),
                    lector.GetString(3),
                    lector.GetString(4),
                    lector.GetString(5),
                    string.Join(", ", ObtenerNumeros(version)),
                    info.Fecha,
                    info.Cliente,
                    info.Encargado,
                    cadenaId,
                    lector.GetString(6),
                    ObtenerDatos(version)
                )
            );
        }
        return resultados;
    }

    public IReadOnlyList<DatoBuscadorMaestro> ObtenerDatos(long versionId)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT COALESCE(dd.nombre,cd.nombre),vd.valor_original FROM valores_documento vd JOIN campos_documento cd ON cd.id=vd.campo_id LEFT JOIN diccionario_datos dd ON dd.id=vd.dato_diccionario_id WHERE vd.version_id=$v AND vd.estado='vigente' ORDER BY COALESCE(dd.orden,999),cd.nombre; SELECT dato,valor FROM valores_informativos_documento WHERE version_id=$v ORDER BY dato;";
        comando.Parameters.AddWithValue("$v", versionId);
        var datos = new List<DatoBuscadorMaestro>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
            datos.Add(new(lector.GetString(0), lector.GetString(1), false));
        if (lector.NextResult())
            while (lector.Read())
                datos.Add(new(NombreInformativo(lector.GetString(0)), lector.GetString(1), true));
        return datos;
    }

    private IReadOnlyList<string> ObtenerNumeros(long versionId)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT valor_original FROM valores_documento WHERE version_id=$v AND estado='vigente' ORDER BY id;";
        comando.Parameters.AddWithValue("$v", versionId);
        using var lector = comando.ExecuteReader();
        var valores = new List<string>();
        while (lector.Read())
            valores.Add(lector.GetString(0));
        return valores.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private (DateTime? Fecha, string Cliente, string Encargado) ObtenerInformativos(long versionId)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT dato,valor,fecha_reconocida FROM valores_informativos_documento WHERE version_id=$v;";
        comando.Parameters.AddWithValue("$v", versionId);
        using var lector = comando.ExecuteReader();
        DateTime? fecha = null;
        string cliente = "",
            encargado = "";
        while (lector.Read())
        {
            if (lector.GetString(0) == "fecha_documento" && !lector.IsDBNull(2))
                fecha = ParsearFecha(lector.GetString(2));
            else if (lector.GetString(0) == "nombre_cliente")
                cliente = lector.GetString(1);
            else if (lector.GetString(0) == "encargado")
                encargado = lector.GetString(1);
        }
        return (fecha, cliente, encargado);
    }

    private static DateTime? ParsearFecha(string? valor) =>
        DateTime.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha
            : null;

    private static string NombreInformativo(string dato) =>
        dato switch
        {
            "fecha_documento" => "Fecha del documento",
            "nombre_cliente" => "Cliente",
            "encargado" => "Encargado",
            _ => dato,
        };

    private static string EscaparLike(string valor) =>
        valor.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static string NormalizarTexto(string valor) =>
        string.Concat(
                valor
                    .Normalize(NormalizationForm.FormD)
                    .Where(c =>
                        CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark
                    )
            )
            .ToUpperInvariant();

    private static bool ContieneSinTildes(string valor, string consulta) =>
        NormalizarTexto(valor).Contains(NormalizarTexto(consulta.Trim()), StringComparison.Ordinal);
}
