using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Tests;

public sealed class B6bDatosTests : IDisposable
{
    private readonly SqliteConnection conexion;

    public B6bDatosTests()
    {
        conexion = new SqliteConnection("Data Source=:memory:");
        conexion.Open();
        using var activar = conexion.CreateCommand();
        activar.CommandText = "PRAGMA foreign_keys=ON;";
        activar.ExecuteNonQuery();
        Migraciones.Aplicar(conexion, Migraciones.Todas);
    }

    public void Dispose() => conexion.Dispose();

    [Fact]
    public void V5_repetida_y_aplicada_sobre_v4_con_datos()
    {
        using var v4 = new SqliteConnection("Data Source=:memory:");
        v4.Open();
        Migraciones.Aplicar(v4, Migraciones.Todas.Take(4).ToArray());
        using (var guardar = v4.CreateCommand())
        {
            guardar.CommandText =
                "INSERT INTO documentos(ruta,carpeta_raiz,nombre,tamano,modificado,estado,tiene_texto,indexado_en) VALUES('r','c','n',1,'f','ok',0,'f');";
            guardar.ExecuteNonQuery();
        }
        Migraciones.Aplicar(v4, Migraciones.Todas);
        Migraciones.Aplicar(v4, Migraciones.Todas);
        using var consultar = v4.CreateCommand();
        consultar.CommandText = "SELECT estado_baja, COUNT(*) FROM documentos;";
        using var lector = consultar.ExecuteReader();
        Assert.True(lector.Read());
        Assert.Equal("activo", lector.GetString(0));
        Assert.Equal(1L, lector.GetInt64(1));
        Assert.Equal(
            (long)Migraciones.Todas.Count,
            Convert.ToInt64(Escalar(v4, "SELECT COUNT(*) FROM migraciones;"))
        );
        Assert.True(
            Convert.ToInt64(
                Escalar(
                    v4,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('modelos_cadena','vagones_modelo','cadenas','vagones_cadena','reglas_vagon','enlaces_cadena','versiones_documento','campos_documento','valores_documento','marcas_version');"
                )
            ) == 10
        );
        Assert.Equal(
            5L,
            Convert.ToInt64(
                Escalar(
                    v4,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name IN ('idx_vagones_modelo_modelo','idx_cadenas_madre','idx_valores_campo_clave_estado','idx_valores_version_campo','idx_versiones_huella');"
                )
            )
        );
    }

