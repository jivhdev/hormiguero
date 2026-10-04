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
    public void Observador_publica_sin_modificar_archivo_y_registra_procedencia()
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

    private static object? Escalar(Microsoft.Data.Sqlite.SqliteConnection conexion, string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return comando.ExecuteScalar();
    }
}
