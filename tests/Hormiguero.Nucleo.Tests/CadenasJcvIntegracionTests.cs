using System.Globalization;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Tests;

public sealed class CadenasJcvIntegracionTests : IDisposable
{
    private readonly SqliteConnection _conexion = new("Data Source=:memory:");
    private readonly Dictionary<(string Tipo, string Emisor), long> _identificaciones = [];

    public CadenasJcvIntegracionTests()
    {
        _conexion.Open();
        Migraciones.Aplicar(_conexion, Migraciones.Todas);
        string configuracion = BuscarConfiguracion();
        bool importada = PrecargaCadenasJcv.IntentarImportar(_conexion, configuracion, "Pruebas");
        using (var error = _conexion.CreateCommand())
        {
            error.CommandText = "SELECT destino FROM auditoria ORDER BY id DESC LIMIT 1;";
            Assert.True(importada, Convert.ToString(error.ExecuteScalar()));
        }
        foreach (var tipo in DiccionarioDatosEnlazantes.Todos)
        {
            string emisor = tipo.Id switch
            {
                "oc_propia" or "nota_venta_propia" or "oc_cliente" => "Empresa",
                "guia_firmada_proveedor" => "(sin texto)",
                _ => "HOFFENS",
            };
            string grupo = tipo.Id
                is "oc_propia"
                    or "nota_venta_propia"
                    or "oc_cliente"
                    or "cotizacion_propia"
                ? "Emitido"
                : "Recibido";
            _identificaciones[(tipo.Id, emisor)] = CrearIdentificacion(tipo.Id, emisor, grupo);
        }
    }

    public void Dispose() => _conexion.Dispose();

    [Fact]
    public void Reconoce_rut_emisor_y_nombre_en_la_misma_cadena_y_conserva_el_comportamiento_sin_calce()
    {
        long occ = Documento(
            "OCC",
            "OCC 20417",
            "20417",
            DateOnly.Parse("2025-03-03"),
            ("rut_proveedor", "96.548.020-K"),
            ("oc_propia", "20417")
        );
        long cadena = new MotorCadenas(_conexion).ProcesarVersion(occ).CadenaId!.Value;
        var motor = new MotorCadenas(_conexion);
        long grc = Documento(
            "GRC",
            "GRC 1190036",
            "1190036",
            DateOnly.Parse("2025-03-04"),
            ("guia_proveedor", "1190036"),
            ("oc_propia", "20417")
        );
        Assert.Equal(cadena, motor.ProcesarVersion(grc).CadenaId);
        long fcc = Documento(
            "FCC",
            "FCC 2419619",
            "2419619",
            DateOnly.Parse("2025-03-05"),
            ("factura_proveedor", "2419619"),
            ("guia_proveedor", "1190036")
        );
        var resultadoFactura = motor.ProcesarVersion(fcc);
        Assert.True(
            resultadoFactura.CadenaId == cadena,
            $"{resultadoFactura.Estado}: {resultadoFactura.Motivo}"
        );
        long sinCalce = Documento(
            "OCC",
            "OCC sin calce",
            "999",
            DateOnly.Parse("2025-03-03"),
            ("rut_proveedor", "12.345.678-5"),
            ("oc_propia", "999")
        );
        Assert.Equal("12.345.678-5", ProveedorDeDocumento.Determinar(_conexion, sinCalce)!.Clave);
        Assert.Equal("sin_esquema", motor.ProcesarVersion(sinCalce).Estado);
    }

