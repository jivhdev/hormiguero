using System.Collections.ObjectModel;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed record DatoEnlazante(
    string Id,
    string Nombre,
    string Grupo,
    int Orden,
    string? CodigoReferencia
);

public sealed record DatoTipoDocumento(
    long Id,
    long IdentificacionId,
    string DatoId,
    string Diseno,
    long? CampoId,
    int Pagina,
    double X,
    double Y,
    double Ancho,
    double Alto,
    bool Activo
);

public sealed record DocumentoConDato(long DocumentoId, long VersionId, string Ruta, string Valor);

public sealed record CoincidenciasPorDato(
    DatoEnlazante Dato,
    string Valor,
    IReadOnlyList<DocumentoConDato> Documentos
);

public static class DiccionarioDatosEnlazantes
{
    // Clave para comparar un dato del diccionario entre documentos: el mismo número se imprime
    // distinto según el documento ("NVV-12345", "0000020508", "1066 086"). Si hay dígitos,
    // cuentan solo los dígitos, sin ceros a la izquierda; si no, letras y dígitos en mayúscula.
    // ponytail: ignora letras cuando hay dígitos; si un tipo usa folios con letras significativas
    // ("A-123" vs "B-123"), agregar una regla por dato.
    public static string ClaveDeEnlace(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return "";
        string digitos = string.Concat(valor.Where(char.IsAsciiDigit));
        if (digitos.Length > 0)
            return digitos.TrimStart('0') is { Length: > 0 } sinCeros ? sinCeros : "0";
        return string.Concat(valor.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    }

    public static IReadOnlyList<DatoEnlazante> Todos { get; } =
        new ReadOnlyCollection<DatoEnlazante>([
            new("cotizacion_propia", "N° Cotización propia", "Ventas propias", 1, "COV"),
            new("nota_venta_propia", "N° Nota de venta propia", "Ventas propias", 2, "NVV"),
            new("guia_despacho_propia", "N° Guía de despacho propia", "Ventas propias", 3, "GDV"),
            new("factura_propia", "N° Factura propia", "Ventas propias", 4, "FCV"),
            new("nota_credito_propia", "N° Nota de crédito propia", "Ventas propias", 5, "NCV"),
            new("nota_debito_propia", "N° Nota de débito propia", "Ventas propias", 6, null),
            new("oc_cliente", "N° OC del cliente", "Del cliente", 7, "OCL"),
            new(
                "recepcion_cliente",
                "N° Recepción del cliente",
                "Del cliente",
                8,
                "HES/HEM/recepción"
            ),
            new("oc_propia", "N° OC propia", "Compras propias", 9, "OCC"),
            new("cotizacion_proveedor", "N° Cotización del proveedor", "Del proveedor", 10, null),
            new(
                "nota_venta_proveedor",
                "N° Nota de venta del proveedor",
                "Del proveedor",
                11,
                "NVV"
            ),
            new("guia_proveedor", "N° Guía del proveedor", "Del proveedor", 12, "GRC"),
            new("factura_proveedor", "N° Factura del proveedor", "Del proveedor", 13, "FCC"),
            new(
                "nota_credito_proveedor",
                "N° Nota de crédito del proveedor",
                "Del proveedor",
                14,
                "NCC"
            ),
            new(
                "nota_debito_proveedor",
                "N° Nota de débito del proveedor",
                "Del proveedor",
                15,
                null
            ),
            new("codigo_obra", "Código de obra o proyecto", "Otros", 16, null),
            new("comprobante_pago", "N° Comprobante de pago", "Otros", 17, null),
        ]);
}

public sealed class RepositorioDatosEnlazantes(SqliteConnection conexion)
{
    public IReadOnlyList<CoincidenciasPorDato> SugerirPorDato(string datoId, string valor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datoId);
        ArgumentException.ThrowIfNullOrWhiteSpace(valor);
        var dato =
            DiccionarioDatosEnlazantes.Todos.SingleOrDefault(d => d.Id == datoId)
            ?? throw new ArgumentException("El dato no pertenece al diccionario.", nameof(datoId));
        return [new(dato, valor, BuscarDocumentos(datoId, valor))];
    }

