using Archivero.Datos;
using Archivero.Servicios;

namespace Archivero.Tests;

public class ClasificadorServiceTests : IDisposable
{
    private readonly string _raiz;
    private readonly string _carpetaOrigen;
    private readonly string _carpetaDestino;

    public ClasificadorServiceTests()
    {
        _raiz = Path.Combine(Path.GetTempPath(), "archivero-tests-" + Guid.NewGuid().ToString("N"));
        _carpetaOrigen = Path.Combine(_raiz, "origen");
        _carpetaDestino = Path.Combine(_raiz, "destino");
        Directory.CreateDirectory(_carpetaOrigen);
        Directory.CreateDirectory(_carpetaDestino);
    }

    private ConfiguracionDocumento Configuracion() => new()
    {
        Id = 1,
        Emisor = "Banco de Prueba SA",
        Tipo = "Resumen de cuenta",
        CarpetaDestino = _carpetaDestino,
        FormatoCarpeta = FormatoCarpeta.Directo,
        PatronCarpeta = null,
        Renombrar = false,
        Patrones = []
    };

    private string CrearArchivo(string carpeta, string nombre, string contenido = "contenido")
    {
        var ruta = Path.Combine(carpeta, nombre);
        File.WriteAllText(ruta, contenido);
        return ruta;
    }

    [Fact]
    public void CalcularRutaDestino_NoTocaElDisco_SoloCalcula()
    {
        var origen = CrearArchivo(_carpetaOrigen, "factura.pdf");

        var ruta = ClasificadorService.CalcularRutaDestino(origen, Configuracion(), null, null);

        Assert.Equal(Path.Combine(_carpetaDestino, "factura.pdf"), ruta);
        Assert.True(File.Exists(origen), "CalcularRutaDestino no debería mover ni borrar nada");
    }

    [Fact]
    public void Clasificar_ConArchivoYaExistenteEnDestino_LanzaArchivoDuplicadoException()
    {
        var origen = CrearArchivo(_carpetaOrigen, "factura.pdf", "nuevo");
        CrearArchivo(_carpetaDestino, "factura.pdf", "viejo");

        var ex = Assert.Throws<ArchivoDuplicadoException>(() =>
            ClasificadorService.Clasificar(origen, Configuracion(), null, null));

        Assert.Equal(Path.Combine(_carpetaDestino, "factura.pdf"), ex.RutaDestino);
        Assert.True(File.Exists(origen), "El original no se debe tocar ante un duplicado");
    }

    [Fact]
    public void ReemplazarYClasificar_BorraElExistenteYGuardaElNuevo()
    {
        var origen = CrearArchivo(_carpetaOrigen, "factura.pdf", "contenido nuevo");
        var destino = CrearArchivo(_carpetaDestino, "factura.pdf", "contenido viejo");

        ClasificadorService.ReemplazarYClasificar(origen, destino);

        Assert.False(File.Exists(origen));
        Assert.True(File.Exists(destino));
        Assert.Equal("contenido nuevo", File.ReadAllText(destino));
    }

    [Fact]
    public void GuardarComoExcepcion_GuardaEnLaCarpetaElegidaConElMismoNombre()
    {
        var origen = CrearArchivo(_carpetaOrigen, "factura.pdf");
        var carpetaExcepcion = Path.Combine(_raiz, "excepcion");
        var rutaExcepcion = Path.Combine(carpetaExcepcion, "factura.pdf");

        ClasificadorService.GuardarComoExcepcion(origen, rutaExcepcion);

        Assert.False(File.Exists(origen));
        Assert.True(File.Exists(rutaExcepcion));
    }

    [Fact]
    public void GuardarComoExcepcion_SiTambienExisteAhi_LanzaArchivoDuplicadoException()
    {
        var origen = CrearArchivo(_carpetaOrigen, "factura.pdf");
        var carpetaExcepcion = Path.Combine(_raiz, "excepcion");
        Directory.CreateDirectory(carpetaExcepcion);
        var rutaExcepcion = CrearArchivo(carpetaExcepcion, "factura.pdf");

        Assert.Throws<ArchivoDuplicadoException>(() =>
            ClasificadorService.GuardarComoExcepcion(origen, rutaExcepcion));

        Assert.True(File.Exists(origen), "El original no se debe tocar si tambien hay duplicado en la excepcion");
    }

