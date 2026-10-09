using Archivero.Datos;
using Archivero.Servicios;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Archivero.Tests;

public sealed class C3ePuntaAPuntaTests
{
    [Fact]
    public async Task Publicacion_de_archivero_recorrre_cadena_alertas_y_decisiones()
    {
        string raiz = Path.Combine(
            Path.GetTempPath(),
            "ArchiveroTests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(raiz);
        string? datosAnterior = Environment.GetEnvironmentVariable("HORMIGUERO_DATOS");
        var fallasEnlace = new List<string>();
        void RegistrarFalla(long? _, string mensaje) => fallasEnlace.Add(mensaje);
        PublicadorDatosDocumentoService.RevisionEnlacesFallida += RegistrarFalla;
        Environment.SetEnvironmentVariable("HORMIGUERO_DATOS", Path.Combine(raiz, "datos"));
        try
        {
            var tipos = new Dictionary<string, (long Identificacion, string Nombre)>();
            var definiciones = new[]
            {
                (
                    "Empresa",
                    "nota_venta_propia",
                    "Emitido",
                    new[] { "nota_venta_propia", "oc_cliente", "nombre_cliente" }
                ),
                (
                    "Empresa",
                    "oc_propia",
                    "Emitido",
                    new[] { "oc_propia", "nota_venta_propia", "nombre_proveedor" }
                ),
                (
                    "PROVEEDOR UNO",
                    "guia_proveedor",
                    "Recibido",
                    new[] { "guia_proveedor", "oc_propia" }
                ),
                (
                    "PROVEEDOR UNO",
                    "factura_proveedor",
                    "Recibido",
                    new[] { "factura_proveedor", "guia_proveedor", "oc_propia" }
                ),
                (
                    "Empresa",
                    "factura_propia",
                    "Emitido",
                    new[] { "factura_propia", "nota_venta_propia" }
                ),
            };
            foreach (var (emisor, identificador, grupo, datos) in definiciones)
                tipos[identificador] = ConfigurarTipo(emisor, identificador, grupo, datos);

            long esquemaId;
            EsquemaCadena esquema;
            using (var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun))
            {
                var esquemas = new RepositorioEsquemas(conexion);
                esquemaId = esquemas.Guardar(
                    "PROVEEDOR UNO",
                    "Compra para venta",
                    [
                        new(
                            0,
                            tipos["nota_venta_propia"].Identificacion,
                            true,
                            "Nota de venta propia"
                        ),
                        new(1, tipos["oc_propia"].Identificacion, false, "OC propia"),
                        new(2, tipos["guia_proveedor"].Identificacion, false, "Guía del proveedor"),
                        new(
                            3,
                            tipos["factura_proveedor"].Identificacion,
                            false,
                            "Factura del proveedor"
                        ),
                        new(4, tipos["factura_propia"].Identificacion, false, "Factura propia"),
                    ],
                    [new(2, 3, "guia_proveedor")],
                    ["Retiro", "Despacho"],
                    lugarDecideModoOrden: 1
                );
                esquema = esquemas.ObtenerPorProveedor("PROVEEDOR UNO")!;
                esquemas.GuardarReglaAlerta(
                    esquemaId,
                    1,
                    "Retiro",
                    "falta_dato",
                    """{"dato":"Fecha de retiro","texto":"Falta fecha de retiro"}"""
                );
                esquemas.GuardarReglaAlerta(
                    esquemaId,
                    1,
                    "Retiro",
                    "plazo",
                    $$"""{"desdeDato":"Fecha de retiro","hastaLugarId":{{esquema.Lugares.Single(l => l.Orden == 2).Id}},"dias":0,"tipoDias":"corridos","texto":"Plazo para guía","porLinea":false}"""
                );
                esquemas.GuardarReglaAlerta(
                    esquemaId,
                    3,
                    null,
                    "plazo",
                    $$"""{"desdeLugarId":{{esquema.Lugares.Single(l => l.Orden == 3).Id}},"hastaLugarId":{{esquema.Lugares.Single(l => l.Orden == 4).Id}},"dias":7,"tipoDias":"corridos","texto":"Plazo para facturar al cliente","porLinea":true}"""
                );
                esquemas.GuardarReglaAlerta(
                    esquemaId,
                    2,
                    null,
                    "listo_para",
                    $$"""{"cuandoLugarId":{{esquema.Lugares.Single(l => l.Orden == 2).Id}},"condicionLugarId":{{esquema.Lugares.Single(l => l.Orden == 3).Id}},"hastaLugarId":{{esquema.Lugares.Single(l => l.Orden == 4).Id}},"lista":"Listos para facturar al cliente"}"""
                );
            }

            var cadenaRepo = AbrirRepoCadena(out var conexionCadena);
            try
            {
                long nvv = await Publicar(
                    "nvv.pdf",
                    "Empresa",
                    tipos["nota_venta_propia"].Nombre,
                    "Emitido",
                    ("nota_venta_propia", "NVV-100"),
                    ("oc_cliente", "OC-CLIENTE-9"),
                    ("nombre_cliente", "CLIENTE UNO")
                );
                var cadena = Assert.Single(cadenaRepo.ListarPorProveedor("PROVEEDOR UNO"));
                long cadenaId = cadena.Id;
                Assert.Equal("CLIENTE UNO", LeerCliente(conexionCadena, cadenaId));
                Assert.Single(cadenaRepo.ArbolPorLugares(cadenaId));

                await Publicar(
                    "oc.pdf",
                    "Empresa",
                    tipos["oc_propia"].Nombre,
                    "Emitido",
                    ("oc_propia", "OC-900"),
                    ("nota_venta_propia", "NVV-100"),
                    ("nombre_proveedor", "PROVEEDOR UNO")
                );
                Assert.Single(new RepositorioModosEsquema(conexionCadena).ListarPendientes());
                var lugarRepo = new EvaluadorAlertasEsquema(conexionCadena, () => DateTime.Today);
                long retiro = esquema.Modos.Single(m => m.Nombre == "Retiro").Id;
                new RepositorioModosEsquema(conexionCadena).FijarModo(cadenaId, retiro);
                lugarRepo.Evaluar(DateOnly.FromDateTime(DateTime.Today));
                var falta = Assert.Single(
                    lugarRepo.ListarAbiertas(cadenaId: cadenaId, tipo: "falta_dato")
                );
                Assert.Equal("normal", falta.Urgencia);
                new RepositorioDatosCadena(conexionCadena).GuardarFecha(
                    cadenaId,
                    "Fecha de retiro",
                    DateOnly.FromDateTime(DateTime.Today)
                );
                lugarRepo.Evaluar(DateOnly.FromDateTime(DateTime.Today));
                Assert.Empty(lugarRepo.ListarAbiertas(cadenaId: cadenaId, tipo: "falta_dato"));
                Assert.Equal(
                    ("cerrada", "normal"),
                    LeerEstadoAlerta(conexionCadena, cadenaId, "Falta fecha de retiro", "cadena")
                );
                var plazoRetiro = Assert.Single(
                    lugarRepo.ListarAbiertas(cadenaId: cadenaId, tipo: "plazo")
                );
                Assert.Equal("por_vencer", plazoRetiro.Urgencia);

                await Publicar(
                    "guia-1.pdf",
                    "PROVEEDOR UNO",
                    tipos["guia_proveedor"].Nombre,
                    "Recibido",
                    ("guia_proveedor", "G-1"),
                    ("oc_propia", "OC-900")
                );
                await Publicar(
                    "guia-2.pdf",
                    "PROVEEDOR UNO",
                    tipos["guia_proveedor"].Nombre,
                    "Recibido",
                    ("guia_proveedor", "G-2"),
                    ("oc_propia", "OC-900")
                );
                Assert.DoesNotContain(
                    lugarRepo.ListarAbiertas(cadenaId: cadenaId, tipo: "plazo"),
                    a => a.Texto == "Plazo para guía"
                );
                Assert.Equal(
                    ("cerrada", "por_vencer"),
                    LeerEstadoAlerta(conexionCadena, cadenaId, "Plazo para guía", "cadena")
                );

                await Publicar(
                    "factura-1.pdf",
                    "PROVEEDOR UNO",
                    tipos["factura_proveedor"].Nombre,
                    "Recibido",
                    ("factura_proveedor", "F-1"),
                    ("guia_proveedor", "G-1"),
                    ("oc_propia", "OC-900")
                );
                await Publicar(
                    "factura-2.pdf",
                    "PROVEEDOR UNO",
                    tipos["factura_proveedor"].Nombre,
                    "Recibido",
                    ("factura_proveedor", "F-2"),
                    ("guia_proveedor", "G-2"),
                    ("oc_propia", "OC-900")
                );
                var arbol = cadenaRepo.ArbolPorLugares(cadenaId);
                var lineas = arbol
                    .Where(d => d.Lugar is "Guía del proveedor" or "Factura del proveedor")
                    .GroupBy(d => d.LineaId)
                    .ToArray();
                Assert.Equal(2, lineas.Length);
                Assert.All(lineas, l => Assert.Equal(2, l.Count()));
                var avisosLinea = lugarRepo
                    .ListarAbiertas(cadenaId: cadenaId, tipo: "plazo")
                    .Where(a => a.Texto == "Plazo para facturar al cliente")
                    .ToArray();
                Assert.Equal(2, avisosLinea.Length);
                Assert.All(
                    avisosLinea,
                    a =>
                    {
                        Assert.StartsWith("linea:", a.Alcance);
                        Assert.Equal("normal", a.Urgencia);
                    }
                );
                var listos = lugarRepo.ListarListos("Listos para facturar al cliente");
                Assert.Equal(2, listos.Count);
                Assert.All(listos, l => Assert.StartsWith("linea:", l.Alcance));

                await Publicar(
                    "factura-propia.pdf",
                    "Empresa",
                    tipos["factura_propia"].Nombre,
                    "Emitido",
                    ("factura_propia", "FCV-77"),
                    ("nota_venta_propia", "NVV-100")
                );
                Assert.Empty(lugarRepo.ListarListos("Listos para facturar al cliente"));
                Assert.DoesNotContain(
                    lugarRepo.ListarAbiertas(cadenaId: cadenaId, tipo: "plazo"),
                    a => a.Texto == "Plazo para facturar al cliente"
                );
                var plazosCerrados = LeerEstadosAlertas(
                    conexionCadena,
                    cadenaId,
                    "Plazo para facturar al cliente"
                );
                Assert.Equal(2, plazosCerrados.Count);
                Assert.All(
                    plazosCerrados,
                    alerta =>
                    {
                        Assert.StartsWith("linea:", alerta.Alcance);
                        Assert.Equal("cerrada", alerta.Estado);
                        Assert.Equal("normal", alerta.Urgencia);
                    }
                );

                long dudoso = await Publicar(
                    "guia-limpia.pdf",
                    "PROVEEDOR UNO",
                    tipos["guia_proveedor"].Nombre,
                    "Recibido",
                    ("guia_proveedor", "G-DUDOSA"),
                    ("oc_propia", "900")
                );
                Assert.True(EnlaceDudoso(conexionCadena, dudoso));

                long sinProveedor = await Publicar(
                    "compra-sin-proveedor.pdf",
                    "Empresa",
                    tipos["oc_propia"].Nombre,
                    "Emitido",
                    ("oc_propia", "OC-SIN-PROVEEDOR"),
                    ("nota_venta_propia", "NVV-SIN-PROVEEDOR")
                );
                Assert.DoesNotContain(
                    cadenaRepo.ListarPorProveedor("PROVEEDOR UNO"),
                    c => cadenaRepo.ArbolPorLugares(c.Id).Any(d => d.VersionId == sinProveedor)
                );

                long sinPiso = await Publicar(
                    "sin-piso.pdf",
                    "PROVEEDOR UNO",
                    tipos["factura_proveedor"].Nombre,
                    "Recibido",
                    ("factura_proveedor", "F-SIN-PISO"),
                    ("guia_proveedor", "G-SIN-PISO"),
                    ("oc_propia", "OC-777")
                );
                Assert.Contains(
                    cadenaRepo.ListarDecisionesPendientes(),
                    d => d.VersionId == sinPiso && d.Tipo == tipos["factura_proveedor"].Nombre
                );
                long origenTardio = await Publicar(
                    "origen-tardio.pdf",
                    "Empresa",
                    tipos["nota_venta_propia"].Nombre,
                    "Emitido",
                    ("nota_venta_propia", "NVV-777"),
                    ("oc_cliente", "OC-CLIENTE-777"),
                    ("nombre_cliente", "CLIENTE DOS")
                );
                long compraTardia = await Publicar(
                    "origen-compra-tardia.pdf",
                    "Empresa",
                    tipos["oc_propia"].Nombre,
                    "Emitido",
                    ("oc_propia", "OC-777"),
                    ("nota_venta_propia", "NVV-777"),
                    ("nombre_proveedor", "PROVEEDOR UNO")
                );
                Assert.Contains(
                    cadenaRepo.ListarPorProveedor("PROVEEDOR UNO"),
                    c => cadenaRepo.ArbolPorLugares(c.Id).Any(d => d.VersionId == origenTardio)
                );
                Assert.Contains(
                    cadenaRepo.ListarPorProveedor("PROVEEDOR UNO"),
                    c => cadenaRepo.ArbolPorLugares(c.Id).Any(d => d.VersionId == compraTardia)
                );
                Assert.Equal("activo", LeerEstadoEnlace(conexionCadena, compraTardia));
                Assert.True(ValorPublicadoExiste(conexionCadena, compraTardia, "oc_propia", "777"));
                Assert.True(ValorPublicadoExiste(conexionCadena, sinPiso, "oc_propia", "777"));
                Assert.Empty(fallasEnlace);
                Assert.DoesNotContain(
                    cadenaRepo.ListarDecisionesPendientes(),
                    d => d.VersionId == sinPiso
                );
                Assert.Contains(
                    cadenaRepo.ListarPorProveedor("PROVEEDOR UNO"),
                    c => cadenaRepo.ArbolPorLugares(c.Id).Any(d => d.VersionId == sinPiso)
                );
            }
            finally
            {
                conexionCadena.Dispose();
            }
        }
        finally
        {
            PublicadorDatosDocumentoService.RevisionEnlacesFallida -= RegistrarFalla;
            Environment.SetEnvironmentVariable("HORMIGUERO_DATOS", datosAnterior);
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(raiz))
                Directory.Delete(raiz, recursive: true);
        }
    }

    private static (long Identificacion, string Nombre) ConfigurarTipo(
        string emisor,
        string identificador,
        string grupo,
        IReadOnlyList<string> datos
    )
    {
        const int diseno = 1;
        var diccionario = DiccionarioDatosEnlazantes.Todos.ToDictionary(d => d.Id);
        var configurados = datos
            .Select(
                (id, indice) =>
                {
                    var dato = diccionario[id];
                    return new DatoEnlazanteConfigurado(
                        id,
                        dato.Nombre,
                        dato.Grupo,
                        true,
                        true,
                        0,
                        indice * 0.1,
                        0,
                        0.08,
                        0.05,
                        "",
                        true,
                        id == identificador
                    );
                }
            )
            .ToArray();
        string tipo = diccionario[identificador].EtiquetaTipo;
        string nombre = DiccionarioDatosEnlazantes.NombreEstandar(identificador, emisor);
        DatosEnlazantesConfiguracionService.Guardar(
            emisor,
            tipo,
            grupo,
            nombre,
            diseno,
            configurados
        );
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        var identificacion = new Identificaciones(conexion)
            .Listar()
            .Single(i => i.Emisor == emisor && i.Tipo == tipo);
        return (identificacion.Id, tipo);
    }

    private static async Task<long> Publicar(
        string nombre,
        string emisor,
        string tipo,
        string grupo,
        params (string Dato, string Valor)[] valores
    )
    {
        string raizDatos = Environment.GetEnvironmentVariable("HORMIGUERO_DATOS")!;
        string ruta = Path.Combine(
            Path.GetDirectoryName(raizDatos)!,
            "documentos",
            Guid.NewGuid().ToString("N"),
            nombre
        );
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta, nombre);
        var configuracion = new ConfiguracionDocumento
        {
            Emisor = emisor,
            Tipo = tipo,
            CarpetaDestino = Path.GetDirectoryName(ruta)!,
            FormatoCarpeta = FormatoCarpeta.Directo,
            Renombrar = false,
            GrupoDocumento = grupo,
            NombreEstandar = DiccionarioDatosEnlazantes.NombreEstandar(
                DiccionarioDatosEnlazantes.Todos.First(d => d.EtiquetaTipo == tipo).Id,
                emisor
            ),
            Patrones = [new(1, [])],
        };
        var leidos = valores
            .Select(v => new ValorDocumentoLeido(
                DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == v.Dato).Nombre,
                v.Dato,
                v.Valor,
                DiccionarioDatosEnlazantes.ClaveDeEnlace(v.Dato, v.Valor),
                "observador",
                DatoDiccionarioId: v.Dato
            ))
            .ToArray();
        PublicadorDatosDocumentoService.Publicar(ruta, configuracion, leidos, "observador");
        await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id FROM versiones_documento WHERE ruta_observada=$r ORDER BY id DESC LIMIT 1;";
        comando.Parameters.AddWithValue("$r", ruta);
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private static RepositorioCadenas AbrirRepoCadena(out SqliteConnection conexion)
    {
        conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        return new RepositorioCadenas(conexion);
    }

    private static string LeerCliente(SqliteConnection conexion, long cadenaId)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT cliente FROM cadenas WHERE id=$c;";
        comando.Parameters.AddWithValue("$c", cadenaId);
        return Convert.ToString(comando.ExecuteScalar())!;
    }

    private static bool EnlaceDudoso(SqliteConnection conexion, long versionId)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT EXISTS(SELECT 1 FROM enlaces_cadena WHERE version_id=$v AND estado='dudoso');";
        comando.Parameters.AddWithValue("$v", versionId);
        return Convert.ToInt64(comando.ExecuteScalar()) == 1;
    }

    private static bool ValorPublicadoExiste(
        SqliteConnection conexion,
        long versionId,
        string dato,
        string clave
    )
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT EXISTS(SELECT 1 FROM valores_documento WHERE version_id=$v AND dato_diccionario_id=$d AND valor_clave=$k AND estado='vigente');";
        comando.Parameters.AddWithValue("$v", versionId);
        comando.Parameters.AddWithValue("$d", dato);
        comando.Parameters.AddWithValue("$k", clave);
        return Convert.ToInt64(comando.ExecuteScalar()) == 1;
    }

    private static string? LeerEstadoEnlace(SqliteConnection conexion, long versionId)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT estado FROM enlaces_cadena WHERE version_id=$v ORDER BY id DESC LIMIT 1;";
        comando.Parameters.AddWithValue("$v", versionId);
        return comando.ExecuteScalar() as string;
    }

    private static (string Estado, string Urgencia) LeerEstadoAlerta(
        SqliteConnection conexion,
        long cadenaId,
        string texto,
        string alcance
    )
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT estado,urgencia FROM alertas_esquema WHERE cadena_id=$c AND texto=$t AND alcance=$a;";
        comando.Parameters.AddWithValue("$c", cadenaId);
        comando.Parameters.AddWithValue("$t", texto);
        comando.Parameters.AddWithValue("$a", alcance);
        using var lector = comando.ExecuteReader();
        Assert.True(lector.Read());
        return (lector.GetString(0), lector.GetString(1));
    }

    private static IReadOnlyList<(
        string Alcance,
        string Estado,
        string Urgencia
    )> LeerEstadosAlertas(SqliteConnection conexion, long cadenaId, string texto)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT alcance,estado,urgencia FROM alertas_esquema WHERE cadena_id=$c AND texto=$t ORDER BY alcance;";
        comando.Parameters.AddWithValue("$c", cadenaId);
        comando.Parameters.AddWithValue("$t", texto);
        using var lector = comando.ExecuteReader();
        var estados = new List<(string, string, string)>();
        while (lector.Read())
            estados.Add((lector.GetString(0), lector.GetString(1), lector.GetString(2)));
        return estados;
    }
}
