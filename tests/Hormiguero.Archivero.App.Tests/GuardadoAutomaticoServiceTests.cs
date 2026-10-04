using Archivero.Datos;
using Archivero.Servicios;

namespace Archivero.Tests;

public class GuardadoAutomaticoServiceTests : IDisposable
{
    private readonly string _raiz;
    private readonly string _carpetaOrigen;
    private readonly string _carpetaDestino;
    private readonly string _rutaLogOriginal;

    public GuardadoAutomaticoServiceTests()
    {
        _raiz = Path.Combine(Path.GetTempPath(), "archivero-tests-" + Guid.NewGuid().ToString("N"));
        _carpetaOrigen = Path.Combine(_raiz, "origen");
        _carpetaDestino = Path.Combine(_raiz, "destino");
        Directory.CreateDirectory(_carpetaOrigen);
        Directory.CreateDirectory(_carpetaDestino);

        // Procesar() ahora registra en el log de auditoria (Caso-9, mejora 2): redirigirlo para
        // no escribir en el auditoria.log real del usuario al correr los tests.
        _rutaLogOriginal = AuditoriaService.RutaLog;
        AuditoriaService.RutaLog = Path.Combine(_raiz, "auditoria.log");
    }

    private static Marca MarcaDeLinea(CampoMarca campo, int indiceLinea)
    {
        var banda = CreadorPdfDePrueba.ObtenerBandaDeLinea(indiceLinea);
        return new Marca(campo, 0, banda.X, banda.Y, banda.Ancho, banda.Alto);
    }

    [Fact]
    public void Procesar_ConFormatoDirecto_ClasificaElArchivoSinSubcarpetas()
    {
        var ruta = CreadorPdfDePrueba.CrearConLineas(_carpetaOrigen, "Banco de Prueba SA", "Resumen de cuenta");
        var configuracion = new ConfiguracionDocumento
        {
            Id = 1,
            Emisor = "Banco de Prueba SA",
            Tipo = "Resumen de cuenta",
            CarpetaDestino = _carpetaDestino,
            FormatoCarpeta = FormatoCarpeta.Directo,
            PatronCarpeta = null,
            Renombrar = false,
            Patrones = [new PatronReconocimiento(1, [MarcaDeLinea(CampoMarca.Emisor, 0), MarcaDeLinea(CampoMarca.Tipo, 1)])]
        };

        var resultado = GuardadoAutomaticoService.Procesar(ruta, configuracion);

        Assert.Equal(ResultadoGuardadoAutomatico.Guardado, resultado.Resultado);
        Assert.True(File.Exists(resultado.RutaFinal));
        Assert.False(File.Exists(ruta));
    }

    [Fact]
    public void Procesar_ConFormatoAnioYCarpetaDelPeriodoYaExiste_GuardaSolo()
    {
        // Caso comun (silencioso, sin intervencion): la carpeta del periodo actual ya existe,
        // asi que Archivero guarda directo, como pide REQ-002.
        Directory.CreateDirectory(Path.Combine(_carpetaDestino, "2026"));
        var ruta = CreadorPdfDePrueba.CrearConLineas(_carpetaOrigen, "Banco de Prueba SA", "Resumen de cuenta", "12/09/2026");
        var configuracion = new ConfiguracionDocumento
        {
            Id = 1,
            Emisor = "Banco de Prueba SA",
            Tipo = "Resumen de cuenta",
            CarpetaDestino = _carpetaDestino,
            FormatoCarpeta = FormatoCarpeta.Anio,
            PatronCarpeta = "yyyy",
            Renombrar = false,
            Patrones =
            [
                new PatronReconocimiento(1,
                [
                    MarcaDeLinea(CampoMarca.Emisor, 0),
                    MarcaDeLinea(CampoMarca.Tipo, 1),
                    MarcaDeLinea(CampoMarca.Fecha, 2)
                ])
            ]
        };

        var resultado = GuardadoAutomaticoService.Procesar(ruta, configuracion);

        Assert.Equal(ResultadoGuardadoAutomatico.Guardado, resultado.Resultado);
        Assert.Equal(Path.Combine(_carpetaDestino, "2026"), Path.GetDirectoryName(resultado.RutaFinal));
    }

    [Fact]
    public void Procesar_ConFormatoAnioYCarpetaDelPeriodoTodaviaNoExiste_DevuelvePeriodoNuevoYNoLaCrea()
    {
        // Caso-1, punto 2 (ultimo parrafo): la carpeta del periodo actual todavia no existe --
        // Archivero nunca la crea sola, se lo deja a una decision activa del usuario.
        var ruta = CreadorPdfDePrueba.CrearConLineas(_carpetaOrigen, "Banco de Prueba SA", "Resumen de cuenta", "12/09/2026");
        var configuracion = new ConfiguracionDocumento
        {
            Id = 1,
            Emisor = "Banco de Prueba SA",
            Tipo = "Resumen de cuenta",
            CarpetaDestino = _carpetaDestino,
            FormatoCarpeta = FormatoCarpeta.Anio,
            PatronCarpeta = "yyyy",
            Renombrar = false,
            Patrones =
            [
                new PatronReconocimiento(1,
                [
                    MarcaDeLinea(CampoMarca.Emisor, 0),
                    MarcaDeLinea(CampoMarca.Tipo, 1),
                    MarcaDeLinea(CampoMarca.Fecha, 2)
                ])
            ]
        };

        var resultado = GuardadoAutomaticoService.Procesar(ruta, configuracion);

        Assert.Equal(ResultadoGuardadoAutomatico.PeriodoNuevo, resultado.Resultado);
        Assert.False(Directory.Exists(Path.Combine(_carpetaDestino, "2026")));
        Assert.True(File.Exists(ruta));
    }

