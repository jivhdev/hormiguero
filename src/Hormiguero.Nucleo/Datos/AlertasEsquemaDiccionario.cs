using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed class RepositorioModosEsquemaDiccionario(SqliteConnection conexion)
{
    public void Guardar(long esquemaId, IReadOnlyList<string> nombres)
    {
        using var tx = conexion.BeginTransaction();
        using (var borrar = conexion.CreateCommand())
        {
            borrar.Transaction = tx;
            borrar.CommandText = "DELETE FROM modos_esquema_diccionario WHERE esquema_id=$e;";
            borrar.Parameters.AddWithValue("$e", esquemaId);
            borrar.ExecuteNonQuery();
        }
        foreach (
            string nombre in nombres
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
        )
        {
            using var q = conexion.CreateCommand();
            q.Transaction = tx;
            q.CommandText =
                "INSERT INTO modos_esquema_diccionario(esquema_id,nombre) VALUES($e,$n);";
            q.Parameters.AddWithValue("$e", esquemaId);
            q.Parameters.AddWithValue("$n", nombre);
            q.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public IReadOnlyList<ModoEsquema> Listar(long esquemaId)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT id,esquema_id,nombre FROM modos_esquema_diccionario WHERE esquema_id=$e ORDER BY id;";
        q.Parameters.AddWithValue("$e", esquemaId);
        using var r = q.ExecuteReader();
        var filas = new List<ModoEsquema>();
        while (r.Read())
            filas.Add(new(r.GetInt64(0), r.GetInt64(1), r.GetString(2)));
        return filas;
    }

    public IReadOnlyList<DecisionModoPendiente> ListarPendientes()
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT version_id,cadena_id,proveedor,opciones_json FROM decisiones_esquema_diccionario WHERE tipo='modo' AND estado='pendiente' AND cadena_id IS NOT NULL ORDER BY creada_en;";
        using var r = q.ExecuteReader();
        var filas = new List<DecisionModoPendiente>();
        while (r.Read())
            filas.Add(
                new(
                    r.GetInt64(0),
                    r.GetInt64(1),
                    r.GetString(2),
                    System.Text.Json.JsonSerializer.Deserialize<string[]>(r.GetString(3)) ?? []
                )
            );
        return filas;
    }

    public void FijarModo(long cadenaId, long? modoId)
    {
        using var tx = conexion.BeginTransaction();
        long esquema;
        long? anterior;
        using (var q = conexion.CreateCommand())
        {
            q.Transaction = tx;
            q.CommandText = "SELECT esquema_id,modo_id FROM cadenas_diccionario WHERE id=$c;";
            q.Parameters.AddWithValue("$c", cadenaId);
            using var r = q.ExecuteReader();
            if (!r.Read())
                throw new InvalidOperationException("No existe la cadena indicada.");
            esquema = r.GetInt64(0);
            anterior = r.IsDBNull(1) ? null : r.GetInt64(1);
        }
        if (modoId is long modo)
        {
            using var q = conexion.CreateCommand();
            q.Transaction = tx;
            q.CommandText =
                "SELECT 1 FROM modos_esquema_diccionario WHERE id=$m AND esquema_id=$e;";
            q.Parameters.AddWithValue("$m", modo);
            q.Parameters.AddWithValue("$e", esquema);
            if (q.ExecuteScalar() is null)
                throw new ArgumentException("El modo no pertenece al esquema.");
        }
        if (anterior != modoId)
        {
            using var q = conexion.CreateCommand();
            q.Transaction = tx;
            q.CommandText =
                "UPDATE cadenas_diccionario SET modo_id=$m WHERE id=$c; INSERT INTO historial_modos_cadena_diccionario(cadena_id,modo_anterior_id,modo_nuevo_id,fecha) VALUES($c,$a,$m,$f);";
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
            using var q = conexion.CreateCommand();
            q.Transaction = tx;
            q.CommandText =
                "UPDATE decisiones_esquema_diccionario SET estado='resuelta' WHERE cadena_id=$c AND tipo='modo' AND estado='pendiente';";
            q.Parameters.AddWithValue("$c", cadenaId);
            q.ExecuteNonQuery();
        }
        tx.Commit();
    }
}

