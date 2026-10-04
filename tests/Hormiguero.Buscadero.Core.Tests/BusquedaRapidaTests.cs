using Buscadero.Core.Busqueda;
using Buscadero.Core.Indexado;

namespace Buscadero.Core.Tests;

// Fase B-2a (D-66): buscar ya no recorre todas las carpetas cuando el documento
// ya está en el índice; solo lo actualiza cuando no encuentra nada.
public sealed class BusquedaRapidaTests
{
    private sealed class Contador
    {
        public int Pausas;
    }

    private static (ServicioBusqueda Servicio, Indexador Indexador, Contador Contador) Armar(
        EntornoDePrueba entorno,
        IndexadoEnSegundoPlano? enSegundoPlano = null
    )
    {
        var contador = new Contador();
        var indexador = new Indexador(
            entorno.RepositorioIndice,
            TimeSpan.FromMilliseconds(1),
            _ => Interlocked.Increment(ref contador.Pausas)
        );
        var servicio = new ServicioBusqueda(
            entorno.ServicioCarpetas,
            indexador,
            entorno.RepositorioIndice,
            enSegundoPlano
        );
        return (servicio, indexador, contador);
    }

    [Fact]
    public void Documento_ya_indexado_se_encuentra_sin_recorrer_carpetas()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        var sub = entorno.CrearCarpeta("Documentos", "2026");
        entorno.CrearArchivo(sub, "OCC104523.pdf");
        entorno.ServicioCarpetas.Agregar(raiz);
        var (servicio, _, contador) = Armar(entorno);
        Assert.Single(servicio.Buscar("104523"));

        contador.Pausas = 0;
        var resultados = servicio.Buscar("104523");

        Assert.Single(resultados);
        Assert.Equal(0, contador.Pausas);
    }

    [Fact]
    public void Documento_recien_llegado_se_encuentra_igual()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        var sub = entorno.CrearCarpeta("Documentos", "2026");
        entorno.CrearArchivo(sub, "OCC104523.pdf");
        entorno.ServicioCarpetas.Agregar(raiz);
        var (servicio, _, _) = Armar(entorno);
        Assert.Single(servicio.Buscar("104523"));

        entorno.CrearArchivo(sub, "FCV25001.pdf");
        var resultados = servicio.Buscar("25001");

        Assert.Equal("FCV25001.pdf", Assert.Single(resultados).Nombre);
    }

    [Fact]
    public void Numero_que_no_existe_sigue_sin_resultados()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearArchivo(raiz, "OCC104523.pdf");
        entorno.ServicioCarpetas.Agregar(raiz);
        var (servicio, _, _) = Armar(entorno);

        Assert.Empty(servicio.Buscar("999999"));
    }

    [Fact]
    public async Task El_indice_en_segundo_plano_deja_listo_lo_nuevo()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        var sub = entorno.CrearCarpeta("Documentos", "2026");
        entorno.CrearArchivo(sub, "OCC104523.pdf");
        entorno.ServicioCarpetas.Agregar(raiz);
        var indexador = new Indexador(entorno.RepositorioIndice, TimeSpan.Zero, _ => { });
        using var enSegundoPlano = new IndexadoEnSegundoPlano(
            indexador,
            () => entorno.ServicioCarpetas.ObtenerTodas().Select(c => c.Ruta).ToList()
        );
        var terminado = new TaskCompletionSource();
        enSegundoPlano.Terminado += () => terminado.TrySetResult();

        enSegundoPlano.Pedir();
        await terminado.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // Ya está en el índice: se encuentra sin que la búsqueda recorra nada.
        var contador = new Contador();
        var servicio = new ServicioBusqueda(
            entorno.ServicioCarpetas,
            new Indexador(
                entorno.RepositorioIndice,
                TimeSpan.FromMilliseconds(1),
                _ => Interlocked.Increment(ref contador.Pausas)
            ),
            entorno.RepositorioIndice,
            enSegundoPlano
        );
        Assert.Single(servicio.Buscar("104523"));
        Assert.Equal(0, contador.Pausas);
    }

    [Fact]
    public void Cerrar_el_indice_en_segundo_plano_no_se_cuelga()
    {
        using var entorno = new EntornoDePrueba();
        var indexador = new Indexador(entorno.RepositorioIndice, TimeSpan.Zero, _ => { });
        var enSegundoPlano = new IndexadoEnSegundoPlano(indexador, () => []);
        var reloj = System.Diagnostics.Stopwatch.StartNew();

        enSegundoPlano.Dispose();

        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(5));
    }
}