    [Fact]
    public void Procesar_ConFormatoAnioYFechaInvalida_DevuelveValorInvalidoYNoTocaElOriginal()
    {
        var ruta = CreadorPdfDePrueba.CrearConLineas(_carpetaOrigen, "Banco de Prueba SA", "Resumen de cuenta", "esto no es una fecha");
        var configuracion = new ConfiguracionDocumento
        {
            Id = 1,
            Emisor = "Banco de Prueba SA",
            Tipo = "Resumen de cuenta",
            CarpetaDestino = _carpetaDestino,
            FormatoCarpeta = FormatoCarpeta.Anio,
            PatronCarpeta = "yyyy",
            Renombrar = false,
            Patrones =
            [
                new PatronReconocimiento(1,
                [
                    MarcaDeLinea(CampoMarca.Emisor, 0),
                    MarcaDeLinea(CampoMarca.Tipo, 1),
                    MarcaDeLinea(CampoMarca.Fecha, 2)
                ])
            ]
        };

        var resultado = GuardadoAutomaticoService.Procesar(ruta, configuracion);

        Assert.Equal(ResultadoGuardadoAutomatico.ValorInvalido, resultado.Resultado);
        Assert.True(File.Exists(ruta));
    }

    [Fact]
    public void Procesar_ConRenombrar_UsaElCampoExtraidoComoNombreDeArchivo()
    {
        var ruta = CreadorPdfDePrueba.CrearConLineas(_carpetaOrigen, "Banco de Prueba SA", "Resumen de cuenta", "FACTURA-001");
        var configuracion = new ConfiguracionDocumento
        {
            Id = 1,
            Emisor = "Banco de Prueba SA",
            Tipo = "Resumen de cuenta",
            CarpetaDestino = _carpetaDestino,
            FormatoCarpeta = FormatoCarpeta.Directo,
            PatronCarpeta = null,
            Renombrar = true,
            Patrones =
            [
                new PatronReconocimiento(1,
                [
                    MarcaDeLinea(CampoMarca.Emisor, 0),
                    MarcaDeLinea(CampoMarca.Tipo, 1),
                    MarcaDeLinea(CampoMarca.NombreArchivo, 2)
                ])
            ]
        };

        var resultado = GuardadoAutomaticoService.Procesar(ruta, configuracion);

        Assert.Equal(ResultadoGuardadoAutomatico.Guardado, resultado.Resultado);
        Assert.Equal("FACTURA-001.pdf", Path.GetFileName(resultado.RutaFinal));
    }

    // ----- Caso-11, punto 1: "Preguntar el nombre cada vez" -----

    private ConfiguracionDocumento ConfiguracionQuePreguntaElNombre(FormatoCarpeta formato) => new()
    {
        Id = 1,
        Emisor = "Banco de Prueba SA",
        Tipo = "Resumen de cuenta",
        CarpetaDestino = _carpetaDestino,
        FormatoCarpeta = formato,
        PatronCarpeta = formato == FormatoCarpeta.Directo ? null : "yyyy",
        Renombrar = false,
        PreguntarNombre = true,
        Patrones =
        [
            new PatronReconocimiento(1,
            [
                MarcaDeLinea(CampoMarca.Emisor, 0),
                MarcaDeLinea(CampoMarca.Tipo, 1),
                MarcaDeLinea(CampoMarca.Fecha, 2)
            ])
        ]
    };

    [Fact]
    public void Procesar_ConPreguntarNombre_DevuelveNombrePorConfirmarYNoTocaElArchivo()
    {
        var ruta = CreadorPdfDePrueba.CrearConLineas(_carpetaOrigen, "Banco de Prueba SA", "Resumen de cuenta", "12/09/2026");

        var resultado = GuardadoAutomaticoService.Procesar(ruta, ConfiguracionQuePreguntaElNombre(FormatoCarpeta.Directo));

        Assert.Equal(ResultadoGuardadoAutomatico.NombrePorConfirmar, resultado.Resultado);
        Assert.True(File.Exists(ruta));
        Assert.Empty(Directory.GetFileSystemEntries(_carpetaDestino));
    }

