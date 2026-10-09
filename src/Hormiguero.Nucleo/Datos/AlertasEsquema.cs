using System.Globalization;
using System.Text.Json;
using Hormiguero.Nucleo.Utilidades;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed record DecisionModoPendiente(
    long VersionId,
    long CadenaId,
    string Proveedor,
    IReadOnlyList<string> Opciones
);

public sealed record DatoCadena(string Nombre, string Valor, DateTime ActualizadoEn);

public sealed record AlertaEsquema(
    long Id,
    long ReglaId,
    long CadenaId,
    string Tipo,
    string Alcance,
    string Texto,
    DateOnly? FechaVencimiento,
    string Urgencia,
    string Estado,
    string? Motivo,
    string Proveedor,
    string? Cliente
);

public sealed record ConteoAlertasCadena(long CadenaId, int Cantidad, string? Urgencia);

public sealed record ParametrosFaltaDato(string Dato, string Texto);

public sealed record ParametrosPlazo(
    long? DesdeLugarId,
    string? DesdeDato,
    long HastaLugarId,
    int Dias,
    string TipoDias,
    long? CalendarioId,
    long? CondicionLugarId,
    string Texto,
    bool PorLinea
);

public sealed record ParametrosListoPara(
    long CuandoLugarId,
    long? CondicionLugarId,
    long HastaLugarId,
    string Lista
);

public sealed class RepositorioModosEsquema(SqliteConnection conexion)
{
    public void FijarModo(long cadenaId, long? modoId)
    {
        using var tx = conexion.BeginTransaction();
        long? anterior;
        long esquemaId;
        using (var q = conexion.CreateCommand())
        {
            q.Transaction = tx;
            q.CommandText =
                "SELECT esquema_id,modo_id FROM cadenas WHERE id=$c AND esquema_id IS NOT NULL;";
            q.Parameters.AddWithValue("$c", cadenaId);
            using var r = q.ExecuteReader();
            if (!r.Read())
                throw new InvalidOperationException("No existe la cadena indicada.");
            esquemaId = r.GetInt64(0);
            anterior = r.IsDBNull(1) ? null : r.GetInt64(1);
        }
        if (modoId is not null)
        {
            long modo = modoId.Value;
            using var q = conexion.CreateCommand();
            q.Transaction = tx;
            q.CommandText = "SELECT 1 FROM modos_esquema WHERE id=$m AND esquema_id=$e;";
            q.Parameters.AddWithValue("$m", modo);
            q.Parameters.AddWithValue("$e", esquemaId);
            if (q.ExecuteScalar() is null)
                throw new ArgumentException(
                    "El modo no pertenece al esquema de la cadena.",
                    nameof(modoId)
                );
        }
        if (anterior == modoId)
        {
            tx.Commit();
            return;
        }
        using (var q = conexion.CreateCommand())
        {
            q.Transaction = tx;
            q.CommandText =
                "UPDATE cadenas SET modo_id=$m WHERE id=$c; INSERT INTO historial_modos_cadena(cadena_id,modo_anterior_id,modo_nuevo_id,fecha) VALUES($c,$a,$m,$f);";
            q.Parameters.AddWithValue("$m", (object?)modoId ?? DBNull.Value);
            q.Parameters.AddWithValue("$a", (object?)anterior ?? DBNull.Value);
            q.Parameters.AddWithValue("$c", cadenaId);
            q.Parameters.AddWithValue(
                "$f",
                DateTime.Now.ToString("o", CultureInfo.InvariantCulture)
            );
            q.ExecuteNonQuery();
        }
        if (modoId is not null)
        {
            using var resolver = conexion.CreateCommand();
            resolver.Transaction = tx;
            resolver.CommandText =
                "UPDATE decisiones_pendientes_cadena SET estado='resuelta' WHERE cadena_id=$c AND tipo='modo' AND estado='pendiente';";
            resolver.Parameters.AddWithValue("$c", cadenaId);
            resolver.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public IReadOnlyList<DecisionModoPendiente> ListarPendientes()
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT version_id,cadena_id,proveedor,opciones_json FROM decisiones_pendientes_cadena WHERE estado='pendiente' AND tipo='modo' ORDER BY creada_en;";
        using var r = q.ExecuteReader();
        var salida = new List<DecisionModoPendiente>();
        while (r.Read())
            salida.Add(
                new(
                    r.GetInt64(0),
                    r.GetInt64(1),
                    r.GetString(2),
                    JsonSerializer.Deserialize<string[]>(r.GetString(3)) ?? []
                )
            );
        return salida;
    }

    public IReadOnlyList<string> ListarHistorial(long cadenaId)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT COALESCE(a.nombre,''),COALESCE(n.nombre,''),h.fecha FROM historial_modos_cadena h LEFT JOIN modos_esquema a ON a.id=h.modo_anterior_id LEFT JOIN modos_esquema n ON n.id=h.modo_nuevo_id WHERE h.cadena_id=$c ORDER BY h.id;";
        q.Parameters.AddWithValue("$c", cadenaId);
        using var r = q.ExecuteReader();
        var salida = new List<string>();
        while (r.Read())
            salida.Add($"{r.GetString(0)} → {r.GetString(1)} ({r.GetString(2)})");
        return salida;
    }

