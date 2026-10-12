using System.Text.Json;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Tests;

public sealed class Q2bAlertasDiccionarioTests : IDisposable
{
    private readonly SqliteConnection _conexion = new("Data Source=:memory:");

    public Q2bAlertasDiccionarioTests()
    {
        _conexion.Open();
        Migraciones.Aplicar(_conexion, Migraciones.Todas);
    }

    public void Dispose() => _conexion.Dispose();

    [Fact]
    public void Aviso_falta_dato_es_idempotente_y_se_cierra_al_guardar_el_dato()
    {
        long esquema = new RepositorioEsquemasDiccionario(_conexion).Guardar(
            "ACME",
            "Proceso",
            [new(0, "oc_propia", true, "OC propia")],
            []
        );
        long lugar;
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText = "SELECT id FROM lugares_esquema_diccionario WHERE esquema_id=$e;";
            q.Parameters.AddWithValue("$e", esquema);
            lugar = Convert.ToInt64(q.ExecuteScalar());
        }
        long cadena;
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText =
                "INSERT INTO cadenas_diccionario(esquema_id,nombre) VALUES($e,'OC 1') RETURNING id;";
            q.Parameters.AddWithValue("$e", esquema);
            cadena = Convert.ToInt64(q.ExecuteScalar());
        }
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText =
                """INSERT INTO reglas_alerta_esquema_diccionario(esquema_id,lugar_id,tipo,parametros_json) VALUES($e,$l,'falta_dato','{"dato":"fecha_pago","texto":"Falta fecha de pago"}');""";
            q.Parameters.AddWithValue("$e", esquema);
            q.Parameters.AddWithValue("$l", lugar);
            q.ExecuteNonQuery();
        }

        var evaluador = new EvaluadorAlertasEsquemaDiccionario(_conexion);
        evaluador.Evaluar(new DateOnly(2026, 10, 10));
        evaluador.Evaluar(new DateOnly(2026, 10, 10));
        Assert.Single(evaluador.ListarAbiertas(cadenaId: cadena));

        new RepositorioDatosCadenaDiccionario(_conexion).Guardar(
            cadena,
            "fecha_pago",
            "2026-10-10"
        );
        Assert.Empty(evaluador.ListarAbiertas(cadenaId: cadena));
    }

    [Fact]
    public void Fijar_modo_guarda_historial_y_resuelve_la_decision()
    {
        long esquema = new RepositorioEsquemasDiccionario(_conexion).Guardar(
            "ACME",
            "Proceso",
            [new(0, "oc_propia", true, "OC propia")],
            []
        );
        long cadena;
        long modo;
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText =
                "INSERT INTO cadenas_diccionario(esquema_id,nombre) VALUES($e,'OC 1') RETURNING id;";
            q.Parameters.AddWithValue("$e", esquema);
            cadena = Convert.ToInt64(q.ExecuteScalar());
        }
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText =
                "INSERT INTO modos_esquema_diccionario(esquema_id,nombre) VALUES($e,'Urgente') RETURNING id;";
            q.Parameters.AddWithValue("$e", esquema);
            modo = Convert.ToInt64(q.ExecuteScalar());
        }
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText =
                "INSERT INTO documentos(ruta,carpeta_raiz,nombre,tamano,modificado,estado,tiene_texto,indexado_en) VALUES('x.pdf','/','x.pdf',1,'2026-10-10','activo',1,'2026-10-10'); INSERT INTO versiones_documento(documento_id,huella,ruta_observada,registrada_en) VALUES(1,'x','x.pdf','2026-10-10');";
            q.ExecuteNonQuery();
        }
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText =
                """INSERT INTO decisiones_esquema_diccionario(version_id,proveedor,motivo,creada_en,tipo,cadena_id,opciones_json) VALUES(1,'ACME','Modo pendiente','2026-10-10','modo',$c,'["Urgente"]');""";
            q.Parameters.AddWithValue("$c", cadena);
            q.ExecuteNonQuery();
        }

        new RepositorioModosEsquemaDiccionario(_conexion).FijarModo(cadena, modo);

        using var consulta = _conexion.CreateCommand();
        consulta.CommandText =
            "SELECT c.modo_id,h.modo_nuevo_id,d.estado FROM cadenas_diccionario c JOIN historial_modos_cadena_diccionario h ON h.cadena_id=c.id JOIN decisiones_esquema_diccionario d ON d.cadena_id=c.id WHERE c.id=$c;";
        consulta.Parameters.AddWithValue("$c", cadena);
        using var lector = consulta.ExecuteReader();
        Assert.True(lector.Read());
        Assert.Equal(modo, lector.GetInt64(0));
        Assert.Equal(modo, lector.GetInt64(1));
        Assert.Equal("resuelta", lector.GetString(2));
    }

    [Fact]
    public void Archivar_sin_cadena_resuelve_la_decision_nueva()
    {
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText =
                "INSERT INTO documentos(ruta,carpeta_raiz,nombre,tamano,modificado,estado,tiene_texto,indexado_en) VALUES('sin-cadena.pdf','/','sin-cadena.pdf',1,'2026-10-10','activo',1,'2026-10-10'); INSERT INTO versiones_documento(documento_id,huella,ruta_observada,registrada_en) VALUES(1,'sin-cadena','sin-cadena.pdf','2026-10-10');";
            q.ExecuteNonQuery();
        }
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText =
                "INSERT INTO decisiones_esquema_diccionario(version_id,proveedor,motivo,creada_en,tipo) VALUES(1,'ACME','Sin piso','2026-10-10','sin_piso');";
            q.ExecuteNonQuery();
        }

        Assert.True(new MotorCadenas(_conexion).ArchivarSinCadena(1));

        using var consulta = _conexion.CreateCommand();
        consulta.CommandText =
            "SELECT estado FROM decisiones_esquema_diccionario WHERE version_id=1;";
        Assert.Equal("resuelta", consulta.ExecuteScalar());
    }

    [Fact]
    public void Plazo_usa_fecha_de_emision_y_cierra_el_aviso_al_llegar_el_destino()
    {
        long esquema = new RepositorioEsquemasDiccionario(_conexion).Guardar(
            "ACME",
            "Proceso",
            [
                new(0, "oc_propia", true, "OC propia"),
                new(1, "guia_proveedor", false, "Guía del proveedor"),
            ],
            [new(0, 1, "oc_propia", "uno_a_uno")]
        );
        long origen;
        long destino;
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText =
                "SELECT id,orden FROM lugares_esquema_diccionario WHERE esquema_id=$e ORDER BY orden;";
            q.Parameters.AddWithValue("$e", esquema);
            using var r = q.ExecuteReader();
            Assert.True(r.Read());
            origen = r.GetInt64(0);
            Assert.True(r.Read());
            destino = r.GetInt64(0);
        }
        long cadena;
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText =
                "INSERT INTO cadenas_diccionario(esquema_id,nombre) VALUES($e,'OC-1') RETURNING id;";
            q.Parameters.AddWithValue("$e", esquema);
            cadena = Convert.ToInt64(q.ExecuteScalar());
        }
        InsertarVersion(1, "origen.pdf", "2026-10-01");
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText =
                "INSERT INTO ubicaciones_cadena_diccionario(cadena_id,version_id,lugar_id,estado,orden) VALUES($c,1,$l,'activo',0);";
            q.Parameters.AddWithValue("$c", cadena);
            q.Parameters.AddWithValue("$l", origen);
            q.ExecuteNonQuery();
        }
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText =
                "INSERT INTO reglas_alerta_esquema_diccionario(esquema_id,lugar_id,tipo,parametros_json) VALUES($e,$l,'plazo',$j);";
            q.Parameters.AddWithValue("$e", esquema);
            q.Parameters.AddWithValue("$l", origen);
            q.Parameters.AddWithValue(
                "$j",
                JsonSerializer.Serialize(
                    new
                    {
                        desdeLugarId = origen,
                        hastaLugarId = destino,
                        dias = 2,
                        tipoDias = "corridos",
                        texto = "Falta guía",
                        porLinea = false,
                    }
                )
            );
            q.ExecuteNonQuery();
        }

        var evaluador = new EvaluadorAlertasEsquemaDiccionario(_conexion);
        evaluador.Evaluar(new DateOnly(2026, 10, 10));
        var aviso = Assert.Single(evaluador.ListarAbiertas(cadenaId: cadena));
        Assert.Equal(new DateOnly(2026, 10, 3), aviso.FechaVencimiento);
        Assert.Equal("vencido", aviso.Urgencia);

        InsertarVersion(2, "destino.pdf", "2026-10-05");
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText =
                "INSERT INTO ubicaciones_cadena_diccionario(cadena_id,version_id,lugar_id,padre_id,linea_id,estado,orden) VALUES($c,2,$l,1,1,'activo',1);";
            q.Parameters.AddWithValue("$c", cadena);
            q.Parameters.AddWithValue("$l", destino);
            q.ExecuteNonQuery();
        }
        evaluador.Evaluar(new DateOnly(2026, 10, 10));
        Assert.Empty(evaluador.ListarAbiertas(cadenaId: cadena));
    }

    private void InsertarVersion(long id, string ruta, string fecha)
    {
        using var q = _conexion.CreateCommand();
        q.CommandText =
            "INSERT INTO documentos(id,ruta,carpeta_raiz,nombre,tamano,modificado,estado,tiene_texto,indexado_en) VALUES($id,$r,'/',$r,1,$f,'activo',1,$f); INSERT INTO versiones_documento(id,documento_id,huella,ruta_observada,registrada_en) VALUES($id,$id,$r,$r,$f);";
        q.Parameters.AddWithValue("$id", id);
        q.Parameters.AddWithValue("$r", ruta);
        q.Parameters.AddWithValue("$f", fecha);
        q.ExecuteNonQuery();
    }

    [Fact]
    public void Quitar_un_modo_lo_borra_salvo_si_una_cadena_lo_usa()
    {
        long esquema = new RepositorioEsquemasDiccionario(_conexion).Guardar(
            "ACME",
            "Proceso",
            [new(0, "oc_propia", true, "OC propia")],
            []
        );
        var config = new RepositorioConfiguracionEsquemaDiccionario(_conexion);
        config.GuardarModos(esquema, 0, ["Retiro", "Despacho", "Mixto"]);
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText =
                "INSERT INTO cadenas_diccionario(esquema_id,nombre,modo_id) SELECT $e,'OC 1',id FROM modos_esquema_diccionario WHERE nombre='Mixto';";
            q.Parameters.AddWithValue("$e", esquema);
            q.ExecuteNonQuery();
        }

        config.GuardarModos(esquema, 0, ["retiro"]);

        Assert.Equal(
            ["Mixto", "Retiro"],
            new RepositorioModosEsquemaDiccionario(_conexion)
                .Listar(esquema)
                .Select(m => m.Nombre)
                .Order()
        );
    }

    [Fact]
    public void Guardar_aviso_con_lugar_opcional_vacio_no_falla()
    {
        long esquema = new RepositorioEsquemasDiccionario(_conexion).Guardar(
            "ACME",
            "Proceso",
            [new(0, "guia_proveedor", true, "Guía"), new(1, "factura_proveedor", false, "Factura")],
            []
        );

        new RepositorioConfiguracionEsquemaDiccionario(_conexion).GuardarReglas(
            esquema,
            [
                new(
                    0,
                    null,
                    "plazo",
                    """{"desdeLugarId":0,"hastaLugarId":1,"condicionLugarId":null,"dias":7}"""
                ),
            ]
        );

        using var q = _conexion.CreateCommand();
        q.CommandText = "SELECT count(*) FROM reglas_alerta_esquema_diccionario;";
        Assert.Equal(1L, q.ExecuteScalar());
    }
}