    [Fact]
    public void Procesar_ConPreguntarNombreYPeriodoInexistente_PideElNombreAntesQueElPeriodo()
    {
        // Si el período se resolviera primero, la pantalla de crear período guardaría con el
        // nombre original y el usuario nunca vería la pregunta del nombre.
        var ruta = CreadorPdfDePrueba.CrearConLineas(_carpetaOrigen, "Banco de Prueba SA", "Resumen de cuenta", "12/09/2026");

        var resultado = GuardadoAutomaticoService.Procesar(ruta, ConfiguracionQuePreguntaElNombre(FormatoCarpeta.Anio));

        Assert.Equal(ResultadoGuardadoAutomatico.NombrePorConfirmar, resultado.Resultado);
        Assert.False(Directory.Exists(Path.Combine(_carpetaDestino, "2026")));
    }

    [Fact]
    public void Procesar_ConPreguntarNombreYFechaInvalida_DevuelveValorInvalido()
    {
        var ruta = CreadorPdfDePrueba.CrearConLineas(_carpetaOrigen, "Banco de Prueba SA", "Resumen de cuenta", "esto no es una fecha");

        var resultado = GuardadoAutomaticoService.Procesar(ruta, ConfiguracionQuePreguntaElNombre(FormatoCarpeta.Anio));

        Assert.Equal(ResultadoGuardadoAutomatico.ValorInvalido, resultado.Resultado);
    }

    [Fact]
    public void GuardarConNombreConfirmado_GuardaConElNombreEscritoEnLaCarpetaCalculada()
    {
        Directory.CreateDirectory(Path.Combine(_carpetaDestino, "2026"));
        var ruta = CreadorPdfDePrueba.CrearConLineas(_carpetaOrigen, "Banco de Prueba SA", "Resumen de cuenta", "12/09/2026");

        var resultado = GuardadoAutomaticoService.GuardarConNombreConfirmado(
            ruta, ConfiguracionQuePreguntaElNombre(FormatoCarpeta.Anio), "NC 555");

        Assert.Equal(ResultadoGuardadoAutomatico.Guardado, resultado.Resultado);
        Assert.Equal(Path.Combine(_carpetaDestino, "2026", "NC 555.pdf"), resultado.RutaFinal);
        Assert.True(File.Exists(resultado.RutaFinal));
        Assert.False(File.Exists(ruta));
    }

    [Fact]
    public void GuardarConNombreConfirmado_ConPeriodoInexistente_DevuelvePeriodoNuevoSinCrearlo()
    {
        var ruta = CreadorPdfDePrueba.CrearConLineas(_carpetaOrigen, "Banco de Prueba SA", "Resumen de cuenta", "12/09/2026");

        var resultado = GuardadoAutomaticoService.GuardarConNombreConfirmado(
            ruta, ConfiguracionQuePreguntaElNombre(FormatoCarpeta.Anio), "NC 555");

        Assert.Equal(ResultadoGuardadoAutomatico.PeriodoNuevo, resultado.Resultado);
        Assert.Equal(Path.Combine(_carpetaDestino, "2026"), resultado.Detalle);
        Assert.False(Directory.Exists(Path.Combine(_carpetaDestino, "2026")));
        Assert.True(File.Exists(ruta));
    }

    [Fact]
    public void GuardarConNombreConfirmado_ConUnArchivoDelMismoNombre_DevuelveDuplicadoSinSobrescribir()
    {
        var existente = Path.Combine(_carpetaDestino, "NC 555.pdf");
        File.WriteAllText(existente, "el que ya estaba");
        var ruta = CreadorPdfDePrueba.CrearConLineas(_carpetaOrigen, "Banco de Prueba SA", "Resumen de cuenta", "12/09/2026");

        var resultado = GuardadoAutomaticoService.GuardarConNombreConfirmado(
            ruta, ConfiguracionQuePreguntaElNombre(FormatoCarpeta.Directo), "NC 555");

        Assert.Equal(ResultadoGuardadoAutomatico.Duplicado, resultado.Resultado);
        Assert.Equal("el que ya estaba", File.ReadAllText(existente));
        Assert.True(File.Exists(ruta));
    }

    [Fact]
    public void GuardarConNombreConfirmado_ConUnNombreReservadoPorWindows_LoRechazaSinTocarNada()
    {
        var ruta = CreadorPdfDePrueba.CrearConLineas(_carpetaOrigen, "Banco de Prueba SA", "Resumen de cuenta", "12/09/2026");

        var resultado = GuardadoAutomaticoService.GuardarConNombreConfirmado(
            ruta, ConfiguracionQuePreguntaElNombre(FormatoCarpeta.Directo), "CON");

        Assert.Equal(ResultadoGuardadoAutomatico.ValidacionFallida, resultado.Resultado);
        Assert.Equal(MotivoPendiente.NombreReservadoPorWindows, resultado.MotivoValidacion);
        Assert.True(File.Exists(ruta));
    }

    public void Dispose()
    {
        AuditoriaService.RutaLog = _rutaLogOriginal;
        Directory.Delete(_raiz, recursive: true);
    }
}
