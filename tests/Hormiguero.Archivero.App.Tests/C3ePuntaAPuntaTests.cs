using Archivero.Datos;
using Archivero.Servicios;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Archivero.Tests;

public sealed class C3ePuntaAPuntaTests
{
    [Fact]
    public async Task Publicacion_usa_lugares_del_diccionario_y_enlaces_declarados()
    {
        string raiz = Path.Combine(
            Path.GetTempPath(),
            "ArchiveroTests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(raiz);
        string? datosAnterior = Environment.GetEnvironmentVariable("HORMIGUERO_DATOS");
        Environment.SetEnvironmentVariable("HORMIGUERO_DATOS", Path.Combine(raiz, "datos"));
        try
        {
            ConfigurarTipo("Empresa", "oc_propia", "Emitido", ["oc_propia", "rut_proveedor"]);
            ConfigurarTipo(
                "PROVEEDOR Q2",
                "guia_proveedor",
                "Recibido",
                ["guia_proveedor", "oc_propia"]
            );
            using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
            var esquema = new RepositorioEsquemasDiccionario(conexion);
            esquema.Guardar(
                "PROVEEDOR Q2",
                "Compra",
                [
                    new(0, "oc_propia", true, "OC propia"),
                    new(1, "guia_proveedor", false, "Guía del proveedor"),
                ],
                [new(0, 1, "oc_propia", "uno_a_varios")]
            );
            long oc = await Publicar(
                "q2-oc.pdf",
                "Empresa",
                DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "oc_propia").EtiquetaTipo,
                "Emitido",
                ("oc_propia", "Q2-900"),
                ("rut_proveedor", "PROVEEDOR Q2")
            );
            long guia1 = await Publicar(
                "q2-guia-1.pdf",
                "PROVEEDOR Q2",
                DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "guia_proveedor").EtiquetaTipo,
                "Recibido",
                ("guia_proveedor", "Q2-G1"),
                ("oc_propia", "Q2-900")
            );
            long guia2 = await Publicar(
                "q2-guia-2.pdf",
                "PROVEEDOR Q2",
                DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == "guia_proveedor").EtiquetaTipo,
                "Recibido",
                ("guia_proveedor", "Q2-G2"),
                ("oc_propia", "Q2-900")
            );
            Assert.Equal(
                3,
                new MotorCadenas(conexion).Procesar().Count(r => r.Estado == "ya_procesado")
            );
            using var consulta = conexion.CreateCommand();
            consulta.CommandText =
                "SELECT COUNT(*) FROM ubicaciones_cadena_diccionario WHERE cadena_id=(SELECT cadena_id FROM ubicaciones_cadena_diccionario WHERE version_id=$v) AND version_id IN ($g1,$g2) AND padre_id IS NOT NULL;";
            consulta.Parameters.AddWithValue("$v", oc);
            consulta.Parameters.AddWithValue("$g1", guia1);
            consulta.Parameters.AddWithValue("$g2", guia2);
            Assert.Equal(2L, Convert.ToInt64(consulta.ExecuteScalar()));
            Assert.Equal(
                2L,
                Convert.ToInt64(
                    EsquemaCount(
                        conexion,
                        "SELECT COUNT(DISTINCT linea_id) FROM ubicaciones_cadena_diccionario WHERE version_id IN ($g1,$g2);",
                        guia1,
                        guia2
                    )
                )
            );
        }
        finally
        {
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

    private static long EsquemaCount(SqliteConnection conexion, string sql, long guia1, long guia2)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        comando.Parameters.AddWithValue("$g1", guia1);
        comando.Parameters.AddWithValue("$g2", guia2);
        return Convert.ToInt64(comando.ExecuteScalar());
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
