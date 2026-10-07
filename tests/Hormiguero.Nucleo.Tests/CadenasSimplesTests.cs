using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Tests;

public sealed class CadenasSimplesTests : IDisposable
{
    private readonly SqliteConnection _conexion = new("Data Source=:memory:");

    public CadenasSimplesTests()
    {
        _conexion.Open();
        Migraciones.Aplicar(_conexion, Migraciones.Todas);
    }

    public void Dispose() => _conexion.Dispose();

    [Fact]
    public void Cadena_simple_admite_edicion_orden_y_anulacion_con_historial()
    {
        long v1 = CrearVersion("uno.pdf", "uno", "1");
        long v2 = CrearVersion("dos.pdf", "dos", "2");
        long cadena = new RepositorioCadenas(_conexion).CrearCadenaSimple("Cadena", DateTime.Now);
        var repo = new RepositorioCadenas(_conexion);
        var enlaces = new RepositorioReglasYEnlaces(_conexion);
        long uno = repo.AgregarDocumentoCadena(cadena, v1, "Uno");
        enlaces.CrearEnlace(uno, v1, "manual");
        long dos = repo.AgregarDocumentoCadena(cadena, v2, "Dos");
        enlaces.CrearEnlace(dos, v2, "manual");

        repo.ReordenarDocumentoCadena(dos, -1);
        repo.RenombrarCadena(cadena, "Cadena editada");
        Assert.Equal("Cadena editada", Assert.Single(repo.ListarCadenasSimples()).Nombre);
        Assert.Equal(
            new[] { "Dos", "Uno" },
            repo.ListarDocumentosCadena(cadena).Select(v => v.Nombre)
        );
        Assert.True(enlaces.DeshacerEnlace(Assert.Single(enlaces.HistorialEnlaces(uno)).Id));
        Assert.DoesNotContain(enlaces.HistorialEnlaces(uno), e => e.Estado == "activo");
        repo.QuitarDocumentoCadena(dos);
        Assert.Single(repo.ListarDocumentosCadena(cadena));
    }

    [Fact]
    public void Asistencia_agrupa_documentos_por_dato_compartido()
    {
        long uno = CrearVersion("uno.pdf", "uno", "4500012345");
        long dos = CrearVersion("dos.pdf", "dos", "4500012345");
        var sugerencias = new RepositorioDatosEnlazantes(_conexion).SugerirParaDocumento(uno);
        var sugerencia = Assert.Single(sugerencias);
        Assert.Equal("oc_cliente", sugerencia.Dato.Id);
        Assert.Equal("4500012345", sugerencia.Valor);
        Assert.Equal(dos, Assert.Single(sugerencia.Documentos).VersionId);
    }

    [Fact]
    public void Enlace_automatico_se_agrega_una_sola_vez_y_se_puede_deshacer()
    {
        long baseVersion = CrearVersion("base.pdf", "base", "123");
        long nuevaVersion = CrearVersion("nueva.pdf", "nueva", "123");
        long cadena = CrearCadenaConDocumento(baseVersion);
        var motor = new MotorCadenasSimples(_conexion);

        Assert.Equal(1, motor.Procesar(nuevaVersion));
        Assert.Equal(0, motor.Procesar(nuevaVersion));
        var repo = new RepositorioCadenas(_conexion);
        var vagones = repo.ListarDocumentosCadena(cadena);
        Assert.Equal(2, vagones.Count);
        var enlace = Assert.Single(
            new RepositorioReglasYEnlaces(_conexion).HistorialEnlaces(vagones[1].Id)
        );
        Assert.Equal("automatico", enlace.Origen);
        Assert.True(new RepositorioReglasYEnlaces(_conexion).DeshacerEnlace(enlace.Id));
        Assert.Equal(
            "anulado",
            Assert
                .Single(new RepositorioReglasYEnlaces(_conexion).HistorialEnlaces(vagones[1].Id))
                .Estado
        );
    }

