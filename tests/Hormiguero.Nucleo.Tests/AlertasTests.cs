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
    public void Alerta_manual_cambia_estado_reabre_y_guarda_historial_y_auditoria()
    {
        long cadenaId = new RepositorioCadenas(conexion).CrearCadenaSimple(
            "Cadena de prueba",
            DateTime.Today
        );
        var repo = new RepositorioAlertas(conexion);

        long alertaId = repo.CrearAlertaManual(
            "Revisar factura",
            new DateOnly(2026, 12, 31),
            cadenaId
        );
        Alerta alertaManual = Assert.Single(repo.Listar(estado: "pendiente").Alertas);
        Assert.Equal(TipoDias.Corridos, alertaManual.ModoDias);
        Assert.Null(alertaManual.CalendarioId);
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
    public void Evaluador_marca_alerta_manual_vencida_al_llegar_su_fecha()
    {
        long cadena = new RepositorioCadenas(conexion).CrearCadenaSimple("Cadena", DateTime.Today);
        var alertas = new RepositorioAlertas(conexion);
        long id = alertas.CrearAlertaManual(
            "Revisar",
            DateOnly.FromDateTime(DateTime.Today),
            cadena
        );

        // El mismo día de la fecha objetivo sigue pendiente ("vence hoy"); vence al día siguiente.
        new EvaluadorAlertas(conexion).Evaluar(DateOnly.FromDateTime(DateTime.Today));
        Assert.Equal("pendiente", Assert.Single(alertas.Listar().Alertas).Estado);
        new EvaluadorAlertas(conexion).Evaluar(DateOnly.FromDateTime(DateTime.Today.AddDays(1)));

        Assert.Equal("vencida", Assert.Single(alertas.Listar().Alertas).Estado);
        Assert.Equal("marcar_vencida", alertas.ObtenerHistorial(id).Last().Accion);
    }

    [Fact]
    public void Regla_de_cadena_simple_crea_y_resuelve_alerta_por_dato_del_diccionario()
    {
        long origen = CrearVersionConDato("Guia", "oc_cliente");
        long cadena = new RepositorioCadenas(conexion).CrearCadenaSimple(
            "Cadena simple",
            DateTime.Now
        );
        long vagon = new RepositorioCadenas(conexion).AgregarDocumentoCadena(
            cadena,
            origen,
            "Guía"
        );
        new RepositorioReglasYEnlaces(conexion).CrearEnlace(vagon, origen, "manual");
        var alertas = new RepositorioAlertas(conexion);
        long regla = alertas.CrearReglaCadenaSimple(
            "Esperar factura",
            "oc_cliente",
            null,
            "factura_propia",
            null,
            5,
            TipoDias.Corridos,
            "Falta la factura"
        );
        var evaluador = new EvaluadorAlertas(conexion);

        evaluador.Evaluar();
        Alerta alerta = Assert.Single(alertas.Listar(cadenaId: cadena).Alertas);
        Assert.Equal("pendiente", alerta.Estado);
        Assert.Equal(regla, alertas.ListarReglasCadenaSimple().Single().Id);

        long destino = CrearVersionConDato("Factura", "factura_propia");
        long vagonDestino = new RepositorioCadenas(conexion).AgregarDocumentoCadena(
            cadena,
            destino,
            "Factura"
        );
        new RepositorioReglasYEnlaces(conexion).CrearEnlace(vagonDestino, destino, "manual");
        evaluador.Evaluar();

        Assert.Equal("resuelta", Assert.Single(alertas.Listar(cadenaId: cadena).Alertas).Estado);
        Assert.Equal(
            "resuelta automáticamente: llegó el documento esperado",
            alertas.ObtenerHistorial(alerta.Id).Last().Motivo
        );
    }

    [Fact]
    public void Evaluador_de_alerta_simple_cuenta_desde_fecha_documento_y_saltea_feriado()
    {
        var (cadena, version, regla, calendario) = CrearAlertaSimpleConFecha();
        new RepositorioDatosInformativos(conexion).GuardarValores(
            version,
            [FechaDocumentoParser.InterpretarFecha("24-12-2026")]
        );

        new EvaluadorAlertas(conexion).Evaluar(new DateOnly(2026, 12, 27));

        Alerta alerta = Assert.Single(
            new RepositorioAlertas(conexion).Listar(cadenaId: cadena).Alertas
        );
        Assert.Equal(new DateOnly(2026, 12, 24), alerta.FechaBase);
        Assert.Equal("fecha_documento", alerta.OrigenFecha);
        Assert.Equal(new DateOnly(2026, 12, 28), alerta.FechaObjetivo);
        Assert.Equal(TipoDias.Habiles, alerta.ModoDias);
        Assert.Equal(calendario, alerta.CalendarioId);
        Assert.Equal(calendario, ObtenerCalendarioAlerta(alerta.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ilegible")]
    public void Evaluador_de_alerta_simple_usa_entrada_a_cadena_si_fecha_no_reconocida(
        string? valor
    )
    {
        var (cadena, version, _, _) = CrearAlertaSimpleConFecha();
        if (valor is not null)
            new RepositorioDatosInformativos(conexion).GuardarValores(
                version,
                [FechaDocumentoParser.InterpretarFecha(valor)]
            );
        using (var fecha = conexion.CreateCommand())
        {
            fecha.CommandText =
                "UPDATE enlaces_cadena SET creada_en='2026-10-01T12:00:00.0000000' WHERE version_id=$v;";
            fecha.Parameters.AddWithValue("$v", version);
            fecha.ExecuteNonQuery();
        }

        new EvaluadorAlertas(conexion).Evaluar(new DateOnly(2026, 10, 3));

        Alerta alerta = Assert.Single(
            new RepositorioAlertas(conexion).Listar(cadenaId: cadena).Alertas
        );
        Assert.Equal(new DateOnly(2026, 10, 1), alerta.FechaBase);
        Assert.Equal("entrada_cadena", alerta.OrigenFecha);
        Assert.Equal(new DateOnly(2026, 10, 2), alerta.FechaObjetivo);
    }

    [Fact]
    public void Evaluador_no_cambia_vencimiento_de_alerta_simple_existente()
    {
        var (cadena, version, _, _) = CrearAlertaSimpleConFecha();
        var datos = new RepositorioDatosInformativos(conexion);
        datos.GuardarValores(version, [FechaDocumentoParser.InterpretarFecha("24-12-2026")]);
        var evaluador = new EvaluadorAlertas(conexion);
        evaluador.Evaluar(new DateOnly(2026, 12, 24));
        Alerta inicial = Assert.Single(
            new RepositorioAlertas(conexion).Listar(cadenaId: cadena).Alertas
        );
        datos.GuardarValores(version, [FechaDocumentoParser.InterpretarFecha("30-12-2026")]);

        evaluador.Evaluar(new DateOnly(2026, 12, 24));

        Alerta actual = Assert.Single(
            new RepositorioAlertas(conexion).Listar(cadenaId: cadena).Alertas
        );
        Assert.Equal(inicial.FechaObjetivo, actual.FechaObjetivo);
        Assert.Equal(inicial.FechaBase, actual.FechaBase);
        Assert.Equal(inicial.OrigenFecha, actual.OrigenFecha);
    }

    private (long Cadena, long Version, long Regla, long Calendario) CrearAlertaSimpleConFecha()
    {
        long version = CrearVersionConDato("Guia alerta", "oc_cliente");
        long cadena = new RepositorioCadenas(conexion).CrearCadenaSimple(
            "Cadena alerta",
            DateTime.Now
        );
        long vagon = new RepositorioCadenas(conexion).AgregarDocumentoCadena(
            cadena,
            version,
            "Guía"
        );
        new RepositorioReglasYEnlaces(conexion).CrearEnlace(vagon, version, "manual");
        var calendarios = new RepositorioCalendariosFeriados(conexion);
        long calendario = calendarios.CrearCalendario("Feriados de alerta", "ZZ");
        calendarios.AgregarFeriado(calendario, new DateOnly(2026, 12, 25), "Feriado");
        long regla = new RepositorioAlertas(conexion).CrearReglaCadenaSimple(
            "Esperar factura con fecha",
            "oc_cliente",
            null,
            "factura_propia",
            null,
            1,
            TipoDias.Habiles,
            "Falta la factura",
            calendario
        );
        return (cadena, version, regla, calendario);
    }

    private long ObtenerCalendarioAlerta(long alertaId)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT calendario_id FROM alertas WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", alertaId);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private long CrearVersionConDato(string nombre, string dato)
    {
        string ruta = Path.Combine(
            Path.GetTempPath(),
            $"alerta-simple-{nombre}-{Guid.NewGuid():N}.pdf"
        );
        File.WriteAllText(ruta, $"contenido sintético {nombre}");
        try
        {
            long version = new RepositorioDocumentosDatos(conexion)
                .AsegurarDocumentoYVersionVigente(ruta)
                .Version.Id;
            using var id = conexion.CreateCommand();
            id.CommandText =
                "INSERT INTO identificaciones(tipo,emisor,datos,actualizada) VALUES($t,'Prueba','{}','ahora') RETURNING id;";
            id.Parameters.AddWithValue("$t", nombre);
            long identificacion = Convert.ToInt64(id.ExecuteScalar());
            using var campo = conexion.CreateCommand();
            campo.CommandText =
                "INSERT INTO campos_documento(identificacion_id,nombre,nombre_estable,tipo_dato,origen_lectura,dato_diccionario_id) VALUES($i,$n,$d,'texto','prueba',$d) RETURNING id;";
            campo.Parameters.AddWithValue("$i", identificacion);
            campo.Parameters.AddWithValue("$n", dato);
            campo.Parameters.AddWithValue("$d", dato);
            long campoId = Convert.ToInt64(campo.ExecuteScalar());
            using var valor = conexion.CreateCommand();
            valor.CommandText =
                "INSERT INTO valores_documento(version_id,campo_id,valor_original,valor_clave,origen,fecha_creacion,dato_diccionario_id) VALUES($v,$c,'4500012345','4500012345','prueba','ahora',$d);";
            valor.Parameters.AddWithValue("$v", version);
            valor.Parameters.AddWithValue("$c", campoId);
            valor.Parameters.AddWithValue("$d", dato);
            valor.ExecuteNonQuery();
            return version;
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public void Lista_por_estado_y_cadena_con_contador_total()
    {
        long cadena = new RepositorioCadenas(conexion).CrearCadenaSimple("Cadena", DateTime.Today);
        var repo = new RepositorioAlertas(conexion);
        repo.CrearAlertaManual("Una", new DateOnly(2026, 1, 1), cadena);
        repo.CrearAlertaManual("Dos", new DateOnly(2026, 1, 2), cadena);

        ResultadoAlertas pagina = repo.Listar(estado: "pendiente", cadenaId: cadena, limite: 1);

        Assert.Single(pagina.Alertas);
        Assert.Equal(2, pagina.Total);
        Assert.Equal(2, repo.Listar(estado: "pendiente").Total);
    }

    private long CrearVersion(string nombre)
    {
        string ruta = Path.Combine(Path.GetTempPath(), $"alerta-{nombre}-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(ruta, "contenido sintético");
        try
        {
            return new RepositorioDocumentosDatos(conexion)
                .AsegurarDocumentoYVersionVigente(ruta)
                .Version.Id;
        }
        finally
        {
            File.Delete(ruta);
        }
    }
}