    // ----- Caso-9, mejora 1: estos tests describen el comportamiento pedido y tienen que fallar
    // contra el ClasificadorService de hoy (sin la validación centralizada todavía). -----

    [Fact]
    public void CalcularRutaDestino_ConPathTraversalEnElNombreExtraido_NoEscapaLaCarpetaDestino()
    {
        // Texto extraido de un PDF no es confiable: no puede escapar de la carpeta configurada
        // aunque contenga "..\" -- (e), última línea de defensa, siempre se aplica.
        var origen = CrearArchivo(_carpetaOrigen, "factura.pdf");
        var configuracion = Configuracion() with { Renombrar = true };

        var ruta = ClasificadorService.CalcularRutaDestino(origen, configuracion, null, @"..\..\fuera");

        var rutaResuelta = Path.GetFullPath(ruta);
        var destinoResuelto = Path.GetFullPath(_carpetaDestino);
        Assert.StartsWith(destinoResuelto + Path.DirectorySeparatorChar, rutaResuelta);
    }

    [Fact]
    public void Clasificar_ConCaracteresInvalidosDeWindowsEnElNombreExtraido_LosSaneaYGuardaIgual()
    {
        var origen = CrearArchivo(_carpetaOrigen, "factura.pdf");
        var configuracion = Configuracion() with { Renombrar = true };

        var rutaFinal = ClasificadorService.Clasificar(origen, configuracion, null, "Factura:2026");

        Assert.Equal(Path.Combine(_carpetaDestino, "Factura_2026.pdf"), rutaFinal);
        Assert.True(File.Exists(rutaFinal));
    }

    [Fact]
    public void Clasificar_ConNombreExtraidoReservadoDeWindows_RechazaYNoTocaElOriginal()
    {
        var origen = CrearArchivo(_carpetaOrigen, "factura.pdf");
        var configuracion = Configuracion() with { Renombrar = true };

        Assert.Throws<ValidacionSeguridadException>(() =>
            ClasificadorService.Clasificar(origen, configuracion, null, "CON"));

        Assert.True(File.Exists(origen), "El original no se debe tocar ante un nombre reservado");
    }

    [Fact]
    public void Clasificar_ConCaracterDeControlEnElNombreExtraido_RechazaYNoTocaElOriginal()
    {
        var origen = CrearArchivo(_carpetaOrigen, "factura.pdf");
        var configuracion = Configuracion() with { Renombrar = true };

        Assert.Throws<ValidacionSeguridadException>(() =>
            ClasificadorService.Clasificar(origen, configuracion, null, "Factura\u0000Falsa"));

        Assert.True(File.Exists(origen));
    }

    [Fact]
    public void Clasificar_ConNombreExtraidoDemasiadoLargo_Rechaza()
    {
        var origen = CrearArchivo(_carpetaOrigen, "factura.pdf");
        var configuracion = Configuracion() with { Renombrar = true };
        var nombreLargo = new string('a', 250);

        Assert.Throws<ValidacionSeguridadException>(() =>
            ClasificadorService.Clasificar(origen, configuracion, null, nombreLargo));

        Assert.True(File.Exists(origen));
    }

    [Fact]
    public void CalcularRutaDestino_ConPatronPersonalizadoConCaracterInvalido_LoSaneaEnLaSubcarpeta()
    {
        var origen = CrearArchivo(_carpetaOrigen, "factura.pdf");
        var configuracion = Configuracion() with
        {
            FormatoCarpeta = FormatoCarpeta.Personalizado,
            PatronCarpeta = "'Año: 'yyyy"
        };

        var ruta = ClasificadorService.CalcularRutaDestino(origen, configuracion, new DateTime(2026, 3, 15), null);

        Assert.Equal(Path.Combine(_carpetaDestino, "Año_ 2026", "factura.pdf"), ruta);
    }

    public void Dispose()
    {
        Directory.Delete(_raiz, recursive: true);
    }
}
