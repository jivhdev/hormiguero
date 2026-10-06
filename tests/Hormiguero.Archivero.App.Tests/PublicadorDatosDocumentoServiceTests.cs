using Archivero.Datos;
using Archivero.Servicios;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Utilidades;
using Microsoft.Data.Sqlite;

namespace Archivero.Tests;

public sealed class PublicadorDatosDocumentoServiceTests
{
    [Fact]
    public void Configuracion_admite_campos_propios_con_zona_de_lectura()
    {
        string anterior = BaseDeDatos.RutaArchivo;
        string raiz = Path.Combine(
            Path.GetTempPath(),
            "ArchiveroTests",
            Guid.NewGuid().ToString("N")
        );
        BaseDeDatos.RutaArchivo = Path.Combine(raiz, "archivero.db");
        try
        {
            BaseDeDatos.AsegurarEsquema();
            var repo = new ConfiguracionDocumentoRepository();
            int id = repo.GuardarNueva(
                "Emisor",
                "Factura",
                raiz,
                FormatoCarpeta.Directo,
                null,
                false,
                []
            );
            repo.GuardarCamposPropios(
                id,
                [new("Orden de compra", "orden_compra", 0, 0.1, 0.2, 0.3, 0.1)]
            );
            var campo = Assert.Single(
                repo.BuscarPorEmisorYTipo("Emisor", "Factura")!.CamposPropios
            );
            Assert.Equal("orden_compra", campo.NombreEstable);
            Assert.Equal(0.3, campo.Ancho);
        }
        finally
        {
            BaseDeDatos.RutaArchivo = anterior;
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(raiz))
                Directory.Delete(raiz, recursive: true);
        }
    }

    [Fact]
    public async Task Observador_publica_sin_modificar_archivo_y_registra_procedencia()
    {
        string raiz = Path.Combine(
            Path.GetTempPath(),
            "ArchiveroTests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(raiz);
        string archivo = Path.Combine(raiz, "observado.pdf");
        string original = "contenido de prueba Ñ 000123";
        File.WriteAllText(archivo, original);
        DateTime fecha = File.GetLastWriteTimeUtc(archivo);
        string huella = Huella.Calcular(archivo);
        string? carpetaAnterior = Environment.GetEnvironmentVariable("HORMIGUERO_DATOS");
        Environment.SetEnvironmentVariable("HORMIGUERO_DATOS", Path.Combine(raiz, "datos"));
        try
        {
            var config = new ConfiguracionDocumento
            {
                Emisor = "Emisor Ñ",
                Tipo = "Factura",
                CarpetaDestino = raiz,
                FormatoCarpeta = FormatoCarpeta.Directo,
                Renombrar = false,
                Patrones = [],
            };
            PublicadorDatosDocumentoService.PublicarObservado(
                archivo,
                config,
                [new("Orden", "orden", "000123", "000123", "observador")]
            );
            await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);
            Assert.Equal(original, File.ReadAllText(archivo));
            Assert.Equal(huella, Huella.Calcular(archivo));
            Assert.Equal(fecha, File.GetLastWriteTimeUtc(archivo));
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            Assert.Equal(
                "observador",
                Convert.ToString(
                    Escalar(
                        conexion,
                        "SELECT origen FROM auditoria WHERE accion='publicar_documento' ORDER BY id DESC LIMIT 1;"
                    )
                )
            );
            long campo = Convert.ToInt64(
                Escalar(conexion, "SELECT id FROM campos_documento WHERE nombre_estable='orden';")
            );
            Assert.Equal(
                "000123",
                Convert.ToString(
                    Escalar(
                        conexion,
                        $"SELECT valor_original FROM valores_documento WHERE campo_id={campo};"
                    )
                )
            );
            Assert.Equal(
                5L,
                Convert.ToInt64(Escalar(conexion, "SELECT COUNT(*) FROM campos_documento;"))
            );
        }
        finally
        {
            Environment.SetEnvironmentVariable("HORMIGURO_DATOS", carpetaAnterior);
            SqliteConnection.ClearAllPools();
            Directory.Delete(raiz, recursive: true);
        }
    }

    [Fact]
    public async Task Publicacion_desde_archivero_dispara_el_enlace_de_cadena_simple()
    {
        string raiz = Path.Combine(
            Path.GetTempPath(),
            "ArchiveroTests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(raiz);
        string basePath = Path.Combine(raiz, "base.pdf");
        string nuevaPath = Path.Combine(raiz, "nueva.pdf");
        File.WriteAllText(basePath, "base");
        File.WriteAllText(nuevaPath, "nuevo");
        string? carpetaAnterior = Environment.GetEnvironmentVariable("HORMIGUERO_DATOS");
        Environment.SetEnvironmentVariable("HORMIGUERO_DATOS", Path.Combine(raiz, "datos"));
        try
        {
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            long identificacion = new Identificaciones(conexion).Guardar(
                new(0, "Factura", "Emisor", "{}")
            );
            var documentos = new RepositorioDocumentosDatos(conexion);
            long campo = documentos.GuardarCampo(
                new(0, identificacion, "OC", "oc_cliente", "texto", true, "marca", "oc_cliente")
            );
            new RepositorioDatosEnlazantes(conexion).GuardarDatoTipo(
                new(0, identificacion, "oc_cliente", "diseño", campo, 1, 0.1, 0.1, 0.2, 0.1, true)
            );
            var info = new FileInfo(basePath);
            var publicadoBase = documentos.PublicarDocumento(
                basePath,
                info.Length,
                info.LastWriteTimeUtc,
                Huella.Calcular(basePath),
                "Emisor",
                "Factura",
                [new("OC", "oc_cliente", "123", "123", "marca")]
            );
            long cadena = new RepositorioCadenas(conexion).CrearCadenaSimple(
                "Cadena",
                DateTime.Now
            );
            long vagon = new RepositorioCadenas(conexion).AgregarDocumentoCadena(
                cadena,
                publicadoBase.Version.Id,
                "Base"
            );
            new RepositorioReglasYEnlaces(conexion).CrearEnlace(
                vagon,
                publicadoBase.Version.Id,
                "manual"
            );

            var configuracion = new ConfiguracionDocumento
            {
                Emisor = "Emisor",
                Tipo = "Factura",
                CarpetaDestino = raiz,
                FormatoCarpeta = FormatoCarpeta.Directo,
                Renombrar = false,
                Patrones = [],
            };
            PublicadorDatosDocumentoService.PublicarObservado(
                nuevaPath,
                configuracion,
                [new("OC", "oc_cliente", "123", "123", "observador")]
            );
            await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);

            Assert.Equal(
                1L,
                Convert.ToInt64(
                    Escalar(
                        conexion,
                        "SELECT COUNT(*) FROM enlaces_cadena WHERE origen='automatico' AND estado='activo';"
                    )
                )
            );
            Assert.Equal(
                2L,
                Convert.ToInt64(
                    Escalar(
                        conexion,
                        "SELECT COUNT(*) FROM vagones_cadena WHERE cadena_id="
                            + cadena
                            + " AND estado='activo';"
                    )
                )
            );
        }
        finally
        {
            Environment.SetEnvironmentVariable("HORMIGUERO_DATOS", carpetaAnterior);
            SqliteConnection.ClearAllPools();
            Directory.Delete(raiz, recursive: true);
        }
    }

    private static object? Escalar(Microsoft.Data.Sqlite.SqliteConnection conexion, string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return comando.ExecuteScalar();
    }
}