    internal static void RegistrarDecision(
        SqliteConnection conexion,
        long versionId,
        long cadenaId,
        long lugarId
    )
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT e.proveedor,e.lugar_decide_modo,(SELECT json_group_array(nombre) FROM (SELECT nombre FROM modos_esquema WHERE esquema_id=e.id ORDER BY id)) FROM cadenas c JOIN esquemas_cadena e ON e.id=c.esquema_id WHERE c.id=$c;";
        q.Parameters.AddWithValue("$c", cadenaId);
        using var r = q.ExecuteReader();
        if (!r.Read() || r.IsDBNull(1) || r.GetInt64(1) != lugarId)
            return;
        string proveedor = r.GetString(0);
        string opciones = r.GetString(2);
        r.Close();
        if (opciones == "[]")
            return;
        using var existe = conexion.CreateCommand();
        existe.CommandText = "SELECT 1 FROM cadenas WHERE id=$c AND modo_id IS NULL;";
        existe.Parameters.AddWithValue("$c", cadenaId);
        if (existe.ExecuteScalar() is null)
            return;
        using var insertar = conexion.CreateCommand();
        insertar.CommandText =
            "INSERT INTO decisiones_pendientes_cadena(version_id,proveedor,numeros_json,motivo,creada_en,estado,tipo,opciones_json,cadena_id) VALUES($v,$p,'{}','modo pendiente',$f,'pendiente','modo',$o,$c) ON CONFLICT(version_id) DO NOTHING;";
        insertar.Parameters.AddWithValue("$v", versionId);
        insertar.Parameters.AddWithValue("$p", proveedor);
        insertar.Parameters.AddWithValue(
            "$f",
            DateTime.Now.ToString("o", CultureInfo.InvariantCulture)
        );
        insertar.Parameters.AddWithValue("$o", opciones);
        insertar.Parameters.AddWithValue("$c", cadenaId);
        insertar.ExecuteNonQuery();
    }
}

public sealed class RepositorioDatosCadena(SqliteConnection conexion)
{
    public void Guardar(long cadenaId, string nombre, string valor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(valor);
        if (!DatoConfigurado(cadenaId, nombre.Trim()))
            throw new ArgumentException(
                "El nombre del dato no está definido por una regla del esquema.",
                nameof(nombre)
            );
        using var q = conexion.CreateCommand();
        q.CommandText =
            "INSERT INTO datos_cadena(cadena_id,nombre,valor,actualizado_en) SELECT $c,$n,$v,$f WHERE EXISTS(SELECT 1 FROM cadenas WHERE id=$c AND esquema_id IS NOT NULL) ON CONFLICT(cadena_id,nombre) DO UPDATE SET valor=excluded.valor,actualizado_en=excluded.actualizado_en;";
        q.Parameters.AddWithValue("$c", cadenaId);
        q.Parameters.AddWithValue("$n", nombre.Trim());
        q.Parameters.AddWithValue("$v", valor.Trim());
        q.Parameters.AddWithValue("$f", DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
        if (q.ExecuteNonQuery() == 0)
            throw new InvalidOperationException("No existe la cadena indicada.");
        new EvaluadorAlertasEsquema(conexion).Evaluar();
    }

    private bool DatoConfigurado(long cadenaId, string nombre)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT r.tipo,r.parametros_json FROM reglas_alerta_esquema r JOIN cadenas c ON c.esquema_id=r.esquema_id WHERE c.id=$c AND r.tipo IN ('falta_dato','plazo');";
        q.Parameters.AddWithValue("$c", cadenaId);
        using var r = q.ExecuteReader();
        while (r.Read())
        {
            using var json = JsonDocument.Parse(r.GetString(1));
            string propiedad = r.GetString(0) == "falta_dato" ? "dato" : "desdeDato";
            if (
                json.RootElement.TryGetProperty(propiedad, out var dato)
                && dato.ValueKind == JsonValueKind.String
                && string.Equals(dato.GetString(), nombre, StringComparison.OrdinalIgnoreCase)
            )
                return true;
        }
        return false;
    }

