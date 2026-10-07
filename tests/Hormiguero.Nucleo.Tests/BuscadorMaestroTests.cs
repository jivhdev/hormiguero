using Hormiguero.Nucleo.Datos;

namespace Hormiguero.Nucleo.Tests;

public sealed class BuscadorMaestroTests
{
    [Fact]
    public void Busca_por_filtros_normaliza_numero_y_devuelve_datos_de_cadena()
    {
        string carpeta = Path.Combine(
            Path.GetTempPath(),
            "BuscadorMaestro",
            Guid.NewGuid().ToString("N")
        );
        string ruta = Path.Combine(carpeta, "datos.db");
        try
        {
            using var conexion = BaseComun.Abrir(ruta);
            var repo = new RepositorioBuscadorMaestro(conexion);
            var occ = CrearDocumento(
                conexion,
                "OCC",
                "Proveedor Álamo",
                "Recibido",
                "0001-234",
                "Cliente Ñandú",
                "José Pérez",
                "2026-03-14",
                true
            );
            var nvv = CrearDocumento(
                conexion,
                "NVV",
                "Proveedor Sur",
                "Recibido",
                "999",
                "Cliente de la cadena",
                "María",
                "2025-01-01",
                false
            );
            VincularEnCadena(conexion, occ.CadenaId!.Value, nvv.VersionId);
            CrearDocumento(
                conexion,
                "Factura",
                "Proveedor Norte",
                "Emitido",
                "777",
                "Cliente Otro",
                "Elena",
                "2024-01-01",
                false
            );

            Assert.Contains("OCC", repo.Tipos());
            Assert.Single(repo.Buscar(new FiltrosBuscadorMaestro(Numero: "OCC 1234")));
            Assert.Single(repo.Buscar(new FiltrosBuscadorMaestro(Tipo: "OCC")));
            Assert.Single(repo.Buscar(new FiltrosBuscadorMaestro(Emisor: "Proveedor Álamo")));
            Assert.Single(repo.Buscar(new FiltrosBuscadorMaestro(Cliente: "Ñandú")));
            Assert.Single(repo.Buscar(new FiltrosBuscadorMaestro(Encargado: "jose")));
            Assert.Equal(2, repo.Buscar(new FiltrosBuscadorMaestro(Grupo: "Recibido")).Count);
            Assert.Single(
                repo.Buscar(
                    new FiltrosBuscadorMaestro(
                        FechaDesde: new DateTime(2026, 3, 1),
                        FechaHasta: new DateTime(2026, 3, 31)
                    )
                )
            );
            var combinados = Assert.Single(
                repo.Buscar(
                    new FiltrosBuscadorMaestro(
                        "1234",
                        "OCC",
                        "Proveedor Álamo",
                        "Ñandú",
                        "JOSE",
                        "Recibido",
                        new DateTime(2026, 3, 1),
                        new DateTime(2026, 3, 31)
                    )
                )
            );
            Assert.Equal(occ.CadenaId, combinados.CadenaId);
            Assert.Contains(
                repo.ObtenerDatos(combinados.VersionId),
                d => d.Nombre == "Cliente" && d.Valor == "Cliente Ñandú"
            );
            var cadena = Assert.Single(
                repo.ObtenerCadena(occ.CadenaId!.Value),
                d => d.Tipo == "OCC"
            );
            Assert.Contains(
                repo.ObtenerCadena(occ.CadenaId!.Value).SelectMany(d => d.Datos),
                d => d.Valor == "Cliente de la cadena"
            );
            Assert.Contains(cadena.Datos, d => d.Valor == "Cliente Ñandú");
            Assert.Single(repo.Buscar(new FiltrosBuscadorMaestro(Grupo: "Emitido")));
            Assert.Null(
                Assert.Single(repo.Buscar(new FiltrosBuscadorMaestro(Tipo: "Factura"))).CadenaId
            );
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(carpeta))
                Directory.Delete(carpeta, true);
        }
    }

    private static void VincularEnCadena(
        Microsoft.Data.Sqlite.SqliteConnection conexion,
        long cadenaId,
        long versionId
    )
    {
        using var vagon = conexion.CreateCommand();
        vagon.CommandText =
            "INSERT INTO vagones_cadena(cadena_id,orden,nombre,version_id) VALUES($c,1,'NVV',$v) RETURNING id;";
        vagon.Parameters.AddWithValue("$c", cadenaId);
        vagon.Parameters.AddWithValue("$v", versionId);
        long vagonId = Convert.ToInt64(vagon.ExecuteScalar());
        using var enlace = conexion.CreateCommand();
        enlace.CommandText =
            "INSERT INTO enlaces_cadena(vagon_cadena_id,version_id,origen,estado,creada_en) VALUES($vc,$v,'manual','activo',$f);";
        enlace.Parameters.AddWithValue("$vc", vagonId);
        enlace.Parameters.AddWithValue("$v", versionId);
        enlace.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        enlace.ExecuteNonQuery();
    }

    private static DocumentoBuscadorMaestro CrearDocumento(
        Microsoft.Data.Sqlite.SqliteConnection conexion,
        string tipo,
        string emisor,
        string grupo,
        string numero,
        string cliente,
        string encargado,
        string fecha,
        bool enCadena
    )
    {
        long identificacion;
        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText =
                "INSERT INTO identificaciones(tipo,emisor,datos,actualizada,grupo_documento,nombre_estandar) VALUES($t,$e,'{}',$f,$g,$n) RETURNING id;";
            comando.Parameters.AddWithValue("$t", tipo);
            comando.Parameters.AddWithValue("$e", emisor);
            comando.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
            comando.Parameters.AddWithValue("$g", grupo);
            comando.Parameters.AddWithValue("$n", tipo + " · " + emisor);
            identificacion = Convert.ToInt64(comando.ExecuteScalar());
        }
        long documento;
        long version;
        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText =
                "INSERT INTO documentos(ruta,carpeta_raiz,nombre,tamano,modificado,huella,estado,tiene_texto,indexado_en) VALUES($r,'',$n,1,$f,$h,'ok',1,$f) RETURNING id;";
            comando.Parameters.AddWithValue(
                "$r",
                Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf")
            );
            comando.Parameters.AddWithValue("$n", tipo + ".pdf");
            comando.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
            comando.Parameters.AddWithValue("$h", Guid.NewGuid().ToString("N"));
            documento = Convert.ToInt64(comando.ExecuteScalar());
        }
        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText =
                "INSERT INTO versiones_documento(documento_id,huella,ruta_observada,registrada_en) VALUES($d,$h,'',$f) RETURNING id;";
            comando.Parameters.AddWithValue("$d", documento);
            comando.Parameters.AddWithValue("$h", Guid.NewGuid().ToString("N"));
            comando.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
            version = Convert.ToInt64(comando.ExecuteScalar());
        }
        long campo;
        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText =
                "INSERT INTO campos_documento(identificacion_id,nombre,nombre_estable,tipo_dato,origen_lectura,dato_diccionario_id) VALUES($i,'Número','numero','texto','marca','oc_propia') RETURNING id;";
            comando.Parameters.AddWithValue("$i", identificacion);
            campo = Convert.ToInt64(comando.ExecuteScalar());
        }
        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText =
                "INSERT INTO valores_documento(version_id,campo_id,valor_original,valor_clave,origen,fecha_creacion,dato_diccionario_id) VALUES($v,$c,$o,$k,'marca',$f,'oc_propia'); INSERT INTO valores_informativos_documento(version_id,dato,valor,fecha_reconocida,creada_en) VALUES($v,'nombre_cliente',$cliente,NULL,$f),($v,'encargado',$encargado,NULL,$f),($v,'fecha_documento',$fecha,$fecha,$f);";
            comando.Parameters.AddWithValue("$v", version);
            comando.Parameters.AddWithValue("$c", campo);
            comando.Parameters.AddWithValue("$o", numero);
            comando.Parameters.AddWithValue("$k", DiccionarioDatosEnlazantes.ClaveDeEnlace(numero));
            comando.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
            comando.Parameters.AddWithValue("$cliente", cliente);
            comando.Parameters.AddWithValue("$encargado", encargado);
            comando.Parameters.AddWithValue("$fecha", fecha);
            comando.ExecuteNonQuery();
        }
        long? cadena = null;
        if (enCadena)
        {
            using var comando = conexion.CreateCommand();
            comando.CommandText =
                "INSERT INTO cadenas(modelo_id,nombre_modelo_origen,nombre,fecha_creacion,estructura_json) VALUES(NULL,NULL,'Cadena OCC', $f,'{}') RETURNING id;";
            comando.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
            cadena = Convert.ToInt64(comando.ExecuteScalar());
            using var vagon = conexion.CreateCommand();
            vagon.CommandText =
                "INSERT INTO vagones_cadena(cadena_id,orden,nombre,version_id) VALUES($c,0,'OCC',$v) RETURNING id;";
            vagon.Parameters.AddWithValue("$c", cadena);
            vagon.Parameters.AddWithValue("$v", version);
            long vagonId = Convert.ToInt64(vagon.ExecuteScalar());
            using var enlace = conexion.CreateCommand();
            enlace.CommandText =
                "INSERT INTO enlaces_cadena(vagon_cadena_id,version_id,origen,estado,creada_en) VALUES($vc,$v,'manual','activo',$f);";
            enlace.Parameters.AddWithValue("$vc", vagonId);
            enlace.Parameters.AddWithValue("$v", version);
            enlace.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
            enlace.ExecuteNonQuery();
        }
        return new(
            documento,
            version,
            "",
            tipo,
            emisor,
            grupo,
            numero,
            DateTime.Parse(fecha),
            cliente,
            encargado,
            cadena,
            enCadena ? "Cadena OCC" : "",
            []
        );
    }
}