    [Fact]
    public void Guia_firmada_sin_texto_se_ubica_en_linea_de_guia_y_el_orden_inverso_queda_sin_piso()
    {
        long occ = Documento(
            "OCC",
            "OCC 20417",
            "20417",
            DateOnly.Parse("2025-03-03"),
            ("rut_proveedor", "96.548.020-K"),
            ("oc_propia", "20417")
        );
        var motor = new MotorCadenas(_conexion);
        long cadena = motor.ProcesarVersion(occ).CadenaId!.Value;
        long grc = Documento(
            "GRC",
            "GRC 1190036",
            "1190036",
            DateOnly.Parse("2025-03-04"),
            ("guia_proveedor", "1190036"),
            ("oc_propia", "20417")
        );
        Assert.Equal("ubicado", motor.ProcesarVersion(grc).Estado);
        long grcf = Documento(
            "GRCF",
            "0001190036.pdf",
            "0001190036",
            DateOnly.Parse("2025-03-05"),
            ("guia_firmada_proveedor", "1190036")
        );
        Assert.Equal(cadena, motor.ProcesarVersion(grcf).CadenaId);
        Assert.Equal(Linea(grc), Linea(grcf));

        long grcfPrimero = Documento(
            "GRCF",
            "0001190037.pdf",
            "0001190037",
            DateOnly.Parse("2025-03-06"),
            ("guia_firmada_proveedor", "1190037")
        );
        Assert.Equal("sin_piso", motor.ProcesarVersion(grcfPrimero).Estado);
        long grcDespues = Documento(
            "GRC",
            "GRC 1190037",
            "1190037",
            DateOnly.Parse("2025-03-07"),
            ("guia_proveedor", "1190037"),
            ("oc_propia", "20417")
        );
        Assert.Equal("ubicado", motor.ProcesarVersion(grcDespues).Estado);
        Assert.NotEqual(Linea(grcfPrimero), Linea(grcDespues));
        Assert.Equal("sin_piso", EstadoDecision(grcfPrimero));
    }

    [Fact]
    public void Hoffens_completa_el_ciclo_de_despacho_guia_firmada_y_facturacion()
    {
        DateOnly lunes = new(2025, 3, 3);
        long occ = Documento(
            "OCC",
            "OCC 20417",
            "20417",
            lunes,
            ("rut_proveedor", "96.548.020-K"),
            ("oc_propia", "20417"),
            ("nota_venta_propia", "20092"),
            ("oc_cliente", "73106")
        );
        var motor = new MotorCadenas(_conexion);
        long cadena = motor.ProcesarVersion(occ).CadenaId!.Value;
        var modos = new RepositorioModosEsquemaDiccionario(_conexion);
        Assert.Contains(modos.ListarPendientes(), p => p.CadenaId == cadena);
        long modo = Modo("HOFFENS", "Despacho RM");
        modos.FijarModo(cadena, modo);
        var evaluador = new EvaluadorAlertasEsquemaDiccionario(_conexion);
        evaluador.Evaluar(new DateOnly(2025, 3, 11));
        Assert.Contains(
            evaluador.ListarAbiertas(cadenaId: cadena),
            a => a.Texto == "Proveedor no ha despachado"
        );

        long grc = Documento(
            "GRC",
            "GRC 1190036",
            "1190036",
            new(2025, 3, 11),
            ("guia_proveedor", "1190036"),
            ("oc_propia", "20417")
        );
        Assert.Equal(cadena, motor.ProcesarVersion(grc).CadenaId);
        evaluador.Evaluar(new DateOnly(2025, 3, 11));
        Assert.DoesNotContain(
            evaluador.ListarAbiertas(cadenaId: cadena),
            a => a.Texto == "Proveedor no ha despachado"
        );

        long fcc = Documento(
            "FCC",
            "FCC 2419619",
            "2419619",
            new(2025, 3, 12),
            ("factura_proveedor", "2419619"),
            ("guia_proveedor", "1190036")
        );
        Assert.Equal(cadena, motor.ProcesarVersion(fcc).CadenaId);
        Assert.Equal(Linea(grc), Linea(fcc));
        evaluador.Evaluar(new DateOnly(2025, 3, 20));
        Assert.Contains(
            evaluador.ListarAbiertas(cadenaId: cadena),
            a => a.Texto == "Falta gu\u00eda firmada"
        );
        long grcf = Documento(
            "GRCF",
            "0001190036.pdf",
            "0001190036",
            new(2025, 3, 21),
            ("guia_firmada_proveedor", "1190036")
        );
        Assert.Equal(cadena, motor.ProcesarVersion(grcf).CadenaId);
        evaluador.Evaluar(new DateOnly(2025, 3, 21));
        Assert.DoesNotContain(
            evaluador.ListarAbiertas(cadenaId: cadena),
            a => a.Texto == "Falta gu\u00eda firmada"
        );
        Assert.Contains(evaluador.ListarListos("Listos para facturar"), a => a.CadenaId == cadena);
        long fcv = Documento(
            "FCV",
            "FCV 445",
            "445",
            new(2025, 3, 22),
            ("factura_propia", "445"),
            ("guia_proveedor", "1190036")
        );
        Assert.Equal(cadena, motor.ProcesarVersion(fcv).CadenaId);
        evaluador.Evaluar(new DateOnly(2025, 3, 22));
        Assert.DoesNotContain(
            evaluador.ListarListos("Listos para facturar"),
            a => a.CadenaId == cadena
        );
    }

