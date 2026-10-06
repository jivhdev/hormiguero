using Archivero.Datos;
using Archivero.Servicios;
using Archivero.Vistas;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Archivero.Tests;

public sealed class DatosEnlazantesConfiguracionServiceTests
{
    [Fact]
    public void Configuracion_GuardaGrupoYNombreEstandarParaLaLista()
    {
        string anterior = BaseDeDatos.RutaArchivo;
        string raiz = Path.Combine(Path.GetTempPath(), $"Archivero-config-{Guid.NewGuid():N}");
        BaseDeDatos.RutaArchivo = Path.Combine(raiz, "archivero.db");
        try
        {
            BaseDeDatos.AsegurarEsquema();
            var repo = new ConfiguracionDocumentoRepository();
            int id = repo.GuardarNueva(
                "Cliente",
                "Factura",
                raiz,
                FormatoCarpeta.Directo,
                null,
                false,
                []
            );
            repo.ActualizarTipoDocumento(id, "Recibido", "Factura · Cliente Uno");
            var config = Assert.Single(repo.ObtenerTodas());
            var fila = new FilaClasificacion(config, true);
            Assert.Equal("Recibidos", fila.GrupoDocumento);
            Assert.Equal("Factura · Cliente Uno", fila.NombreEstandar);
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
    public void DatoPorDiseno_SePuedeEditarAnularYVolverAMarcarSinBorrarHistorial()
    {
        string? anterior = Environment.GetEnvironmentVariable("HORMIGUERO_DATOS");
        string raiz = Path.Combine(Path.GetTempPath(), $"Archivero-datos-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable("HORMIGUERO_DATOS", raiz);
        try
        {
            using (BaseComun.Abrir(DocumentosGuardados.RutaBaseComun)) { }
            var definicion = Assert.Single(
                DiccionarioDatosEnlazantes.Todos,
                d => d.Id == "oc_cliente"
            );
            var datos = new[]
            {
                new DatoEnlazanteConfigurado(
                    definicion.Id,
                    definicion.Nombre,
                    definicion.Grupo,
                    true,
                    true,
                    0,
                    0.1,
                    0.2,
                    0.3,
                    0.1,
                    ""
                ),
            };

            DatosEnlazantesConfiguracionService.Guardar(
                "Cliente",
                "Factura",
                "Recibido",
                "Factura de prueba",
                7,
                datos
            );
            var leidos = DatosEnlazantesConfiguracionService.Leer("Cliente", "Factura", 7);
            var datoLeido = Assert.Single(leidos, d => d.Id == definicion.Id);
            Assert.True(datoLeido.Incluido);
            Assert.Equal(0.1, datoLeido.X);
            Assert.Equal(
                "Recibido",
                Assert.Single(LeerIdentificacion(), i => i.Tipo == "Factura").GrupoDocumento
            );

            var editado = datos[0] with { X = 0.4, TextoLeido = "45001" };
            DatosEnlazantesConfiguracionService.Guardar(
                "Cliente",
                "Factura",
                "Recibido",
                "Factura de prueba",
                7,
                [editado]
            );
            var anulacion = editado with { Incluido = false, Marcado = false };
            DatosEnlazantesConfiguracionService.Guardar(
                "Cliente",
                "Factura",
                "Recibido",
                "Factura de prueba",
                7,
                [anulacion]
            );
            var errorRemarcado = Assert.Throws<InvalidOperationException>(() =>
                DatosEnlazantesConfiguracionService.Guardar(
                    "Cliente",
                    "Factura",
                    "Recibido",
                    "Factura de prueba",
                    7,
                    [editado]
                )
            );
            Assert.Contains("no permite reactivar", errorRemarcado.Message);
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            var identificacion = Assert.Single(
                new Identificaciones(conexion).Listar(),
                i => i.Tipo == "Factura"
            );
            var historial = new RepositorioDatosEnlazantes(conexion).ListarDatosTipo(
                identificacion.Id,
                "7",
                incluirAnulados: true
            );
            Assert.Single(historial);
            Assert.Contains(historial, d => !d.Activo);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HORMIGUERO_DATOS", anterior);
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(raiz))
                Directory.Delete(raiz, recursive: true);
        }
    }

    [Fact]
    public async Task Publicar_UsaElIdentificadorDelDiccionarioEnElValor()
    {
        string? anterior = Environment.GetEnvironmentVariable("HORMIGUERO_DATOS");
        string raiz = Path.Combine(Path.GetTempPath(), $"Archivero-publicacion-{Guid.NewGuid():N}");
        Directory.CreateDirectory(raiz);
        Environment.SetEnvironmentVariable("HORMIGUERO_DATOS", Path.Combine(raiz, "datos"));
        string archivo = Path.Combine(raiz, "factura.pdf");
        File.WriteAllText(archivo, "PDF sintético para publicación");
        try
        {
            using (BaseComun.Abrir(DocumentosGuardados.RutaBaseComun)) { }
            var diccionario = Assert.Single(
                DiccionarioDatosEnlazantes.Todos,
                d => d.Id == "oc_cliente"
            );
            DatosEnlazantesConfiguracionService.Guardar(
                "Cliente",
                "Factura",
                "Emitido",
                "Factura · Cliente",
                3,
                [
                    new(
                        diccionario.Id,
                        diccionario.Nombre,
                        diccionario.Grupo,
                        true,
                        true,
                        0,
                        0.1,
                        0.2,
                        0.3,
                        0.1,
                        ""
                    ),
                ]
            );
            var config = new ConfiguracionDocumento
            {
                Emisor = "Cliente",
                Tipo = "Factura",
                CarpetaDestino = raiz,
                FormatoCarpeta = FormatoCarpeta.Directo,
                Renombrar = false,
                GrupoDocumento = "Emitido",
                NombreEstandar = "Factura · Cliente",
                Patrones = [new(3, [])],
            };
            PublicadorDatosDocumentoService.Publicar(
                archivo,
                config,
                [new(diccionario.Nombre, diccionario.Id, "45001", "45001", "marca")],
                "marca"
            );
            await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);

            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            using var comando = conexion.CreateCommand();
            comando.CommandText =
                "SELECT dato_diccionario_id FROM valores_documento WHERE valor_original='45001';";
            Assert.Equal(diccionario.Id, comando.ExecuteScalar());
        }
        finally
        {
            Environment.SetEnvironmentVariable("HORMIGURO_DATOS", anterior);
            SqliteConnection.ClearAllPools();
            Directory.Delete(raiz, recursive: true);
        }
    }

    private static IReadOnlyList<Identificacion> LeerIdentificacion()
    {
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        return new Identificaciones(conexion).Listar();
    }
}
