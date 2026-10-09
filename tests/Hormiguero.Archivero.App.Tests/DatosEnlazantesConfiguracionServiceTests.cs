using Archivero.Datos;
using Archivero.Servicios;
using Archivero.Vistas;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Archivero.Tests;

public sealed class DatosEnlazantesConfiguracionServiceTests
{
    [Fact]
    public void Guarda_tipo_derivado_y_exige_dato_identificador()
    {
        string? anterior = Environment.GetEnvironmentVariable("HORMIGUERO_DATOS");
        string raiz = Path.Combine(
            Path.GetTempPath(),
            $"Archivero-identificador-{Guid.NewGuid():N}"
        );
        Environment.SetEnvironmentVariable("HORMIGUERO_DATOS", raiz);
        try
        {
            using (BaseComun.Abrir(DocumentosGuardados.RutaBaseComun)) { }
            var dato = Assert.Single(
                DiccionarioDatosEnlazantes.Todos,
                d => d.Id == "factura_proveedor"
            );
            var sinIdentificador = new DatoEnlazanteConfigurado(
                dato.Id,
                dato.Nombre,
                dato.Grupo,
                true,
                true,
                0,
                0.1,
                0.1,
                0.2,
                0.1,
                "",
                true,
                false
            );
            Assert.Throws<InvalidOperationException>(() =>
                DatosEnlazantesConfiguracionService.Guardar(
                    "Proveedor",
                    "Factura del proveedor",
                    "Recibido",
                    "Factura del proveedor · Proveedor",
                    1,
                    [sinIdentificador]
                )
            );
            DatosEnlazantesConfiguracionService.Guardar(
                "Proveedor",
                "Factura del proveedor",
                "Recibido",
                "Factura del proveedor · Proveedor",
                1,
                [sinIdentificador with { DefineTipo = true }]
            );
            var guardada = Assert.Single(LeerIdentificacion());
            Assert.Equal("Factura del proveedor", guardada.Tipo);
            Assert.Equal("Factura del proveedor · Proveedor", guardada.NombreEstandar);
            Assert.Equal(
                "Factura del proveedor · Proveedor",
                DiccionarioDatosEnlazantes.NombreEstandar(dato.Id, "Proveedor")
            );
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
            var identificador = Assert.Single(
                DiccionarioDatosEnlazantes.Todos,
                d => d.Id == "factura_proveedor"
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
                    "",
                    true,
                    false
                ),
                new DatoEnlazanteConfigurado(
                    identificador.Id,
                    identificador.Nombre,
                    identificador.Grupo,
                    true,
                    true,
                    0,
                    0.5,
                    0.2,
                    0.3,
                    0.1,
                    "",
                    true,
                    true
                ),
            };

            DatosEnlazantesConfiguracionService.Guardar(
                "Cliente",
                "Factura del proveedor",
                "Recibido",
                "Factura de prueba",
                7,
                datos
            );
            var leidos = DatosEnlazantesConfiguracionService.Leer(
                "Cliente",
                "Factura del proveedor",
                7
            );
            var datoLeido = Assert.Single(leidos, d => d.Id == definicion.Id);
            Assert.True(datoLeido.Incluido);
            Assert.Equal(0.1, datoLeido.X);
            Assert.Equal(
                "Recibido",
                Assert
                    .Single(LeerIdentificacion(), i => i.Tipo == "Factura del proveedor")
                    .GrupoDocumento
            );

            var editado = datos[0] with { X = 0.4, TextoLeido = "45001" };
            DatosEnlazantesConfiguracionService.Guardar(
                "Cliente",
                "Factura del proveedor",
                "Recibido",
                "Factura de prueba",
                7,
                [editado, datos[1]]
            );
            var anulacion = editado with { Incluido = false, Marcado = false };
            DatosEnlazantesConfiguracionService.Guardar(
                "Cliente",
                "Factura del proveedor",
                "Recibido",
                "Factura de prueba",
                7,
                [anulacion, datos[1]]
            );
            var errorRemarcado = Assert.Throws<InvalidOperationException>(() =>
                DatosEnlazantesConfiguracionService.Guardar(
                    "Cliente",
                    "Factura del proveedor",
                    "Recibido",
                    "Factura de prueba",
                    7,
                    [editado, datos[1]]
                )
            );
            Assert.Contains("no permite reactivar", errorRemarcado.Message);
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            var identificacion = Assert.Single(
                new Identificaciones(conexion).Listar(),
                i => i.Tipo == "Factura del proveedor"
            );
            var historial = new RepositorioDatosEnlazantes(conexion).ListarDatosTipo(
                identificacion.Id,
                "7",
                incluirAnulados: true
            );
            Assert.Single(historial, d => d.DatoId == definicion.Id && !d.Activo);
            Assert.Contains(historial, d => d.DatoId == identificador.Id && d.Activo);
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
                "OC del cliente",
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
                        "",
                        true,
                        true
                    ),
                ]
            );
            var config = new ConfiguracionDocumento
            {
                Emisor = "Cliente",
                Tipo = "OC del cliente",
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

            PublicadorDatosDocumentoService.PublicarGuardado(
                archivo,
                config,
                new CamposExtraidos(null, null),
                "Proveedor manual"
            );
            await (PublicadorDatosDocumentoService.UltimaRevisionEnlaces ?? Task.CompletedTask);
            comando.CommandText =
                "SELECT origen FROM valores_documento WHERE dato_diccionario_id='nombre_proveedor' AND valor_original='Proveedor manual';";
            Assert.Equal("manual", comando.ExecuteScalar());
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
