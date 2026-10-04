using Buscadero.Core.Marcas;
using Microsoft.Data.Sqlite;

namespace Buscadero.Core.Tests;

public sealed class SesionMarcasTests
{
    private static string Documento(EntornoDePrueba entorno) => entorno.CrearDocumento("12345.pdf");

    [Fact]
    public void Agregar_GuardaLaMarcaAsociadaAlDocumento()
    {
        using var entorno = new EntornoDePrueba();
        var sesion = new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno));

        sesion.Agregar(TipoMarca.Tick, 0, 0.1, 0.2, 0.05, 0.05);

        var recuperada = new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno)).Marcas;
        var marca = Assert.Single(recuperada);
        Assert.Equal(TipoMarca.Tick, marca.Tipo);
        Assert.Equal(0, marca.Pagina);
        Assert.Equal(0.1, marca.X);
    }

    [Fact]
    public void Agregar_GuardaTextoYVariasPaginas()
    {
        using var entorno = new EntornoDePrueba();
        var sesion = new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno));

        sesion.Agregar(TipoMarca.Texto, 1, 0.3, 0.4, 0.2, 0.03, "observado");
        sesion.Agregar(TipoMarca.Circulo, 2, 0.5, 0.5, 0.1, 0.1);

        var recuperadas = new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno)).Marcas;

        Assert.Equal(2, recuperadas.Count);
        Assert.Contains(
            recuperadas,
            m => m.Tipo == TipoMarca.Texto && m.Texto == "observado" && m.Pagina == 1
        );
        Assert.Contains(recuperadas, m => m.Tipo == TipoMarca.Circulo && m.Pagina == 2);
    }

    [Fact]
    public void Deshacer_QuitaLaUltimaMarcaYLaPersiste()
    {
        using var entorno = new EntornoDePrueba();
        var sesion = new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno));
        sesion.Agregar(TipoMarca.Tick, 0, 0.1, 0.1, 0.05, 0.05);

        var resultado = sesion.Deshacer();

        Assert.True(resultado);
        Assert.Empty(new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno)).Marcas);
    }

    [Fact]
    public void Rehacer_VuelveAAgregarLaMarca()
    {
        using var entorno = new EntornoDePrueba();
        var sesion = new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno));
        sesion.Agregar(TipoMarca.Tick, 0, 0.1, 0.1, 0.05, 0.05);
        sesion.Deshacer();

        var resultado = sesion.Rehacer();

        Assert.True(resultado);
        Assert.Single(new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno)).Marcas);
    }

    [Fact]
    public void BorrarTodas_Deshacer_RestauraLasMarcas()
    {
        using var entorno = new EntornoDePrueba();
        var sesion = new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno));
        sesion.Agregar(TipoMarca.Tick, 0, 0.1, 0.1, 0.05, 0.05);
        sesion.Agregar(TipoMarca.Equis, 0, 0.5, 0.5, 0.05, 0.05);

        sesion.BorrarTodas();
        Assert.Empty(new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno)).Marcas);

        sesion.Deshacer();

        Assert.Equal(
            2,
            new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno)).Marcas.Count
        );
    }

    [Fact]
    public void Mover_ActualizaLaPosicionYDeshacerLaRestaura()
    {
        using var entorno = new EntornoDePrueba();
        var sesion = new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno));
        var marca = sesion.Agregar(TipoMarca.Texto, 0, 0.1, 0.1, 0.2, 0.03, "hola");

        sesion.Mover(marca.Id, 0.6, 0.7);

        var movida = Assert.Single(
            new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno)).Marcas
        );
        Assert.Equal(0.6, movida.X);
        Assert.Equal(0.7, movida.Y);

        sesion.Deshacer();

        var restaurada = Assert.Single(
            new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno)).Marcas
        );
        Assert.Equal(0.1, restaurada.X);
    }

    [Fact]
    public void Quitar_EliminaSoloLaMarcaIndicada()
    {
        using var entorno = new EntornoDePrueba();
        var sesion = new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno));
        var primera = sesion.Agregar(TipoMarca.Tick, 0, 0.1, 0.1, 0.05, 0.05);
        sesion.Agregar(TipoMarca.Equis, 0, 0.5, 0.5, 0.05, 0.05);

        sesion.Quitar(primera.Id);

        var restantes = new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno)).Marcas;
        var marca = Assert.Single(restantes);
        Assert.Equal(TipoMarca.Equis, marca.Tipo);
    }

    [Fact]
    public void Marcas_DeDocumentosDistintos_NoSeMezclan()
    {
        using var entorno = new EntornoDePrueba();
        var otro = entorno.CrearDocumento("67890.pdf", "otro PDF");

        new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno)).Agregar(
            TipoMarca.Tick,
            0,
            0.1,
            0.1,
            0.05,
            0.05
        );
        new SesionMarcas(entorno.RepositorioMarcas, otro).Agregar(
            TipoMarca.Raya,
            0,
            0.2,
            0.2,
            0.1,
            0.1
        );

        Assert.Single(new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno)).Marcas);
        Assert.Single(new SesionMarcas(entorno.RepositorioMarcas, otro).Marcas);
    }

    [Fact]
    public void AbrirSinMarcar_NoRegistraElDocumentoEnLaBaseComun()
    {
        using var entorno = new EntornoDePrueba();
        string ruta = Documento(entorno);

        var sesion = new SesionMarcas(entorno.RepositorioMarcas, ruta);

        Assert.Empty(sesion.Marcas);
        using var conexion = new SqliteConnection($"Data Source={entorno.RutaBaseComun}");
        conexion.Open();
        Assert.Equal(
            0L,
            Convert.ToInt64(Escalar(conexion, "SELECT COUNT(*) FROM versiones_documento;"))
        );
        Assert.Equal(0L, Convert.ToInt64(Escalar(conexion, "SELECT COUNT(*) FROM auditoria;")));
    }

    [Fact]
    public void CambioDeContenido_MantieneLaMarcaAnteriorComoHistorial()
    {
        using var entorno = new EntornoDePrueba();
        string ruta = Documento(entorno);
        var sesion = new SesionMarcas(entorno.RepositorioMarcas, ruta);
        sesion.Agregar(TipoMarca.Tick, 0, 0.1, 0.2, 0.05, 0.05);

        File.WriteAllText(ruta, "contenido nuevo");
        var nuevaSesion = new SesionMarcas(entorno.RepositorioMarcas, ruta);

        Assert.Empty(nuevaSesion.Marcas);
        nuevaSesion.Agregar(TipoMarca.Equis, 1, 0.3, 0.4, 0.05, 0.05);
        Assert.Equal(TipoMarca.Equis, Assert.Single(nuevaSesion.Marcas).Tipo);
        using var conexion = new SqliteConnection($"Data Source={entorno.RutaBaseComun}");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT COUNT(*), COUNT(DISTINCT version_id) FROM marcas_version;";
        using var lector = comando.ExecuteReader();
        Assert.True(lector.Read());
        Assert.Equal(2L, lector.GetInt64(0));
        Assert.Equal(2L, lector.GetInt64(1));
        Assert.Equal(
            2L,
            Convert.ToInt64(
                Escalar(
                    conexion,
                    "SELECT COUNT(*) FROM auditoria WHERE app='Buscadero' AND accion='guardar_marca';"
                )
            )
        );
    }

    [Fact]
    public void EditarYQuitar_AnulaSinBorrarYDejaAuditoria()
    {
        using var entorno = new EntornoDePrueba();
        var sesion = new SesionMarcas(entorno.RepositorioMarcas, Documento(entorno));
        var marca = sesion.Agregar(TipoMarca.Tick, 0, 0.1, 0.2, 0.05, 0.05);
        sesion.Mover(marca.Id, 0.5, 0.6);
        sesion.Quitar(marca.Id);

        using var conexion = new SqliteConnection($"Data Source={entorno.RutaBaseComun}");
        conexion.Open();
        Assert.Equal(
            2L,
            Convert.ToInt64(Escalar(conexion, "SELECT COUNT(*) FROM marcas_version;"))
        );
        Assert.Equal(
            2L,
            Convert.ToInt64(
                Escalar(conexion, "SELECT COUNT(*) FROM marcas_version WHERE estado='anulada';")
            )
        );
        Assert.Equal(
            0L,
            Convert.ToInt64(
                Escalar(conexion, "SELECT COUNT(*) FROM marcas_version WHERE estado='activa';")
            )
        );
        Assert.Equal(
            4L,
            Convert.ToInt64(
                Escalar(
                    conexion,
                    "SELECT COUNT(*) FROM auditoria WHERE app='Buscadero' AND accion IN ('guardar_marca','anular_marca');"
                )
            )
        );
    }

    [Fact]
    public void GuardarMarca_PermiteUnLectorAbiertoEnWAL()
    {
        using var entorno = new EntornoDePrueba();
        string ruta = Documento(entorno);
        using var lectorConexion = new SqliteConnection($"Data Source={entorno.RutaBaseComun}");
        lectorConexion.Open();
        using var comando = lectorConexion.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM documentos;";
        using var lector = comando.ExecuteReader();
        Assert.True(lector.Read());

        var sesion = new SesionMarcas(entorno.RepositorioMarcas, ruta);
        sesion.Agregar(TipoMarca.Tick, 0, 0.1, 0.2, 0.05, 0.05);

        Assert.Single(sesion.Marcas);
    }

    private static object? Escalar(SqliteConnection conexion, string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return comando.ExecuteScalar();
    }
}
