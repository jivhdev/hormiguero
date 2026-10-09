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
    public void Inicio_ubica_documentos_y_empareja_guia_con_factura_una_a_una()
    {
        CrearEsquema();
        long nvv = CrearVersion(
            "nvv.pdf",
            "NVV",
            "Emitido",
            ("rut_proveedor", "ACME", true),
            ("nota_venta_propia", "451", true),
            ("nombre_cliente", "Cliente Uno", true)
        );
        var motor = new MotorCadenas(_conexion);
        var inicio = motor.ProcesarVersion(nvv);
        Assert.Equal("cadena_creada", inicio.Estado);
        long cadena = inicio.CadenaId!.Value;
        Assert.Single(new RepositorioCadenas(_conexion).ListarPorProveedor("ACME"));
        Assert.Equal("Cliente Uno", LeerCliente(cadena));

        long occ = CrearVersion(
            "occ.pdf",
            "OCC",
            "Emitido",
            ("rut_proveedor", "ACME", true),
            ("nota_venta_propia", "451", true),
            ("oc_propia", "900", true)
        );
        Assert.Equal("ubicado", motor.ProcesarVersion(occ).Estado);

        long guia1 = CrearVersion(
            "guia1.pdf",
            "Guía",
            "Recibido",
            ("guia_proveedor", "G-1", true),
            ("oc_propia", "900", true)
        );
        long guia2 = CrearVersion(
            "guia2.pdf",
            "Guía",
            "Recibido",
            ("guia_proveedor", "G-2", true),
            ("oc_propia", "900", true)
        );
        Assert.Equal("ubicado", motor.ProcesarVersion(guia1).Estado);
        Assert.Equal("ubicado", motor.ProcesarVersion(guia2).Estado);
        long factura1 = CrearVersion(
            "factura1.pdf",
            "Factura",
            "Recibido",
            ("factura_proveedor", "F-1", true),
            ("oc_propia", "900", true),
            ("guia_proveedor", "G-1", true)
        );
        long factura2 = CrearVersion(
            "factura2.pdf",
            "Factura",
            "Recibido",
            ("factura_proveedor", "F-2", true),
            ("oc_propia", "900", true),
            ("guia_proveedor", "G-2", true)
        );
        Assert.Equal("ubicado", motor.ProcesarVersion(factura1).Estado);
        Assert.Equal("ubicado", motor.ProcesarVersion(factura2).Estado);
        var arbol = new RepositorioCadenas(_conexion).ArbolPorLugares(cadena);
        var pares = arbol
            .Where(d => d.Lugar is "Guía" or "Factura")
            .GroupBy(d => d.LineaId)
            .ToArray();
        Assert.Equal(2, pares.Length);
        Assert.All(pares, par => Assert.Equal(2, par.Count()));
    }

    [Fact]
    public void Coincidencia_solo_limpia_es_dudosa_y_tipo_sin_piso_se_registra()
    {
        CrearEsquema("P-1");
        long inicio = CrearVersion(
            "inicio.pdf",
            "NVV",
            "Emitido",
            ("rut_proveedor", "P-1", true),
            ("nota_venta_propia", "123-45", true)
        );
        var motor = new MotorCadenas(_conexion);
        motor.ProcesarVersion(inicio);
        long limpio = CrearVersion(
            "limpio.pdf",
            "Documento P1",
            "Recibido",
            ("nota_venta_propia", "12345", true),
            ("factura_proveedor", "F-P1-1", true)
        );
        Assert.Equal("dudoso", motor.ProcesarVersion(limpio).Estado);
        long sinPiso = CrearVersion(
            "sinpiso.pdf",
            "Documento P1",
            "Recibido",
            ("oc_propia", "888", true),
            ("factura_proveedor", "F-P1-2", true)
        );
        Assert.Equal("sin_piso", motor.ProcesarVersion(sinPiso).Estado);
        using var cmd = _conexion.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM decisiones_pendientes_cadena WHERE version_id=$v AND proveedor='P-1' AND numeros_json LIKE '%888%';";
        cmd.Parameters.AddWithValue("$v", sinPiso);
        Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));
    }

    [Fact]
    public void Decision_sin_piso_se_puede_crear_como_cadena_igual()
    {
        CrearEsquema("P-1");
        long version = CrearVersion(
            "igual.pdf",
            "Documento P1",
            "Recibido",
            ("rut_proveedor", "P-1", true),
            ("oc_propia", "20417", true)
        );
        var motor = new MotorCadenas(_conexion);
        Assert.Equal("sin_piso", motor.ProcesarVersion(version).Estado);
        var decision = Assert.Single(
            new RepositorioCadenas(_conexion).ListarDecisionesPendientes()
        );
        Assert.Equal("20417", decision.NumeroMencionado);
        Assert.Equal("Documento P1", decision.Tipo);

        var resultado = motor.CrearCadenaIgual(version);
        Assert.Equal("cadena_creada", resultado.Estado);
        Assert.Empty(new RepositorioCadenas(_conexion).ListarDecisionesPendientes());
        Assert.Single(new RepositorioCadenas(_conexion).ArbolPorLugares(resultado.CadenaId!.Value));
    }

    [Fact]
    public void Solo_archivar_resuelve_decision_sin_crear_cadena()
    {
        CrearEsquema("P-1");
        long version = CrearVersion(
            "archivar.pdf",
            "Documento P1",
            "Recibido",
            ("rut_proveedor", "P-1", true),
            ("oc_propia", "20418", true)
        );
        var motor = new MotorCadenas(_conexion);
        Assert.Equal("sin_piso", motor.ProcesarVersion(version).Estado);

        Assert.True(motor.ArchivarSinCadena(version));
        Assert.Empty(new RepositorioCadenas(_conexion).ListarDecisionesPendientes());
        Assert.Empty(new RepositorioCadenas(_conexion).ListarPorProveedor("P-1"));
    }

    [Fact]
    public void Al_llegar_origen_reprocesa_version_pendiente_y_cierra_decision()
    {
        CrearEsquema("P-1");
        long pendiente = CrearVersion(
            "pendiente.pdf",
            "Documento P1",
            "Recibido",
            ("rut_proveedor", "P-1", true),
            ("oc_propia", "20419", true)
        );
        var motor = new MotorCadenas(_conexion);
        Assert.Equal("sin_piso", motor.ProcesarVersion(pendiente).Estado);

        long origen = CrearVersion(
            "origen.pdf",
            "NVV",
            "Emitido",
            ("rut_proveedor", "P-1", true),
            ("nota_venta_propia", "NV-20419", true),
            ("oc_propia", "20419", true)
        );
        var resultado = motor.ProcesarVersion(origen);

        Assert.Equal("cadena_creada", resultado.Estado);
        Assert.Empty(new RepositorioCadenas(_conexion).ListarDecisionesPendientes());
        Assert.Contains(
            new RepositorioCadenas(_conexion).ArbolPorLugares(resultado.CadenaId!.Value),
            documento => documento.VersionId == pendiente
        );
    }

    [Fact]
    public void Proveedor_sin_esquema_no_crea_cadena_y_hay_un_esquema_por_proveedor()
    {
        long version = CrearVersion(
            "sin-esquema.pdf",
            "OCC",
            "Emitido",
            ("rut_proveedor", "P-2", true)
        );
        Assert.Equal("sin_esquema", new MotorCadenas(_conexion).ProcesarVersion(version).Estado);
        CrearEsquema("P-2");
        CrearEsquema("p-2");
        Assert.Single(new RepositorioEsquemas(_conexion).Listar(), e => e.Proveedor == "P-2");
    }

    [Fact]
    public void Calce_solo_limpio_hacia_cadena_que_tambien_calza_exacto_no_genera_duda()
    {
        CrearEsquema("P-3");
        long inicio = CrearVersion(
            "inicio-p3.pdf",
            "NVV",
            "Emitido",
            ("rut_proveedor", "P-3", true),
            ("nota_venta_propia", "123-45", true),
            ("oc_propia", "900", true)
        );
        var motor = new MotorCadenas(_conexion);
        motor.ProcesarVersion(inicio);
        long nuevo = CrearVersion(
            "nuevo-p3.pdf",
            "OCC",
            "Emitido",
            ("rut_proveedor", "P-3", true),
            ("nota_venta_propia", "12345", true),
            ("oc_propia", "900", true)
        );

        Assert.Equal("ubicado", motor.ProcesarVersion(nuevo).Estado);
        Assert.Empty(new RepositorioReglasYEnlaces(_conexion).ListarDudososCadenasSimples());
    }

    [Fact]
    public void Lugar_que_decide_modo_crea_decision_y_fijar_modo_guarda_historial()
    {
        var esquemas = new RepositorioEsquemas(_conexion);
        long esquemaId = esquemas.Guardar(
            "P-MODO",
            "Proceso",
            [new(0, _identificaciones["NVV"], true, "Inicio")],
            modos: ["Retiro", "Despacho"],
            lugarDecideModoOrden: 0
        );
        long version = CrearVersion(
            "modo.pdf",
            "NVV",
            "Emitido",
            ("rut_proveedor", "P-MODO", true),
            ("nota_venta_propia", "77", true)
        );
        var resultado = new MotorCadenas(_conexion).ProcesarVersion(version);
        var decision = Assert.Single(new RepositorioModosEsquema(_conexion).ListarPendientes());
        Assert.Equal(resultado.CadenaId, decision.CadenaId);
        Assert.Equal(new[] { "Despacho", "Retiro" }, decision.Opciones.OrderBy(nombre => nombre));

        long modoId = esquemas
            .ObtenerPorProveedor("P-MODO")!
            .Modos.Single(m => m.Nombre == "Retiro")
            .Id;
        var modos = new RepositorioModosEsquema(_conexion);
        modos.FijarModo(resultado.CadenaId!.Value, modoId);
        Assert.Empty(modos.ListarPendientes());
        Assert.Contains("Retiro", Assert.Single(modos.ListarHistorial(resultado.CadenaId.Value)));
    }

    [Fact]
    public void Alerta_falta_dato_se_abre_se_cierra_al_ingresarlo_y_se_actualiza_sin_duplicar()
    {
        var esquemas = new RepositorioEsquemas(_conexion);
        long esquemaId = esquemas.Guardar(
            "P-ALERTA",
            "Proceso",
            [new(0, _identificaciones["NVV"], true, "Inicio")]
        );
        long version = CrearVersion(
            "alerta.pdf",
            "NVV",
            "Emitido",
            ("rut_proveedor", "P-ALERTA", true),
            ("nota_venta_propia", "88", true)
        );
        long cadena = new MotorCadenas(_conexion).ProcesarVersion(version).CadenaId!.Value;
        esquemas.GuardarReglaAlerta(
            esquemaId,
            0,
            null,
            "falta_dato",
            "{\"dato\":\"Fecha de retiro\",\"texto\":\"Ingresar fecha de retiro\"}"
        );
        var evaluador = new EvaluadorAlertasEsquema(_conexion, () => new DateTime(2026, 10, 8));
        evaluador.Evaluar(new DateOnly(2026, 10, 8));
        evaluador.Evaluar(new DateOnly(2026, 10, 8));
        Assert.Equal("normal", Assert.Single(evaluador.ListarAbiertas(cadenaId: cadena)).Urgencia);

        Assert.Throws<ArgumentException>(() =>
            new RepositorioDatosCadena(_conexion).Guardar(cadena, "Dato no configurado", "valor")
        );
        new RepositorioDatosCadena(_conexion).GuardarFecha(
            cadena,
            "Fecha de retiro",
            new DateOnly(2026, 10, 8)
        );
        Assert.Empty(evaluador.ListarAbiertas(cadenaId: cadena));
    }

    [Fact]
    public void Plazo_usa_fecha_de_emision_guarda_vencimiento_urgencia_y_se_cierra_al_llegar_destino()
    {
        var esquemas = new RepositorioEsquemas(_conexion);
        long esquemaId = esquemas.Guardar(
            "P-PLAZO",
            "Proceso",
            [
                new(0, _identificaciones["NVV"], true, "Inicio"),
                new(1, _identificaciones["OCC"], false, "Compra"),
            ]
        );
        long version = CrearVersion(
            "plazo.pdf",
            "NVV",
            "Emitido",
            ("rut_proveedor", "P-PLAZO", true),
            ("nota_venta_propia", "99", true)
        );
        using (var fecha = _conexion.CreateCommand())
        {
            fecha.CommandText =
                "INSERT INTO valores_informativos_documento(version_id,dato,valor,fecha_reconocida,creada_en) VALUES($v,'fecha_documento','2026-10-01','2026-10-01','2026-10-01');";
            fecha.Parameters.AddWithValue("$v", version);
            fecha.ExecuteNonQuery();
        }
        long cadena = new MotorCadenas(_conexion).ProcesarVersion(version).CadenaId!.Value;
        EsquemaCadena esquema = esquemas.ObtenerPorProveedor("P-PLAZO")!;
        long origen = esquema.Lugares.Single(l => l.Orden == 0).Id;
        long destino = esquema.Lugares.Single(l => l.Orden == 1).Id;
        esquemas.GuardarReglaAlerta(
            esquemaId,
            0,
            null,
            "plazo",
            $$"""{"desdeLugarId":{{origen}},"hastaLugarId":{{destino}},"dias":7,"tipoDias":"corridos","texto":"Falta la compra","porLinea":false}"""
        );
        var evaluador = new EvaluadorAlertasEsquema(_conexion, () => new DateTime(2026, 10, 9));
        evaluador.Evaluar(new DateOnly(2026, 10, 9));
        var abierta = Assert.Single(evaluador.ListarAbiertas(cadenaId: cadena));
        Assert.Equal(new DateOnly(2026, 10, 8), abierta.FechaVencimiento);
        Assert.Equal("vencido", abierta.Urgencia);

        long compra = CrearVersion(
            "compra.pdf",
            "OCC",
            "Emitido",
            ("rut_proveedor", "P-PLAZO", true),
            ("nota_venta_propia", "99", true),
            ("oc_propia", "100", true)
        );
        Assert.Equal("ubicado", new MotorCadenas(_conexion).ProcesarVersion(compra).Estado);
        evaluador.Evaluar(new DateOnly(2026, 10, 9));
        Assert.Empty(evaluador.ListarAbiertas(cadenaId: cadena));
    }

    [Fact]
    public void Regla_alerta_rechaza_tipo_y_parametros_incompletos()
    {
        long esquemaId = new RepositorioEsquemas(_conexion).Guardar(
            "P-INVALIDA",
            "Proceso",
            [new(0, _identificaciones["NVV"], true, "Inicio")]
        );
        var repo = new RepositorioEsquemas(_conexion);
        Assert.Throws<ArgumentException>(() =>
            repo.GuardarReglaAlerta(esquemaId, 0, null, "otro", "{}")
        );
        Assert.Throws<ArgumentException>(() =>
            repo.GuardarReglaAlerta(esquemaId, 0, null, "falta_dato", "{}")
        );
    }

    [Fact]
    public void Listo_para_aparece_en_lista_y_se_cierra_al_llegar_destino()
    {
        var esquemas = new RepositorioEsquemas(_conexion);
        long esquemaId = esquemas.Guardar(
            "P-LISTO",
            "Proceso",
            [
                new(0, _identificaciones["NVV"], true, "Inicio"),
                new(1, _identificaciones["OCC"], false, "Compra"),
            ]
        );
        long inicio = CrearVersion(
            "listo.pdf",
            "NVV",
            "Emitido",
            ("rut_proveedor", "P-LISTO", true),
            ("nota_venta_propia", "101", true)
        );
        long cadena = new MotorCadenas(_conexion).ProcesarVersion(inicio).CadenaId!.Value;
        var esquema = esquemas.ObtenerPorProveedor("P-LISTO")!;
        long origen = esquema.Lugares.Single(l => l.Orden == 0).Id;
        long destino = esquema.Lugares.Single(l => l.Orden == 1).Id;
        esquemas.GuardarReglaAlerta(
            esquemaId,
            0,
            null,
            "listo_para",
            $$"""{"cuandoLugarId":{{origen}},"hastaLugarId":{{destino}},"lista":"Listos para facturar"}"""
        );
        var evaluador = new EvaluadorAlertasEsquema(_conexion);
        evaluador.Evaluar();
        Assert.Single(evaluador.ListarListos("Listos para facturar"));
        long compra = CrearVersion(
            "listo-compra.pdf",
            "OCC",
            "Emitido",
            ("rut_proveedor", "P-LISTO", true),
            ("nota_venta_propia", "101", true)
        );
        Assert.Equal("ubicado", new MotorCadenas(_conexion).ProcesarVersion(compra).Estado);
        evaluador.Evaluar();
        Assert.Empty(evaluador.ListarListos("Listos para facturar"));
        Assert.Empty(evaluador.ListarAbiertas(cadenaId: cadena));
    }

    [Fact]
    public void Regla_limitada_a_modo_no_se_aplica_a_los_demas_modos()
    {
        var esquemas = new RepositorioEsquemas(_conexion);
        long esquemaId = esquemas.Guardar(
            "P-MODO-REGLA",
            "Proceso",
            [new(0, _identificaciones["NVV"], true, "Inicio")],
            modos: ["Retiro", "Despacho"],
            lugarDecideModoOrden: 0
        );
        var esquema = esquemas.ObtenerPorProveedor("P-MODO-REGLA")!;
        long retiro = esquema.Modos.Single(m => m.Nombre == "Retiro").Id;
        long despacho = esquema.Modos.Single(m => m.Nombre == "Despacho").Id;
        long versionRetiro = CrearVersion(
            "modo-retiro.pdf",
            "NVV",
            "Emitido",
            ("rut_proveedor", "P-MODO-REGLA", false),
            ("nota_venta_propia", "201", true)
        );
        long cadenaRetiro = new MotorCadenas(_conexion)
            .ProcesarVersion(versionRetiro)
            .CadenaId!.Value;
        long versionDespacho = CrearVersion(
            "modo-despacho.pdf",
            "NVV",
            "Emitido",
            ("rut_proveedor", "P-MODO-REGLA", false),
            ("nota_venta_propia", "202", true)
        );
        long cadenaDespacho = new MotorCadenas(_conexion)
            .ProcesarVersion(versionDespacho)
            .CadenaId!.Value;
        var modos = new RepositorioModosEsquema(_conexion);
        modos.FijarModo(cadenaRetiro, retiro);
        modos.FijarModo(cadenaDespacho, despacho);
        using (var consulta = _conexion.CreateCommand())
        {
            consulta.CommandText =
                "SELECT COUNT(*) FROM cadenas WHERE esquema_id=$e AND modo_id IN ($r,$d);";
            consulta.Parameters.AddWithValue("$e", esquemaId);
            consulta.Parameters.AddWithValue("$r", retiro);
            consulta.Parameters.AddWithValue("$d", despacho);
            Assert.Equal(2L, Convert.ToInt64(consulta.ExecuteScalar()));
        }
        using (var consulta = _conexion.CreateCommand())
        {
            consulta.CommandText = "SELECT modo_id FROM cadenas WHERE id=$c;";
            consulta.Parameters.AddWithValue("$c", cadenaRetiro);
            Assert.Equal(retiro, Convert.ToInt64(consulta.ExecuteScalar()));
        }
        esquemas.GuardarReglaAlerta(
            esquemaId,
            0,
            "Retiro",
            "falta_dato",
            "{\"dato\":\"Fecha de retiro\",\"texto\":\"Ingresar fecha de retiro\"}"
        );
        var alertas = new EvaluadorAlertasEsquema(_conexion);
        Assert.Equal(
            retiro,
            Assert.Single(esquemas.ObtenerPorProveedor("P-MODO-REGLA")!.ReglasAlerta).ModoId
        );
        alertas.Evaluar();
        Assert.Equal(
            cadenaRetiro,
            Assert.Single(alertas.ListarAbiertas(tipo: "falta_dato")).CadenaId
        );
    }

    [Fact]
    public void Plazo_desde_dato_usa_feriado_y_espera_condicion()
    {
        var esquemas = new RepositorioEsquemas(_conexion);
        long esquemaId = esquemas.Guardar(
            "P-FERIADO",
            "Proceso",
            [
                new(0, _identificaciones["NVV"], true, "Inicio"),
                new(1, _identificaciones["OCC"], false, "Condición"),
                new(2, _identificaciones["Guía"], false, "Destino"),
            ]
        );
        long inicio = CrearVersion(
            "feriado.pdf",
            "NVV",
            "Emitido",
            ("rut_proveedor", "P-FERIADO", true),
            ("nota_venta_propia", "301", true)
        );
        long cadena = new MotorCadenas(_conexion).ProcesarVersion(inicio).CadenaId!.Value;
        var esquema = esquemas.ObtenerPorProveedor("P-FERIADO")!;
        long condicion = esquema.Lugares.Single(l => l.Orden == 1).Id;
        long destino = esquema.Lugares.Single(l => l.Orden == 2).Id;
        var calendarioRepo = new RepositorioCalendariosFeriados(_conexion);
        long calendario = calendarioRepo.CrearCalendario("Feriados de prueba", "CL");
        calendarioRepo.AgregarFeriado(calendario, new DateOnly(2026, 10, 5), "Feriado de prueba");
        esquemas.GuardarReglaAlerta(
            esquemaId,
            0,
            null,
            "plazo",
            $$"""{"desdeDato":"Fecha de retiro","hastaLugarId":{{destino}},"dias":1,"tipoDias":"habiles","calendarioId":{{calendario}},"condicionLugarId":{{condicion}},"texto":"Falta guía","porLinea":false}"""
        );
        new RepositorioDatosCadena(_conexion).GuardarFecha(
            cadena,
            "Fecha de retiro",
            new DateOnly(2026, 10, 2)
        );
        var evaluador = new EvaluadorAlertasEsquema(_conexion, () => new DateTime(2026, 10, 5));
        evaluador.Evaluar(new DateOnly(2026, 10, 5));
        Assert.Empty(evaluador.ListarAbiertas(cadenaId: cadena));
        long compra = CrearVersion(
            "feriado-compra.pdf",
            "OCC",
            "Emitido",
            ("rut_proveedor", "P-FERIADO", true),
            ("nota_venta_propia", "301", true)
        );
        Assert.Equal("ubicado", new MotorCadenas(_conexion).ProcesarVersion(compra).Estado);
        evaluador.Evaluar(new DateOnly(2026, 10, 5));
        var alerta = Assert.Single(evaluador.ListarAbiertas(cadenaId: cadena));
        Assert.Equal(new DateOnly(2026, 10, 6), alerta.FechaVencimiento);
        Assert.Equal("por_vencer", alerta.Urgencia);
    }

    [Fact]
    public void Plazo_por_linea_deja_aviso_solo_en_la_guia_sin_factura()
    {
        var esquemas = new RepositorioEsquemas(_conexion);
        long esquemaId = esquemas.Guardar(
            "P-LINEA",
            "Proceso",
            [
                new(0, _identificaciones["NVV"], true, "Inicio"),
                new(1, _identificaciones["OCC"], false, "Compra"),
                new(2, _identificaciones["Guía"], false, "Guía"),
                new(3, _identificaciones["Factura"], false, "Factura"),
            ],
            [new(2, 3, "guia_proveedor")]
        );
        using (var tipo = _conexion.CreateCommand())
        {
            tipo.CommandText =
                "UPDATE identificaciones SET emisor='P-LINEA' WHERE tipo IN ('Guía','Factura');";
            tipo.ExecuteNonQuery();
        }
        var motor = new MotorCadenas(_conexion);
        long inicio = CrearVersion(
            "linea-inicio.pdf",
            "NVV",
            "Emitido",
            ("rut_proveedor", "P-LINEA", true),
            ("nota_venta_propia", "401", true)
        );
        long cadena = motor.ProcesarVersion(inicio).CadenaId!.Value;
        long compra = CrearVersion(
            "linea-compra.pdf",
            "OCC",
            "Emitido",
            ("rut_proveedor", "P-LINEA", true),
            ("nota_venta_propia", "401", true),
            ("oc_propia", "402", true)
        );
        Assert.Equal("ubicado", motor.ProcesarVersion(compra).Estado);
        long guiaUno = CrearVersion(
            "linea-guia-1.pdf",
            "Guía",
            "Recibido",
            ("oc_propia", "402", true),
            ("guia_proveedor", "G-401", true)
        );
        long guiaDos = CrearVersion(
            "linea-guia-2.pdf",
            "Guía",
            "Recibido",
            ("oc_propia", "402", true),
            ("guia_proveedor", "G-402", true)
        );
        Assert.Equal("ubicado", motor.ProcesarVersion(guiaUno).Estado);
        Assert.Equal("ubicado", motor.ProcesarVersion(guiaDos).Estado);
        long factura = CrearVersion(
            "linea-factura.pdf",
            "Factura",
            "Recibido",
            ("oc_propia", "402", true),
            ("guia_proveedor", "G-401", true)
        );
        Assert.Equal("ubicado", motor.ProcesarVersion(factura).Estado);
        var esquema = esquemas.ObtenerPorProveedor("P-LINEA")!;
        long guia = esquema.Lugares.Single(l => l.Orden == 2).Id;
        long facturaLugar = esquema.Lugares.Single(l => l.Orden == 3).Id;
        esquemas.GuardarReglaAlerta(
            esquemaId,
            2,
            null,
            "plazo",
            $$"""{"desdeLugarId":{{guia}},"hastaLugarId":{{facturaLugar}},"dias":0,"tipoDias":"corridos","texto":"Falta factura","porLinea":true}"""
        );
        var evaluador = new EvaluadorAlertasEsquema(_conexion, () => DateTime.Now);
        evaluador.Evaluar(DateOnly.FromDateTime(DateTime.Now));
        Assert.StartsWith(
            "linea:",
            Assert.Single(evaluador.ListarAbiertas(cadenaId: cadena)).Alcance
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
                "INSERT INTO tipos_documento_datos(identificacion_id,dato_diccionario_id,diseno,pagina,x,y,ancho,alto,enlazable,creada_en,actualizada_en) VALUES($i,$d,'1',1,0,0,1,1,$e,'ahora','ahora') ON CONFLICT(identificacion_id,dato_diccionario_id,diseno) DO UPDATE SET enlazable=excluded.enlazable;";
            tipoDato.Parameters.AddWithValue("$i", identificacion);
            tipoDato.Parameters.AddWithValue("$d", valor.Dato);
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
