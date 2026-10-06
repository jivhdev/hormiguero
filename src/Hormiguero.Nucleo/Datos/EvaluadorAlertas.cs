// Una alerta vence el día siguiente a su fecha objetivo: el mismo día queda pendiente ("vence hoy").
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed class EvaluadorAlertas(SqliteConnection conexion)
{
    public void Evaluar(DateOnly? hoy = null)
    {
        DateOnly fechaHoy = hoy ?? DateOnly.FromDateTime(DateTime.Today);
        try
        {
            var alertas = new RepositorioAlertas(conexion);
            EvaluarReglas(alertas, fechaHoy);
            EvaluarReglasCadenasSimples(alertas, fechaHoy);
            EvaluarManuales(alertas, fechaHoy);
        }
        catch (Exception error)
        {
            try
            {
                using var auditoria = conexion.CreateCommand();
                auditoria.CommandText =
                    "INSERT INTO auditoria(fecha,app,accion,origen,destino,resultado) VALUES($fecha,'Nucleo','error_evaluar_alertas','evaluador_alertas',$detalle,'No se pudieron evaluar las alertas.');";
                auditoria.Parameters.AddWithValue("$fecha", DateTime.Now.ToString("o"));
                auditoria.Parameters.AddWithValue("$detalle", error.Message);
                auditoria.ExecuteNonQuery();
            }
            catch (Exception errorAuditoria)
            {
                throw new AggregateException(
                    "Falló la evaluación de alertas y también su registro de auditoría.",
                    error,
                    errorAuditoria
                );
            }
            throw;
        }
    }

    private void EvaluarReglas(RepositorioAlertas alertas, DateOnly hoy)
    {
        foreach (ReglaAlerta regla in alertas.ListarReglas().Where(r => r.Estado == "activa"))
        {
            foreach (var cadena in CadenasDelModelo(regla.ModeloCadenaId))
            {
                var origen = EnlaceActivo(cadena.Id, regla.VagonOrigenModeloId);
                if (origen is null)
                    continue;

                var destino = Vagon(cadena.Id, regla.VagonDestinoModeloId);
                if (destino is null)
                    continue;

                bool destinoConfirmado =
                    EnlaceActivo(cadena.Id, regla.VagonDestinoModeloId) is not null;
                Alerta? existente = alertas
                    .Listar(cadenaId: cadena.Id)
                    .Alertas.FirstOrDefault(a =>
                        a.ReglaId == regla.Id && a.VagonCadenaId == destino.Value.Id
                    );

                if (destinoConfirmado)
                {
                    if (existente is { Estado: "pendiente" or "vencida" })
                        alertas.Resolver(
                            existente.Id,
                            $"resuelta automáticamente: llegó {destino.Value.Nombre}"
                        );
                    continue;
                }

                long alertaId =
                    existente?.Id
                    ?? alertas.CrearAlertaDeRegla(
                        regla.Id,
                        cadena.Id,
                        destino.Value.Id,
                        DateOnly.FromDateTime(origen.Value.CreadaEn)
                    );
                Alerta alerta = alertas
                    .Listar(cadenaId: cadena.Id)
                    .Alertas.Single(a => a.Id == alertaId);
                if (alerta.Estado == "pendiente" && alerta.FechaObjetivo < hoy)
                    alertas.MarcarVencida(alerta.Id);
            }
        }
    }

    private void EvaluarManuales(RepositorioAlertas alertas, DateOnly hoy)
    {
        foreach (Alerta alerta in alertas.Listar(estado: "pendiente").Alertas)
            if (alerta.ReglaId is null && alerta.FechaObjetivo < hoy)
                alertas.MarcarVencida(alerta.Id);
    }

    private void EvaluarReglasCadenasSimples(RepositorioAlertas alertas, DateOnly hoy)
    {
        foreach (
            ReglaAlertaCadenaSimple regla in alertas
                .ListarReglasCadenaSimple()
                .Where(r => r.Estado == "activa")
        )
        {
            foreach (long cadenaId in CadenasSimples())
            {
                var origen = DocumentoDeRegla(
                    cadenaId,
                    regla.DatoOrigenId,
                    regla.IdentificacionOrigenId
                );
                if (origen is null)
                    continue;
                long? alertaId = alertas.AlertaDeReglaCadenaSimple(regla.Id, cadenaId);
                var destino = DocumentoDeRegla(
                    cadenaId,
                    regla.DatoDestinoId,
                    regla.IdentificacionDestinoId
                );
                if (destino is not null)
                {
                    if (alertaId is long existente)
                    {
                        Alerta? alerta = alertas
                            .Listar(cadenaId: cadenaId)
                            .Alertas.SingleOrDefault(a => a.Id == existente);
                        if (alerta is { Estado: "pendiente" or "vencida" })
                            alertas.Resolver(
                                alerta.Id,
                                "resuelta automáticamente: llegó el documento esperado"
                            );
                    }
                    continue;
                }
                long id =
                    alertaId
                    ?? alertas.CrearAlertaDeReglaCadenaSimple(
                        regla.Id,
                        cadenaId,
                        DateOnly.FromDateTime(origen.Value.Fecha)
                    );
                Alerta actual = alertas.Listar(cadenaId: cadenaId).Alertas.Single(a => a.Id == id);
                if (actual.Estado == "pendiente" && actual.FechaObjetivo < hoy)
                    alertas.MarcarVencida(actual.Id);
            }
        }
    }

    private IReadOnlyList<long> CadenasSimples()
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id FROM cadenas WHERE modelo_id IS NULL AND estado='activa' ORDER BY id;";
        using var r = cmd.ExecuteReader();
        var ids = new List<long>();
        while (r.Read())
            ids.Add(r.GetInt64(0));
        return ids;
    }

    private (DateTime Fecha, long VersionId)? DocumentoDeRegla(
        long cadenaId,
        string? datoId,
        long? identificacionId
    )
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT e.creada_en,e.version_id FROM enlaces_cadena e JOIN vagones_cadena v ON v.id=e.vagon_cadena_id JOIN versiones_documento ver ON ver.id=e.version_id WHERE v.cadena_id=$c AND v.estado='activo' AND e.estado='activo' AND ver.estado='vigente' AND (($dato IS NOT NULL AND EXISTS(SELECT 1 FROM valores_documento x WHERE x.version_id=e.version_id AND x.estado='vigente' AND x.dato_diccionario_id=$dato)) OR ($identificacion IS NOT NULL AND EXISTS(SELECT 1 FROM campos_documento f WHERE f.identificacion_id=$identificacion AND f.id IN (SELECT campo_id FROM valores_documento WHERE version_id=e.version_id AND estado='vigente')))) ORDER BY e.id LIMIT 1;";
        cmd.Parameters.AddWithValue("$c", cadenaId);
        cmd.Parameters.AddWithValue("$dato", (object?)datoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$identificacion", (object?)identificacionId ?? DBNull.Value);
        using var r = cmd.ExecuteReader();
        return r.Read()
            ? (DateTime.Parse(r.GetString(0), CultureInfo.InvariantCulture), r.GetInt64(1))
            : null;
    }

    private IReadOnlyList<(long Id, string Estado)> CadenasDelModelo(long modeloId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id,estado FROM cadenas WHERE modelo_id=$modelo AND estado='activa' ORDER BY id;";
        cmd.Parameters.AddWithValue("$modelo", modeloId);
        using var reader = cmd.ExecuteReader();
        var cadenas = new List<(long, string)>();
        while (reader.Read())
            cadenas.Add((reader.GetInt64(0), reader.GetString(1)));
        return cadenas;
    }

    private (long Id, string Nombre)? Vagon(long cadenaId, long vagonModeloId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id,nombre FROM vagones_cadena WHERE cadena_id=$cadena AND vagon_modelo_id=$modelo AND estado='activo' LIMIT 1;";
        cmd.Parameters.AddWithValue("$cadena", cadenaId);
        cmd.Parameters.AddWithValue("$modelo", vagonModeloId);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? (reader.GetInt64(0), reader.GetString(1)) : null;
    }

    private (DateTime CreadaEn, long Id)? EnlaceActivo(long cadenaId, long vagonModeloId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT e.creada_en,e.id FROM enlaces_cadena e JOIN vagones_cadena v ON v.id=e.vagon_cadena_id JOIN versiones_documento d ON d.id=e.version_id WHERE v.cadena_id=$cadena AND v.vagon_modelo_id=$modelo AND v.estado='activo' AND e.estado='activo' AND d.estado='vigente' ORDER BY e.id DESC LIMIT 1;";
        cmd.Parameters.AddWithValue("$cadena", cadenaId);
        cmd.Parameters.AddWithValue("$modelo", vagonModeloId);
        using var reader = cmd.ExecuteReader();
        return reader.Read()
            ? (
                DateTime.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
                reader.GetInt64(1)
            )
            : null;
    }
}