    public IReadOnlyList<CoincidenciasPorDato> SugerirParaDocumento(long versionId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT DISTINCT dato_diccionario_id,valor_clave FROM valores_documento WHERE version_id=$v AND estado='vigente' AND dato_diccionario_id IS NOT NULL AND valor_clave<>'' ORDER BY dato_diccionario_id;";
        cmd.Parameters.AddWithValue("$v", versionId);
        var datos = new List<(string Id, string Valor)>();
        using (var r = cmd.ExecuteReader())
            while (r.Read())
                datos.Add((r.GetString(0), r.GetString(1)));
        var resultado = new List<CoincidenciasPorDato>();
        foreach (var (id, valor) in datos)
        {
            var coincidencias = BuscarDocumentos(id, valor)
                .Where(d => d.VersionId != versionId)
                .ToArray();
            if (coincidencias.Length == 0)
                continue;
            var dato = DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == id);
            resultado.Add(new(dato, valor, coincidencias));
        }
        return resultado;
    }

    public IReadOnlyList<DatoEnlazante> LeerDiccionario()
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,nombre,grupo,orden,codigo_referencia FROM diccionario_datos ORDER BY orden;";
        using var lector = comando.ExecuteReader();
        var datos = new List<DatoEnlazante>();
        while (lector.Read())
            datos.Add(
                new(
                    lector.GetString(0),
                    lector.GetString(1),
                    lector.GetString(2),
                    lector.GetInt32(3),
                    lector.IsDBNull(4) ? null : lector.GetString(4)
                )
            );
        return datos;
    }

    public long GuardarDatoTipo(DatoTipoDocumento dato)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dato.DatoId);
        ArgumentException.ThrowIfNullOrWhiteSpace(dato.Diseno);
        if (dato.Pagina < 1 || dato.X < 0 || dato.Y < 0 || dato.Ancho <= 0 || dato.Alto <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(dato),
                "La zona del diseño de PDF no es válida."
            );
        using var transaccion = conexion.BeginTransaction();
        using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText =
            dato.Id == 0
                ? "INSERT INTO tipos_documento_datos(identificacion_id,dato_diccionario_id,diseno,campo_id,pagina,x,y,ancho,alto,creada_en,actualizada_en) VALUES($i,$d,$s,$c,$p,$x,$y,$a,$l,$f,$f) RETURNING id;"
                : "UPDATE tipos_documento_datos SET dato_diccionario_id=$d,diseno=$s,campo_id=$c,pagina=$p,x=$x,y=$y,ancho=$a,alto=$l,actualizada_en=$f WHERE id=$id AND identificacion_id=$i AND activo=1 RETURNING id;";
        comando.Parameters.AddWithValue("$i", dato.IdentificacionId);
        comando.Parameters.AddWithValue("$d", dato.DatoId);
        comando.Parameters.AddWithValue("$s", dato.Diseno.Trim());
        comando.Parameters.AddWithValue("$c", (object?)dato.CampoId ?? DBNull.Value);
        comando.Parameters.AddWithValue("$p", dato.Pagina);
        comando.Parameters.AddWithValue("$x", dato.X);
        comando.Parameters.AddWithValue("$y", dato.Y);
        comando.Parameters.AddWithValue("$a", dato.Ancho);
        comando.Parameters.AddWithValue("$l", dato.Alto);
        comando.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        comando.Parameters.AddWithValue("$id", dato.Id);
        long id = Convert.ToInt64(
            comando.ExecuteScalar()
                ?? throw new InvalidOperationException(
                    "No existe la configuración de dato del tipo."
                )
        );
        if (dato.CampoId is long campoId)
        {
            using var campo = conexion.CreateCommand();
            campo.Transaction = transaccion;
            campo.CommandText =
                "UPDATE campos_documento SET dato_diccionario_id=$d WHERE id=$c AND identificacion_id=$i;";
            campo.Parameters.AddWithValue("$d", dato.DatoId);
            campo.Parameters.AddWithValue("$c", campoId);
            campo.Parameters.AddWithValue("$i", dato.IdentificacionId);
            if (campo.ExecuteNonQuery() == 0)
                throw new InvalidOperationException(
                    "El campo asociado no pertenece al tipo de documento."
                );
        }
        transaccion.Commit();
        return id;
    }

    public bool AnularDatoTipo(long id)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "UPDATE tipos_documento_datos SET activo=0,actualizada_en=$f WHERE id=$id AND activo=1;";
        comando.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        comando.Parameters.AddWithValue("$id", id);
        return comando.ExecuteNonQuery() > 0;
    }

    public IReadOnlyList<DatoTipoDocumento> ListarDatosTipo(
        long identificacionId,
        string? diseno = null,
        bool incluirAnulados = false
    )
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,identificacion_id,dato_diccionario_id,diseno,campo_id,pagina,x,y,ancho,alto,activo FROM tipos_documento_datos WHERE identificacion_id=$i"
            + (diseno is null ? "" : " AND diseno=$s")
            + (incluirAnulados ? "" : " AND activo=1")
            + " ORDER BY id;";
        comando.Parameters.AddWithValue("$i", identificacionId);
        comando.Parameters.AddWithValue("$s", (object?)diseno ?? DBNull.Value);
        using var lector = comando.ExecuteReader();
        var lista = new List<DatoTipoDocumento>();
        while (lector.Read())
            lista.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    lector.GetString(2),
                    lector.GetString(3),
                    lector.IsDBNull(4) ? null : lector.GetInt64(4),
                    lector.GetInt32(5),
                    lector.GetDouble(6),
                    lector.GetDouble(7),
                    lector.GetDouble(8),
                    lector.GetDouble(9),
                    lector.GetInt32(10) != 0
                )
            );
        return lista;
    }

    public IReadOnlyList<DocumentoConDato> BuscarDocumentos(string datoId, string valorClave)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datoId);
        ArgumentNullException.ThrowIfNull(valorClave);
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT DISTINCT vd.id, val.version_id, vd.ruta, val.valor_original FROM valores_documento val JOIN versiones_documento ver ON ver.id=val.version_id JOIN documentos vd ON vd.id=ver.documento_id WHERE val.dato_diccionario_id=$d AND val.valor_clave=$v AND val.estado='vigente' AND ver.estado='vigente' ORDER BY vd.id,val.version_id;";
        comando.Parameters.AddWithValue("$d", datoId);
        comando.Parameters.AddWithValue("$v", valorClave);
        using var lector = comando.ExecuteReader();
        var resultados = new List<DocumentoConDato>();
        while (lector.Read())
            resultados.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    lector.GetString(2),
                    lector.GetString(3)
                )
            );
        return resultados;
    }
}
