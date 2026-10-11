using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Tests;

public sealed class DatosEnlazantesTests : IDisposable
{
    private readonly SqliteConnection conexion = new("Data Source=:memory:");

    public DatosEnlazantesTests()
    {
        conexion.Open();
        using var fk = conexion.CreateCommand();
        fk.CommandText = "PRAGMA foreign_keys=ON;";
        fk.ExecuteNonQuery();
        Migraciones.Aplicar(conexion, Migraciones.Todas);
    }

    public void Dispose() => conexion.Dispose();

    [Fact]
    public void Diccionario_tiene_las_23_entradas_fijas_en_orden_y_la_base_impide_cambios()
    {
        var diccionario = DiccionarioDatosEnlazantes.Todos;
        Assert.Equal(23, diccionario.Count);
        Assert.Equal(23, diccionario.Select(d => d.Id).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 23), diccionario.Select(d => d.Orden));
        Assert.Equal(
            new[] { "Ventas propias", "Del cliente", "Compras propias", "Del proveedor", "Otros" },
            diccionario.Select(d => d.Grupo).Distinct()
        );
        Assert.Equal(diccionario, new RepositorioDatosEnlazantes(conexion).LeerDiccionario());
        Assert.Throws<SqliteException>(() =>
            Ejecutar("UPDATE diccionario_datos SET nombre='otro' WHERE id='oc_cliente';")
        );
        Assert.Throws<SqliteException>(() =>
            Ejecutar("DELETE FROM diccionario_datos WHERE id='oc_cliente';")
        );
    }

    [Fact]
    public void Partes_se_normalizan_y_la_migracion_reinstala_el_trigger()
    {
        Assert.Equal("0111-10-25", DiccionarioDatosEnlazantes.ValorComoSeLee(" 0111-10-25 "));
        Assert.Equal(
            "76123456-K",
            DiccionarioDatosEnlazantes.ClaveDeEnlace("rut_proveedor", " 76.123.456-k ")
        );
        Assert.Equal(
            "ACME SPA",
            DiccionarioDatosEnlazantes.ClaveDeEnlace("nombre_propio", "  Acme   Spa ")
        );
        Assert.Equal(6, DiccionarioDatosEnlazantes.Todos.Count(d => d.Orden >= 18));
        Assert.Equal(
            "Del proveedor",
            DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "rut_proveedor").Grupo
        );
        Assert.Equal(
            "Del proveedor",
            DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "nombre_proveedor").Grupo
        );
        Assert.Equal(
            "Del cliente",
            DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "rut_cliente").Grupo
        );
        Assert.Equal(
            "Del cliente",
            DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "nombre_cliente").Grupo
        );
        Assert.All(
            DiccionarioDatosEnlazantes.Todos.Where(d => d.Id is "rut_propio" or "nombre_propio"),
            d => Assert.Equal("Otros", d.Grupo)
        );
        Assert.Equal(23L, Escalar(conexion, "SELECT COUNT(*) FROM diccionario_datos;"));
        Assert.Throws<SqliteException>(() =>
            Ejecutar(
                "INSERT INTO diccionario_datos(id,nombre,grupo,orden) VALUES('x','x','Otros',24);"
            )
        );
    }

    [Fact]
    public void Regla_aditiva_permite_drop_trigger_y_sigue_rechazando_drop_table()
    {
        using var nueva = new SqliteConnection("Data Source=:memory:");
        nueva.Open();
        Assert.Throws<InvalidOperationException>(() =>
            Migraciones.Aplicar(nueva, [(1, "DROP TABLE x;")])
        );
        Migraciones.Aplicar(
            nueva,
            [
                (
                    1,
                    "CREATE TABLE x(id INTEGER); CREATE TRIGGER tx BEFORE INSERT ON x BEGIN SELECT RAISE(ABORT,'fijo'); END;"
                ),
                (
                    2,
                    "DROP TRIGGER tx; CREATE TRIGGER tx BEFORE INSERT ON x BEGIN SELECT RAISE(ABORT,'fijo'); END;"
                ),
            ]
        );
        using var insert = nueva.CreateCommand();
        insert.CommandText = "INSERT INTO x VALUES(1);";
        Assert.Throws<SqliteException>(() => insert.ExecuteNonQuery());
    }

    [Fact]
    public void Migracion_v9_conserva_datos_existentes_y_se_puede_repetir()
    {
        using var v8 = new SqliteConnection("Data Source=:memory:");
        v8.Open();
        Migraciones.Aplicar(v8, Migraciones.Todas.Take(8).ToArray());
        EjecutarEn(
            v8,
            "INSERT INTO identificaciones(tipo,emisor,datos,actualizada) VALUES('Factura','Emisor','{}','ahora');"
                + "INSERT INTO documentos(ruta,carpeta_raiz,nombre,tamano,modificado,estado,tiene_texto,indexado_en) VALUES('r','c','n',1,'f','ok',0,'f');"
                + "INSERT INTO versiones_documento(documento_id,huella,ruta_observada,registrada_en) VALUES(1,'h','r','f');"
                + "INSERT INTO campos_documento(identificacion_id,nombre,nombre_estable,tipo_dato,origen_lectura) VALUES(1,'OC','oc','texto','marca');"
                + "INSERT INTO valores_documento(version_id,campo_id,valor_original,valor_clave,origen,fecha_creacion) VALUES(1,1,'123','123','pdf','f');"
        );
        Migraciones.Aplicar(v8, Migraciones.Todas);
        Migraciones.Aplicar(v8, Migraciones.Todas);
        Assert.Equal(23L, Escalar(v8, "SELECT COUNT(*) FROM diccionario_datos;"));
        Assert.Equal("Factura", EscalarTexto(v8, "SELECT tipo FROM identificaciones WHERE id=1;"));
        Assert.Equal(
            "Recibido",
            EscalarTexto(v8, "SELECT grupo_documento FROM identificaciones WHERE id=1;")
        );
        Assert.Equal(
            "Factura · Emisor",
            EscalarTexto(v8, "SELECT nombre_estandar FROM identificaciones WHERE id=1;")
        );
        Assert.Equal(
            "123",
            EscalarTexto(v8, "SELECT valor_original FROM valores_documento WHERE id=1;")
        );
        Assert.Equal(
            0L,
            Escalar(
                v8,
                "SELECT COUNT(*) FROM valores_documento WHERE dato_diccionario_id IS NOT NULL;"
            )
        );
        Assert.Equal(
            (long)Migraciones.Todas.Max(migracion => migracion.Version),
            Escalar(v8, "SELECT MAX(version) FROM migraciones;")
        );
    }

    [Fact]
    public void Edita_clasificacion_y_nombre_y_admite_varios_disenos_anulables()
    {
        long tipo = new Identificaciones(conexion).Guardar(new(0, "Factura", "Proveedor", "{}"));
        var ident = new Identificaciones(conexion);
        Assert.True(ident.ConfigurarTipoDocumento(tipo, "Emitido", "Factura · Proveedor ajustado"));
        Assert.Equal(
            ("Emitido", "Factura · Proveedor ajustado"),
            (ident.Listar()[0].GrupoDocumento, ident.Listar()[0].NombreEstandar)
        );
        var repo = new RepositorioDatosEnlazantes(conexion);
        long a = repo.GuardarDatoTipo(
            new(0, tipo, "oc_cliente", "pdf-a", null, 1, 0.1, 0.2, 0.3, 0.04, true)
        );
        long b = repo.GuardarDatoTipo(
            new(0, tipo, "oc_cliente", "pdf-b", null, 2, 0.5, 0.6, 0.2, 0.03, true)
        );
        Assert.Equal(new[] { "pdf-a", "pdf-b" }, repo.ListarDatosTipo(tipo).Select(d => d.Diseno));
        Assert.True(repo.AnularDatoTipo(a));
        Assert.Single(repo.ListarDatosTipo(tipo));
        Assert.Equal(2, repo.ListarDatosTipo(tipo, incluirAnulados: true).Count);
        Assert.Equal(
            a,
            repo.ListarDatosTipo(tipo, incluirAnulados: true).Single(d => !d.Activo).Id
        );
        Assert.True(repo.AnularDatoTipo(b));

        // D-73: volver a marcar un dato quitado en el mismo diseño lo reactiva con la zona nueva.
        long otraVez = repo.GuardarDatoTipo(
            new(0, tipo, "oc_cliente", "pdf-a", null, 1, 0.7, 0.8, 0.1, 0.02, true)
        );
        Assert.Equal(a, otraVez);
        var reactivado = Assert.Single(repo.ListarDatosTipo(tipo));
        Assert.Equal((0.7, 0.8), (reactivado.X, reactivado.Y));
    }

    [Fact]
    public void Busca_documentos_por_dato_y_valor_indexado()
    {
        Ejecutar(
            "INSERT INTO documentos(ruta,carpeta_raiz,nombre,tamano,modificado,estado,tiene_texto,indexado_en) VALUES('C:/a.pdf','C:/','a.pdf',1,'f','ok',1,'f');"
        );
        Ejecutar(
            "INSERT INTO versiones_documento(documento_id,huella,ruta_observada,registrada_en) VALUES(1,'h','C:/a.pdf','f');"
        );
        long identificacion = new Identificaciones(conexion).Guardar(
            new(0, "Factura", "Proveedor", "{}")
        );
        long campo = new RepositorioDocumentosDatos(conexion).GuardarCampo(
            new(0, identificacion, "OC cliente", "oc_cliente", "texto", true, "marca", "oc_cliente")
        );
        new RepositorioDatosEnlazantes(conexion).GuardarDatoTipo(
            new(
                0,
                identificacion,
                "oc_cliente",
                "1",
                campo,
                1,
                0.1,
                0.1,
                0.2,
                0.1,
                true,
                false,
                true
            )
        );
        var datos = new RepositorioDocumentosDatos(conexion);
        datos.GuardarValor(1, campo, " 4500 ", "4500", "pdf");
        var repo = new RepositorioDatosEnlazantes(conexion);
        Assert.Equal("C:/a.pdf", Assert.Single(repo.BuscarDocumentos("oc_cliente", "4500")).Ruta);
        var valorId = Convert.ToInt64(Escalar(conexion, "SELECT id FROM valores_documento;"));
        Assert.True(datos.AnularValor(valorId));
        Assert.Empty(repo.BuscarDocumentos("oc_cliente", "4500"));
    }

    [Fact]
    public void Existe_original_vigente_por_numero_emisor_y_tipo_y_excluye_cedibles()
    {
        var identificaciones = new Identificaciones(conexion);
        var documentos = new RepositorioDocumentosDatos(conexion);
        var repo = new RepositorioDatosEnlazantes(conexion);
        var ids = new Dictionary<(string Emisor, string Tipo), long>();
        var campos = new Dictionary<long, long>();
        long id = 0;
        foreach (
            var (emisor, tipo, origen) in new[]
            {
                ("Proveedor", "Factura", "cedible"),
                ("Proveedor", "Factura", "marca"),
                ("Otro", "Factura", "marca"),
                ("Proveedor", "Guía", "marca"),
            }
        )
        {
            int indice = (int)++id;
            Ejecutar(
                $"INSERT INTO documentos(ruta,carpeta_raiz,nombre,tamano,modificado,estado,tiene_texto,indexado_en) VALUES('C:/{indice}.pdf','C:/','{indice}.pdf',1,'f','ok',1,'f');"
            );
            Ejecutar(
                $"INSERT INTO versiones_documento(documento_id,huella,ruta_observada,registrada_en) VALUES({indice},'h{indice}','C:/{indice}.pdf','f');"
            );
            var claveIdentificacion = (emisor, tipo);
            if (!ids.TryGetValue(claveIdentificacion, out long identificacion))
            {
                identificacion = identificaciones.Guardar(new(0, tipo, emisor, "{}"));
                ids.Add(claveIdentificacion, identificacion);
            }
            if (!campos.TryGetValue(identificacion, out long campo))
            {
                campo = documentos.GuardarCampo(
                    new(
                        0,
                        identificacion,
                        "Factura",
                        "factura_proveedor",
                        "texto",
                        true,
                        "marca",
                        "factura_proveedor"
                    )
                );
                campos.Add(identificacion, campo);
                repo.GuardarDatoTipo(
                    new(
                        0,
                        identificacion,
                        "factura_proveedor",
                        "1",
                        campo,
                        1,
                        0.1,
                        0.1,
                        0.2,
                        0.1,
                        true,
                        true,
                        true
                    )
                );
            }
            documentos.GuardarValor(
                indice,
                campo,
                " 00-123 ",
                "00-123",
                origen,
                datoDiccionarioId: "factura_proveedor"
            );
        }

        Assert.True(
            repo.ExisteDocumentoVigente("factura_proveedor", "123", "Proveedor", "Factura")
        );
        Assert.False(
            repo.ExisteDocumentoVigente("factura_proveedor", "123", "Proveedor", "Boleta")
        );
        Assert.False(repo.ExisteDocumentoVigente("factura_proveedor", "123", "Cliente", "Factura"));
        Assert.False(
            repo.ExisteDocumentoVigente("factura_proveedor", "---", "Proveedor", "Factura")
        );
    }

    [Theory]
    [InlineData("06-10-2026", 2026, 10, 6)]
    [InlineData("6/10/2026", 2026, 10, 6)]
    [InlineData("06.10.2026", 2026, 10, 6)]
    [InlineData("6 de octubre de 2026", 2026, 10, 6)]
    public void Reconoce_fechas_comunes_chilenas(string texto, int anio, int mes, int dia)
    {
        var fecha = FechaDocumentoParser.InterpretarFecha(texto);
        Assert.Equal(new DateTime(anio, mes, dia), fecha.Fecha);
        Assert.False(fecha.FechaNoReconocida);
    }

    [Fact]
    public void Conserva_texto_si_no_reconoce_fecha()
    {
        var fecha = FechaDocumentoParser.InterpretarFecha("fecha ilegible");
        Assert.Null(fecha.Fecha);
        Assert.True(fecha.FechaNoReconocida);
        Assert.Equal("fecha ilegible", fecha.Valor);
    }

    [Fact]
    public void Sugerencia_calce_devuelve_cero_uno_y_hasta_cuatro_coincidencias()
    {
        var repo = new RepositorioDatosEnlazantes(conexion);
        Assert.Empty(repo.SugerirCalce("oc_cliente", "00004500", 4));
        for (int i = 1; i <= 4; i++)
        {
            Ejecutar(
                $"INSERT INTO documentos(ruta,carpeta_raiz,nombre,tamano,modificado,estado,tiene_texto,indexado_en) VALUES('C:/{i}.pdf','C:/','{i}.pdf',1,'f','ok',1,'f');"
            );
            Ejecutar(
                $"INSERT INTO versiones_documento(documento_id,huella,ruta_observada,registrada_en) VALUES({i},'h{i}','C:/{i}.pdf','f');"
            );
            long identificacion = new Identificaciones(conexion).Guardar(
                new(0, "Factura del proveedor", $"Proveedor {i}", "{}")
            );
            long campo = new RepositorioDocumentosDatos(conexion).GuardarCampo(
                new(
                    0,
                    identificacion,
                    "OC cliente",
                    "oc_cliente",
                    "texto",
                    true,
                    "marca",
                    "oc_cliente"
                )
            );
            repo.GuardarDatoTipo(
                new(
                    0,
                    identificacion,
                    "oc_cliente",
                    "1",
                    campo,
                    1,
                    0.1,
                    0.1,
                    0.2,
                    0.1,
                    true,
                    true,
                    true
                )
            );
            new RepositorioDocumentosDatos(conexion).GuardarValor(i, campo, "4500", "4500", "pdf");
            if (i == 1)
                Assert.Single(repo.SugerirCalce("oc_cliente", "00004500", 4));
        }
        Assert.Equal(4, repo.SugerirCalce("oc_cliente", "00004500", 4).Count);
    }

    [Fact]
    public void Guarda_fecha_encargado_y_cliente_y_expone_fecha_para_consulta()
    {
        Ejecutar(
            "INSERT INTO documentos(ruta,carpeta_raiz,nombre,tamano,modificado,estado,tiene_texto,indexado_en) VALUES('C:/info.pdf','C:/','info.pdf',1,'f','ok',1,'f');"
        );
        Ejecutar(
            "INSERT INTO versiones_documento(documento_id,huella,ruta_observada,registrada_en) VALUES(1,'info','C:/info.pdf','f');"
        );
        var repo = new RepositorioDatosInformativos(conexion);
        var fecha = FechaDocumentoParser.InterpretarFecha("6 de octubre de 2026");
        repo.GuardarValores(
            1,
            [
                fecha,
                new("encargado", "Ana", null, false),
                new("nombre_cliente", "Cliente Uno", null, false),
            ]
        );
        Assert.Equal(
            new[] { "encargado", "fecha_documento", "nombre_cliente" },
            repo.ListarValores(1).Select(v => v.Dato).Order()
        );
        Assert.Single(repo.ConsultarFechas(new(2026, 10, 1), new(2026, 10, 31)));
        Assert.Equal("Ana", repo.ListarValores(1).Single(v => v.Dato == "encargado").Valor);
        Assert.Equal(
            "Cliente Uno",
            repo.ListarValores(1).Single(v => v.Dato == "nombre_cliente").Valor
        );
        Assert.Equal(3, repo.BuscarPorRuta("C:/info.pdf").Count);
    }

    private void Ejecutar(string sql) => EjecutarEn(conexion, sql);

    private static void EjecutarEn(SqliteConnection db, string sql)
    {
        using var comando = db.CreateCommand();
        comando.CommandText = sql;
        comando.ExecuteNonQuery();
    }

    private static long Escalar(SqliteConnection db, string sql)
    {
        using var comando = db.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private static string EscalarTexto(SqliteConnection db, string sql)
    {
        using var comando = db.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToString(comando.ExecuteScalar())!;
    }
}
