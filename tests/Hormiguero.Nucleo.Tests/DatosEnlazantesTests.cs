using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Tests;

public sealed class DatosEnlazantesTests : IDisposable
{
    private readonly SqliteConnection conexion = new("Data Source=:memory:");

    public DatosEnlazantesTests()
    {
        conexion.Open();
        using var fk = conexion.CreateCommand();
        fk.CommandText = "PRAGMA foreign_keys=ON;";
        fk.ExecuteNonQuery();
        Migraciones.Aplicar(conexion, Migraciones.Todas);
    }

    public void Dispose() => conexion.Dispose();

    [Fact]
    public void Diccionario_tiene_las_17_entradas_fijas_en_orden_y_la_base_impide_cambios()
    {
        var diccionario = DiccionarioDatosEnlazantes.Todos;
        Assert.Equal(17, diccionario.Count);
        Assert.Equal(17, diccionario.Select(d => d.Id).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 17), diccionario.Select(d => d.Orden));
        Assert.Equal(
            new[] { "Ventas propias", "Del cliente", "Compras propias", "Del proveedor", "Otros" },
            diccionario.Select(d => d.Grupo).Distinct()
        );
        Assert.Equal(diccionario, new RepositorioDatosEnlazantes(conexion).LeerDiccionario());
        Assert.Throws<SqliteException>(() =>
            Ejecutar("UPDATE diccionario_datos SET nombre='otro' WHERE id='oc_cliente';")
        );
        Assert.Throws<SqliteException>(() =>
            Ejecutar("DELETE FROM diccionario_datos WHERE id='oc_cliente';")
        );
    }

    [Fact]
    public void Migracion_v9_conserva_datos_existentes_y_se_puede_repetir()
    {
        using var v8 = new SqliteConnection("Data Source=:memory:");
        v8.Open();
        Migraciones.Aplicar(v8, Migraciones.Todas.Take(8).ToArray());
        EjecutarEn(
            v8,
            "INSERT INTO identificaciones(tipo,emisor,datos,actualizada) VALUES('Factura','Emisor','{}','ahora');"
                + "INSERT INTO documentos(ruta,carpeta_raiz,nombre,tamano,modificado,estado,tiene_texto,indexado_en) VALUES('r','c','n',1,'f','ok',0,'f');"
                + "INSERT INTO versiones_documento(documento_id,huella,ruta_observada,registrada_en) VALUES(1,'h','r','f');"
                + "INSERT INTO campos_documento(identificacion_id,nombre,nombre_estable,tipo_dato,origen_lectura) VALUES(1,'OC','oc','texto','marca');"
                + "INSERT INTO valores_documento(version_id,campo_id,valor_original,valor_clave,origen,fecha_creacion) VALUES(1,1,'123','123','pdf','f');"
        );
        Migraciones.Aplicar(v8, Migraciones.Todas);
        Migraciones.Aplicar(v8, Migraciones.Todas);
        Assert.Equal(17L, Escalar(v8, "SELECT COUNT(*) FROM diccionario_datos;"));
        Assert.Equal("Factura", EscalarTexto(v8, "SELECT tipo FROM identificaciones WHERE id=1;"));
        Assert.Equal(
            "Recibido",
            EscalarTexto(v8, "SELECT grupo_documento FROM identificaciones WHERE id=1;")
        );
        Assert.Equal(
            "Factura · Emisor",
            EscalarTexto(v8, "SELECT nombre_estandar FROM identificaciones WHERE id=1;")
        );
        Assert.Equal(
            "123",
            EscalarTexto(v8, "SELECT valor_original FROM valores_documento WHERE id=1;")
        );
        Assert.Equal(
            0L,
            Escalar(
                v8,
                "SELECT COUNT(*) FROM valores_documento WHERE dato_diccionario_id IS NOT NULL;"
            )
        );
        Assert.Equal(10L, Escalar(v8, "SELECT MAX(version) FROM migraciones;"));
    }

    [Fact]
    public void Edita_clasificacion_y_nombre_y_admite_varios_disenos_anulables()
    {
        long tipo = new Identificaciones(conexion).Guardar(new(0, "Factura", "Proveedor", "{}"));
        var ident = new Identificaciones(conexion);
        Assert.True(ident.ConfigurarTipoDocumento(tipo, "Emitido", "Factura · Proveedor ajustado"));
        Assert.Equal(
            ("Emitido", "Factura · Proveedor ajustado"),
            (ident.Listar()[0].GrupoDocumento, ident.Listar()[0].NombreEstandar)
        );
        var repo = new RepositorioDatosEnlazantes(conexion);
        long a = repo.GuardarDatoTipo(
            new(0, tipo, "oc_cliente", "pdf-a", null, 1, 0.1, 0.2, 0.3, 0.04, true)
        );
        long b = repo.GuardarDatoTipo(
            new(0, tipo, "oc_cliente", "pdf-b", null, 2, 0.5, 0.6, 0.2, 0.03, true)
        );
        Assert.Equal(new[] { "pdf-a", "pdf-b" }, repo.ListarDatosTipo(tipo).Select(d => d.Diseno));
        Assert.True(repo.AnularDatoTipo(a));
        Assert.Single(repo.ListarDatosTipo(tipo));
        Assert.Equal(2, repo.ListarDatosTipo(tipo, incluirAnulados: true).Count);
        Assert.Equal(
            a,
            repo.ListarDatosTipo(tipo, incluirAnulados: true).Single(d => !d.Activo).Id
        );
        Assert.True(repo.AnularDatoTipo(b));

        // D-73: volver a marcar un dato quitado en el mismo diseño lo reactiva con la zona nueva.
        long otraVez = repo.GuardarDatoTipo(
            new(0, tipo, "oc_cliente", "pdf-a", null, 1, 0.7, 0.8, 0.1, 0.02, true)
        );
        Assert.Equal(a, otraVez);
        var reactivado = Assert.Single(repo.ListarDatosTipo(tipo));
        Assert.Equal((0.7, 0.8), (reactivado.X, reactivado.Y));
    }

    [Fact]
    public void Busca_documentos_por_dato_y_valor_indexado()
    {
        Ejecutar(
            "INSERT INTO documentos(ruta,carpeta_raiz,nombre,tamano,modificado,estado,tiene_texto,indexado_en) VALUES('C:/a.pdf','C:/','a.pdf',1,'f','ok',1,'f');"
        );
        Ejecutar(
            "INSERT INTO versiones_documento(documento_id,huella,ruta_observada,registrada_en) VALUES(1,'h','C:/a.pdf','f');"
        );
        long identificacion = new Identificaciones(conexion).Guardar(
            new(0, "Factura", "Proveedor", "{}")
        );
        long campo = new RepositorioDocumentosDatos(conexion).GuardarCampo(
            new(0, identificacion, "OC cliente", "oc_cliente", "texto", true, "marca", "oc_cliente")
        );
        var datos = new RepositorioDocumentosDatos(conexion);
        datos.GuardarValor(1, campo, " 4500 ", "4500", "pdf");
        var repo = new RepositorioDatosEnlazantes(conexion);
        Assert.Equal("C:/a.pdf", Assert.Single(repo.BuscarDocumentos("oc_cliente", "4500")).Ruta);
        var valorId = Convert.ToInt64(Escalar(conexion, "SELECT id FROM valores_documento;"));
        Assert.True(datos.AnularValor(valorId));
        Assert.Empty(repo.BuscarDocumentos("oc_cliente", "4500"));
    }

    private void Ejecutar(string sql) => EjecutarEn(conexion, sql);

    private static void EjecutarEn(SqliteConnection db, string sql)
    {
        using var comando = db.CreateCommand();
        comando.CommandText = sql;
        comando.ExecuteNonQuery();
    }

    private static long Escalar(SqliteConnection db, string sql)
    {
        using var comando = db.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private static string EscalarTexto(SqliteConnection db, string sql)
    {
        using var comando = db.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToString(comando.ExecuteScalar())!;
    }
}