    public void GuardarFecha(long cadenaId, string nombre, DateOnly valor) =>
        Guardar(cadenaId, nombre, valor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    public IReadOnlyList<DatoCadena> Listar(long cadenaId)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT nombre,valor,actualizado_en FROM datos_cadena WHERE cadena_id=$c ORDER BY nombre COLLATE NOCASE;";
        q.Parameters.AddWithValue("$c", cadenaId);
        using var r = q.ExecuteReader();
        var salida = new List<DatoCadena>();
        while (r.Read())
            salida.Add(
                new(
                    r.GetString(0),
                    r.GetString(1),
                    DateTime.Parse(r.GetString(2), CultureInfo.InvariantCulture)
                )
            );
        return salida;
    }
}

public sealed class EvaluadorAlertasEsquema(SqliteConnection conexion, Func<DateTime>? reloj = null)
{
    private readonly Func<DateTime> _reloj = reloj ?? (() => DateTime.Now);

    public void Evaluar(DateOnly? hoy = null)
    {
        DateOnly fechaHoy = hoy ?? DateOnly.FromDateTime(_reloj());
        var opcionesJson = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        foreach (var regla in LeerReglas())
        foreach (long cadena in CadenasDeEsquema(regla.EsquemaId, regla.ModoId))
        {
            using var json = JsonDocument.Parse(regla.Json);
            string? texto = null;
            DateOnly? vencimiento = null;
            bool activo = false;
            string alcance = "cadena";
            switch (regla.Tipo)
            {
                case "falta_dato":
                    var falta = json.RootElement.Deserialize<ParametrosFaltaDato>(opcionesJson)!;
                    activo = !DatoExiste(cadena, falta.Dato);
                    texto = falta.Texto;
                    break;
                case "plazo":
                    var plazo = json.RootElement.Deserialize<ParametrosPlazo>(opcionesJson)!;
                    texto = plazo.Texto;
                    foreach (var scope in AlcancesPlazo(cadena, regla.LugarId, plazo))
                    {
                        (bool abierto, DateOnly? vence, string clave) = EstadoPlazo(
                            cadena,
                            scope,
                            plazo,
                            fechaHoy
                        );
                        Sincronizar(regla, cadena, clave, texto, vence, abierto, fechaHoy);
                    }
                    continue;
                case "listo_para":
                    var listo = json.RootElement.Deserialize<ParametrosListoPara>(opcionesJson)!;
                    texto = listo.Lista;
                    foreach (var scope in AlcancesListo(cadena, listo.CuandoLugarId))
                    {
                        bool listoActivo =
                            ExisteLugar(cadena, listo.CuandoLugarId, scope.Linea)
                            && (
                                listo.CondicionLugarId is null
                                || ExisteLugar(cadena, listo.CondicionLugarId.Value, scope.Linea)
                            )
                            && !ExisteLugar(cadena, listo.HastaLugarId, scope.Linea);
                        Sincronizar(regla, cadena, scope.Clave, texto, null, listoActivo, fechaHoy);
                    }
                    continue;
            }
            Sincronizar(regla, cadena, alcance, texto!, vencimiento, activo, fechaHoy);
        }
    }