    [Fact]
    public void Cobelcar_queda_listo_con_factura_sin_requerir_guia_firmada()
    {
        var motor = new MotorCadenas(_conexion);
        long occ = Documento(
            "OCC",
            "OCC 20527",
            "20527",
            new(2025, 4, 1),
            ("rut_proveedor", "78.615.580-0"),
            ("oc_propia", "20527")
        );
        long cadena = motor.ProcesarVersion(occ).CadenaId!.Value;
        long grc = Documento(
            "GRC",
            "GRC 313091",
            "313091",
            new(2025, 4, 2),
            ("guia_proveedor", "313091"),
            ("oc_propia", "20527")
        );
        Assert.Equal(cadena, motor.ProcesarVersion(grc).CadenaId);
        long fcc = Documento(
            "FCC",
            "FCC Cobelcar",
            "1001",
            new(2025, 4, 3),
            ("factura_proveedor", "1001"),
            ("guia_proveedor", "313091")
        );
        var resultadoFactura = motor.ProcesarVersion(fcc);
        Assert.True(
            resultadoFactura.CadenaId == cadena,
            $"{resultadoFactura.Estado}: {resultadoFactura.Motivo}"
        );
        var evaluador = new EvaluadorAlertasEsquemaDiccionario(_conexion);
        evaluador.Evaluar(new DateOnly(2025, 4, 3));
        Assert.Contains(evaluador.ListarListos("Listos para facturar"), a => a.CadenaId == cadena);
    }

    [Fact]
    public void Retiro_avisa_al_dia_siguiente_de_la_fecha_ingresada()
    {
        var motor = new MotorCadenas(_conexion);
        long occ = Documento(
            "OCC",
            "OCC retiro",
            "30001",
            new(2025, 5, 5),
            ("rut_proveedor", "96.548.020-K"),
            ("oc_propia", "30001")
        );
        long cadena = motor.ProcesarVersion(occ).CadenaId!.Value;
        var modos = new RepositorioModosEsquemaDiccionario(_conexion);
        modos.FijarModo(cadena, Modo("HOFFENS", "Retiro"));
        using (var q = _conexion.CreateCommand())
        {
            q.CommandText =
                "INSERT INTO datos_cadena_diccionario(cadena_id,nombre,valor,actualizado_en) VALUES($c,'Fecha de retiro','2025-05-05','2025-05-05');";
            q.Parameters.AddWithValue("$c", cadena);
            q.ExecuteNonQuery();
        }
        var evaluador = new EvaluadorAlertasEsquemaDiccionario(_conexion);
        evaluador.Evaluar(new DateOnly(2025, 5, 6));
        Assert.Contains(
            evaluador.ListarAbiertas(cadenaId: cadena),
            a => a.Texto == "Cliente no retir\u00f3"
        );
    }

