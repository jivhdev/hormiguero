using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Utilidades;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Tests;

public sealed class AlertasTests : IDisposable
{
    private readonly SqliteConnection conexion = new("Data Source=:memory:");

    public AlertasTests()
    {
        conexion.Open();
        Migraciones.Aplicar(conexion, Migraciones.Todas);
    }

    public void Dispose() => conexion.Dispose();

    [Fact]
    public void Migracion_v8_repetida_sobre_v7_conserva_datos()
    {
        using var anterior = new SqliteConnection("Data Source=:memory:");
        anterior.Open();
        Migraciones.Aplicar(anterior, Migraciones.Todas.Take(7).ToArray());
        using (var agregar = anterior.CreateCommand())
        {
            agregar.CommandText =
                "INSERT INTO configuracion(clave,valor) VALUES('previo','conservado');";
            agregar.ExecuteNonQuery();
        }

        Migraciones.Aplicar(anterior, Migraciones.Todas);
        Migraciones.Aplicar(anterior, Migraciones.Todas);

        using var verificar = anterior.CreateCommand();
        verificar.CommandText =
            "SELECT (SELECT valor FROM configuracion WHERE clave='previo'),(SELECT COUNT(*) FROM migraciones WHERE version=8),(SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('reglas_alerta','alertas','historial_alertas'));";
        using var lector = verificar.ExecuteReader();
        Assert.True(lector.Read());
        Assert.Equal("conservado", lector.GetString(0));
        Assert.Equal(1L, lector.GetInt64(1));
        Assert.Equal(3L, lector.GetInt64(2));
    }

    [Fact]
    public void Crea_regla_valida_y_rechaza_vagon_de_otro_modelo()
    {
        var (modelId, sourceId, targetId, otherTargetId) = CrearModelos();
        var repo = new RepositorioAlertas(conexion);

        long ruleId = repo.CrearRegla(
            "Espera factura",
            modelId,
            sourceId,
            targetId,
            "vagon_completado",
            2,
            TipoDias.Habiles,
            "Guía esperando factura"
        );

        Assert.Equal("activa", Assert.Single(repo.ListarReglas()).Estado);
        Assert.Throws<InvalidOperationException>(() =>
            repo.CrearRegla(
                "Regla inválida",
                modelId,
                sourceId,
                otherTargetId,
                "vagon_completado",
                2,
                TipoDias.Habiles,
                "Aviso"
            )
        );
        Assert.Throws<InvalidOperationException>(() =>
            repo.EditarRegla(
                ruleId,
                "Inválida",
                modelId,
                sourceId,
                otherTargetId,
                "vagon_completado",
                2,
                TipoDias.Habiles,
                "Aviso",
                null,
                false
            )
        );
        repo.EditarRegla(
            ruleId,
            "Editada",
            modelId,
            sourceId,
            targetId,
            "vagon_completado",
            3,
            TipoDias.Corridos,
            "Aviso actualizado",
            null,
            true
        );
        Assert.Equal("Editada", Assert.Single(repo.ListarReglas()).Nombre);
        Assert.True(repo.AnularRegla(ruleId));
        Assert.False(repo.AnularRegla(ruleId));
    }

    [Fact]
    public void Alerta_manual_cambia_estado_reabre_y_guarda_historial_y_auditoria()
    {
        var (modelId, _, targetId, _) = CrearModelos();
        long cadenaId = new RepositorioCadenas(conexion).CrearCadena(
            modelId,
            "Cadena de prueba",
            DateTime.Today
        );
        long vagonId = IdVagonCadena(cadenaId, targetId);
        var repo = new RepositorioAlertas(conexion);

        long alertaId = repo.CrearAlertaManual(
            "Revisar factura",
            new DateOnly(2026, 12, 31),
            cadenaId,
            vagonId
        );
        repo.Resolver(alertaId, "Documento verificado");
        repo.Reabrir(alertaId);
        repo.Descartar(alertaId, "Ya no corresponde");

        Assert.Equal("descartada", Assert.Single(repo.Listar(estado: "descartada").Alertas).Estado);
        Assert.Collection(
            repo.ObtenerHistorial(alertaId),
            h => Assert.Equal("creada_manual", h.Accion),
            h =>
            {
                Assert.Equal("resuelta", h.EstadoNuevo);
                Assert.Equal("Documento verificado", h.Motivo);
            },
            h =>
            {
                Assert.Equal("pendiente", h.EstadoNuevo);
                Assert.Equal("resuelta", h.EstadoAnterior);
            },
            h =>
            {
                Assert.Equal("descartada", h.EstadoNuevo);
                Assert.Equal("Ya no corresponde", h.Motivo);
            }
        );
        using var audit = conexion.CreateCommand();
        audit.CommandText = "SELECT COUNT(*) FROM auditoria WHERE origen=$origen;";
        audit.Parameters.AddWithValue("$origen", $"alerta:{alertaId}");
        Assert.Equal(4L, Convert.ToInt64(audit.ExecuteScalar()));
        Assert.Throws<ArgumentException>(() => repo.Resolver(alertaId, " "));
        using var borrar = conexion.CreateCommand();
        borrar.CommandText = "DELETE FROM historial_alertas WHERE alerta_id=$id;";
        borrar.Parameters.AddWithValue("$id", alertaId);
        Assert.Throws<SqliteException>(() => borrar.ExecuteNonQuery());
        using var borrarAlerta = conexion.CreateCommand();
        borrarAlerta.CommandText = "DELETE FROM alertas WHERE id=$id;";
        borrarAlerta.Parameters.AddWithValue("$id", alertaId);
        Assert.Throws<SqliteException>(() => borrarAlerta.ExecuteNonQuery());
    }