    [Fact]
    public void Dato_no_enlazable_no_participa_en_enlace_automatico()
    {
        long baseVersion = CrearVersion("base.pdf", "base", "123");
        long nuevaVersion = CrearVersion("nueva.pdf", "nueva", "123");
        CrearCadenaConDocumento(baseVersion);
        using (var cmd = _conexion.CreateCommand())
        {
            cmd.CommandText =
                "UPDATE tipos_documento_datos SET enlazable=0 WHERE dato_diccionario_id='oc_cliente';";
            cmd.ExecuteNonQuery();
        }
        Assert.Equal(0, new MotorCadenasSimples(_conexion).Procesar(nuevaVersion));
        Assert.Empty(new RepositorioReglasYEnlaces(_conexion).ListarDudosos());
    }

    [Fact]
    public void Varias_cadenas_crean_dudoso_y_sin_coincidencia_no_hacen_nada()
    {
        long base1 = CrearVersion("base1.pdf", "base1", "123");
        long base2 = CrearVersion("base2.pdf", "base2", "123");
        long nueva = CrearVersion("nueva.pdf", "nueva", "123");
        CrearCadenaConDocumento(base1);
        CrearCadenaConDocumento(base2);

        Assert.Equal(0, new MotorCadenasSimples(_conexion).Procesar(nueva));
        var dudoso = Assert.Single(new RepositorioReglasYEnlaces(_conexion).ListarDudosos());
        Assert.Contains("Coincide con 2 cadenas por N\u00b0 OC del cliente 123", dudoso.Motivo);
        Assert.Equal(
            0,
            new MotorCadenasSimples(_conexion).Procesar(CrearVersion("sin.pdf", "sin", "otro"))
        );
    }

    [Fact]
    public void Dudoso_asistido_se_enlaza_a_la_cadena_elegida()
    {
        long base1 = CrearVersion("uno.pdf", "uno", "789");
        long base2 = CrearVersion("dos.pdf", "dos", "789");
        long nueva = CrearVersion("nueva.pdf", "nueva", "789");
        var repo = new RepositorioCadenas(_conexion);
        long primera = CrearCadenaConDocumento(base1);
        CrearCadenaConDocumento(base2);
        new MotorCadenasSimples(_conexion).Procesar(nueva);
        var servicio = new Buscadero.Core.Lineas.ServicioCadenasSimples(_conexion);
        var dudoso = Assert.Single(servicio.DudososCadenasSimples());

        servicio.VincularDudosoA(dudoso.EnlaceId, primera);

        var vagonEnlazado = Assert.Single(
            repo.ListarDocumentosCadena(primera),
            v => v.VersionId == nueva
        );
        Assert.DoesNotContain(servicio.Dudosos(), e => e.Id == dudoso.EnlaceId);
        Assert.Contains(
            new RepositorioReglasYEnlaces(_conexion).HistorialEnlaces(vagonEnlazado.Id),
            e => e.Estado == "activo"
        );
    }

    [Fact]
    public void Servicio_crea_con_sugerencia_busca_por_numero_quita_y_reordena()
    {
        long uno = CrearVersion("primero.pdf", "primero", "4500012345");
        long dos = CrearVersion("segundo.pdf", "segundo", "4500012345");
        var servicio = new Buscadero.Core.Lineas.ServicioCadenasSimples(_conexion);
        var disponibles = servicio.BuscarDocumentos("4500012345");
        Assert.Equal(2, disponibles.Count);
        Assert.Contains(
            servicio.Sugerir(uno),
            c => c.Dato.Id == "oc_cliente" && c.Documentos.Any(d => d.VersionId == dos)
        );
        long cadena = servicio.CrearCadena("primero.pdf");
        long v1 = servicio.AgregarDocumento(cadena, uno, "Primero");
        long v2 = servicio.AgregarDocumento(cadena, dos, "Segundo");

        servicio.MoverDocumento(v2, -1);
        Assert.Equal(["Segundo", "Primero"], servicio.Documentos(cadena).Select(v => v.Nombre));
        servicio.QuitarDocumento(v1);
        Assert.Single(servicio.Documentos(cadena));
        Assert.Single(servicio.BuscarCadenas("4500012345"));
    }