    private long Documento(
        string tipo,
        string nombre,
        string huella,
        DateOnly fecha,
        params (string Dato, string Valor)[] valores
    )
    {
        string tipoId = tipo switch
        {
            "OCC" => "oc_propia",
            "NVV" => "nota_venta_propia",
            "OCL" => "oc_cliente",
            "GRC" => "guia_proveedor",
            "GRCF" => "guia_firmada_proveedor",
            "FCC" => "factura_proveedor",
            "FCV" => "factura_propia",
            _ => throw new ArgumentOutOfRangeException(nameof(tipo)),
        };
        string proveedor =
            valores.FirstOrDefault(v => v.Dato == "rut_proveedor").Valor == "78.615.580-0"
            || valores.FirstOrDefault(v => v.Dato == "oc_propia").Valor == "20527"
                ? "COBELCAR"
                : "HOFFENS";
        if (tipo == "FCC")
        {
            using var proveedorGuia = _conexion.CreateCommand();
            proveedorGuia.CommandText =
                "SELECT e.proveedor FROM valores_documento x JOIN ubicaciones_cadena_diccionario u ON u.version_id=x.version_id JOIN cadenas_diccionario c ON c.id=u.cadena_id JOIN esquemas_diccionario e ON e.id=c.esquema_id WHERE x.dato_diccionario_id='guia_proveedor' AND x.valor_original=$g AND u.estado='activo' LIMIT 1;";
            proveedorGuia.Parameters.AddWithValue(
                "$g",
                valores.First(v => v.Dato == "guia_proveedor").Valor
            );
            proveedor = Convert.ToString(proveedorGuia.ExecuteScalar()) ?? proveedor;
        }
        string emisor = tipo is "OCC" or "NVV" or "OCL" or "FCV" ? "Empresa" : proveedor;
        string grupo = emisor == "Empresa" ? "Emitido" : "Recibido";
        long identificacion = Identificacion(tipoId, emisor, grupo);
        if (tipo == "GRC" && proveedor == "COBELCAR")
        {
            emisor = "VELIZ BELTRAN LIMITADA";
            identificacion = Identificacion(tipoId, emisor, grupo);
        }
        if (tipo is "GRCF" or "FCC")
            valores =
            [
                .. valores,
                (
                    "nombre_proveedor",
                    proveedor == "HOFFENS" ? "HOFFENS S.A." : "VELIZ BELTRAN LIMITADA"
                ),
            ];
        if (tipo == "GRCF")
        {
            string numeroArchivo = string.Concat(
                    Path.GetFileNameWithoutExtension(nombre).Where(char.IsDigit)
                )
                .TrimStart('0');
            int indice = Array.FindIndex(valores, v => v.Dato == "guia_firmada_proveedor");
            if (indice >= 0)
                valores[indice] = ("guia_firmada_proveedor", numeroArchivo);
        }
        using var q = _conexion.CreateCommand();
        q.CommandText =
            "INSERT INTO documentos(ruta,carpeta_raiz,nombre,tamano,modificado,estado,tiene_texto,indexado_en) VALUES($r,'/prueba',$n,1,$f,'ok',CASE WHEN $t='GRCF' THEN 0 ELSE 1 END,$f) RETURNING id;";
        q.Parameters.AddWithValue("$r", "/prueba/" + nombre);
        q.Parameters.AddWithValue("$n", nombre);
        q.Parameters.AddWithValue("$t", tipo);
        q.Parameters.AddWithValue(
            "$f",
            fecha.ToDateTime(TimeOnly.MinValue).ToString("o", CultureInfo.InvariantCulture)
        );
        long documento = Convert.ToInt64(q.ExecuteScalar());
        using var version = _conexion.CreateCommand();
        version.CommandText =
            "INSERT INTO versiones_documento(documento_id,huella,ruta_observada,registrada_en) VALUES($d,$h,$r,$f) RETURNING id;";
        version.Parameters.AddWithValue("$d", documento);
        version.Parameters.AddWithValue("$h", huella);
        version.Parameters.AddWithValue("$r", "/prueba/" + nombre);
        version.Parameters.AddWithValue(
            "$f",
            fecha.ToDateTime(TimeOnly.MinValue).ToString("o", CultureInfo.InvariantCulture)
        );
        long versionId = Convert.ToInt64(version.ExecuteScalar());
        foreach (var valor in valores)
        {
            long campo = Campo(identificacion, valor.Dato, tipoId);
            using var insertar = _conexion.CreateCommand();
            insertar.CommandText =
                "INSERT INTO valores_documento(version_id,campo_id,valor_original,valor_clave,origen,fecha_creacion,dato_diccionario_id) VALUES($v,$c,$o,$k,'prueba',$f,$d);";
            insertar.Parameters.AddWithValue("$v", versionId);
            insertar.Parameters.AddWithValue("$c", campo);
            insertar.Parameters.AddWithValue("$o", valor.Valor);
            insertar.Parameters.AddWithValue(
                "$k",
                DiccionarioDatosEnlazantes.ClaveDeEnlace(valor.Dato, valor.Valor)
            );
            insertar.Parameters.AddWithValue(
                "$f",
                fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            );
            insertar.Parameters.AddWithValue("$d", valor.Dato);
            insertar.ExecuteNonQuery();
        }
        return versionId;
    }

