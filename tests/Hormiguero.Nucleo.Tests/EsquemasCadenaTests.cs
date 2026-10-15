using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Tests;

public sealed class EsquemasCadenaTests : IDisposable
{
    private readonly SqliteConnection _conexion = new("Data Source=:memory:");
    private readonly Dictionary<string, long> _identificaciones = new();

    public EsquemasCadenaTests()
    {
        _conexion.Open();
        Migraciones.Aplicar(_conexion, Migraciones.Todas);
        foreach (
            string tipo in new[] { "NVV", "OCC", "Guía", "Factura", "Sin piso", "Documento P1" }
        )
            _identificaciones[tipo] = CrearIdentificacion(tipo);
    }

    public void Dispose() => _conexion.Dispose();

    [Fact]
    public void Q2_un_enlace_no_declarado_no_une_documentos_con_numero_compartido()
    {
        var esquemas = new RepositorioEsquemasDiccionario(_conexion);
        esquemas.Guardar(
            "ACME",
            "Proceso",
            [
                new(0, "oc_propia", true, "OC propia"),
                new(1, "guia_proveedor", false, "Guía del proveedor"),
            ],
            []
        );
        long inicio = CrearVersion(
            "q2-inicio.pdf",
            "OCC",
            "Emitido",
            ("rut_proveedor", "ACME", true),
            ("oc_propia", "900", true)
        );
        long guia = CrearVersion(
            "q2-guia.pdf",
            "Guía",
            "Recibido",
            ("guia_proveedor", "G-1", true),
            ("oc_propia", "900", true)
        );
        Assert.Equal("cadena_creada", new MotorCadenas(_conexion).ProcesarVersion(inicio).Estado);
        Assert.Equal("sin_inicio", new MotorCadenas(_conexion).ProcesarVersion(guia).Estado);
    }

    [Fact]
    public void Q2_enlace_uno_a_varios_cuelga_dos_guias_bajo_la_oc()
    {
        var esquemas = new RepositorioEsquemasDiccionario(_conexion);
        esquemas.Guardar(
            "ACME",
            "Proceso",
            [
                new(0, "oc_propia", true, "OC propia"),
                new(1, "guia_proveedor", false, "Guía del proveedor"),
            ],
            [new(0, 1, "oc_propia", "uno_a_varios")]
        );
        long inicio = CrearVersion(
            "q2-oc.pdf",
            "OCC",
            "Emitido",
            ("rut_proveedor", "ACME", true),
            ("oc_propia", "901", true)
        );
        long guia1 = CrearVersion(
            "q2-guia1.pdf",
            "Guía",
            "Recibido",
            ("guia_proveedor", "G-11", true),
            ("oc_propia", "901", true)
        );
        long guia2 = CrearVersion(
            "q2-guia2.pdf",
            "Guía",
            "Recibido",
            ("guia_proveedor", "G-12", true),
            ("oc_propia", "901", true)
        );
        var motor = new MotorCadenas(_conexion);
        long cadena = motor.ProcesarVersion(inicio).CadenaId!.Value;
        Assert.Equal("ya_procesado", motor.ProcesarVersion(guia1).Estado);
        Assert.Equal("ya_procesado", motor.ProcesarVersion(guia2).Estado);
        using var consulta = _conexion.CreateCommand();
        consulta.CommandText =
            "SELECT COUNT(*),COUNT(DISTINCT linea_id),COUNT(DISTINCT padre_id) FROM ubicaciones_cadena_diccionario WHERE cadena_id=$c AND lugar_id=(SELECT id FROM lugares_esquema_diccionario WHERE esquema_id=(SELECT esquema_id FROM cadenas_diccionario WHERE id=$c) AND dato_tipo_id='guia_proveedor');";
        consulta.Parameters.AddWithValue("$c", cadena);
        using var lector = consulta.ExecuteReader();
        Assert.True(lector.Read());
        Assert.Equal(2, lector.GetInt32(0));
        Assert.Equal(2, lector.GetInt32(1));
        Assert.Equal(1, lector.GetInt32(2));
    }