    private long CrearCadenaConDocumento(long versionId)
    {
        var cadenas = new RepositorioCadenas(_conexion);
        long cadena = cadenas.CrearCadenaSimple("Cadena", DateTime.Now);
        long vagon = cadenas.AgregarDocumentoCadena(cadena, versionId, "Documento base");
        new RepositorioReglasYEnlaces(_conexion).CrearEnlace(vagon, versionId, "manual");
        return cadena;
    }

    private long CrearVersion(string nombre, string campoNombre, string valor)
    {
        using var cmd = _conexion.CreateCommand();
        cmd.CommandText =
            "INSERT INTO documentos(ruta,carpeta_raiz,nombre,tamano,modificado,estado,tiene_texto,indexado_en) VALUES($r,'/',$n,1,'ahora','ok',1,'ahora') RETURNING id;";
        cmd.Parameters.AddWithValue("$r", "/" + nombre);
        cmd.Parameters.AddWithValue("$n", nombre);
        long documento = Convert.ToInt64(cmd.ExecuteScalar());
        using var version = _conexion.CreateCommand();
        version.CommandText =
            "INSERT INTO versiones_documento(documento_id,huella,ruta_observada,registrada_en) VALUES($d,$h,$r,$fecha) RETURNING id;";
        version.Parameters.AddWithValue("$d", documento);
        version.Parameters.AddWithValue("$h", campoNombre);
        version.Parameters.AddWithValue("$r", "/" + nombre);
        version.Parameters.AddWithValue("$fecha", DateTime.Now.ToString("o"));
        long versionId = Convert.ToInt64(version.ExecuteScalar());
        using var numero = _conexion.CreateCommand();
        numero.CommandText =
            "INSERT INTO numeros_documento(documento_id,numero,prefijo,sufijo,origen) VALUES($d,$n,'','','prueba');";
        numero.Parameters.AddWithValue("$d", documento);
        numero.Parameters.AddWithValue("$n", valor);
        numero.ExecuteNonQuery();
        using var campo = _conexion.CreateCommand();
        campo.CommandText =
            "INSERT INTO identificaciones(tipo,emisor,datos,actualizada) VALUES('Factura','Emisor','{}','ahora') ON CONFLICT(tipo,emisor) DO UPDATE SET actualizada='ahora' RETURNING id;";
        long identificacion = Convert.ToInt64(campo.ExecuteScalar());
        using var crearCampo = _conexion.CreateCommand();
        crearCampo.CommandText =
            "INSERT INTO campos_documento(identificacion_id,nombre,nombre_estable,tipo_dato,origen_lectura,dato_diccionario_id) VALUES($i,'OC','oc_cliente','texto','marca','oc_cliente') ON CONFLICT(identificacion_id,nombre_estable) DO UPDATE SET nombre=excluded.nombre RETURNING id;";
        crearCampo.Parameters.AddWithValue("$i", identificacion);
        long campoId = Convert.ToInt64(crearCampo.ExecuteScalar());
        new RepositorioDatosEnlazantes(_conexion).GuardarDatoTipo(
            new(0, identificacion, "oc_cliente", "1", campoId, 1, 0.1, 0.1, 0.2, 0.1, true)
        );
        using var dato = _conexion.CreateCommand();
        dato.CommandText =
            "INSERT INTO valores_documento(version_id,campo_id,valor_original,valor_clave,origen,fecha_creacion,dato_diccionario_id) VALUES($v,$c,$x,$x,'pdf','ahora','oc_cliente');";
        dato.Parameters.AddWithValue("$v", versionId);
        dato.Parameters.AddWithValue("$c", campoId);
        dato.Parameters.AddWithValue("$x", valor);
        dato.ExecuteNonQuery();
        return versionId;
    }

    [Theory]
    [InlineData("NVV-12345", "12345")]
    [InlineData("0000020508", "20508")]
    [InlineData("1066 086", "1066086")]
    [InlineData("OC 4500012345", "4500012345")]
    [InlineData("000", "0")]
    [InlineData("ob-sur", "OBSUR")]
    [InlineData("  ", "")]
    public void La_clave_de_enlace_iguala_formatos_distintos_del_mismo_numero(
        string valor,
        string esperada
    ) => Assert.Equal(esperada, DiccionarioDatosEnlazantes.ClaveDeEnlace(valor));
}
