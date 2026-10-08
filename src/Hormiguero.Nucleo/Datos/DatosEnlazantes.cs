using System.Collections.ObjectModel;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed record DatoEnlazante(
    string Id,
    string Nombre,
    string Grupo,
    int Orden,
    string? CodigoReferencia,
    string EtiquetaTipo
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
    bool Activo,
    bool DefineTipo = false,
    bool Enlazable = true
);

public sealed record ZonaInformativa(
    string Dato,
    int Pagina,
    double X,
    double Y,
    double Ancho,
    double Alto
);

public sealed record ValorInformativo(
    string Dato,
    string Valor,
    DateTime? Fecha,
    bool FechaNoReconocida
);

public static class FechaDocumentoParser
{
    private static readonly string[] formatos =
    [
        "d-M-yyyy",
        "dd-MM-yyyy",
        "d/M/yyyy",
        "dd/MM/yyyy",
        "d.M.yyyy",
        "dd.MM.yyyy",
    ];
    private static readonly System.Globalization.CultureInfo cultura =
        System.Globalization.CultureInfo.GetCultureInfo("es-CL");

    public static ValorInformativo InterpretarFecha(string? valor)
    {
        string texto = valor?.Trim() ?? "";
        if (
            DateTime.TryParseExact(
                texto,
                formatos,
                cultura,
                System.Globalization.DateTimeStyles.None,
                out var fecha
            )
            || DateTime.TryParse(
                texto,
                cultura,
                System.Globalization.DateTimeStyles.None,
                out fecha
            )
        )
            return new("fecha_documento", texto, fecha.Date, false);
        return new("fecha_documento", texto, null, texto.Length > 0);
    }
}

public sealed class RepositorioDatosInformativos(SqliteConnection conexion)
{
    public DateTime? ObtenerFechaDocumento(long versionId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT fecha_reconocida FROM valores_informativos_documento WHERE version_id=$v AND dato='fecha_documento';";
        cmd.Parameters.AddWithValue("$v", versionId);
        object? fecha = cmd.ExecuteScalar();
        return fecha is string texto
            ? DateTime.Parse(texto, System.Globalization.CultureInfo.InvariantCulture)
            : null;
    }