    [Fact]
    public void Q2_enlace_uno_a_uno_guia_factura_une_por_la_guia()
    {
        var esquemas = new RepositorioEsquemasDiccionario(_conexion);
        esquemas.Guardar(
            "ACME",
            "Proceso",
            [
                new(0, "guia_proveedor", true, "Guía del proveedor"),
                new(1, "factura_proveedor", false, "Factura del proveedor"),
            ],
            [new(0, 1, "guia_proveedor", "uno_a_uno")]
        );
        long guia = CrearVersion(
            "q2-guia-unica.pdf",
            "Guía",
            "Recibido",
            ("rut_proveedor", "ACME", true),
            ("guia_proveedor", "G-21", true)
        );
        long factura = CrearVersion(
            "q2-factura.pdf",
            "Factura",
            "Recibido",
            ("factura_proveedor", "F-21", true),
            ("guia_proveedor", "G-21", true)
        );
        var motor = new MotorCadenas(_conexion);
        long cadena = motor.ProcesarVersion(guia).CadenaId!.Value;
        Assert.Equal("ya_procesado", motor.ProcesarVersion(factura).Estado);
        using var consulta = _conexion.CreateCommand();
        consulta.CommandText =
            "SELECT COUNT(*) FROM ubicaciones_cadena_diccionario WHERE cadena_id=$c AND padre_id IS NOT NULL;";
        consulta.Parameters.AddWithValue("$c", cadena);
        Assert.Equal(1L, Convert.ToInt64(consulta.ExecuteScalar()));
    }

    [Fact]
    public void Q2_diccionario_lista_tipos_y_precarga_datos_sugeridos()
    {
        CrearVersion(
            "q2-sugerencia-oc.pdf",
            "OCC",
            "Emitido",
            ("oc_propia", "902", true),
            ("rut_proveedor", "Q2-SUG", true)
        );
        CrearVersion(
            "q2-sugerencia-guia.pdf",
            "Guía",
            "Recibido",
            ("guia_proveedor", "G-30", true),
            ("oc_propia", "902", true)
        );
        Assert.Contains(
            RepositorioEsquemasDiccionario.ListarTiposDocumento(_conexion),
            d => d.Id == "oc_propia"
        );
        Assert.Contains(
            RepositorioEsquemasDiccionario.ListarDatos(_conexion, "oc_propia", "guia_proveedor"),
            d => d.Id == "oc_propia" && d.Sugerido
        );
    }

    private void CrearEsquema(string proveedor = "ACME")
    {
        var repo = new RepositorioEsquemas(_conexion);
        repo.Guardar(
            proveedor,
            "Proceso",
            [
                new(0, _identificaciones["NVV"], true, "Inicio"),
                new(1, _identificaciones["OCC"], false, "Compra"),
                new(2, _identificaciones["Guía"], false, "Guía"),
                new(3, _identificaciones["Factura"], false, "Factura"),
                new(4, _identificaciones["Sin piso"], false, "Otro"),
                new(5, _identificaciones["Documento P1"], false, "Documento de proveedor P1"),
            ],
            [new(2, 3, "guia_proveedor")]
        );
    }