    public IReadOnlyList<AlertaEsquema> ListarAbiertas(
        string? proveedor = null,
        string? cliente = null,
        long? cadenaId = null,
        string? tipo = null
    )
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT a.id,a.regla_id,a.cadena_id,a.alcance,a.texto,a.fecha_vencimiento,a.urgencia,a.estado,a.motivo,c.proveedor,c.cliente,r.tipo FROM alertas_esquema a JOIN cadenas c ON c.id=a.cadena_id JOIN reglas_alerta_esquema r ON r.id=a.regla_id WHERE a.estado='abierta' AND ($p IS NULL OR c.proveedor=$p COLLATE NOCASE) AND ($cl IS NULL OR c.cliente=$cl COLLATE NOCASE) AND ($c IS NULL OR c.id=$c) AND ($t IS NULL OR r.tipo=$t) ORDER BY CASE a.urgencia WHEN 'vencido' THEN 0 WHEN 'por_vencer' THEN 1 ELSE 2 END,a.fecha_vencimiento,a.id;";
        q.Parameters.AddWithValue("$p", (object?)proveedor ?? DBNull.Value);
        q.Parameters.AddWithValue("$cl", (object?)cliente ?? DBNull.Value);
        q.Parameters.AddWithValue("$c", (object?)cadenaId ?? DBNull.Value);
        q.Parameters.AddWithValue("$t", (object?)tipo ?? DBNull.Value);
        using var r = q.ExecuteReader();
        var salida = new List<AlertaEsquema>();
        while (r.Read())
            salida.Add(
                new(
                    r.GetInt64(0),
                    r.GetInt64(1),
                    r.GetInt64(2),
                    r.GetString(11),
                    r.GetString(3),
                    r.GetString(4),
                    r.IsDBNull(5)
                        ? null
                        : DateOnly.ParseExact(
                            r.GetString(5),
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture
                        ),
                    r.GetString(6),
                    r.GetString(7),
                    r.IsDBNull(8) ? null : r.GetString(8),
                    r.GetString(9),
                    r.IsDBNull(10) ? null : r.GetString(10)
                )
            );
        return salida;
    }

    public IReadOnlyList<(
        string Lista,
        long CadenaId,
        string Proveedor,
        string? Cliente,
        string Alcance
    )> ListarListos(string? lista = null)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT a.texto,a.cadena_id,c.proveedor,c.cliente,a.alcance FROM alertas_esquema a JOIN reglas_alerta_esquema r ON r.id=a.regla_id JOIN cadenas c ON c.id=a.cadena_id WHERE a.estado='abierta' AND r.tipo='listo_para' AND ($l IS NULL OR a.texto=$l) ORDER BY a.texto,c.proveedor,c.cliente,a.cadena_id;";
        q.Parameters.AddWithValue("$l", (object?)lista ?? DBNull.Value);
        using var r = q.ExecuteReader();
        var salida = new List<(string, long, string, string?, string)>();
        while (r.Read())
            salida.Add(
                (
                    r.GetString(0),
                    r.GetInt64(1),
                    r.GetString(2),
                    r.IsDBNull(3) ? null : r.GetString(3),
                    r.GetString(4)
                )
            );
        return salida;
    }

    public IReadOnlyList<ConteoAlertasCadena> Conteos()
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT cadena_id,COUNT(*),CASE MIN(CASE urgencia WHEN 'vencido' THEN 0 WHEN 'por_vencer' THEN 1 ELSE 2 END) WHEN 0 THEN 'vencido' WHEN 1 THEN 'por_vencer' ELSE 'normal' END FROM alertas_esquema WHERE estado='abierta' GROUP BY cadena_id ORDER BY cadena_id;";
        using var r = q.ExecuteReader();
        var salida = new List<ConteoAlertasCadena>();
        while (r.Read())
            salida.Add(new(r.GetInt64(0), r.GetInt32(1), r.GetString(2)));
        return salida;
    }

    public void Descartar(long alertaId, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        using var q = conexion.CreateCommand();
        q.CommandText =
            "UPDATE alertas_esquema SET estado='descartada',motivo=$m,actualizada_en=$f WHERE id=$i AND estado='abierta';";
        q.Parameters.AddWithValue("$m", motivo.Trim());
        q.Parameters.AddWithValue("$f", _reloj().ToString("o", CultureInfo.InvariantCulture));
        q.Parameters.AddWithValue("$i", alertaId);
        if (q.ExecuteNonQuery() == 0)
            throw new InvalidOperationException("El aviso no está abierto.");
    }

    private (bool Abierto, DateOnly? Vence, string Alcance) EstadoPlazo(
        long cadena,
        (long? Linea, string Clave) scope,
        ParametrosPlazo p,
        DateOnly hoy
    )
    {
        if (p.CondicionLugarId is long condicion && !ExisteLugar(cadena, condicion, scope.Linea))
            return (false, null, scope.Clave);
        var fecha = FechaBase(cadena, p, scope.Linea);
        if (fecha is null)
            return (false, null, scope.Clave);
        IReadOnlySet<DateOnly>? feriados = null;
        if (p.CalendarioId is long calendario)
            feriados = new RepositorioCalendariosFeriados(conexion)
                .ListarFeriados(calendario, false)
                .Select(f => f.Fecha)
                .ToHashSet();
        DateOnly vence = CalculoFechas.Sumar(
            fecha.Value,
            p.Dias,
            p.TipoDias == "habiles" ? TipoDias.Habiles : TipoDias.Corridos,
            feriados
        );
        bool destino = ExisteLugar(cadena, p.HastaLugarId, p.PorLinea ? scope.Linea : null);
        // Visible desde que falta el destino (D-84): la urgencia (normal/por vencer/vencido) da el color.
        return (!destino, vence, scope.Clave);
    }

    private DateOnly? FechaBase(long cadena, ParametrosPlazo p, long? linea)
    {
        if (p.DesdeDato is string dato)
        {
            string? valor = LeerDato(cadena, dato);
            return DateOnly.TryParse(valor, CultureInfo.InvariantCulture, out var fecha)
                ? fecha
                : null;
        }
        if (p.DesdeLugarId is not long lugar)
            return null;
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT COALESCE((SELECT vi.fecha_reconocida FROM valores_informativos_documento vi WHERE vi.version_id=e.version_id AND vi.dato='fecha_documento'),substr(e.creada_en,1,10)) FROM vagones_cadena v JOIN enlaces_cadena e ON e.vagon_cadena_id=v.id AND e.estado='activo' WHERE v.cadena_id=$c AND v.lugar_esquema_id=$l AND v.estado='activo' AND ($linea IS NULL OR v.linea_id=$linea) ORDER BY v.id LIMIT 1;";
        q.Parameters.AddWithValue("$c", cadena);
        q.Parameters.AddWithValue("$l", lugar);
        q.Parameters.AddWithValue("$linea", (object?)linea ?? DBNull.Value);
        object? value = q.ExecuteScalar();
        return value is string s && DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var f)
            ? f
            : null;
    }

    private IEnumerable<(long? Linea, string Clave)> AlcancesPlazo(
        long cadena,
        long lugar,
        ParametrosPlazo p
    )
    {
        if (!p.PorLinea)
        {
            yield return (null, "cadena");
            yield break;
        }
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT DISTINCT linea_id FROM vagones_cadena WHERE cadena_id=$c AND lugar_esquema_id=$l AND linea_id IS NOT NULL AND estado='activo' ORDER BY linea_id;";
        q.Parameters.AddWithValue("$c", cadena);
        q.Parameters.AddWithValue("$l", p.DesdeLugarId ?? lugar);
        using var r = q.ExecuteReader();
        var lineas = new List<long>();
        while (r.Read())
            lineas.Add(r.GetInt64(0));
        foreach (long linea in lineas)
            yield return (linea, $"linea:{linea}");
    }

    private IEnumerable<(long? Linea, string Clave)> AlcancesListo(long cadena, long lugar)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT DISTINCT linea_id FROM vagones_cadena WHERE cadena_id=$c AND lugar_esquema_id=$l AND linea_id IS NOT NULL AND estado='activo' ORDER BY linea_id;";
        q.Parameters.AddWithValue("$c", cadena);
        q.Parameters.AddWithValue("$l", lugar);
        using var r = q.ExecuteReader();
        var lineas = new List<long>();
        while (r.Read())
            lineas.Add(r.GetInt64(0));
        if (lineas.Count == 0)
        {
            yield return (null, "cadena");
            yield break;
        }
        foreach (long linea in lineas)
            yield return (linea, $"linea:{linea}");
    }

    private bool ExisteLugar(long cadena, long lugar, long? linea)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT EXISTS(SELECT 1 FROM vagones_cadena v JOIN enlaces_cadena e ON e.vagon_cadena_id=v.id AND e.estado='activo' WHERE v.cadena_id=$c AND v.lugar_esquema_id=$l AND v.estado='activo' AND ($linea IS NULL OR v.linea_id=$linea OR v.linea_id IS NULL));";
        q.Parameters.AddWithValue("$c", cadena);
        q.Parameters.AddWithValue("$l", lugar);
        q.Parameters.AddWithValue("$linea", (object?)linea ?? DBNull.Value);
        return Convert.ToInt64(q.ExecuteScalar()) != 0;
    }

    private string? LeerDato(long cadena, string nombre)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT valor FROM datos_cadena WHERE cadena_id=$c AND nombre=$n COLLATE NOCASE;";
        q.Parameters.AddWithValue("$c", cadena);
        q.Parameters.AddWithValue("$n", nombre);
        return q.ExecuteScalar() as string;
    }

    private bool DatoExiste(long cadena, string nombre) =>
        !string.IsNullOrWhiteSpace(LeerDato(cadena, nombre));

    private void Sincronizar(
        (long Id, long EsquemaId, long LugarId, long? ModoId, string Tipo, string Json) regla,
        long cadena,
        string alcance,
        string texto,
        DateOnly? vence,
        bool activo,
        DateOnly hoy
    )
    {
        string urgencia =
            vence is DateOnly fecha && fecha < hoy ? "vencido"
            : vence is DateOnly d && d <= hoy.AddDays(1) ? "por_vencer"
            : "normal";
        using var q = conexion.CreateCommand();
        q.CommandText =
            "INSERT INTO alertas_esquema(regla_id,cadena_id,alcance,texto,fecha_vencimiento,urgencia,estado,actualizada_en) VALUES($r,$c,$a,$t,$v,$u,$e,$f) ON CONFLICT(regla_id,cadena_id,alcance) DO UPDATE SET texto=excluded.texto,fecha_vencimiento=excluded.fecha_vencimiento,urgencia=excluded.urgencia,estado=CASE WHEN alertas_esquema.estado='descartada' THEN 'descartada' WHEN excluded.estado='cerrada' THEN 'cerrada' ELSE 'abierta' END,actualizada_en=excluded.actualizada_en;";
        q.Parameters.AddWithValue("$r", regla.Id);
        q.Parameters.AddWithValue("$c", cadena);
        q.Parameters.AddWithValue("$a", alcance);
        q.Parameters.AddWithValue("$t", texto);
        q.Parameters.AddWithValue(
            "$v",
            vence?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? (object)DBNull.Value
        );
        q.Parameters.AddWithValue("$u", urgencia);
        q.Parameters.AddWithValue("$e", activo ? "abierta" : "cerrada");
        q.Parameters.AddWithValue("$f", _reloj().ToString("o", CultureInfo.InvariantCulture));
        q.ExecuteNonQuery();
    }

    private IEnumerable<(
        long Id,
        long EsquemaId,
        long LugarId,
        long? ModoId,
        string Tipo,
        string Json
    )> LeerReglas()
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT id,esquema_id,lugar_id,modo_id,tipo,parametros_json FROM reglas_alerta_esquema;";
        using var r = q.ExecuteReader();
        var x = new List<(long, long, long, long?, string, string)>();
        while (r.Read())
            x.Add(
                (
                    r.GetInt64(0),
                    r.GetInt64(1),
                    r.GetInt64(2),
                    r.IsDBNull(3) ? null : r.GetInt64(3),
                    r.GetString(4),
                    r.GetString(5)
                )
            );
        return x;
    }

    private IEnumerable<long> CadenasDeEsquema(long esquema, long? modo)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT id FROM cadenas WHERE esquema_id=$e AND estado='activa' AND ($m IS NULL OR modo_id=$m) ORDER BY id;";
        q.Parameters.AddWithValue("$e", esquema);
        q.Parameters.AddWithValue("$m", (object?)modo ?? DBNull.Value);
        using var r = q.ExecuteReader();
        var x = new List<long>();
        while (r.Read())
            x.Add(r.GetInt64(0));
        return x;
    }
}