public sealed class EvaluadorAlertasEsquemaDiccionario(
    SqliteConnection conexion,
    Func<DateTime>? reloj = null
)
{
    private readonly Func<DateTime> _reloj = reloj ?? (() => DateTime.Now);

    public void Evaluar(DateOnly? hoy = null)
    {
        DateOnly fecha = hoy ?? DateOnly.FromDateTime(_reloj());
        var reglas =
            new List<(long Id, long Esquema, long Lugar, long? Modo, string Tipo, string Json)>();
        using (var q = conexion.CreateCommand())
        {
            q.CommandText =
                "SELECT id,esquema_id,lugar_id,modo_id,tipo,parametros_json FROM reglas_alerta_esquema_diccionario;";
            using var r = q.ExecuteReader();
            while (r.Read())
                reglas.Add(
                    (
                        r.GetInt64(0),
                        r.GetInt64(1),
                        r.GetInt64(2),
                        r.IsDBNull(3) ? null : r.GetInt64(3),
                        r.GetString(4),
                        r.GetString(5)
                    )
                );
        }
        foreach (var regla in reglas)
        {
            var cadenas = new List<(long Id, string Proveedor, string? Cliente)>();
            using (var q = conexion.CreateCommand())
            {
                q.CommandText =
                    "SELECT c.id,e.proveedor,c.cliente FROM cadenas_diccionario c JOIN esquemas_diccionario e ON e.id=c.esquema_id WHERE c.esquema_id=$e AND c.estado='activa' AND ($m IS NULL OR c.modo_id=$m);";
                q.Parameters.AddWithValue("$e", regla.Esquema);
                q.Parameters.AddWithValue("$m", (object?)regla.Modo ?? DBNull.Value);
                using var r = q.ExecuteReader();
                while (r.Read())
                    cadenas.Add(
                        (r.GetInt64(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2))
                    );
            }
            foreach (var c in cadenas)
            {
                using var json = JsonDocument.Parse(regla.Json);
                var root = json.RootElement;
                string texto = "";
                bool activa = false;
                DateOnly? vence = null;
                if (regla.Tipo == "falta_dato")
                {
                    string dato = root.GetProperty("dato").GetString() ?? "";
                    texto = root.GetProperty("texto").GetString() ?? "Falta un dato";
                    activa = string.IsNullOrWhiteSpace(LeerDato(c.Id, dato));
                }
                else if (regla.Tipo == "plazo")
                {
                    texto = root.GetProperty("texto").GetString() ?? "Plazo pendiente";
                    int dias = root.GetProperty("dias").GetInt32();
                    long desdeLugar = root.TryGetProperty("desdeLugarId", out var lugarDesde)
                        ? lugarDesde.GetInt64()
                        : regla.Lugar;
                    long hastaLugar = root.GetProperty("hastaLugarId").GetInt64();
                    string? dato = root.TryGetProperty("desdeDato", out var d)
                        ? d.GetString()
                        : null;
                    if (root.TryGetProperty("porLinea", out var porLinea) && porLinea.GetBoolean())
                    {
                        foreach (long linea in Lineas(c.Id, desdeLugar))
                        {
                            DateOnly? baseLinea = dato is null
                                ? FechaDocumento(c.Id, desdeLugar, linea)
                                : LeerFecha(LeerDato(c.Id, dato));
                            DateOnly? venceLinea = baseLinea is null
                                ? null
                                : SumarDias(baseLinea.Value, dias, root);
                            bool abiertaLinea =
                                baseLinea is not null && !ExisteLugar(c.Id, hastaLugar, linea);
                            GuardarAlerta(
                                regla.Id,
                                c.Id,
                                texto,
                                venceLinea,
                                abiertaLinea,
                                fecha,
                                $"linea:{linea}"
                            );
                        }
                        continue;
                    }
                    DateOnly? fechaBase = dato is null
                        ? FechaDocumento(c.Id, desdeLugar)
                        : LeerFecha(LeerDato(c.Id, dato));
                    if (fechaBase is not null)
                    {
                        vence = SumarDias(fechaBase.Value, dias, root);
                        activa = !ExisteLugar(c.Id, hastaLugar);
                    }
                }
                else if (regla.Tipo == "listo_para")
                {
                    texto = root.GetProperty("lista").GetString() ?? "Listo para";
                    long cuando = root.GetProperty("cuandoLugarId").GetInt64();
                    long hasta = root.GetProperty("hastaLugarId").GetInt64();
                    long? condicion =
                        root.TryGetProperty("condicionLugarId", out var datoCondicion)
                        && datoCondicion.ValueKind == JsonValueKind.Number
                            ? datoCondicion.GetInt64()
                            : null;
                    if (condicion is long tipoCondicion)
                    {
                        foreach (long linea in Lineas(c.Id, cuando))
                        {
                            bool listaPorLinea =
                                ExisteLugar(c.Id, cuando, linea)
                                && ExisteLugar(c.Id, tipoCondicion, linea)
                                && !ExisteLugar(c.Id, hasta, linea);
                            GuardarAlerta(
                                regla.Id,
                                c.Id,
                                texto,
                                null,
                                listaPorLinea,
                                fecha,
                                $"linea:{linea}"
                            );
                        }
                        continue;
                    }
                    activa = ExisteLugar(c.Id, cuando) && !ExisteLugar(c.Id, hasta);
                }
                else
                    continue;
                GuardarAlerta(regla.Id, c.Id, texto, vence, activa, fecha);
            }
        }
    }

    private DateOnly SumarDias(DateOnly fecha, int dias, JsonElement parametros)
    {
        if (!parametros.TryGetProperty("tipoDias", out var tipo) || tipo.GetString() != "habiles")
            return fecha.AddDays(dias);
        int restantes = dias;
        while (restantes > 0)
        {
            fecha = fecha.AddDays(1);
            if (
                fecha.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
                && !EsFeriado(fecha, parametros)
            )
                restantes--;
        }
        return fecha;
    }

    private bool EsFeriado(DateOnly fecha, JsonElement parametros)
    {
        object? calendario =
            parametros.TryGetProperty("calendarioId", out var seleccionado)
            && seleccionado.ValueKind == JsonValueKind.Number
                ? seleccionado.GetInt64()
                : null;
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT EXISTS(SELECT 1 FROM feriados f JOIN calendarios_feriados c ON c.id=f.calendario_id WHERE f.fecha=$f AND f.activo=1 AND c.activo=1 AND c.id=COALESCE($c,(SELECT id FROM calendarios_feriados WHERE activo=1 AND predeterminado=1 LIMIT 1)));";
        q.Parameters.AddWithValue("$f", fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        q.Parameters.AddWithValue("$c", calendario ?? DBNull.Value);
        return Convert.ToInt64(q.ExecuteScalar()) != 0;
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
            "SELECT a.id,a.regla_id,a.cadena_id,r.tipo,a.alcance,a.texto,a.fecha_vencimiento,a.urgencia,a.estado,a.motivo,e.proveedor,c.cliente FROM alertas_esquema_diccionario a JOIN reglas_alerta_esquema_diccionario r ON r.id=a.regla_id JOIN cadenas_diccionario c ON c.id=a.cadena_id JOIN esquemas_diccionario e ON e.id=c.esquema_id WHERE a.estado='abierta' AND ($p IS NULL OR e.proveedor=$p COLLATE NOCASE) AND ($cl IS NULL OR c.cliente=$cl COLLATE NOCASE) AND ($c IS NULL OR c.id=$c) AND ($t IS NULL OR r.tipo=$t) ORDER BY CASE a.urgencia WHEN 'vencido' THEN 0 WHEN 'por_vencer' THEN 1 ELSE 2 END,a.fecha_vencimiento,a.id;";
        q.Parameters.AddWithValue("$p", (object?)proveedor ?? DBNull.Value);
        q.Parameters.AddWithValue("$cl", (object?)cliente ?? DBNull.Value);
        q.Parameters.AddWithValue("$c", (object?)cadenaId ?? DBNull.Value);
        q.Parameters.AddWithValue("$t", (object?)tipo ?? DBNull.Value);
        using var r = q.ExecuteReader();
        var filas = new List<AlertaEsquema>();
        while (r.Read())
            filas.Add(
                new(
                    r.GetInt64(0),
                    r.GetInt64(1),
                    r.GetInt64(2),
                    r.GetString(3),
                    r.GetString(4),
                    r.GetString(5),
                    r.IsDBNull(6)
                        ? null
                        : DateOnly.ParseExact(
                            r.GetString(6),
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture
                        ),
                    r.GetString(7),
                    r.GetString(8),
                    r.IsDBNull(9) ? null : r.GetString(9),
                    r.GetString(10),
                    r.IsDBNull(11) ? null : r.GetString(11)
                )
            );
        return filas;
    }

    public IReadOnlyList<ConteoAlertasCadena> Conteos()
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT cadena_id,COUNT(*),CASE MIN(CASE urgencia WHEN 'vencido' THEN 0 WHEN 'por_vencer' THEN 1 ELSE 2 END) WHEN 0 THEN 'vencido' WHEN 1 THEN 'por_vencer' ELSE 'normal' END FROM alertas_esquema_diccionario WHERE estado='abierta' GROUP BY cadena_id;";
        using var r = q.ExecuteReader();
        var filas = new List<ConteoAlertasCadena>();
        while (r.Read())
            filas.Add(new(r.GetInt64(0), r.GetInt32(1), r.GetString(2)));
        return filas;
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
            "SELECT a.texto,a.cadena_id,e.proveedor,c.cliente,a.alcance FROM alertas_esquema_diccionario a JOIN reglas_alerta_esquema_diccionario r ON r.id=a.regla_id JOIN cadenas_diccionario c ON c.id=a.cadena_id JOIN esquemas_diccionario e ON e.id=c.esquema_id WHERE a.estado='abierta' AND r.tipo='listo_para' AND ($l IS NULL OR a.texto=$l) ORDER BY a.texto,e.proveedor,c.cliente,c.id;";
        q.Parameters.AddWithValue("$l", (object?)lista ?? DBNull.Value);
        using var r = q.ExecuteReader();
        var filas = new List<(string, long, string, string?, string)>();
        while (r.Read())
            filas.Add(
                (
                    r.GetString(0),
                    r.GetInt64(1),
                    r.GetString(2),
                    r.IsDBNull(3) ? null : r.GetString(3),
                    r.GetString(4)
                )
            );
        return filas;
    }

    public void Descartar(long id, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        using var q = conexion.CreateCommand();
        q.CommandText =
            "UPDATE alertas_esquema_diccionario SET estado='descartada',motivo=$m,actualizada_en=$f WHERE id=$i AND estado='abierta';";
        q.Parameters.AddWithValue("$m", motivo.Trim());
        q.Parameters.AddWithValue("$f", _reloj().ToString("o", CultureInfo.InvariantCulture));
        q.Parameters.AddWithValue("$i", id);
        if (q.ExecuteNonQuery() == 0)
            throw new InvalidOperationException("El aviso no está abierto.");
    }

    private string? LeerDato(long c, string n)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT valor FROM datos_cadena_diccionario WHERE cadena_id=$c AND nombre=$n COLLATE NOCASE;";
        q.Parameters.AddWithValue("$c", c);
        q.Parameters.AddWithValue("$n", n);
        return q.ExecuteScalar() as string;
    }

    private bool ExisteLugar(long c, long l, long? linea = null)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT EXISTS(SELECT 1 FROM ubicaciones_cadena_diccionario WHERE cadena_id=$c AND lugar_id=$l AND estado='activo' AND ($linea IS NULL OR linea_id=$linea OR linea_id IS NULL));";
        q.Parameters.AddWithValue("$c", c);
        q.Parameters.AddWithValue("$l", l);
        q.Parameters.AddWithValue("$linea", (object?)linea ?? DBNull.Value);
        return Convert.ToInt64(q.ExecuteScalar()) != 0;
    }

    private IReadOnlyList<long> Lineas(long c, long lugar)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT DISTINCT linea_id FROM ubicaciones_cadena_diccionario WHERE cadena_id=$c AND lugar_id=$l AND linea_id IS NOT NULL AND estado='activo' ORDER BY linea_id;";
        q.Parameters.AddWithValue("$c", c);
        q.Parameters.AddWithValue("$l", lugar);
        using var r = q.ExecuteReader();
        var filas = new List<long>();
        while (r.Read())
            filas.Add(r.GetInt64(0));
        return filas;
    }

    private DateOnly? FechaDocumento(long c, long l, long? linea = null)
    {
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT COALESCE((SELECT fecha_reconocida FROM valores_informativos_documento WHERE version_id=u.version_id AND dato='fecha_documento'),substr(v.registrada_en,1,10)) FROM ubicaciones_cadena_diccionario u JOIN versiones_documento v ON v.id=u.version_id WHERE u.cadena_id=$c AND u.lugar_id=$l AND u.estado='activo' AND ($linea IS NULL OR u.linea_id=$linea) ORDER BY u.orden LIMIT 1;";
        q.Parameters.AddWithValue("$c", c);
        q.Parameters.AddWithValue("$l", l);
        q.Parameters.AddWithValue("$linea", (object?)linea ?? DBNull.Value);
        return LeerFecha(q.ExecuteScalar() as string);
    }

    private static DateOnly? LeerFecha(string? valor) =>
        DateOnly.TryParse(valor, CultureInfo.InvariantCulture, out var f) ? f : null;

    private void GuardarAlerta(
        long regla,
        long cadena,
        string texto,
        DateOnly? vence,
        bool activa,
        DateOnly hoy,
        string alcance = "cadena"
    )
    {
        string urgencia =
            vence is DateOnly d && d < hoy ? "vencido"
            : vence is DateOnly x && x <= hoy.AddDays(1) ? "por_vencer"
            : "normal";
        using var q = conexion.CreateCommand();
        q.CommandText =
            "INSERT INTO alertas_esquema_diccionario(regla_id,cadena_id,alcance,texto,fecha_vencimiento,urgencia,estado,actualizada_en) VALUES($r,$c,$a,$t,$v,$u,$e,$f) ON CONFLICT(regla_id,cadena_id,alcance) DO UPDATE SET texto=excluded.texto,fecha_vencimiento=excluded.fecha_vencimiento,urgencia=excluded.urgencia,estado=CASE WHEN alertas_esquema_diccionario.estado='descartada' THEN 'descartada' WHEN excluded.estado='cerrada' THEN 'cerrada' ELSE 'abierta' END,actualizada_en=excluded.actualizada_en;";
        q.Parameters.AddWithValue("$r", regla);
        q.Parameters.AddWithValue("$c", cadena);
        q.Parameters.AddWithValue("$a", alcance);
        q.Parameters.AddWithValue("$t", texto);
        q.Parameters.AddWithValue(
            "$v",
            vence?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? (object)DBNull.Value
        );
        q.Parameters.AddWithValue("$u", urgencia);
        q.Parameters.AddWithValue("$e", activa ? "abierta" : "cerrada");
        q.Parameters.AddWithValue("$f", _reloj().ToString("o", CultureInfo.InvariantCulture));
        q.ExecuteNonQuery();
    }
}