    [Fact]
    public void Claves_foraneas_nuevas_rechazan_referencias_inexistentes()
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "INSERT INTO versiones_documento(documento_id,huella,ruta_observada,registrada_en) VALUES(999,'h','r','f');";
        Assert.Throws<SqliteException>(() => comando.ExecuteNonQuery());
        using var acciones = conexion.CreateCommand();
        acciones.CommandText = "PRAGMA foreign_key_list('versiones_documento');";
        using var lector = acciones.ExecuteReader();
        Assert.True(lector.Read());
        Assert.Equal("NO ACTION", lector.GetString(6));
    }

    [Fact]
    public void Arbol_de_modelo_instantanea_orden_y_cadena_hija()
    {
        var repo = new RepositorioCadenas(conexion);
        long modeloHijo = repo.CrearModelo("Hija", DateTime.UtcNow, true);
        repo.AgregarVagonModelo(modeloHijo, null, "Documento hijo");
        long modelo = repo.CrearModelo("Principal", DateTime.UtcNow);
        long principal = repo.AgregarVagonModelo(
            modelo,
            null,
            "Factura",
            esMultiple: true,
            modeloCadenaHijaId: modeloHijo
        );
        long anexo = repo.AgregarVagonModelo(modelo, principal, "Anexo", esAnexo: true);
        Assert.Equal(
            principal,
            Convert.ToInt64(
                Escalar(conexion, "SELECT padre_id FROM vagones_modelo WHERE id=" + anexo)
            )
        );
        repo.AgregarVagonModelo(modelo, null, "Orden");
        var arbol = repo.ObtenerArbolModelo(modelo);
        Assert.Equal(new[] { "Factura", "Orden" }, arbol.Select(n => n.Vagon.Nombre));
        Assert.True(arbol[0].Vagon.EsMultiple);
        Assert.Equal("Anexo", Assert.Single(arbol[0].Hijos).Vagon.Nombre);
        Assert.True(arbol[0].Hijos[0].Vagon.EsAnexo);
        long cadena = repo.CrearCadena(modelo, "Principal 1", DateTime.UtcNow);
        var instancias = repo.ObtenerArbolCadena(cadena);
        Assert.Equal(new[] { "Factura", "Orden" }, instancias.Select(n => n.Vagon.Nombre));
        using (var cambio = conexion.CreateCommand())
        {
            cambio.CommandText = "UPDATE vagones_modelo SET nombre='Modificado' WHERE id=$id;";
            cambio.Parameters.AddWithValue("$id", principal);
            cambio.ExecuteNonQuery();
        }
        Assert.Contains("Factura", repo.ObtenerCadena(cadena)!.EstructuraJson);
        long vagonPadre = instancias[0].Vagon.Id;
        long hija = repo.CrearCadena(modeloHijo, "Hija 1", DateTime.UtcNow, cadena, vagonPadre);
        Assert.Equal(cadena, repo.ObtenerCadena(hija)!.CadenaMadreId);
        Assert.Equal(vagonPadre, repo.ObtenerCadena(hija)!.VagonPadreId);
        Assert.Equal(anexo, arbol[0].Hijos[0].Vagon.Id);
    }

    [Fact]
    public void Versiones_campos_valores_marcas_y_enlaces_con_historial_auditado()
    {
        long identificacion = new Identificaciones(conexion).Guardar(
            new(0, "Factura", "Emisor", "{}")
        );
        var datos = new RepositorioDocumentosDatos(conexion);
        long campo = datos.GuardarCampo(
            new(0, identificacion, "Orden de compra", "orden_compra", "texto", true, "marca")
        );
        long docId = AgregarDocumento("h1");
        var version = datos.RegistrarVersion(docId, "huella1", @"C:\docs\a.pdf");
        var misma = datos.RegistrarVersion(docId, "huella1", @"D:\docs\a.pdf");
        Assert.Equal(version.Id, misma.Id);
        Assert.Equal(@"D:\docs\a.pdf", misma.RutaObservada);
        datos.GuardarValor(version.Id, campo, " OC 123 ", "OC 123", "correccion", 0.9);
        Assert.Equal("OC 123", Assert.Single(datos.BuscarValores(campo, "OC 123")).ValorClave);
        long marca = datos.GuardarMarca(
            new(0, version.Id, "texto", 1, 1, 2, 3, 4, "OC 123", DateTime.UtcNow, "activa")
        );
        Assert.True(datos.AnularMarca(marca));
        long modelo = new RepositorioCadenas(conexion).CrearModelo("M", DateTime.UtcNow);
        long vm = new RepositorioCadenas(conexion).AgregarVagonModelo(modelo, null, "V");
        long cadena = new RepositorioCadenas(conexion).CrearCadena(modelo, "C", DateTime.UtcNow);
        long vagon = Assert
            .Single(new RepositorioCadenas(conexion).ObtenerArbolCadena(cadena))
            .Vagon.Id;
        var reglas = new RepositorioReglasYEnlaces(conexion);
        long regla = reglas.GuardarRegla(
            new(0, vm, identificacion, campo, vm, campo, "igual", true, false, false, 6, "activa")
        );
        long enlace = reglas.CrearEnlace(vagon, version.Id, "manual", regla);
        Assert.True(reglas.DeshacerEnlace(enlace));
        Assert.Equal("anulado", Assert.Single(reglas.HistorialEnlaces(vagon)).Estado);
        Assert.True(datos.AnularValor(Assert.Single(datos.BuscarValores(campo, "OC 123")).Id));
        Assert.True(reglas.AnularRegla(regla));
        Assert.Equal(
            9L,
            Convert.ToInt64(Escalar(conexion, "SELECT COUNT(*) FROM auditoria WHERE app='Nucleo';"))
        );
        Assert.Equal(
            DBNull.Value,
            Escalar(conexion, "SELECT version_id FROM vagones_cadena WHERE id=" + vagon + ";")
        );
    }

    [Fact]
    public void Escritor_puede_escribir_mientras_hay_un_lector_abierto_en_wal()
    {
        string ruta = Path.Combine(
            Path.GetTempPath(),
            "HormigueroTests",
            Guid.NewGuid().ToString("N"),
            "hormiguero.db"
        );
        try
        {
            using var escritor = BaseComun.Abrir(ruta);
            using var lectorConexion = BaseComun.Abrir(ruta);
            long id = new Identificaciones(escritor).Guardar(new(0, "T", "E", "{}"));
            using var lectorCmd = lectorConexion.CreateCommand();
            lectorCmd.CommandText = "SELECT id FROM identificaciones;";
            using var lector = lectorCmd.ExecuteReader();
            Assert.True(lector.Read());
            long campo = new RepositorioDocumentosDatos(escritor).GuardarCampo(
                new(0, id, "Nombre", "nombre", "texto", true, "manual")
            );
            Assert.True(campo > 0);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            string carpeta = Path.GetDirectoryName(ruta)!;
            if (Directory.Exists(carpeta))
                Directory.Delete(carpeta, recursive: true);
        }
    }

    [Fact]
    public void Publicacion_conserva_valores_originales_correcciones_y_versiones_por_huella()
    {
        var repo = new RepositorioDocumentosDatos(conexion);
        var valores = new[]
        {
            new ValorDocumentoLeido(
                "Emisor",
                "emisor",
                "Compañía Ñandú",
                "Compañía Ñandú",
                "marca"
            ),
            new ValorDocumentoLeido("Tipo", "tipo", "Factura", "Factura", "marca"),
            new ValorDocumentoLeido("OC", "oc", "000123", "000123", "marca"),
        };
        var uno = repo.PublicarDocumento(
            @"C:\docs\a.pdf",
            3,
            DateTime.UtcNow,
            "h1",
            "Compañía Ñandú",
            "Factura",
            valores
        );
        var campo = Assert.Single(repo.ListarCampos(1), c => c.NombreEstable == "oc");
        var original = Assert.Single(repo.BuscarValores(campo.Id, "000123"));
        Assert.Equal("000123", original.ValorOriginal);
        repo.CorregirValor(uno.Version.Id, campo.Id, "001234", "001234");
        Assert.Equal("manual", Assert.Single(repo.BuscarValores(campo.Id, "001234")).Origen);
        Assert.Empty(repo.BuscarValores(campo.Id, "000123"));

        var movido = repo.PublicarDocumento(
            @"D:\docs\a.pdf",
            3,
            DateTime.UtcNow,
            "h1",
            "Compañía Ñandú",
            "Factura",
            valores
        );
        Assert.Equal(uno.DocumentoId, movido.DocumentoId);
        Assert.Equal(uno.Version.Id, movido.Version.Id);
        Assert.Equal(@"D:\docs\a.pdf", movido.Version.RutaObservada);
        var nuevo = repo.PublicarDocumento(
            @"D:\docs\a.pdf",
            4,
            DateTime.UtcNow,
            "h2",
            "Compañía Ñandú",
            "Factura",
            valores
        );
        Assert.Equal(uno.DocumentoId, nuevo.DocumentoId);
        Assert.NotEqual(uno.Version.Id, nuevo.Version.Id);
    }

    [Fact]
    public void Motor_enlaza_coincidencia_unica_y_deja_empate_como_dudoso()
    {
        long identificacion = new Identificaciones(conexion).Guardar(new(0, "OC", "Emisor", "{}"));
        var datos = new RepositorioDocumentosDatos(conexion);
        long campoOrigen = datos.GuardarCampo(
            new(0, identificacion, "OC", "oc", "texto", true, "marca")
        );
        long campoComparacion = datos.GuardarCampo(
            new(0, identificacion, "OC ref", "oc_ref", "texto", true, "marca")
        );
        long modelo = new RepositorioCadenas(conexion).CrearModelo("M", DateTime.UtcNow);
        long comparador = new RepositorioCadenas(conexion).AgregarVagonModelo(
            modelo,
            null,
            "Factura"
        );
        long destino = new RepositorioCadenas(conexion).AgregarVagonModelo(modelo, null, "Guía");
        long cadena = new RepositorioCadenas(conexion).CrearCadena(modelo, "C", DateTime.UtcNow);
        var vagones = new RepositorioCadenas(conexion)
            .ObtenerArbolCadena(cadena)
            .ToDictionary(n => n.Vagon.VagonModeloId!.Value, n => n.Vagon.Id);
        long docRef = AgregarDocumento("ref");
        long versionRef = datos.RegistrarVersion(docRef, "ref", "ref.pdf").Id;
        datos.GuardarValor(versionRef, campoComparacion, "  OC  00123 ", "OC  00123", "manual");
        var enlaces = new RepositorioReglasYEnlaces(conexion);
        enlaces.CrearEnlace(vagones[comparador], versionRef, "manual");
        long regla = enlaces.GuardarRegla(
            new(
                0,
                destino,
                identificacion,
                campoOrigen,
                comparador,
                campoComparacion,
                "igual",
                true,
                false,
                false,
                6,
                "activa"
            )
        );
        long doc = AgregarDocumento("uno");
        long version = datos.RegistrarVersion(doc, "uno", "uno.pdf").Id;
        datos.GuardarValor(version, campoOrigen, "OC 00123", "OC 00123", "marca");

        new MotorEnlaceAutomatico(conexion).Ejecutar();

        var destinoUno = enlaces.HistorialEnlaces(vagones[destino]);
        Assert.Equal("automatico", Assert.Single(destinoUno).Origen);
        Assert.Equal(regla, destinoUno[0].ReglaId);

        long cadenaDos = new RepositorioCadenas(conexion).CrearCadena(
            modelo,
            "C2",
            DateTime.UtcNow
        );
        var vagonesDos = new RepositorioCadenas(conexion)
            .ObtenerArbolCadena(cadenaDos)
            .ToDictionary(n => n.Vagon.VagonModeloId!.Value, n => n.Vagon.Id);
        enlaces.CrearEnlace(vagonesDos[comparador], versionRef, "manual");
        long segundoDoc = AgregarDocumento("dos");
        long versionDos = datos.RegistrarVersion(segundoDoc, "dos", "dos.pdf").Id;
        datos.GuardarValor(versionDos, campoOrigen, "OC 00123", "OC 00123", "marca");

        new MotorEnlaceAutomatico(conexion).Ejecutar();

        var propuestas = enlaces
            .HistorialEnlaces(vagonesDos[destino])
            .Where(e => e.Estado == "dudoso")
            .ToArray();
        Assert.Equal(2, propuestas.Length);
        var propuesta = propuestas[0];
        Assert.Contains("documentos con el mismo dato", propuesta.Motivo);
        Assert.Equal(propuesta.Id, enlaces.ObtenerDatosDudoso(propuesta.Id)!.EnlaceId);
        Assert.True(enlaces.RechazarDudoso(propuesta.Id));
        new MotorEnlaceAutomatico(conexion).Ejecutar();
        Assert.DoesNotContain(enlaces.ListarDudosos(), e => e.Id == propuesta.Id);
    }

    [Fact]
    public void Motor_normaliza_solo_las_opciones_guardadas_y_persiste_confianza_minima()
    {
        var motor = new MotorEnlaceAutomatico(conexion);
        Assert.Equal(0.90, motor.ConfianzaMinima);
        motor.ConfianzaMinima = 0.95;
        Assert.Equal(0.95, new MotorEnlaceAutomatico(conexion).ConfianzaMinima);

        var regla = new ReglaVagon(1, 1, 1, 1, 1, 1, "igual", true, true, true, 6, "activa");
        Assert.Equal("A 123", MotorEnlaceAutomatico.NormalizarClave(" A   123 ", regla));
        Assert.Equal("A123", MotorEnlaceAutomatico.NormalizarClave("A-123", regla));
        Assert.Equal("123", MotorEnlaceAutomatico.NormalizarClave("00123", regla));
        Assert.Equal(
            "A-123",
            MotorEnlaceAutomatico.NormalizarClave("A-123", regla with { IgnorarGuiones = false })
        );
    }

    [Fact]
    public void Asegurar_documento_reutiliza_ruta_huella_y_crea_version_por_cambio()
    {
        string carpeta = Path.Combine(
            Path.GetTempPath(),
            "nucleo-b6d-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(carpeta);
        string ruta = Path.Combine(carpeta, "a.pdf");
        string rutaMovida = Path.Combine(carpeta, "movido.pdf");
        try
        {
            File.WriteAllText(ruta, "contenido uno");
            var repo = new RepositorioDocumentosDatos(conexion);
            var nuevo = repo.AsegurarDocumentoYVersionVigente(ruta);
            var existente = repo.AsegurarDocumentoYVersionVigente(ruta);
            Assert.Equal(nuevo.DocumentoId, existente.DocumentoId);
            Assert.Equal(nuevo.Version.Id, existente.Version.Id);

            File.Move(ruta, rutaMovida);
            var movido = repo.AsegurarDocumentoYVersionVigente(rutaMovida);
            Assert.Equal(nuevo.DocumentoId, movido.DocumentoId);
            Assert.Equal(nuevo.Version.Id, movido.Version.Id);
            Assert.Equal(Path.GetFullPath(rutaMovida), movido.Version.RutaObservada);

            File.WriteAllText(rutaMovida, "contenido dos distinto");
            var cambiado = repo.AsegurarDocumentoYVersionVigente(rutaMovida);
            Assert.Equal(nuevo.DocumentoId, cambiado.DocumentoId);
            Assert.NotEqual(nuevo.Version.Id, cambiado.Version.Id);
            Assert.Equal("anulada", repo.BuscarVersiones(nuevo.Version.Huella).Single().Estado);
            Assert.Equal(
                5L,
                Convert.ToInt64(
                    Escalar(conexion, "SELECT COUNT(*) FROM auditoria WHERE app='Buscadero';")
                )
            );
        }
        finally
        {
            Directory.Delete(carpeta, true);
        }
    }

    private long AgregarDocumento(string huella)
    {
        new Documentos(conexion).Guardar(
            new(
                $@"C:\docs\{huella}.pdf",
                @"C:\docs",
                $"{huella}.pdf",
                1,
                DateTime.UtcNow,
                huella,
                "ok",
                false,
                []
            )
        );
        return Convert.ToInt64(
            Escalar(conexion, $"SELECT id FROM documentos WHERE ruta='C:\\docs\\{huella}.pdf';")
        );
    }

    private static object? Escalar(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }
}
