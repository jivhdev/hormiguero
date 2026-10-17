using Archivero.Datos;
using Archivero.Servicios;
using Hormiguero.Nucleo.Datos;

namespace Archivero.Tests;

public sealed class BusquedaEnCarpetaIntegracionTests
{
    [Fact]
    public void DatoDeEnlaceFaltante_indicaQueMarcarEnArchivero()
    {
        using var contexto = new ContextoTemporal();
        CrearConfiguracion(
            "COBELCAR",
            DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "guia_proveedor").EtiquetaTipo,
            Path.Combine(contexto.Raiz, "grc"),
            "guia_proveedor",
            ["nombre_proveedor"]
        );
        long guia = PublicarGuiaCobelcar(contexto.Raiz, incluirEnlace: false);
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        var resultado = new MotorCadenasDiccionario(conexion).ProcesarVersion(guia);
        Assert.Equal("sin_inicio", resultado.Estado);
        Assert.Equal(
            "El documento guía 3001 no tiene marcado el N° de OC propia. Complete su clasificación en Archivero.",
            Convert.ToString(
                Ejecutar(
                    conexion,
                    "SELECT motivo FROM decisiones_esquema_diccionario WHERE version_id=$v;",
                    ("$v", guia)
                )
            )
        );
    }

    [Fact]
    public void Cedible_no_se_agrega_a_ninguna_cadena_ni_a_dudosos()
    {
        using var contexto = new ContextoTemporal();
        long guia = PublicarDocumento(
            contexto.Raiz,
            "HOFFENS",
            "guia_proveedor",
            "guia_proveedor",
            "1195630",
            ("nombre_proveedor", "HOFFENS")
        );
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        using (var nombre = conexion.CreateCommand())
        {
            nombre.CommandText =
                "UPDATE documentos SET nombre='GRC-CEDIBLE-1195630.pdf' WHERE id=(SELECT documento_id FROM versiones_documento WHERE id=$v);";
            nombre.Parameters.AddWithValue("$v", guia);
            nombre.ExecuteNonQuery();
        }
        var resultado = new MotorCadenasDiccionario(conexion).ProcesarVersion(guia);
        Assert.Equal("cedible_ignorado", resultado.Estado);
        Assert.Equal(
            0L,
            Convert.ToInt64(
                Ejecutar(
                    conexion,
                    "SELECT COUNT(*) FROM ubicaciones_cadena_diccionario WHERE version_id=$v;",
                    ("$v", guia)
                )
            )
        );
        Assert.Equal(
            0L,
            Convert.ToInt64(
                Ejecutar(
                    conexion,
                    "SELECT COUNT(*) FROM decisiones_esquema_diccionario WHERE version_id=$v;",
                    ("$v", guia)
                )
            )
        );
    }

    [Fact]
    public void BuscaPrimeroEnCarpetaDelEsquema_y_registraDocumentoSinClasificacion()
    {
        using var contexto = new ContextoTemporal();
        string raiz = Path.Combine(contexto.Raiz, "occ-esquema");
        string antigua = Path.Combine(raiz, "2026", "202608");
        Directory.CreateDirectory(antigua);
        foreach (int i in Enumerable.Range(0, 40))
            CreadorPdfDePrueba.CrearConLineas(antigua, $"Documento {i}");
        string temporal = CreadorPdfDePrueba.CrearConLineas(antigua, "OCC 12345");
        string objetivo = Path.Combine(antigua, "OCC0000012345.pdf");
        File.Move(temporal, objetivo);
        Directory.CreateDirectory(Path.Combine(raiz, "2026", "202609"));
        Directory.CreateDirectory(Path.Combine(raiz, "2026", "202610"));
        using (var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun))
        {
            var esquema = new RepositorioEsquemasDiccionario(conexion).ObtenerPorProveedor(
                "HOFFENS"
            )!;
            new RepositorioEsquemasDiccionario(conexion).GuardarDatosYLugares(
                esquema.Id,
                new Dictionary<string, IReadOnlyList<string>> { ["oc_propia"] = ["oc_propia"] },
                new Dictionary<string, string?> { ["oc_propia"] = raiz },
                []
            );
        }
        int lecturas = 0;
        var buscador = new BuscadorDocumentosEnCarpetaService(
            extraerValores: (_, _) =>
            {
                lecturas++;
                return [];
            }
        );
        var actualizado = ObtenerEsquema("HOFFENS");
        var versiones = buscador.Buscar("oc_propia", "12345", actualizado);
        Assert.Single(versiones);
        Assert.Equal(0, lecturas);
        using var conexionFinal = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        Assert.Equal(
            "12345",
            Convert.ToString(
                Ejecutar(
                    conexionFinal,
                    "SELECT x.valor_original FROM valores_documento x WHERE x.version_id=$v AND x.dato_diccionario_id='oc_propia';",
                    ("$v", versiones[0])
                )
            )
        );
        Assert.Equal(
            "HOFFENS",
            Convert.ToString(
                Ejecutar(
                    conexionFinal,
                    "SELECT x.valor_original FROM valores_documento x WHERE x.version_id=$v AND x.dato_diccionario_id='nombre_proveedor';",
                    ("$v", versiones[0])
                )
            )
        );
    }

    [Fact]
    public void Buscar_LeeUnaSolaVezElPdfConNumeroEnElNombre_YExploraSubcarpetasDePeriodo()
    {
        using var contexto = new ContextoTemporal();
        var tipo = DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "oc_propia");
        string raiz = Path.Combine(contexto.Raiz, "destino");
        string periodo = Path.Combine(raiz, "2026", "202610");
        Directory.CreateDirectory(periodo);
        CrearConfiguracion("Empresa", tipo.EtiquetaTipo, raiz, "oc_propia", ["nombre_proveedor"]);
        CrearConfiguracion(
            "COBELCAR",
            DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "guia_proveedor").EtiquetaTipo,
            Path.Combine(contexto.Raiz, "entrada"),
            "guia_proveedor",
            ["oc_propia"]
        );
        for (int i = 0; i < 50; i++)
            CreadorPdfDePrueba.CrearConLineas(periodo, "Documento sin número");
        string ruta = CreadorPdfDePrueba.CrearConLineas(periodo, "20527", "COBELCAR");
        string destino = Path.Combine(periodo, "OCC0000020527.pdf");
        File.Move(ruta, destino);
        var antes = new FileInfo(destino);
        DateTime fechaAntes = antes.LastWriteTimeUtc;
        long tamanoAntes = antes.Length;
        int lecturas = 0;
        var buscador = new BuscadorDocumentosEnCarpetaService(
            extraerValores: (archivo, configuracion) =>
            {
                lecturas++;
                return PublicadorDatosDocumentoService.ExtraerValoresObservados(
                    archivo,
                    configuracion
                );
            }
        );

        long grc = PublicarGuiaCobelcar(contexto.Raiz);
        var buscadorMotor = new BuscadorContador(buscador);
        Assert.True(File.Exists(destino));
        Assert.Equal(tamanoAntes, new FileInfo(destino).Length);
        Assert.Equal(fechaAntes, File.GetLastWriteTimeUtc(destino));
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        var resultado = new MotorCadenasDiccionario(conexion, buscadorMotor).ProcesarVersion(grc);
        Assert.Equal(1, lecturas);
        long versionOcc = Convert.ToInt64(
            Ejecutar(
                conexion,
                "SELECT v.id FROM versiones_documento v JOIN documentos d ON d.id=v.documento_id WHERE d.ruta=$r COLLATE NOCASE AND v.estado='vigente';",
                ("$r", Path.GetFullPath(destino))
            )
        );
        Assert.Equal("COBELCAR", ProveedorDeDocumento.Determinar(conexion, versionOcc)?.Clave);
        Assert.True(File.Exists(destino));
        Assert.Equal(tamanoAntes, new FileInfo(destino).Length);
        Assert.Equal(fechaAntes, File.GetLastWriteTimeUtc(destino));
        Assert.True(
            resultado.Estado == "ubicado",
            $"{resultado.Estado}: {resultado.Motivo}; búsquedas: {string.Join(",", buscadorMotor.Buscados)}"
        );
        Assert.Equal(
            1L,
            Convert.ToInt64(
                Ejecutar(
                    conexion,
                    "SELECT COUNT(*) FROM ubicaciones_cadena_diccionario grc JOIN ubicaciones_cadena_diccionario occ ON occ.cadena_id=grc.cadena_id WHERE grc.version_id=$g AND occ.version_id=$o AND grc.estado='activo' AND occ.estado='activo';",
                    ("$g", grc),
                    ("$o", versionOcc)
                )
            )
        );
    }

    [Fact]
    public void BuscarOtraVez_EncuentraElPdfAgregadoDespues_EnLaCarpetaConfigurada()
    {
        using var contexto = new ContextoTemporal();
        var tipoOcc = DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "oc_propia");
        string carpetaOcc = Path.Combine(contexto.Raiz, "destino");
        Directory.CreateDirectory(carpetaOcc);
        CrearConfiguracion(
            "Empresa",
            tipoOcc.EtiquetaTipo,
            carpetaOcc,
            "oc_propia",
            ["nombre_proveedor"]
        );
        CrearConfiguracion(
            "COBELCAR",
            DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "guia_proveedor").EtiquetaTipo,
            Path.Combine(contexto.Raiz, "grc"),
            "guia_proveedor",
            ["oc_propia"]
        );
        long grc = PublicarGuiaCobelcar(contexto.Raiz);
        var buscador = new BuscadorDocumentosEnCarpetaService();
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        var motor = new MotorCadenasDiccionario(conexion, buscador);
        Assert.Equal("sin_inicio", motor.ProcesarVersion(grc).Estado);
        string temporal = CreadorPdfDePrueba.CrearConLineas(carpetaOcc, "20527", "COBELCAR");
        File.Move(temporal, Path.Combine(carpetaOcc, "OCC0000020527.pdf"));

        var encontrados = buscador.Buscar("oc_propia", "20527", ObtenerEsquema("COBELCAR"));
        Assert.Single(encontrados);
        foreach (long version in encontrados)
            motor.ProcesarVersion(version);
        motor.ProcesarVersion(grc);
        Assert.Equal(
            1L,
            Convert.ToInt64(
                Ejecutar(
                    conexion,
                    "SELECT COUNT(*) FROM ubicaciones_cadena_diccionario WHERE version_id=$v AND estado='activo';",
                    ("$v", grc)
                )
            )
        );
        Assert.Equal(
            "resuelta",
            Convert.ToString(
                Ejecutar(
                    conexion,
                    "SELECT estado FROM decisiones_esquema_diccionario WHERE version_id=$v AND tipo='sin_inicio';",
                    ("$v", grc)
                )
            )
        );
    }

    [Fact]
    public void ProcesarOCC_BuscaLaNVVPreviaEnLaCarpetaConfigurada()
    {
        using var contexto = new ContextoTemporal();
        var tipoNvv = DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "nota_venta_propia");
        string carpetaNvv = Path.Combine(contexto.Raiz, "nvv");
        Directory.CreateDirectory(carpetaNvv);
        CrearConfiguracion("Empresa", tipoNvv.EtiquetaTipo, carpetaNvv, "nota_venta_propia");
        string rutaNvv = CreadorPdfDePrueba.CrearConLineas(carpetaNvv, "20220");
        File.Move(rutaNvv, Path.Combine(carpetaNvv, "NVV0000020220.pdf"));
        string carpetaOcc = Path.Combine(contexto.Raiz, "occ");
        Directory.CreateDirectory(carpetaOcc);
        var tipoOcc = DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "oc_propia");
        CrearConfiguracion(
            "Empresa",
            tipoOcc.EtiquetaTipo,
            carpetaOcc,
            "oc_propia",
            ["nota_venta_propia", "nombre_proveedor"]
        );
        string rutaOcc = CreadorPdfDePrueba.CrearConLineas(
            carpetaOcc,
            "20527",
            "20220",
            "COBELCAR"
        );
        var configuracionOcc = new ConfiguracionDocumento
        {
            Emisor = "Empresa",
            Tipo = tipoOcc.EtiquetaTipo,
            CarpetaDestino = carpetaOcc,
            FormatoCarpeta = FormatoCarpeta.Directo,
            Renombrar = false,
            GrupoDocumento = "Emitido",
            NombreEstandar = DiccionarioDatosEnlazantes.NombreEstandar("oc_propia", "Empresa"),
            Patrones = [new(1, [])],
        };
        PublicadorDatosDocumentoService.Publicar(
            rutaOcc,
            configuracionOcc,
            [
                CrearValor("oc_propia", "20527"),
                CrearValor("nota_venta_propia", "20220"),
                CrearValor("nombre_proveedor", "COBELCAR"),
            ],
            "integracion",
            revisarEnlaces: false
        );
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        long versionOcc = Convert.ToInt64(
            Ejecutar(
                conexion,
                "SELECT v.id FROM versiones_documento v JOIN documentos d ON d.id=v.documento_id WHERE d.ruta=$r COLLATE NOCASE AND v.estado='vigente';",
                ("$r", Path.GetFullPath(rutaOcc))
            )
        );

        var resultado = new MotorCadenasDiccionario(
            conexion,
            new BuscadorDocumentosEnCarpetaService()
        ).ProcesarVersion(versionOcc);

        Assert.Equal("cadena_creada", resultado.Estado);
        Assert.Equal(
            1L,
            Convert.ToInt64(
                Ejecutar(
                    conexion,
                    "SELECT COUNT(*) FROM ubicaciones_cadena_diccionario u JOIN versiones_documento v ON v.id=u.version_id JOIN documentos d ON d.id=v.documento_id WHERE u.estado='activo' AND d.ruta=$r COLLATE NOCASE;",
                    ("$r", Path.GetFullPath(Path.Combine(carpetaNvv, "NVV0000020220.pdf")))
                )
            )
        );
    }

    [Fact]
    public void GuiaFirmada_BuscaSoloEnUbicacionAsociadaATipoYProveedor()
    {
        using var contexto = new ContextoTemporal();
        string hoffens = Path.Combine(contexto.Raiz, "hoffens");
        string otra = Path.Combine(contexto.Raiz, "otra");
        Directory.CreateDirectory(hoffens);
        Directory.CreateDirectory(otra);
        var ubicaciones = new UbicacionSinTextoRepository();
        int ubicacionHoffens = ubicaciones.ObtenerOCrear(hoffens, FormatoCarpeta.Directo, null).Id;
        ubicaciones.AsociarACadena(ubicacionHoffens, "guia_firmada_proveedor", "HOFFENS");
        ubicaciones.ObtenerOCrear(otra, FormatoCarpeta.Directo, null);
        string guiaFirmada = Path.Combine(hoffens, "1190036.pdf");
        File.WriteAllBytes(guiaFirmada, PdfVacio());
        Assert.False(Archivero.Servicios.Pdf.LectorPdf.TieneTextoExtraible(guiaFirmada));
        File.WriteAllBytes(Path.Combine(otra, "1190036.pdf"), PdfVacio());
        var tipoOcc = DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "oc_propia");
        CrearConfiguracion(
            "Empresa",
            tipoOcc.EtiquetaTipo,
            Path.Combine(contexto.Raiz, "occ"),
            "oc_propia",
            ["nombre_proveedor"]
        );
        CrearConfiguracion(
            "HOFFENS",
            DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "guia_proveedor").EtiquetaTipo,
            Path.Combine(contexto.Raiz, "grc"),
            "guia_proveedor",
            ["oc_propia"]
        );
        long versionOcc = PublicarDocumento(
            contexto.Raiz,
            "Empresa",
            "oc_propia",
            "oc_propia",
            "20527",
            ("nombre_proveedor", "HOFFENS")
        );
        long versionGrc = PublicarDocumento(
            contexto.Raiz,
            "HOFFENS",
            "guia_proveedor",
            "guia_proveedor",
            "1190036",
            ("oc_propia", "20527"),
            ("nombre_proveedor", "HOFFENS")
        );
        var buscador = new BuscadorDocumentosEnCarpetaService();
        using (var conexionMotor = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun))
        {
            var motor = new MotorCadenasDiccionario(conexionMotor, buscador);
            motor.ProcesarVersion(versionOcc);
            motor.ProcesarVersion(versionGrc);
        }

        var versiones = buscador.Buscar(
            "guia_firmada_proveedor",
            "1190036",
            ObtenerEsquema("HOFFENS")
        );

        Assert.Single(versiones);
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        Assert.Equal(
            1L,
            Convert.ToInt64(
                Ejecutar(
                    conexion,
                    "SELECT COUNT(*) FROM documentos WHERE ruta=$r COLLATE NOCASE;",
                    ("$r", Path.GetFullPath(Path.Combine(hoffens, "1190036.pdf")))
                )
            )
        );
        Assert.Equal(
            1L,
            Convert.ToInt64(
                Ejecutar(
                    conexion,
                    "SELECT COUNT(*) FROM ubicaciones_cadena_diccionario WHERE version_id=(SELECT v.id FROM versiones_documento v JOIN documentos d ON d.id=v.documento_id WHERE d.ruta=$r COLLATE NOCASE AND v.estado='vigente') AND estado='activo';",
                    ("$r", Path.GetFullPath(Path.Combine(hoffens, "1190036.pdf")))
                )
            )
        );
        Assert.Equal(
            0L,
            Convert.ToInt64(
                Ejecutar(
                    conexion,
                    "SELECT COUNT(*) FROM documentos WHERE ruta=$r COLLATE NOCASE;",
                    ("$r", Path.GetFullPath(Path.Combine(otra, "1190036.pdf")))
                )
            )
        );
    }

    private static object? Ejecutar(
        Microsoft.Data.Sqlite.SqliteConnection conexion,
        string sql,
        params (string Nombre, object Valor)[] parametros
    )
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
            comando.Parameters.AddWithValue(nombre, valor);
        return comando.ExecuteScalar();
    }

    private static EsquemaDiccionario ObtenerEsquema(string proveedor)
    {
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        return new RepositorioEsquemasDiccionario(conexion).ObtenerPorProveedor(proveedor)!;
    }

    private static void CrearConfiguracion(
        string emisor,
        string tipo,
        string carpeta,
        string datoTipo,
        IReadOnlyList<string>? datosAdicionales = null
    )
    {
        var dato = DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == datoTipo);
        var configurados = DiccionarioDatosEnlazantes
            .Todos.Select(
                (d, indice) =>
                    new DatoEnlazanteConfigurado(
                        d.Id,
                        d.Nombre,
                        d.Grupo,
                        d.Id == datoTipo || datosAdicionales?.Contains(d.Id) == true,
                        d.Id == datoTipo || datosAdicionales?.Contains(d.Id) == true,
                        0,
                        0,
                        d.Id == datoTipo ? 0.02 : 0.21,
                        1,
                        0.15,
                        "",
                        true,
                        d.Id == datoTipo
                    )
            )
            .ToArray();
        DatosEnlazantesConfiguracionService.Guardar(
            emisor,
            tipo,
            "Emitido",
            DiccionarioDatosEnlazantes.NombreEstandar(datoTipo, emisor),
            1,
            configurados
        );
        new ConfiguracionDocumentoRepository().GuardarNueva(
            emisor,
            tipo,
            carpeta,
            FormatoCarpeta.AnioMes,
            "yyyy\\yyyyMM",
            false,
            []
        );
    }

    private static long PublicarGuiaCobelcar(string raiz, bool incluirEnlace = true)
    {
        var documento = DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "guia_proveedor");
        string carpeta = Path.Combine(raiz, "entrada");
        Directory.CreateDirectory(carpeta);
        string ruta = CreadorPdfDePrueba.CrearConLineas(carpeta, "GRC 3001", "OCC 20527");
        var configuracion = new ConfiguracionDocumento
        {
            Emisor = "COBELCAR",
            Tipo = documento.EtiquetaTipo,
            CarpetaDestino = carpeta,
            FormatoCarpeta = FormatoCarpeta.Directo,
            Renombrar = false,
            GrupoDocumento = "Recibido",
            NombreEstandar = DiccionarioDatosEnlazantes.NombreEstandar(
                "guia_proveedor",
                "COBELCAR"
            ),
            Patrones = [new(1, [])],
        };
        var valores = new[] { CrearValor("guia_proveedor", "3001") }
            .Concat(incluirEnlace ? [CrearValor("oc_propia", "20527")] : [])
            .Append(CrearValor("nombre_proveedor", "COBELCAR"))
            .ToArray();
        PublicadorDatosDocumentoService.Publicar(
            ruta,
            configuracion,
            valores,
            "integracion",
            revisarEnlaces: false
        );
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        return Convert.ToInt64(
            Ejecutar(
                conexion,
                "SELECT v.id FROM versiones_documento v JOIN documentos d ON d.id=v.documento_id WHERE d.ruta=$r COLLATE NOCASE AND v.estado='vigente';",
                ("$r", Path.GetFullPath(ruta))
            )
        );
    }

    private static long PublicarDocumento(
        string raiz,
        string emisor,
        string tipoId,
        string datoId,
        string numero,
        params (string Dato, string Valor)[] adicionales
    )
    {
        var tipo = DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == tipoId);
        string carpeta = Path.Combine(raiz, "entrada_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        string ruta = CreadorPdfDePrueba.CrearConLineas(
            carpeta,
            new[] { numero }.Concat(adicionales.Select(a => a.Valor)).ToArray()
        );
        var configuracion = new ConfiguracionDocumento
        {
            Emisor = emisor,
            Tipo = tipo.EtiquetaTipo,
            CarpetaDestino = carpeta,
            FormatoCarpeta = FormatoCarpeta.Directo,
            Renombrar = false,
            GrupoDocumento = tipo.Grupo == "Del proveedor" ? "Recibido" : "Emitido",
            NombreEstandar = DiccionarioDatosEnlazantes.NombreEstandar(tipoId, emisor),
            Patrones = [new(1, [])],
        };
        PublicadorDatosDocumentoService.Publicar(
            ruta,
            configuracion,
            new[] { CrearValor(datoId, numero) }
                .Concat(adicionales.Select(a => CrearValor(a.Dato, a.Valor)))
                .ToArray(),
            "integracion",
            revisarEnlaces: false
        );
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        return Convert.ToInt64(
            Ejecutar(
                conexion,
                "SELECT v.id FROM versiones_documento v JOIN documentos d ON d.id=v.documento_id WHERE d.ruta=$r COLLATE NOCASE AND v.estado='vigente';",
                ("$r", Path.GetFullPath(ruta))
            )
        );
    }

    private static ValorDocumentoLeido CrearValor(string id, string valor)
    {
        var dato = DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == id);
        return new ValorDocumentoLeido(
            dato.Nombre,
            id,
            valor,
            DiccionarioDatosEnlazantes.ClaveDeEnlace(id, valor),
            "integracion",
            DatoDiccionarioId: id
        );
    }

    private static byte[] PdfVacio()
    {
        string ruta = CreadorPdfDePrueba.CrearConLineas(Path.GetTempPath(), " ");
        byte[] pdf = File.ReadAllBytes(ruta);
        File.Delete(ruta);
        return pdf;
    }

    private sealed class ContextoTemporal : IDisposable
    {
        private readonly string? datosAnterior = Environment.GetEnvironmentVariable(
            "HORMIGUERO_DATOS"
        );
        private readonly string baseArchiveroAnterior = BaseDeDatos.RutaArchivo;

        public ContextoTemporal()
        {
            Raiz = Path.Combine(
                Path.GetTempPath(),
                "ArchiveroBusqueda",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(Raiz);
            Environment.SetEnvironmentVariable("HORMIGUERO_DATOS", Path.Combine(Raiz, "datos"));
            BaseDeDatos.RutaArchivo = Path.Combine(Raiz, "archivero.db");
            BaseDeDatos.AsegurarEsquema();
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            var origen = Path.GetFullPath(Path.Combine("configuracion", "cadenas-jcv.json"));
            Assert.True(PrecargaCadenasJcv.IntentarImportar(conexion, origen, "Archivero"));
        }

        public string Raiz { get; }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("HORMIGUERO_DATOS", datosAnterior);
            BaseDeDatos.RutaArchivo = baseArchiveroAnterior;
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(Raiz))
                Directory.Delete(Raiz, recursive: true);
        }
    }

    private sealed class BuscadorContador(IBuscadorDocumentosCadena interno)
        : IBuscadorDocumentosCadena
    {
        public List<string> Buscados { get; } = [];

        public IReadOnlyList<long> Buscar(
            string tipoDiccionario,
            string numero,
            EsquemaDiccionario esquema
        )
        {
            Buscados.Add($"{tipoDiccionario}:{numero}");
            return interno.Buscar(tipoDiccionario, numero, esquema);
        }
    }
}
