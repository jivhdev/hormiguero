using System.Globalization;
using System.Text.Json;
using Hormiguero.Nucleo.Utilidades;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed record ReglaAlerta(
    long Id,
    string Nombre,
    long ModeloCadenaId,
    long VagonOrigenModeloId,
    long VagonDestinoModeloId,
    string Evento,
    int Dias,
    TipoDias ModoDias,
    long? CalendarioId,
    string TextoAviso,
    bool Repetir,
    string Estado
);

public sealed record ReglaAlertaCadenaSimple(
    long Id,
    string Nombre,
    string? DatoOrigenId,
    long? IdentificacionOrigenId,
    string? DatoDestinoId,
    long? IdentificacionDestinoId,
    int Dias,
    TipoDias ModoDias,
    long? CalendarioId,
    string TextoAviso,
    string Estado
);

public sealed record Alerta(
    long Id,
    long? ReglaId,
    long? CadenaId,
    long? VagonCadenaId,
    long? VersionId,
    string Texto,
    string Estado,
    string? Motivo,
    DateOnly FechaObjetivo,
    DateTime CreadaEn
);

public sealed record HistorialAlerta(
    long Id,
    long AlertaId,
    string Accion,
    string? EstadoAnterior,
    string EstadoNuevo,
    string? Motivo,
    string? DatosAnteriores,
    string? DatosNuevos,
    DateTime Fecha,
    string App
);

public sealed record ResultadoAlertas(IReadOnlyList<Alerta> Alertas, int Total);

public sealed class RepositorioAlertas(SqliteConnection conexion)
{
    private const string AppAuditoria = "Nucleo";

    public long CrearReglaCadenaSimple(
        string nombre,
        string? datoOrigenId,
        long? identificacionOrigenId,
        string? datoDestinoId,
        long? identificacionDestinoId,
        int dias,
        TipoDias modoDias,
        string textoAviso,
        long? calendarioId = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(textoAviso);
        if (
            (datoOrigenId is null) == (identificacionOrigenId is null)
            || (datoDestinoId is null) == (identificacionDestinoId is null)
        )
            throw new ArgumentException(
                "Indique un tipo de documento o dato del diccionario para cada lado de la regla."
            );
        if (dias is < 0 or > CalculoFechas.CantidadMaxima)
            throw new ArgumentOutOfRangeException(nameof(dias));
        using var tx = conexion.BeginTransaction();
        if (calendarioId is long calendario)
            ValidarCalendario(tx, calendario);
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO reglas_alerta_cadena_simple(nombre,dato_origen_id,identificacion_origen_id,dato_destino_id,identificacion_destino_id,dias,modo_dias,calendario_id,texto_aviso,creada_en,actualizada_en) VALUES($n,$do,$io,$dd,$id,$dias,$modo,$cal,$texto,$ahora,$ahora) RETURNING id;";
        cmd.Parameters.AddWithValue("$n", nombre.Trim());
        cmd.Parameters.AddWithValue("$do", (object?)datoOrigenId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$io", (object?)identificacionOrigenId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$dd", (object?)datoDestinoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", (object?)identificacionDestinoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$dias", dias);
        cmd.Parameters.AddWithValue("$modo", ATexto(modoDias));
        cmd.Parameters.AddWithValue("$cal", (object?)calendarioId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$texto", textoAviso.Trim());
        cmd.Parameters.AddWithValue("$ahora", Ahora());
        long id = Convert.ToInt64(cmd.ExecuteScalar());
        AuditoriaDatos.Registrar(conexion, tx, "crear_regla_alerta_cadena_simple", $"regla:{id}");
        tx.Commit();
        return id;
    }

    public IReadOnlyList<ReglaAlertaCadenaSimple> ListarReglasCadenaSimple()
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id,nombre,dato_origen_id,identificacion_origen_id,dato_destino_id,identificacion_destino_id,dias,modo_dias,calendario_id,texto_aviso,estado FROM reglas_alerta_cadena_simple ORDER BY id;";
        using var r = cmd.ExecuteReader();
        var resultado = new List<ReglaAlertaCadenaSimple>();
        while (r.Read())
            resultado.Add(
                new(
                    r.GetInt64(0),
                    r.GetString(1),
                    r.IsDBNull(2) ? null : r.GetString(2),
                    r.IsDBNull(3) ? null : r.GetInt64(3),
                    r.IsDBNull(4) ? null : r.GetString(4),
                    r.IsDBNull(5) ? null : r.GetInt64(5),
                    r.GetInt32(6),
                    DesdeTexto(r.GetString(7)),
                    r.IsDBNull(8) ? null : r.GetInt64(8),
                    r.GetString(9),
                    r.GetString(10)
                )
            );
        return resultado;
    }

