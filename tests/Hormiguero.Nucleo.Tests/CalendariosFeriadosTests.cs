using System.Text;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Utilidades;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Tests;

public sealed class CalendariosFeriadosTests : IDisposable
{
    private readonly SqliteConnection conexion = new("Data Source=:memory:");

    public CalendariosFeriadosTests()
    {
        conexion.Open();
        Migraciones.Aplicar(conexion, Migraciones.Todas);
    }

    public void Dispose() => conexion.Dispose();

    [Fact]
    public void Migracion_v7_es_idempotente_y_conserva_datos_de_v6()
    {
        using var conexionV6 = new SqliteConnection("Data Source=:memory:");
        conexionV6.Open();
        Migraciones.Aplicar(conexionV6, Migraciones.Todas.Take(6).ToArray());
        using (var insertar = conexionV6.CreateCommand())
        {
            insertar.CommandText =
                "INSERT INTO configuracion(clave,valor) VALUES('antes_v7','conservado');";
            insertar.ExecuteNonQuery();
        }

        Migraciones.Aplicar(conexionV6, Migraciones.Todas);
        Migraciones.Aplicar(conexionV6, Migraciones.Todas);

        using var verificar = conexionV6.CreateCommand();
        verificar.CommandText =
            "SELECT (SELECT valor FROM configuracion WHERE clave='antes_v7'), "
            + "(SELECT COUNT(*) FROM migraciones WHERE version=7), "
            + "(SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('calendarios_feriados','feriados'));";
        using var lector = verificar.ExecuteReader();
        Assert.True(lector.Read());
        Assert.Equal("conservado", lector.GetString(0));
        Assert.Equal(1L, lector.GetInt64(1));
        Assert.Equal(2L, lector.GetInt64(2));
    }

    [Fact]
    public void Precarga_chile_una_vez_y_no_sobrescribe_cambios_del_usuario()
    {
        var repositorio = new RepositorioCalendariosFeriados(conexion);
        var chile = Assert.Single(repositorio.ListarCalendarios());
        Assert.Equal("Chile", chile.Nombre);
        Assert.Equal("CL", chile.PaisCodigo);
        Assert.True(chile.Predeterminado);
        long feriadoId = repositorio
            .ListarFeriados(chile.Id)
            .Single(f => f.Fecha == new DateOnly(2026, 12, 25))
            .Id;
        repositorio.EditarFeriado(feriadoId, new DateOnly(2026, 12, 25), "Navidad editada");

        _ = new RepositorioCalendariosFeriados(conexion);

        Assert.Single(repositorio.ListarCalendarios());
        Assert.Equal(
            "Navidad editada",
            repositorio.ListarFeriados(chile.Id).Single(f => f.Id == feriadoId).Nombre
        );
    }

    [Fact]
    public void Puede_crear_calendario_y_elegirlo_como_predeterminado()
    {
        var repositorio = new RepositorioCalendariosFeriados(conexion);
        long id = repositorio.CrearCalendario("Uruguay", "UY", "Montevideo");

        repositorio.ElegirPredeterminado(id);

        Assert.False(
            repositorio.ListarCalendarios().Single(c => c.Nombre == "Chile").Predeterminado
        );
        Assert.True(repositorio.ListarCalendarios().Single(c => c.Id == id).Predeterminado);
    }

    [Fact]
    public void Anular_feriado_lo_conserva_y_registra_auditoria_en_la_misma_base()
    {
        var repositorio = new RepositorioCalendariosFeriados(conexion);
        long chile = repositorio.ListarCalendarios().Single().Id;
        long id = repositorio.AgregarFeriado(chile, new DateOnly(2026, 2, 2), "Prueba");

        Assert.True(repositorio.AnularFeriado(id));
        Assert.False(repositorio.AnularFeriado(id));

        Feriado anulado = Assert.Single(repositorio.ListarFeriados(chile), f => f.Id == id);
        Assert.False(anulado.Activo);
        Assert.NotNull(anulado.FechaAnulacion);
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT COUNT(*) FROM auditoria WHERE accion='anular_feriado' AND origen=$origen;";
        comando.Parameters.AddWithValue("$origen", $"feriado:{id}");
        Assert.Equal(1L, Convert.ToInt64(comando.ExecuteScalar()));
    }

    [Fact]
    public void Importa_y_exporta_csv_con_tildes_y_omite_lineas_vacias()
    {
        var repositorio = new RepositorioCalendariosFeriados(conexion);
        long calendario = repositorio.CrearCalendario("Perú", "PE");
        using var entrada = new StringReader(
            "\n# Comentario\n2026-07-28;Fiestas Patrias\n\n2026-12-08;Inmaculada Concepción\n"
        );

        Assert.Equal(
            new ResumenImportacionFeriados(2, 0, 0),
            repositorio.ImportarCsv(calendario, entrada)
        );
        using var salida = new StringWriter();
        repositorio.ExportarCsv(calendario, salida);

        Assert.Equal(
            "2026-07-28;Fiestas Patrias\n2026-12-08;Inmaculada Concepción\n",
            salida.ToString().Replace("\r\n", "\n")
        );
    }