    private long CrearIdentificacion(string tipo)
    {
        using var comando = _conexion.CreateCommand();
        comando.CommandText =
            "INSERT INTO identificaciones(tipo,emisor,datos,actualizada,grupo_documento,nombre_estandar) VALUES($t,$e,'{}','ahora',$g,$t) RETURNING id;";
        comando.Parameters.AddWithValue("$t", tipo);
        comando.Parameters.AddWithValue("$e", tipo == "Documento P1" ? "P-1" : "ACME");
        comando.Parameters.AddWithValue(
            "$g",
            tipo is "Guía" or "Factura" or "Sin piso" or "Documento P1" ? "Recibido" : "Emitido"
        );
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private long CrearVersion(
        string nombre,
        string tipo,
        string grupo,
        params (string Dato, string Valor, bool Enlazable)[] valores
    )
    {
        using var comando = _conexion.CreateCommand();
        comando.CommandText =
            "INSERT INTO documentos(ruta,carpeta_raiz,nombre,tamano,modificado,estado,tiene_texto,indexado_en) VALUES($r,'/',$n,1,'ahora','ok',1,'ahora') RETURNING id;";
        comando.Parameters.AddWithValue("$r", "/" + nombre);
        comando.Parameters.AddWithValue("$n", nombre);
        long documento = Convert.ToInt64(comando.ExecuteScalar());
        using var version = _conexion.CreateCommand();
        version.CommandText =
            "INSERT INTO versiones_documento(documento_id,huella,ruta_observada,registrada_en) VALUES($d,$h,$r,'ahora') RETURNING id;";
        version.Parameters.AddWithValue("$d", documento);
        version.Parameters.AddWithValue("$h", nombre);
        version.Parameters.AddWithValue("$r", "/" + nombre);
        long versionId = Convert.ToInt64(version.ExecuteScalar());
        long identificacion = _identificaciones[tipo];
        foreach (var valor in valores)
        {
            using var campo = _conexion.CreateCommand();
            campo.CommandText =
                "INSERT INTO campos_documento(identificacion_id,nombre,nombre_estable,tipo_dato,origen_lectura,dato_diccionario_id) VALUES($i,$d,$d,'texto','prueba',$d) ON CONFLICT(identificacion_id,nombre_estable) DO UPDATE SET dato_diccionario_id=excluded.dato_diccionario_id RETURNING id;";
            campo.Parameters.AddWithValue("$i", identificacion);
            campo.Parameters.AddWithValue("$d", valor.Dato);
            long campoId = Convert.ToInt64(campo.ExecuteScalar());
            using var tipoDato = _conexion.CreateCommand();
            tipoDato.CommandText =
                "INSERT INTO tipos_documento_datos(identificacion_id,dato_diccionario_id,diseno,pagina,x,y,ancho,alto,enlazable,define_tipo,creada_en,actualizada_en) VALUES($i,$d,'1',1,0,0,1,1,$e,CASE WHEN $d=$t THEN 1 ELSE 0 END,'ahora','ahora') ON CONFLICT(identificacion_id,dato_diccionario_id,diseno) DO UPDATE SET enlazable=excluded.enlazable,define_tipo=excluded.define_tipo;";
            tipoDato.Parameters.AddWithValue("$i", identificacion);
            tipoDato.Parameters.AddWithValue("$d", valor.Dato);
            tipoDato.Parameters.AddWithValue(
                "$t",
                tipo switch
                {
                    "NVV" => "nota_venta_propia",
                    "OCC" => "oc_propia",
                    "Guía" => "guia_proveedor",
                    "Factura" or "Documento P1" or "Sin piso" => "factura_proveedor",
                    _ => "",
                }
            );
            tipoDato.Parameters.AddWithValue("$e", valor.Enlazable ? 1 : 0);
            tipoDato.ExecuteNonQuery();
            using var insertar = _conexion.CreateCommand();
            insertar.CommandText =
                "INSERT INTO valores_documento(version_id,campo_id,valor_original,valor_clave,origen,fecha_creacion,dato_diccionario_id) VALUES($v,$c,$o,$k,'prueba','ahora',$d);";
            insertar.Parameters.AddWithValue("$v", versionId);
            insertar.Parameters.AddWithValue("$c", campoId);
            insertar.Parameters.AddWithValue("$o", valor.Valor);
            insertar.Parameters.AddWithValue(
                "$k",
                DiccionarioDatosEnlazantes.ClaveDeEnlace(valor.Dato, valor.Valor)
            );
            insertar.Parameters.AddWithValue("$d", valor.Dato);
            insertar.ExecuteNonQuery();
        }
        return versionId;
    }

    private string? LeerCliente(long cadena)
    {
        using var comando = _conexion.CreateCommand();
        comando.CommandText = "SELECT cliente FROM cadenas WHERE id=$c;";
        comando.Parameters.AddWithValue("$c", cadena);
        return Convert.ToString(comando.ExecuteScalar());
    }
}