    public long CrearAlertaDeReglaCadenaSimple(long reglaId, long cadenaId, DateOnly fechaBase)
    {
        ReglaAlertaCadenaSimple regla = ListarReglasCadenaSimple()
            .Single(r => r.Id == reglaId && r.Estado == "activa");
        DateOnly objetivo = CalculoFechas.Sumar(
            fechaBase,
            regla.Dias,
            regla.ModoDias,
            regla.CalendarioId is long cal
                ? new RepositorioCalendariosFeriados(conexion).ObtenerFeriadosActivos(cal, 1, 9999)
                : null
        );
        using var tx = conexion.BeginTransaction();
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO alertas(regla_id,regla_simple_id,cadena_id,texto,estado,fecha_base,cantidad_dias,modo_dias,calendario_id,fecha_objetivo,creada_en,actualizada_en) VALUES(NULL,$r,$c,$texto,'pendiente',$base,$dias,$modo,$cal,$objetivo,$ahora,$ahora) ON CONFLICT(regla_simple_id,cadena_id) WHERE regla_simple_id IS NOT NULL DO NOTHING RETURNING id;";
        cmd.Parameters.AddWithValue("$r", reglaId);
        cmd.Parameters.AddWithValue("$c", cadenaId);
        cmd.Parameters.AddWithValue("$texto", regla.TextoAviso);
        cmd.Parameters.AddWithValue("$base", Fecha(fechaBase));
        cmd.Parameters.AddWithValue("$dias", regla.Dias);
        cmd.Parameters.AddWithValue("$modo", ATexto(regla.ModoDias));
        cmd.Parameters.AddWithValue("$cal", (object?)regla.CalendarioId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$objetivo", Fecha(objetivo));
        cmd.Parameters.AddWithValue("$ahora", Ahora());
        object? inserted = cmd.ExecuteScalar();
        if (inserted is null)
        {
            using var existing = conexion.CreateCommand();
            existing.Transaction = tx;
            existing.CommandText =
                "SELECT id FROM alertas WHERE regla_simple_id=$r AND cadena_id=$c;";
            existing.Parameters.AddWithValue("$r", reglaId);
            existing.Parameters.AddWithValue("$c", cadenaId);
            long oldId = Convert.ToInt64(existing.ExecuteScalar());
            tx.Commit();
            return oldId;
        }
        long id = Convert.ToInt64(inserted);
        RegistrarHistoria(tx, id, "creada_por_regla", null, "pendiente", null, null, null);
        AuditoriaDatos.Registrar(conexion, tx, "crear_alerta_regla_cadena_simple", $"alerta:{id}");
        tx.Commit();
        return id;
    }

    public long? AlertaDeReglaCadenaSimple(long reglaId, long cadenaId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT id FROM alertas WHERE regla_simple_id=$r AND cadena_id=$c;";
        cmd.Parameters.AddWithValue("$r", reglaId);
        cmd.Parameters.AddWithValue("$c", cadenaId);
        object? id = cmd.ExecuteScalar();
        return id is null ? null : Convert.ToInt64(id);
    }

    public long CrearRegla(
        string nombre,
        long modeloCadenaId,
        long vagonOrigenModeloId,
        long vagonDestinoModeloId,
        string evento,
        int dias,
        TipoDias modoDias,
        string textoAviso,
        long? calendarioId = null,
        bool repetir = false
    )
    {
        ValidarRegla(nombre, evento, textoAviso, dias, calendarioId);
        string ahora = Ahora();
        using var tx = conexion.BeginTransaction();
        ValidarVagonesModelo(tx, modeloCadenaId, vagonOrigenModeloId, vagonDestinoModeloId);
        if (calendarioId is long calendario)
            ValidarCalendario(tx, calendario);
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO reglas_alerta(nombre,modelo_cadena_id,vagon_origen_modelo_id,vagon_destino_modelo_id,evento,dias,modo_dias,calendario_id,texto_aviso,repetir,estado,creada_en,actualizada_en) "
            + "VALUES($nombre,$modelo,$origen,$destino,$evento,$dias,$modo,$calendario,$texto,$repetir,'activa',$ahora,$ahora) RETURNING id;";
        cmd.Parameters.AddWithValue("$nombre", nombre.Trim());
        cmd.Parameters.AddWithValue("$modelo", modeloCadenaId);
        cmd.Parameters.AddWithValue("$origen", vagonOrigenModeloId);
        cmd.Parameters.AddWithValue("$destino", vagonDestinoModeloId);
        cmd.Parameters.AddWithValue("$evento", evento.Trim());
        cmd.Parameters.AddWithValue("$dias", dias);
        cmd.Parameters.AddWithValue("$modo", ATexto(modoDias));
        cmd.Parameters.AddWithValue("$calendario", (object?)calendarioId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$texto", textoAviso.Trim());
        cmd.Parameters.AddWithValue("$repetir", repetir ? 1 : 0);
        cmd.Parameters.AddWithValue("$ahora", ahora);
        long id = Convert.ToInt64(cmd.ExecuteScalar());
        AuditoriaDatos.Registrar(conexion, tx, "crear_regla_alerta", $"regla:{id}");
        tx.Commit();
        return id;
    }

    public void EditarRegla(
        long reglaId,
        string nombre,
        long modeloCadenaId,
        long vagonOrigenModeloId,
        long vagonDestinoModeloId,
        string evento,
        int dias,
        TipoDias modoDias,
        string textoAviso,
        long? calendarioId,
        bool repetir
    )
    {
        ValidarRegla(nombre, evento, textoAviso, dias, calendarioId);
        using var tx = conexion.BeginTransaction();
        ValidarVagonesModelo(tx, modeloCadenaId, vagonOrigenModeloId, vagonDestinoModeloId);
        if (calendarioId is long calendario)
            ValidarCalendario(tx, calendario);
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "UPDATE reglas_alerta SET nombre=$nombre,modelo_cadena_id=$modelo,vagon_origen_modelo_id=$origen,vagon_destino_modelo_id=$destino,evento=$evento,dias=$dias,modo_dias=$modo,calendario_id=$calendario,texto_aviso=$texto,repetir=$repetir,actualizada_en=$ahora "
            + "WHERE id=$id AND estado='activa';";
        cmd.Parameters.AddWithValue("$nombre", nombre.Trim());
        cmd.Parameters.AddWithValue("$modelo", modeloCadenaId);
        cmd.Parameters.AddWithValue("$origen", vagonOrigenModeloId);
        cmd.Parameters.AddWithValue("$destino", vagonDestinoModeloId);
        cmd.Parameters.AddWithValue("$evento", evento.Trim());
        cmd.Parameters.AddWithValue("$dias", dias);
        cmd.Parameters.AddWithValue("$modo", ATexto(modoDias));
        cmd.Parameters.AddWithValue("$calendario", (object?)calendarioId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$texto", textoAviso.Trim());
        cmd.Parameters.AddWithValue("$repetir", repetir ? 1 : 0);
        cmd.Parameters.AddWithValue("$ahora", Ahora());
        cmd.Parameters.AddWithValue("$id", reglaId);
        if (cmd.ExecuteNonQuery() == 0)
            throw new InvalidOperationException(
                $"No existe una regla activa con identificador {reglaId}."
            );
        AuditoriaDatos.Registrar(conexion, tx, "editar_regla_alerta", $"regla:{reglaId}");
        tx.Commit();
    }

    public bool AnularRegla(long reglaId)
    {
        using var tx = conexion.BeginTransaction();
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "UPDATE reglas_alerta SET estado='anulada',fecha_anulacion=$ahora,actualizada_en=$ahora WHERE id=$id AND estado='activa';";
        cmd.Parameters.AddWithValue("$ahora", Ahora());
        cmd.Parameters.AddWithValue("$id", reglaId);
        if (cmd.ExecuteNonQuery() == 0)
        {
            tx.Rollback();
            return false;
        }
        AuditoriaDatos.Registrar(conexion, tx, "anular_regla_alerta", $"regla:{reglaId}");
        tx.Commit();
        return true;
    }

    public IReadOnlyList<ReglaAlerta> ListarReglas()
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id,nombre,modelo_cadena_id,vagon_origen_modelo_id,vagon_destino_modelo_id,evento,dias,modo_dias,calendario_id,texto_aviso,repetir,estado FROM reglas_alerta ORDER BY id;";
        using var reader = cmd.ExecuteReader();
        var lista = new List<ReglaAlerta>();
        while (reader.Read())
            lista.Add(LeerRegla(reader));
        return lista;
    }

    public DateOnly CalcularVencimiento(long reglaId, DateOnly fechaBase)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT dias,modo_dias,calendario_id FROM reglas_alerta WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", reglaId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException(
                $"No existe una regla con identificador {reglaId}."
            );
        int dias = reader.GetInt32(0);
        TipoDias modo = DesdeTexto(reader.GetString(1));
        long? calendario = reader.IsDBNull(2) ? null : reader.GetInt64(2);
        reader.Close();
        IReadOnlySet<DateOnly>? feriados = calendario is long calendarioId
            ? new RepositorioCalendariosFeriados(conexion).ObtenerFeriadosActivos(
                calendarioId,
                1,
                9999
            )
            : null;
        return CalculoFechas.Sumar(fechaBase, dias, modo, feriados);
    }