    [Fact]
    public void Importacion_actualiza_feriado_activo_con_auditoria_y_resumen()
    {
        var repositorio = new RepositorioCalendariosFeriados(conexion);
        long calendario = repositorio.CrearCalendario("Perú", "PE");
        long id = repositorio.AgregarFeriado(
            calendario,
            new DateOnly(2026, 7, 28),
            "Nombre anterior"
        );
        using var entrada = new StringReader("2026-07-28;Fiestas Patrias\n");

        ResumenImportacionFeriados resumen = repositorio.ImportarCsv(calendario, entrada);

        Assert.Equal(new ResumenImportacionFeriados(0, 1, 0), resumen);
        Assert.Equal(
            "Fiestas Patrias",
            Assert.Single(repositorio.ListarFeriados(calendario)).Nombre
        );
        Assert.Equal(1L, ContarAuditoria("actualizar_feriado_importado", id));
    }

    [Fact]
    public void Importacion_reactiva_feriado_anulado_con_auditoria_y_resumen()
    {
        var repositorio = new RepositorioCalendariosFeriados(conexion);
        long calendario = repositorio.CrearCalendario("Perú", "PE");
        long id = repositorio.AgregarFeriado(
            calendario,
            new DateOnly(2026, 7, 28),
            "Nombre anterior"
        );
        repositorio.AnularFeriado(id);
        using var entrada = new StringReader("2026-07-28;Fiestas Patrias\n");

        ResumenImportacionFeriados resumen = repositorio.ImportarCsv(calendario, entrada);

        Assert.Equal(new ResumenImportacionFeriados(0, 0, 1), resumen);
        Feriado feriado = Assert.Single(repositorio.ListarFeriados(calendario));
        Assert.Equal("Fiestas Patrias", feriado.Nombre);
        Assert.True(feriado.Activo);
        Assert.Null(feriado.FechaAnulacion);
        Assert.Equal(1L, ContarAuditoria("reactivar_feriado_importado", id));
    }

    [Fact]
    public void Importacion_rechaza_fechas_repetidas_en_archivo_sin_importar_nada()
    {
        var repositorio = new RepositorioCalendariosFeriados(conexion);
        long calendario = repositorio.CrearCalendario("Perú", "PE");
        using var entrada = new StringReader(
            "2026-07-28;Fiestas Patrias\n2026-07-28;Otra celebración\n"
        );

        FormatException error = Assert.Throws<FormatException>(() =>
            repositorio.ImportarCsv(calendario, entrada)
        );

        Assert.Contains("repetida", error.Message);
        Assert.Empty(repositorio.ListarFeriados(calendario));
    }

    private long ContarAuditoria(string accion, long feriadoId)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT COUNT(*) FROM auditoria WHERE accion=$accion AND origen=$origen;";
        comando.Parameters.AddWithValue("$accion", accion);
        comando.Parameters.AddWithValue("$origen", $"feriado:{feriadoId}");
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    [Fact]
    public void Importacion_rechaza_fecha_invalida_con_linea_y_formato_claros()
    {
        var repositorio = new RepositorioCalendariosFeriados(conexion);
        long calendario = repositorio.CrearCalendario("Perú", "PE");
        using var entrada = new StringReader(
            "2026-07-28;Fiestas Patrias\n32/12/2026;Fecha inválida\n"
        );

        FormatException error = Assert.Throws<FormatException>(() =>
            repositorio.ImportarCsv(calendario, entrada)
        );

        Assert.Contains("Línea 2", error.Message);
        Assert.Contains("AAAA-MM-DD", error.Message);
        Assert.Empty(repositorio.ListarFeriados(calendario));
    }

    [Fact]
    public void Importacion_desde_archivo_y_exportacion_son_utf8()
    {
        var repositorio = new RepositorioCalendariosFeriados(conexion);
        long calendario = repositorio.CrearCalendario("Perú", "PE");
        string directorio = Path.Combine(
            Path.GetTempPath(),
            "HormigueroTests",
            Guid.NewGuid().ToString("N")
        );
        string entrada = Path.Combine(directorio, "entrada.csv");
        string salida = Path.Combine(directorio, "salida.csv");
        try
        {
            Directory.CreateDirectory(directorio);
            File.WriteAllText(
                entrada,
                "2026-12-08;Inmaculada Concepción\n",
                new UTF8Encoding(false)
            );
            Assert.Equal(
                new ResumenImportacionFeriados(1, 0, 0),
                repositorio.ImportarCsv(calendario, entrada)
            );
            repositorio.ExportarCsv(calendario, salida);
            Assert.Contains("Concepción", File.ReadAllText(salida, Encoding.UTF8));
        }
        finally
        {
            if (Directory.Exists(directorio))
                Directory.Delete(directorio, recursive: true);
        }
    }

    [Fact]
    public void Devuelve_feriados_activos_solo_dentro_del_rango_de_anios()
    {
        var repositorio = new RepositorioCalendariosFeriados(conexion);
        long chile = repositorio.ListarCalendarios().Single().Id;

        IReadOnlySet<DateOnly> feriados = repositorio.ObtenerFeriadosActivos(chile, 2027, 2027);

        Assert.Contains(new DateOnly(2027, 1, 1), feriados);
        Assert.DoesNotContain(new DateOnly(2026, 1, 1), feriados);
        Assert.DoesNotContain(new DateOnly(2028, 1, 1), feriados);
    }

    [Fact]
    public void Sumar_habil_salta_navidad_precargada_de_chile()
    {
        var repositorio = new RepositorioCalendariosFeriados(conexion);
        long chile = repositorio.ListarCalendarios().Single().Id;
        IReadOnlySet<DateOnly> feriados = repositorio.ObtenerFeriadosActivos(chile, 2026, 2026);

        DateOnly resultado = CalculoFechas.Sumar(
            new DateOnly(2026, 12, 24),
            1,
            TipoDias.Habiles,
            feriados
        );

        Assert.Equal(new DateOnly(2026, 12, 28), resultado);
    }
}