    private long Campo(long identificacion, string dato, string tipoDocumento)
    {
        using var q = _conexion.CreateCommand();
        q.CommandText =
            "INSERT INTO campos_documento(identificacion_id,nombre,nombre_estable,tipo_dato,origen_lectura) VALUES($i,$d,$d,'texto','prueba') ON CONFLICT(identificacion_id,nombre_estable) DO UPDATE SET nombre=excluded.nombre RETURNING id;";
        q.Parameters.AddWithValue("$i", identificacion);
        q.Parameters.AddWithValue("$d", dato);
        long campo = Convert.ToInt64(q.ExecuteScalar());
        using var tipo = _conexion.CreateCommand();
        tipo.CommandText =
            "INSERT INTO tipos_documento_datos(identificacion_id,dato_diccionario_id,diseno,pagina,x,y,ancho,alto,define_tipo,creada_en,actualizada_en) VALUES($i,$d,'1',1,0,0,1,1,$t,'ahora','ahora') ON CONFLICT(identificacion_id,dato_diccionario_id,diseno) DO UPDATE SET define_tipo=excluded.define_tipo;";
        tipo.Parameters.AddWithValue("$i", identificacion);
        tipo.Parameters.AddWithValue("$d", dato);
        tipo.Parameters.AddWithValue("$t", dato == tipoDocumento ? 1 : 0);
        tipo.ExecuteNonQuery();
        return campo;
    }

    private long Identificacion(string tipoId, string emisor, string grupo)
    {
        if (_identificaciones.TryGetValue((tipoId, emisor), out long id))
            return id;
        return _identificaciones[(tipoId, emisor)] = CrearIdentificacion(tipoId, emisor, grupo);
    }

    private long CrearIdentificacion(string tipoId, string emisor, string grupo) =>
        new Identificaciones(_conexion).Guardar(
            new(
                0,
                DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == tipoId).EtiquetaTipo,
                emisor,
                "{}",
                grupo,
                tipoId
            )
        );

    private long Linea(long version)
    {
        using var q = _conexion.CreateCommand();
        q.CommandText = "SELECT linea_id FROM ubicaciones_cadena_diccionario WHERE version_id=$v;";
        q.Parameters.AddWithValue("$v", version);
        return Convert.ToInt64(q.ExecuteScalar());
    }

    private string EstadoDecision(long version)
    {
        using var q = _conexion.CreateCommand();
        q.CommandText = "SELECT tipo FROM decisiones_esquema_diccionario WHERE version_id=$v;";
        q.Parameters.AddWithValue("$v", version);
        return Convert.ToString(q.ExecuteScalar())!;
    }

    private long Modo(string proveedor, string nombre)
    {
        using var q = _conexion.CreateCommand();
        q.CommandText =
            "SELECT m.id FROM modos_esquema_diccionario m JOIN esquemas_diccionario e ON e.id=m.esquema_id WHERE e.proveedor=$p AND m.nombre=$n;";
        q.Parameters.AddWithValue("$p", proveedor);
        q.Parameters.AddWithValue("$n", nombre);
        return Convert.ToInt64(q.ExecuteScalar());
    }

    private static string BuscarConfiguracion()
    {
        for (
            DirectoryInfo? actual = new(AppContext.BaseDirectory);
            actual is not null;
            actual = actual.Parent
        )
        {
            string ruta = Path.Combine(actual.FullName, "configuracion", "cadenas-jcv.json");
            if (File.Exists(ruta))
                return ruta;
        }
        throw new FileNotFoundException("No se encontró configuracion/cadenas-jcv.json.");
    }
}
