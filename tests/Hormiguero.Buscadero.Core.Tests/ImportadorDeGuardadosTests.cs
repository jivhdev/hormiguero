using Buscadero.Core.Busqueda;
using Buscadero.Core.Indexado;
using Hormiguero.Nucleo.Datos;

namespace Buscadero.Core.Tests;

public sealed class ImportadorDeGuardadosTests
{
    private sealed class Contador
    {
        public int Pausas;
    }

    [Fact]
    public void Lo_guardado_por_Archivero_se_encuentra_sin_reindexar()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        var subcarpeta = entorno.CrearCarpeta("Documentos", "2026");
        entorno.ServicioCarpetas.Agregar(raiz);
        entorno.Indexador.Indexar([raiz]);

        var ruta = entorno.CrearArchivo(subcarpeta, "OCC876543.pdf");
        var rutaBaseComun = Path.Combine(entorno.Raiz, "comun", "hormiguero.db");
        Registrar(rutaBaseComun, ruta);

        var contador = new Contador();
        var servicio = new ServicioBusqueda(
            entorno.ServicioCarpetas,
            new Indexador(
                entorno.RepositorioIndice,
                TimeSpan.FromMilliseconds(1),
                _ => Interlocked.Increment(ref contador.Pausas)
            ),
            entorno.RepositorioIndice,
            importador: new ImportadorDeGuardados(
                entorno.RepositorioIndice,
                () => entorno.ServicioCarpetas.ObtenerTodas().Select(c => c.Ruta).ToList(),
                rutaBaseComun
            )
        );

        var resultados = servicio.Buscar("876543");

        Assert.Equal("OCC876543.pdf", Assert.Single(resultados).Nombre);
        Assert.Equal(0, contador.Pausas);
    }

    [Fact]
    public void Fuera_de_las_carpetas_se_ignora()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        var fuera = entorno.CrearCarpeta("Fuera");
        var ruta = entorno.CrearArchivo(fuera, "OCC123456.pdf");
        var rutaBaseComun = Path.Combine(entorno.Raiz, "comun", "hormiguero.db");
        Registrar(rutaBaseComun, ruta);

        var importador = CrearImportador(entorno, rutaBaseComun, raiz);

        Assert.Equal(0, importador.Importar());
        Assert.Empty(entorno.RepositorioIndice.ObtenerArchivos());
        Assert.Equal(1, entorno.RepositorioIndice.LeerUltimoGuardadoImportado());
    }

    [Fact]
    public void Archivo_que_ya_no_existe_se_ignora()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        var ruta = Path.Combine(raiz, "OCC123456.pdf");
        var rutaBaseComun = Path.Combine(entorno.Raiz, "comun", "hormiguero.db");
        Registrar(rutaBaseComun, ruta);

        var importador = CrearImportador(entorno, rutaBaseComun, raiz);

        Assert.Equal(0, importador.Importar());
        Assert.Empty(entorno.RepositorioIndice.ObtenerArchivos());
        Assert.Equal(1, entorno.RepositorioIndice.LeerUltimoGuardadoImportado());
    }

    [Fact]
    public void No_importa_dos_veces_lo_mismo()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        var ruta = entorno.CrearArchivo(raiz, "OCC123456.pdf");
        var rutaBaseComun = Path.Combine(entorno.Raiz, "comun", "hormiguero.db");
        Registrar(rutaBaseComun, ruta);
        var importador = CrearImportador(entorno, rutaBaseComun, raiz);

        Assert.Equal(1, importador.Importar());
        Assert.Equal(0, importador.Importar());
        Assert.Single(entorno.RepositorioIndice.ObtenerArchivos());
        Assert.Equal(1, entorno.RepositorioIndice.LeerUltimoGuardadoImportado());
    }

    [Fact]
    public void Sin_base_comun_no_hace_nada()
    {
        using var entorno = new EntornoDePrueba();
        var rutaBaseComun = Path.Combine(entorno.Raiz, "inexistente", "hormiguero.db");
        var importador = CrearImportador(
            entorno,
            rutaBaseComun,
            entorno.CrearCarpeta("Documentos")
        );

        Assert.Equal(0, importador.Importar());
        Assert.False(File.Exists(rutaBaseComun));
        Assert.Equal(0, entorno.RepositorioIndice.LeerUltimoGuardadoImportado());
    }

    private static ImportadorDeGuardados CrearImportador(
        EntornoDePrueba entorno,
        string rutaBaseComun,
        params string[] carpetasMadre
    ) => new(entorno.RepositorioIndice, () => carpetasMadre, rutaBaseComun);

    private static void Registrar(string rutaBaseComun, string ruta)
    {
        using var conexion = BaseComun.Abrir(rutaBaseComun);
        new DocumentosGuardados(conexion).Registrar(ruta, "Archivero");
    }
}