    public IReadOnlyList<ZonaInformativa> LeerZonas(long identificacionId, string diseno)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT dato,pagina,x,y,ancho,alto FROM tipos_documento_informativos WHERE identificacion_id=$i AND diseno=$s AND activo=1 ORDER BY dato;";
        cmd.Parameters.AddWithValue("$i", identificacionId);
        cmd.Parameters.AddWithValue("$s", diseno);
        using var r = cmd.ExecuteReader();
        var zonas = new List<ZonaInformativa>();
        while (r.Read())
            zonas.Add(
                new(
                    r.GetString(0),
                    r.GetInt32(1),
                    r.GetDouble(2),
                    r.GetDouble(3),
                    r.GetDouble(4),
                    r.GetDouble(5)
                )
            );
        return zonas;
    }

    public void GuardarZonas(
        long identificacionId,
        string diseno,
        IReadOnlyList<ZonaInformativa> zonas
    )
    {
        using var tx = conexion.BeginTransaction();
        using (var cmd = conexion.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                "UPDATE tipos_documento_informativos SET activo=0 WHERE identificacion_id=$i AND diseno=$s;";
            cmd.Parameters.AddWithValue("$i", identificacionId);
            cmd.Parameters.AddWithValue("$s", diseno);
            cmd.ExecuteNonQuery();
        }
        foreach (var zona in zonas)
        {
            if (zona.Pagina < 1 || zona.X < 0 || zona.Y < 0 || zona.Ancho <= 0 || zona.Alto <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(zonas),
                    "La zona del dato informativo no es válida."
                );
            using var cmd = conexion.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                "INSERT INTO tipos_documento_informativos(identificacion_id,diseno,dato,pagina,x,y,ancho,alto,activo) VALUES($i,$s,$d,$p,$x,$y,$a,$l,1) ON CONFLICT(identificacion_id,diseno,dato) DO UPDATE SET pagina=excluded.pagina,x=excluded.x,y=excluded.y,ancho=excluded.ancho,alto=excluded.alto,activo=1;";
            cmd.Parameters.AddWithValue("$i", identificacionId);
            cmd.Parameters.AddWithValue("$s", diseno);
            cmd.Parameters.AddWithValue("$d", zona.Dato);
            cmd.Parameters.AddWithValue("$p", zona.Pagina);
            cmd.Parameters.AddWithValue("$x", zona.X);
            cmd.Parameters.AddWithValue("$y", zona.Y);
            cmd.Parameters.AddWithValue("$a", zona.Ancho);
            cmd.Parameters.AddWithValue("$l", zona.Alto);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void GuardarValores(long versionId, IReadOnlyList<ValorInformativo> valores)
    {
        foreach (var valor in valores)
        {
            using var cmd = conexion.CreateCommand();
            cmd.CommandText =
                "INSERT INTO valores_informativos_documento(version_id,dato,valor,fecha_reconocida,fecha_no_reconocida,creada_en) VALUES($v,$d,$o,$f,$n,$c) ON CONFLICT(version_id,dato) DO UPDATE SET valor=excluded.valor,fecha_reconocida=excluded.fecha_reconocida,fecha_no_reconocida=excluded.fecha_no_reconocida;";
            cmd.Parameters.AddWithValue("$v", versionId);
            cmd.Parameters.AddWithValue("$d", valor.Dato);
            cmd.Parameters.AddWithValue("$o", valor.Valor);
            cmd.Parameters.AddWithValue(
                "$f",
                (object?)valor.Fecha?.ToString("yyyy-MM-dd") ?? DBNull.Value
            );
            cmd.Parameters.AddWithValue("$n", valor.FechaNoReconocida ? 1 : 0);
            cmd.Parameters.AddWithValue("$c", DateTime.Now.ToString("o"));
            cmd.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<ValorInformativo> ConsultarFechas(DateTime desde, DateTime hasta)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT dato,valor,fecha_reconocida,fecha_no_reconocida FROM valores_informativos_documento WHERE dato='fecha_documento' AND fecha_reconocida >= $d AND fecha_reconocida <= $h ORDER BY fecha_reconocida;";
        cmd.Parameters.AddWithValue("$d", desde.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$h", hasta.ToString("yyyy-MM-dd"));
        using var r = cmd.ExecuteReader();
        var resultado = new List<ValorInformativo>();
        while (r.Read())
            resultado.Add(
                new(
                    r.GetString(0),
                    r.GetString(1),
                    r.IsDBNull(2) ? null : DateTime.Parse(r.GetString(2)),
                    r.GetInt32(3) != 0
                )
            );
        return resultado;
    }

    public IReadOnlyList<ValorInformativo> ListarValores(long versionId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT dato,valor,fecha_reconocida,fecha_no_reconocida FROM valores_informativos_documento WHERE version_id=$v ORDER BY dato;";
        cmd.Parameters.AddWithValue("$v", versionId);
        using var r = cmd.ExecuteReader();
        var resultado = new List<ValorInformativo>();
        while (r.Read())
            resultado.Add(
                new(
                    r.GetString(0),
                    r.GetString(1),
                    r.IsDBNull(2) ? null : DateTime.Parse(r.GetString(2)),
                    r.GetInt32(3) != 0
                )
            );
        return resultado;
    }

    public IReadOnlyList<ValorInformativo> BuscarPorRuta(string ruta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT dato,valor,fecha_reconocida,fecha_no_reconocida FROM valores_informativos_documento vi JOIN versiones_documento v ON v.id=vi.version_id JOIN documentos d ON d.id=v.documento_id WHERE d.ruta=$r AND v.estado='vigente' ORDER BY dato;";
        cmd.Parameters.AddWithValue("$r", ruta);
        using var r = cmd.ExecuteReader();
        var resultado = new List<ValorInformativo>();
        while (r.Read())
            resultado.Add(
                new(
                    r.GetString(0),
                    r.GetString(1),
                    r.IsDBNull(2) ? null : DateTime.Parse(r.GetString(2)),
                    r.GetInt32(3) != 0
                )
            );
        return resultado;
    }
}

public sealed record DocumentoConDato(
    long DocumentoId,
    long VersionId,
    string Ruta,
    string Valor,
    string NombreEstandar = "",
    DateTime? FechaDocumento = null
);

public sealed record CoincidenciasPorDato(
    DatoEnlazante Dato,
    string Valor,
    IReadOnlyList<DocumentoConDato> Documentos
);

public static class DiccionarioDatosEnlazantes
{
    public static bool EsParte(string datoId) =>
        datoId
            is "rut_proveedor"
                or "nombre_proveedor"
                or "rut_cliente"
                or "nombre_cliente"
                or "rut_propio"
                or "nombre_propio";

    public static string NombreEstandar(string datoId, string emisor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emisor);
        var dato =
            Todos.SingleOrDefault(d => d.Id == datoId)
            ?? throw new ArgumentException("El dato no pertenece al diccionario.", nameof(datoId));
        return $"{dato.EtiquetaTipo} · {emisor.Trim()}";
    }

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

    public static string ClaveDeEnlace(string datoId, string? valor)
    {
        if (datoId is "rut_proveedor" or "rut_cliente" or "rut_propio")
        {
            string rut = string.Concat((valor ?? "").Where(char.IsLetterOrDigit))
                .ToUpperInvariant();
            return rut.Length > 1 ? rut[..^1] + "-" + rut[^1] : rut;
        }
        if (datoId is "nombre_proveedor" or "nombre_cliente" or "nombre_propio")
            return string.Join(
                    ' ',
                    (valor ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                )
                .ToUpperInvariant();
        return ClaveDeEnlace(valor);
    }

    public static string ValorComoSeLee(string? valor) => (valor ?? "").Trim();

    public static string ValorLiteralNormalizado(string? valor) =>
        string.Join(' ', (valor ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

    public static IReadOnlyList<DatoEnlazante> Todos { get; } =
        new ReadOnlyCollection<DatoEnlazante>([
            new(
                "cotizacion_propia",
                "N° Cotización propia",
                "Ventas propias",
                1,
                "COV",
                "Cotización propia"
            ),
            new(
                "nota_venta_propia",
                "N° Nota de venta propia",
                "Ventas propias",
                2,
                "NVV",
                "Nota de venta propia"
            ),
            new(
                "guia_despacho_propia",
                "N° Guía de despacho propia",
                "Ventas propias",
                3,
                "GDV",
                "Guía de despacho propia"
            ),
            new(
                "factura_propia",
                "N° Factura propia",
                "Ventas propias",
                4,
                "FCV",
                "Factura propia"
            ),
            new(
                "nota_credito_propia",
                "N° Nota de crédito propia",
                "Ventas propias",
                5,
                "NCV",
                "Nota de crédito propia"
            ),
            new(
                "nota_debito_propia",
                "N° Nota de débito propia",
                "Ventas propias",
                6,
                null,
                "Nota de débito propia"
            ),
            new("oc_cliente", "N° OC del cliente", "Del cliente", 7, "OCL", "OC del cliente"),
            new(
                "recepcion_cliente",
                "N° Recepción del cliente",
                "Del cliente",
                8,
                "HES/HEM/recepción",
                "Recepción del cliente"
            ),
            new("oc_propia", "N° OC propia", "Compras propias", 9, "OCC", "OC propia"),
            new(
                "cotizacion_proveedor",
                "N° Cotización del proveedor",
                "Del proveedor",
                10,
                null,
                "Cotización del proveedor"
            ),
            new(
                "nota_venta_proveedor",
                "N° Nota de venta del proveedor",
                "Del proveedor",
                11,
                "NVV",
                "Nota de venta del proveedor"
            ),
            new(
                "guia_proveedor",
                "N° Guía del proveedor",
                "Del proveedor",
                12,
                "GRC",
                "Guía del proveedor"
            ),
            new(
                "factura_proveedor",
                "N° Factura del proveedor",
                "Del proveedor",
                13,
                "FCC",
                "Factura del proveedor"
            ),
            new(
                "nota_credito_proveedor",
                "N° Nota de crédito del proveedor",
                "Del proveedor",
                14,
                "NCC",
                "Nota de crédito del proveedor"
            ),
            new(
                "nota_debito_proveedor",
                "N° Nota de débito del proveedor",
                "Del proveedor",
                15,
                null,
                "Nota de débito del proveedor"
            ),
            new(
                "codigo_obra",
                "Código de obra o proyecto",
                "Otros",
                16,
                null,
                "Código de obra o proyecto"
            ),
            new(
                "comprobante_pago",
                "N° Comprobante de pago",
                "Otros",
                17,
                null,
                "Comprobante de pago"
            ),
            new("rut_proveedor", "RUT del proveedor", "Otros", 18, null, "RUT del proveedor"),
            new(
                "nombre_proveedor",
                "Nombre o razón social del proveedor",
                "Otros",
                19,
                null,
                "Nombre del proveedor"
            ),
            new("rut_cliente", "RUT del cliente", "Otros", 20, null, "RUT del cliente"),
            new(
                "nombre_cliente",
                "Nombre o razón social del cliente",
                "Otros",
                21,
                null,
                "Nombre del cliente"
            ),
            new("rut_propio", "RUT de mi empresa", "Otros", 22, null, "RUT propio"),
            new(
                "nombre_propio",
                "Nombre o razón social de mi empresa",
                "Otros",
                23,
                null,
                "Nombre propio"
            ),
        ]);
}

public sealed class RepositorioDatosEnlazantes(SqliteConnection conexion)
{
    public bool ExisteDocumentoVigente(string datoId, string valor, string emisor, string tipo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datoId);
        ArgumentException.ThrowIfNullOrWhiteSpace(valor);
        ArgumentException.ThrowIfNullOrWhiteSpace(emisor);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipo);
        string clave = DiccionarioDatosEnlazantes.ClaveDeEnlace(datoId, valor);
        if (clave.Length == 0)
            return false;
        using var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT 1
            FROM valores_documento val
            JOIN versiones_documento ver ON ver.id=val.version_id
            JOIN documentos doc ON doc.id=ver.documento_id
            JOIN campos_documento campo ON campo.id=val.campo_id
            JOIN identificaciones ident ON ident.id=campo.identificacion_id
            WHERE val.dato_diccionario_id=$dato
              AND (val.valor_clave=$clave OR UPPER(TRIM(val.valor_original))=UPPER(TRIM($literal)))
              AND val.estado='vigente'
              AND val.origen<>'cedible'
              AND ver.estado='vigente'
              AND ident.emisor=$emisor
              AND ident.tipo=$tipo
            LIMIT 1;
            """;
        comando.Parameters.AddWithValue("$dato", datoId);
        comando.Parameters.AddWithValue("$clave", clave);
        comando.Parameters.AddWithValue("$literal", valor);
        comando.Parameters.AddWithValue("$emisor", emisor);
        comando.Parameters.AddWithValue("$tipo", tipo);
        return comando.ExecuteScalar() is not null;
    }

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
            "SELECT DISTINCT x.dato_diccionario_id,x.valor_original FROM valores_documento x JOIN campos_documento c ON c.id=x.campo_id WHERE x.version_id=$v AND x.estado='vigente' AND x.dato_diccionario_id IS NOT NULL AND x.valor_clave<>'' AND EXISTS(SELECT 1 FROM tipos_documento_datos t WHERE t.identificacion_id=c.identificacion_id AND t.dato_diccionario_id=x.dato_diccionario_id AND t.activo=1 AND t.enlazable=1) ORDER BY x.dato_diccionario_id;";
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
                    lector.IsDBNull(4) ? null : lector.GetString(4),
                    DiccionarioDatosEnlazantes
                        .Todos.Single(d => d.Id == lector.GetString(0))
                        .EtiquetaTipo
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
        if (dato.DefineTipo)
        {
            using var desmarcar = conexion.CreateCommand();
            desmarcar.Transaction = transaccion;
            desmarcar.CommandText =
                "UPDATE tipos_documento_datos SET define_tipo=0 WHERE identificacion_id=$i AND diseno=$s;";
            desmarcar.Parameters.AddWithValue("$i", dato.IdentificacionId);
            desmarcar.Parameters.AddWithValue("$s", dato.Diseno.Trim());
            desmarcar.ExecuteNonQuery();
        }
        using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText =
            dato.Id == 0
                // Volver a marcar un dato que se había quitado en el mismo diseño lo reactiva con la
                // zona nueva (D-73: todo se puede rehacer); la fila y su historial se conservan.
                ? "INSERT INTO tipos_documento_datos(identificacion_id,dato_diccionario_id,diseno,campo_id,pagina,x,y,ancho,alto,creada_en,actualizada_en,define_tipo,enlazable) VALUES($i,$d,$s,$c,$p,$x,$y,$a,$l,$f,$f,$t,$e) "
                    + "ON CONFLICT(identificacion_id,dato_diccionario_id,diseno) DO UPDATE SET campo_id=excluded.campo_id,pagina=excluded.pagina,x=excluded.x,y=excluded.y,ancho=excluded.ancho,alto=excluded.alto,define_tipo=excluded.define_tipo,enlazable=excluded.enlazable,activo=1,actualizada_en=excluded.actualizada_en RETURNING id;"
                : "UPDATE tipos_documento_datos SET dato_diccionario_id=$d,diseno=$s,campo_id=$c,pagina=$p,x=$x,y=$y,ancho=$a,alto=$l,define_tipo=$t,enlazable=$e,actualizada_en=$f WHERE id=$id AND identificacion_id=$i AND activo=1 RETURNING id;";
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
        comando.Parameters.AddWithValue("$t", dato.DefineTipo ? 1 : 0);
        comando.Parameters.AddWithValue("$e", dato.Enlazable ? 1 : 0);
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
            "SELECT id,identificacion_id,dato_diccionario_id,diseno,campo_id,pagina,x,y,ancho,alto,activo,define_tipo,enlazable FROM tipos_documento_datos WHERE identificacion_id=$i"
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
                    lector.GetInt32(10) != 0,
                    lector.GetInt32(11) != 0,
                    lector.GetInt32(12) != 0
                )
            );
        return lista;
    }

    public string? TipoDerivado(long identificacionId, string diseno)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT d.id FROM tipos_documento_datos t JOIN diccionario_datos d ON d.id=t.dato_diccionario_id WHERE t.identificacion_id=$i AND t.diseno=$s AND t.activo=1 AND t.define_tipo=1 LIMIT 1;";
        comando.Parameters.AddWithValue("$i", identificacionId);
        comando.Parameters.AddWithValue("$s", diseno);
        var id = Convert.ToString(comando.ExecuteScalar());
        return id is null
            ? null
            : DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == id).EtiquetaTipo;
    }

    public IReadOnlyList<DocumentoConDato> SugerirCalce(string datoId, string valor, int maximo = 3)
    {
        string clave = DiccionarioDatosEnlazantes.ClaveDeEnlace(datoId, valor);
        if (clave.Length == 0)
            return [];
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT DISTINCT vd.id,ver.id,vd.ruta,val.valor_original,ident.nombre_estandar,vi.fecha_reconocida FROM valores_documento val JOIN versiones_documento ver ON ver.id=val.version_id JOIN documentos vd ON vd.id=ver.documento_id JOIN campos_documento c ON c.id=val.campo_id JOIN identificaciones ident ON ident.id=c.identificacion_id LEFT JOIN valores_informativos_documento vi ON vi.version_id=ver.id AND vi.dato='fecha_documento' WHERE val.dato_diccionario_id=$d AND (val.valor_clave=$v OR UPPER(TRIM(val.valor_original))=UPPER(TRIM($literal))) AND val.estado='vigente' AND ver.estado='vigente' AND EXISTS(SELECT 1 FROM tipos_documento_datos t WHERE t.identificacion_id=c.identificacion_id AND t.dato_diccionario_id=val.dato_diccionario_id AND t.activo=1 AND t.enlazable=1) ORDER BY ver.id DESC LIMIT $maximo;";
        comando.Parameters.AddWithValue("$d", datoId);
        comando.Parameters.AddWithValue("$v", clave);
        comando.Parameters.AddWithValue("$literal", valor);
        comando.Parameters.AddWithValue("$maximo", maximo);
        using var lector = comando.ExecuteReader();
        var resultados = new List<DocumentoConDato>();
        while (lector.Read())
            resultados.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    lector.GetString(2),
                    lector.GetString(3),
                    lector.GetString(4),
                    lector.IsDBNull(5) ? null : DateTime.Parse(lector.GetString(5))
                )
            );
        return resultados;
    }

    public IReadOnlyList<DocumentoConDato> BuscarDocumentos(string datoId, string valor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datoId);
        ArgumentNullException.ThrowIfNull(valor);
        string clave = DiccionarioDatosEnlazantes.ClaveDeEnlace(datoId, valor);
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT DISTINCT vd.id, val.version_id, vd.ruta, val.valor_original FROM valores_documento val JOIN versiones_documento ver ON ver.id=val.version_id JOIN documentos vd ON vd.id=ver.documento_id JOIN campos_documento c ON c.id=val.campo_id WHERE val.dato_diccionario_id=$d AND (val.valor_clave=$v OR UPPER(TRIM(val.valor_original))=UPPER(TRIM($literal))) AND val.estado='vigente' AND ver.estado='vigente' AND EXISTS(SELECT 1 FROM tipos_documento_datos t WHERE t.identificacion_id=c.identificacion_id AND t.dato_diccionario_id=val.dato_diccionario_id AND t.activo=1 AND t.enlazable=1) ORDER BY vd.id,val.version_id;";
        comando.Parameters.AddWithValue("$d", datoId);
        comando.Parameters.AddWithValue("$v", clave);
        comando.Parameters.AddWithValue("$literal", valor);
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
