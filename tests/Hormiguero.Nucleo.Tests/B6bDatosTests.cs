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
        var cadenas = new RepositorioCadenas(conexion);
        long cadena = cadenas.CrearCadenaSimple("C", DateTime.UtcNow);
        long vagon = cadenas.AgregarDocumentoCadena(cadena, version.Id, "V");
        var reglas = new RepositorioReglasYEnlaces(conexion);
        long enlace = reglas.CrearEnlace(vagon, version.Id, "manual");
        Assert.True(reglas.DeshacerEnlace(enlace));
        Assert.Equal("anulado", Assert.Single(reglas.HistorialEnlaces(vagon)).Estado);
        Assert.True(datos.AnularValor(Assert.Single(datos.BuscarValores(campo, "OC 123")).Id));
        Assert.Equal(
            7L,
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