    public long CrearAlertaDeRegla(
        long reglaId,
        long cadenaId,
        long vagonCadenaId,
        DateOnly fechaBase,
        string? claveEvento = null
    )
    {
        ReglaAlerta regla = ObtenerRegla(reglaId);
        DateOnly objetivo = CalcularVencimiento(reglaId, fechaBase);
        using var tx = conexion.BeginTransaction();
        ValidarDestinoCadena(
            tx,
            regla.ModeloCadenaId,
            cadenaId,
            vagonCadenaId,
            regla.VagonDestinoModeloId
        );
        string ahora = Ahora();
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO alertas(regla_id,cadena_id,vagon_cadena_id,clave_evento,texto,estado,fecha_base,cantidad_dias,modo_dias,calendario_id,fecha_objetivo,creada_en,actualizada_en) VALUES($regla,$cadena,$vagon,$evento,$texto,'pendiente',$base,$dias,$modo,$calendario,$objetivo,$ahora,$ahora) ON CONFLICT(regla_id,cadena_id,vagon_cadena_id) WHERE regla_id IS NOT NULL DO NOTHING RETURNING id;";
        cmd.Parameters.AddWithValue("$regla", reglaId);
        cmd.Parameters.AddWithValue("$cadena", cadenaId);
        cmd.Parameters.AddWithValue("$vagon", vagonCadenaId);
        cmd.Parameters.AddWithValue("$evento", (object?)claveEvento ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$texto", regla.TextoAviso);
        cmd.Parameters.AddWithValue("$base", Fecha(fechaBase));
        cmd.Parameters.AddWithValue("$dias", regla.Dias);
        cmd.Parameters.AddWithValue("$modo", ATexto(regla.ModoDias));
        cmd.Parameters.AddWithValue("$calendario", (object?)regla.CalendarioId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$objetivo", Fecha(objetivo));
        cmd.Parameters.AddWithValue("$ahora", ahora);
        object? result = cmd.ExecuteScalar();
        if (result is null)
        {
            using var existing = conexion.CreateCommand();
            existing.Transaction = tx;
            existing.CommandText =
                "SELECT id FROM alertas WHERE regla_id=$regla AND cadena_id=$cadena AND vagon_cadena_id=$vagon;";
            existing.Parameters.AddWithValue("$regla", reglaId);
            existing.Parameters.AddWithValue("$cadena", cadenaId);
            existing.Parameters.AddWithValue("$vagon", vagonCadenaId);
            long existingId = Convert.ToInt64(existing.ExecuteScalar());
            tx.Commit();
            return existingId;
        }
        long id = Convert.ToInt64(result);
        RegistrarHistoria(
            tx,
            id,
            "creada",
            null,
            "pendiente",
            null,
            null,
            JsonSerializer.Serialize(
                new
                {
                    reglaId,
                    cadenaId,
                    vagonCadenaId,
                    fechaBase,
                    objetivo,
                }
            )
        );
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "crear_alerta_regla",
            $"alerta:{id}",
            $"cadena:{cadenaId}"
        );
        tx.Commit();
        return id;
    }

    public long CrearAlertaManual(
        string texto,
        DateOnly fechaObjetivo,
        long? cadenaId = null,
        long? vagonCadenaId = null,
        long? versionId = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(texto);
        if ((cadenaId is null) == (versionId is null))
            throw new ArgumentException(
                "La alerta debe asociarse a una cadena o a una versión de documento."
            );
        if (vagonCadenaId is not null && cadenaId is null)
            throw new ArgumentException("El vagón requiere una cadena asociada.");
        using var tx = conexion.BeginTransaction();
        if (cadenaId is long cadena)
            ValidarCadena(tx, cadena, vagonCadenaId);
        if (versionId is long version)
            ValidarVersion(tx, version);
        string ahora = Ahora();
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO alertas(cadena_id,vagon_cadena_id,version_id,texto,estado,fecha_objetivo,creada_en,actualizada_en) VALUES($cadena,$vagon,$version,$texto,'pendiente',$fecha,$ahora,$ahora) RETURNING id;";
        cmd.Parameters.AddWithValue("$cadena", (object?)cadenaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$vagon", (object?)vagonCadenaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$version", (object?)versionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$texto", texto.Trim());
        cmd.Parameters.AddWithValue("$fecha", Fecha(fechaObjetivo));
        cmd.Parameters.AddWithValue("$ahora", ahora);
        long id = Convert.ToInt64(cmd.ExecuteScalar());
        RegistrarHistoria(
            tx,
            id,
            "creada_manual",
            null,
            "pendiente",
            null,
            null,
            JsonSerializer.Serialize(
                new
                {
                    texto = texto.Trim(),
                    fechaObjetivo,
                    cadenaId,
                    vagonCadenaId,
                    versionId,
                }
            )
        );
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "crear_alerta_manual",
            $"alerta:{id}",
            cadenaId is null ? $"version:{versionId}" : $"cadena:{cadenaId}"
        );
        tx.Commit();
        return id;
    }

    public void Resolver(long alertaId, string motivo) =>
        CambiarEstado(alertaId, "resuelta", motivo);

    public void Descartar(long alertaId, string motivo) =>
        CambiarEstado(alertaId, "descartada", motivo);

    public void Reabrir(long alertaId) => CambiarEstado(alertaId, "pendiente", null);

    public void MarcarVencida(long alertaId) => CambiarEstado(alertaId, "vencida", null);

    public ResultadoAlertas Listar(
        string? estado = null,
        long? cadenaId = null,
        long? versionId = null,
        long? documentoId = null,
        int limite = 1000,
        int desplazamiento = 0
    )
    {
        if (limite is < 1 or > 10000 || desplazamiento < 0)
            throw new ArgumentOutOfRangeException(nameof(limite));
        if (
            estado is not null
            && estado is not ("pendiente" or "vencida" or "resuelta" or "descartada")
        )
            throw new ArgumentException("El estado de alerta no es válido.", nameof(estado));
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT a.id,a.regla_id,a.cadena_id,a.vagon_cadena_id,a.version_id,a.texto,a.estado,a.motivo,a.fecha_objetivo,a.creada_en,COUNT(*) OVER() FROM alertas a LEFT JOIN versiones_documento v ON v.id=a.version_id WHERE ($estado IS NULL OR a.estado=$estado) AND ($cadena IS NULL OR a.cadena_id=$cadena) AND ($version IS NULL OR a.version_id=$version) AND ($documento IS NULL OR v.documento_id=$documento) ORDER BY a.fecha_objetivo,a.id LIMIT $limite OFFSET $desplazamiento;";
        cmd.Parameters.AddWithValue("$estado", (object?)estado ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cadena", (object?)cadenaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$version", (object?)versionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$documento", (object?)documentoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$limite", limite);
        cmd.Parameters.AddWithValue("$desplazamiento", desplazamiento);
        using var reader = cmd.ExecuteReader();
        var alertas = new List<Alerta>();
        int total = 0;
        while (reader.Read())
        {
            total = reader.GetInt32(10);
            alertas.Add(LeerAlerta(reader));
        }
        return new ResultadoAlertas(alertas, total);
    }

    public IReadOnlyList<HistorialAlerta> ObtenerHistorial(long alertaId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id,alerta_id,accion,estado_anterior,estado_nuevo,motivo,datos_anteriores,datos_nuevos,fecha,app FROM historial_alertas WHERE alerta_id=$id ORDER BY id;";
        cmd.Parameters.AddWithValue("$id", alertaId);
        using var reader = cmd.ExecuteReader();
        var historial = new List<HistorialAlerta>();
        while (reader.Read())
            historial.Add(
                new(
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    reader.GetString(2),
                    NuloTexto(reader, 3),
                    reader.GetString(4),
                    NuloTexto(reader, 5),
                    NuloTexto(reader, 6),
                    NuloTexto(reader, 7),
                    DateTime.Parse(reader.GetString(8), CultureInfo.InvariantCulture),
                    reader.GetString(9)
                )
            );
        return historial;
    }

    private void CambiarEstado(long id, string nuevo, string? motivo)
    {
        if (nuevo is "resuelta" or "descartada")
            ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        using var tx = conexion.BeginTransaction();
        string? anterior;
        using (var consulta = conexion.CreateCommand())
        {
            consulta.Transaction = tx;
            consulta.CommandText = "SELECT estado FROM alertas WHERE id=$id;";
            consulta.Parameters.AddWithValue("$id", id);
            anterior = consulta.ExecuteScalar() as string;
        }
        if (anterior is null)
            throw new InvalidOperationException($"No existe una alerta con identificador {id}.");
        bool valida = nuevo switch
        {
            "pendiente" => anterior is "resuelta" or "descartada",
            "vencida" => anterior == "pendiente",
            "resuelta" or "descartada" => anterior is "pendiente" or "vencida",
            _ => false,
        };
        if (!valida)
            throw new InvalidOperationException(
                $"No se puede cambiar una alerta de estado {anterior} a {nuevo}."
            );
        string ahora = Ahora();
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "UPDATE alertas SET estado=$nuevo,motivo=$motivo,actualizada_en=$ahora,resuelta_en=CASE WHEN $nuevo='resuelta' THEN $ahora WHEN $nuevo='pendiente' THEN NULL ELSE resuelta_en END,descartada_en=CASE WHEN $nuevo='descartada' THEN $ahora WHEN $nuevo='pendiente' THEN NULL ELSE descartada_en END WHERE id=$id;";
        cmd.Parameters.AddWithValue("$nuevo", nuevo);
        cmd.Parameters.AddWithValue("$motivo", (object?)motivo?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ahora", ahora);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
        string accion = nuevo switch
        {
            "resuelta" => "resolver",
            "descartada" => "descartar",
            "vencida" => "marcar_vencida",
            _ => "reabrir",
        };
        RegistrarHistoria(tx, id, accion, anterior, nuevo, motivo, null, null);
        AuditoriaDatos.Registrar(conexion, tx, $"{accion}_alerta", $"alerta:{id}");
        tx.Commit();
    }

    private void ValidarVagonesModelo(SqliteTransaction tx, long modelo, long origen, long destino)
    {
        if (origen == destino)
            throw new InvalidOperationException(
                "Los vagones de origen y destino deben ser distintos."
            );
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "SELECT COUNT(*) FROM vagones_modelo WHERE modelo_id=$modelo AND id IN ($origen,$destino);";
        cmd.Parameters.AddWithValue("$modelo", modelo);
        cmd.Parameters.AddWithValue("$origen", origen);
        cmd.Parameters.AddWithValue("$destino", destino);
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 2)
            throw new InvalidOperationException(
                "Los vagones de origen y destino deben pertenecer al modelo indicado."
            );
    }

    private void ValidarDestinoCadena(
        SqliteTransaction tx,
        long modelo,
        long cadena,
        long vagon,
        long vagonModelo
    )
    {
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "SELECT COUNT(*) FROM cadenas c JOIN vagones_cadena v ON v.cadena_id=c.id WHERE c.id=$cadena AND c.modelo_id=$modelo AND c.estado='activa' AND v.id=$vagon AND v.vagon_modelo_id=$vagonModelo AND v.estado='activo';";
        cmd.Parameters.AddWithValue("$cadena", cadena);
        cmd.Parameters.AddWithValue("$modelo", modelo);
        cmd.Parameters.AddWithValue("$vagon", vagon);
        cmd.Parameters.AddWithValue("$vagonModelo", vagonModelo);
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
            throw new InvalidOperationException(
                "El vagón de alerta no corresponde al modelo y cadena indicados."
            );
    }

    private void ValidarCadena(SqliteTransaction tx, long cadena, long? vagon)
    {
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = vagon is null
            ? "SELECT 1 FROM cadenas WHERE id=$cadena AND estado='activa';"
            : "SELECT 1 FROM vagones_cadena WHERE cadena_id=$cadena AND id=$vagon AND estado='activo';";
        cmd.Parameters.AddWithValue("$cadena", cadena);
        cmd.Parameters.AddWithValue("$vagon", (object?)vagon ?? DBNull.Value);
        if (cmd.ExecuteScalar() is null)
            throw new InvalidOperationException(
                "La cadena o el vagón indicado no existe o está anulado."
            );
    }

    private void ValidarVersion(SqliteTransaction tx, long version)
    {
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT 1 FROM versiones_documento WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", version);
        if (cmd.ExecuteScalar() is null)
            throw new InvalidOperationException("La versión indicada no existe.");
    }

    private void ValidarCalendario(SqliteTransaction tx, long calendario)
    {
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT 1 FROM calendarios_feriados WHERE id=$id AND activo=1;";
        cmd.Parameters.AddWithValue("$id", calendario);
        if (cmd.ExecuteScalar() is null)
            throw new InvalidOperationException("El calendario no existe o está anulado.");
    }

    private ReglaAlerta ObtenerRegla(long id)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id,nombre,modelo_cadena_id,vagon_origen_modelo_id,vagon_destino_modelo_id,evento,dias,modo_dias,calendario_id,texto_aviso,repetir,estado FROM reglas_alerta WHERE id=$id AND estado='activa';";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException(
                $"No existe una regla activa con identificador {id}."
            );
        return LeerRegla(reader);
    }

    private static ReglaAlerta LeerRegla(SqliteDataReader r) =>
        new(
            r.GetInt64(0),
            r.GetString(1),
            r.GetInt64(2),
            r.GetInt64(3),
            r.GetInt64(4),
            r.GetString(5),
            r.GetInt32(6),
            DesdeTexto(r.GetString(7)),
            r.IsDBNull(8) ? null : r.GetInt64(8),
            r.GetString(9),
            r.GetInt32(10) != 0,
            r.GetString(11)
        );

    private static Alerta LeerAlerta(SqliteDataReader r) =>
        new(
            r.GetInt64(0),
            NuloLong(r, 1),
            NuloLong(r, 2),
            NuloLong(r, 3),
            NuloLong(r, 4),
            r.GetString(5),
            r.GetString(6),
            NuloTexto(r, 7),
            DateOnly.ParseExact(r.GetString(8), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime.Parse(r.GetString(9), CultureInfo.InvariantCulture)
        );

    private void RegistrarHistoria(
        SqliteTransaction tx,
        long alerta,
        string accion,
        string? anterior,
        string nuevo,
        string? motivo,
        string? datosAnteriores,
        string? datosNuevos
    )
    {
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO historial_alertas(alerta_id,accion,estado_anterior,estado_nuevo,motivo,datos_anteriores,datos_nuevos,fecha,app) VALUES($alerta,$accion,$anterior,$nuevo,$motivo,$datosAnteriores,$datosNuevos,$fecha,$app);";
        cmd.Parameters.AddWithValue("$alerta", alerta);
        cmd.Parameters.AddWithValue("$accion", accion);
        cmd.Parameters.AddWithValue("$anterior", (object?)anterior ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$nuevo", nuevo);
        cmd.Parameters.AddWithValue("$motivo", (object?)motivo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$datosAnteriores", (object?)datosAnteriores ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$datosNuevos", (object?)datosNuevos ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$fecha", Ahora());
        cmd.Parameters.AddWithValue("$app", AppAuditoria);
        cmd.ExecuteNonQuery();
    }

    private static void ValidarRegla(
        string nombre,
        string evento,
        string texto,
        int dias,
        long? calendario
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(evento);
        ArgumentException.ThrowIfNullOrWhiteSpace(texto);
        if (dias is < 0 or > CalculoFechas.CantidadMaxima)
            throw new ArgumentOutOfRangeException(nameof(dias));
        if (calendario is <= 0)
            throw new ArgumentOutOfRangeException(nameof(calendario));
    }

    private static string ATexto(TipoDias modo) =>
        modo switch
        {
            TipoDias.Corridos => "corridos",
            TipoDias.Habiles => "habiles",
            _ => throw new ArgumentOutOfRangeException(nameof(modo)),
        };

    private static TipoDias DesdeTexto(string valor) =>
        valor switch
        {
            "corridos" => TipoDias.Corridos,
            "habiles" => TipoDias.Habiles,
            _ => throw new InvalidOperationException("El modo de días almacenado no es válido."),
        };

    private static string Fecha(DateOnly fecha) =>
        fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Ahora() => DateTime.Now.ToString("o", CultureInfo.InvariantCulture);

    private static string? NuloTexto(SqliteDataReader r, int indice) =>
        r.IsDBNull(indice) ? null : r.GetString(indice);

    private static long? NuloLong(SqliteDataReader r, int indice) =>
        r.IsDBNull(indice) ? null : r.GetInt64(indice);
}