    [Fact]
    public void Puede_marcar_vencida_y_listar_por_documento()
    {
        var documentos = new RepositorioDocumentosDatos(conexion);
        string ruta = Path.Combine(Path.GetTempPath(), $"alerta-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(ruta, "contenido sintético");
        try
        {
            (long documentoId, VersionDocumento version) =
                documentos.AsegurarDocumentoYVersionVigente(ruta);
            var repo = new RepositorioAlertas(conexion);
            long alerta = repo.CrearAlertaManual(
                "Vence",
                new DateOnly(2026, 1, 1),
                versionId: version.Id
            );
            repo.MarcarVencida(alerta);

            ResultadoAlertas resultado = repo.Listar(estado: "vencida", documentoId: documentoId);

            Assert.Equal("vencida", Assert.Single(resultado.Alertas).Estado);
            Assert.Equal(1, resultado.Total);
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public void Regla_crea_una_alerta_por_cadena_y_vagon_y_calcula_saltando_feriado()
    {
        var (modelId, sourceId, targetId, _) = CrearModelos();
        long cadenaId = new RepositorioCadenas(conexion).CrearCadena(
            modelId,
            "Cadena de prueba",
            DateTime.Today
        );
        long vagonId = IdVagonCadena(cadenaId, targetId);
        var calendarios = new RepositorioCalendariosFeriados(conexion);
        long calendario = calendarios.CrearCalendario("Prueba", "ZZ");
        calendarios.AgregarFeriado(calendario, new DateOnly(2026, 12, 25), "Feriado de prueba");
        var repo = new RepositorioAlertas(conexion);
        long regla = repo.CrearRegla(
            "Espera",
            modelId,
            sourceId,
            targetId,
            "vagon_completado",
            1,
            TipoDias.Habiles,
            "Aviso",
            calendario
        );

        Assert.Equal(
            new DateOnly(2026, 12, 28),
            repo.CalcularVencimiento(regla, new DateOnly(2026, 12, 24))
        );
        long primera = repo.CrearAlertaDeRegla(
            regla,
            cadenaId,
            vagonId,
            new DateOnly(2026, 12, 24),
            "evento-1"
        );
        long segunda = repo.CrearAlertaDeRegla(
            regla,
            cadenaId,
            vagonId,
            new DateOnly(2026, 12, 29),
            "evento-2"
        );

        Assert.Equal(primera, segunda);
        Assert.Single(repo.Listar().Alertas);
    }

    [Fact]
    public void Lista_por_estado_y_cadena_con_contador_total()
    {
        var (modelId, _, targetId, _) = CrearModelos();
        long cadena = new RepositorioCadenas(conexion).CrearCadena(
            modelId,
            "Cadena",
            DateTime.Today
        );
        long vagon = IdVagonCadena(cadena, targetId);
        var repo = new RepositorioAlertas(conexion);
        repo.CrearAlertaManual("Una", new DateOnly(2026, 1, 1), cadena, vagon);
        repo.CrearAlertaManual("Dos", new DateOnly(2026, 1, 2), cadena);

        ResultadoAlertas pagina = repo.Listar(estado: "pendiente", cadenaId: cadena, limite: 1);

        Assert.Single(pagina.Alertas);
        Assert.Equal(2, pagina.Total);
        Assert.Equal(2, repo.Listar(estado: "pendiente").Total);
    }

    private (long Modelo, long Origen, long Destino, long OtroDestino) CrearModelos()
    {
        var cadenas = new RepositorioCadenas(conexion);
        long modelo = cadenas.CrearModelo("Modelo", DateTime.Today);
        long origen = cadenas.AgregarVagonModelo(modelo, null, "Guía");
        long destino = cadenas.AgregarVagonModelo(modelo, null, "Factura");
        long otroModelo = cadenas.CrearModelo("Otro modelo", DateTime.Today);
        long destinoAjeno = cadenas.AgregarVagonModelo(otroModelo, null, "Factura externa");
        return (modelo, origen, destino, destinoAjeno);
    }

    private long IdVagonCadena(long cadenaId, long vagonModeloId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id FROM vagones_cadena WHERE cadena_id=$cadena AND vagon_modelo_id=$modelo;";
        cmd.Parameters.AddWithValue("$cadena", cadenaId);
        cmd.Parameters.AddWithValue("$modelo", vagonModeloId);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }
}
