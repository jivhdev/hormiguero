using Buscadero.Core.Marcas;

namespace Buscadero.Core.Tests;

public sealed class SesionMarcasTests
{
    private const string Documento = @"C:\Documentos\12345.pdf";

    [Fact]
    public void Agregar_GuardaLaMarcaAsociadaAlDocumento()
    {
        using var entorno = new EntornoDePrueba();
        var sesion = new SesionMarcas(entorno.RepositorioMarcas, Documento);

        sesion.Agregar(TipoMarca.Tick, 0, 0.1, 0.2, 0.05, 0.05);

        var recuperada = new SesionMarcas(entorno.RepositorioMarcas, Documento).Marcas;
        var marca = Assert.Single(recuperada);
        Assert.Equal(TipoMarca.Tick, marca.Tipo);
        Assert.Equal(0, marca.Pagina);
        Assert.Equal(0.1, marca.X);
    }

    [Fact]
    public void Agregar_GuardaTextoYVariasPaginas()
    {
        using var entorno = new EntornoDePrueba();
        var sesion = new SesionMarcas(entorno.RepositorioMarcas, Documento);

        sesion.Agregar(TipoMarca.Texto, 1, 0.3, 0.4, 0.2, 0.03, "observado");
        sesion.Agregar(TipoMarca.Circulo, 2, 0.5, 0.5, 0.1, 0.1);

        var recuperadas = new SesionMarcas(entorno.RepositorioMarcas, Documento).Marcas;

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
        var sesion = new SesionMarcas(entorno.RepositorioMarcas, Documento);
        sesion.Agregar(TipoMarca.Tick, 0, 0.1, 0.1, 0.05, 0.05);

        var resultado = sesion.Deshacer();

        Assert.True(resultado);
        Assert.Empty(new SesionMarcas(entorno.RepositorioMarcas, Documento).Marcas);
    }

    [Fact]
    public void Rehacer_VuelveAAgregarLaMarca()
    {
        using var entorno = new EntornoDePrueba();
        var sesion = new SesionMarcas(entorno.RepositorioMarcas, Documento);
        sesion.Agregar(TipoMarca.Tick, 0, 0.1, 0.1, 0.05, 0.05);
        sesion.Deshacer();

        var resultado = sesion.Rehacer();

        Assert.True(resultado);
        Assert.Single(new SesionMarcas(entorno.RepositorioMarcas, Documento).Marcas);
    }

    [Fact]
    public void BorrarTodas_Deshacer_RestauraLasMarcas()
    {
        using var entorno = new EntornoDePrueba();
        var sesion = new SesionMarcas(entorno.RepositorioMarcas, Documento);
        sesion.Agregar(TipoMarca.Tick, 0, 0.1, 0.1, 0.05, 0.05);
        sesion.Agregar(TipoMarca.Equis, 0, 0.5, 0.5, 0.05, 0.05);

        sesion.BorrarTodas();
        Assert.Empty(new SesionMarcas(entorno.RepositorioMarcas, Documento).Marcas);

        sesion.Deshacer();

        Assert.Equal(2, new SesionMarcas(entorno.RepositorioMarcas, Documento).Marcas.Count);
    }

    [Fact]
    public void Mover_ActualizaLaPosicionYDeshacerLaRestaura()
    {
        using var entorno = new EntornoDePrueba();
        var sesion = new SesionMarcas(entorno.RepositorioMarcas, Documento);
        var marca = sesion.Agregar(TipoMarca.Texto, 0, 0.1, 0.1, 0.2, 0.03, "hola");

        sesion.Mover(marca.Id, 0.6, 0.7);

        var movida = Assert.Single(new SesionMarcas(entorno.RepositorioMarcas, Documento).Marcas);
        Assert.Equal(0.6, movida.X);
        Assert.Equal(0.7, movida.Y);

        sesion.Deshacer();

        var restaurada = Assert.Single(
            new SesionMarcas(entorno.RepositorioMarcas, Documento).Marcas
        );
        Assert.Equal(0.1, restaurada.X);
    }

    [Fact]
    public void Quitar_EliminaSoloLaMarcaIndicada()
    {
        using var entorno = new EntornoDePrueba();
        var sesion = new SesionMarcas(entorno.RepositorioMarcas, Documento);
        var primera = sesion.Agregar(TipoMarca.Tick, 0, 0.1, 0.1, 0.05, 0.05);
        sesion.Agregar(TipoMarca.Equis, 0, 0.5, 0.5, 0.05, 0.05);

        sesion.Quitar(primera.Id);

        var restantes = new SesionMarcas(entorno.RepositorioMarcas, Documento).Marcas;
        var marca = Assert.Single(restantes);
        Assert.Equal(TipoMarca.Equis, marca.Tipo);
    }

    [Fact]
    public void Marcas_DeDocumentosDistintos_NoSeMezclan()
    {
        using var entorno = new EntornoDePrueba();
        var otro = @"C:\Documentos\67890.pdf";

        new SesionMarcas(entorno.RepositorioMarcas, Documento).Agregar(
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

        Assert.Single(new SesionMarcas(entorno.RepositorioMarcas, Documento).Marcas);
        Assert.Single(new SesionMarcas(entorno.RepositorioMarcas, otro).Marcas);
    }
}
