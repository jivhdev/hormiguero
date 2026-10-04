using Buscadero.Core.Indexado;

namespace Buscadero.Core.Tests;

public sealed class IndexadorTests
{
    [Fact]
    public void Indexar_IndexaSoloArchivosPdf()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        var sub = entorno.CrearCarpeta("Documentos", "2026");
        entorno.CrearArchivo(raiz, "12345.pdf");
        entorno.CrearArchivo(raiz, "notas.txt");
        entorno.CrearArchivo(sub, "67890.pdf");

        var resumen = entorno.Indexador.Indexar(new[] { raiz });

        var archivos = entorno.RepositorioIndice.ObtenerArchivos();
        Assert.Equal(2, archivos.Count);
        Assert.Equal(2, resumen.ArchivosIndexados);
        Assert.Contains(archivos, a => a.Nombre == "12345.pdf");
        Assert.Contains(archivos, a => a.Nombre == "67890.pdf");
    }

    [Fact]
    public void Indexar_SegundaVezSinCambios_EsIncremental()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearCarpeta("Documentos", "2026");
        entorno.CrearArchivo(raiz, "12345.pdf");

        var primera = entorno.Indexador.Indexar(new[] { raiz });
        var segunda = entorno.Indexador.Indexar(new[] { raiz });

        Assert.Equal(2, primera.CarpetasVisitadas);
        Assert.Equal(0, segunda.CarpetasVisitadas);
    }

    [Fact]
    public void Indexar_ArchivoNuevo_SeAgregaAlIndice()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearArchivo(raiz, "12345.pdf");
        entorno.Indexador.Indexar(new[] { raiz });

        entorno.CrearArchivo(raiz, "67890.pdf");
        entorno.Indexador.Indexar(new[] { raiz });

        var archivos = entorno.RepositorioIndice.ObtenerArchivos();
        Assert.Equal(2, archivos.Count);
        Assert.Contains(archivos, a => a.Nombre == "67890.pdf");
    }

    [Fact]
    public void Indexar_ArchivoBorrado_SeQuitaDelIndice()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        var ruta = entorno.CrearArchivo(raiz, "12345.pdf");
        entorno.Indexador.Indexar(new[] { raiz });

        File.Delete(ruta);
        entorno.Indexador.Indexar(new[] { raiz });

        Assert.Empty(entorno.RepositorioIndice.ObtenerArchivos());
    }

    [Fact]
    public void Indexar_ArchivoNuevoEnSubcarpetaExistente_SeDetectaAunqueLaRaizNoCambio()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        var sub = entorno.CrearCarpeta("Documentos", "2026");
        entorno.CrearArchivo(sub, "primero.pdf");
        entorno.Indexador.Indexar(new[] { raiz });

        // El archivo nuevo se agrega dentro de la subcarpeta, no de la raiz: en NTFS
        // eso actualiza la fecha de modificacion de "sub", pero no la de "raiz".
        entorno.CrearArchivo(sub, "FCV0000025253.pdf");
        entorno.Indexador.Indexar(new[] { raiz });

        var archivos = entorno.RepositorioIndice.ObtenerArchivos();
        Assert.Contains(archivos, a => a.Nombre == "FCV0000025253.pdf");
    }

    [Fact]
    public void Indexar_SubcarpetaEliminada_LimpiaSuSubarbol()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        var sub = entorno.CrearCarpeta("Documentos", "2026");
        entorno.CrearArchivo(sub, "12345.pdf");
        entorno.Indexador.Indexar(new[] { raiz });

        Directory.Delete(sub, true);
        entorno.Indexador.Indexar(new[] { raiz });

        Assert.Empty(entorno.RepositorioIndice.ObtenerArchivos());
    }

    [Fact]
    public void Indexar_PaceaUnaVezPorSubcarpeta()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearCarpeta("Documentos", "2026");
        entorno.CrearCarpeta("Documentos", "2025");
        entorno.CrearCarpeta("Documentos", "2024");

        var esperas = new List<TimeSpan>();
        var indexador = new Indexador(
            entorno.RepositorioIndice,
            TimeSpan.FromMilliseconds(1),
            esperas.Add
        );
        indexador.Indexar(new[] { raiz });

        Assert.Equal(3, esperas.Count);
        Assert.All(esperas, espera => Assert.Equal(TimeSpan.FromMilliseconds(1), espera));
    }

    [Fact]
    public void Indexar_RecorreSubcarpetasHermanas_DeLaMasRecienteALaMasAntigua()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Servidor");
        // Se crean a proposito en orden ascendente (mas vieja primero) para que
        // Directory.GetDirectories (que en NTFS suele devolver orden alfabetico,
        // "2020" antes que "2026") no pueda disfrazar un recorrido mal ordenado.
        entorno.CrearCarpetaConFecha(
            new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            "Servidor",
            "2020"
        );
        entorno.CrearCarpetaConFecha(
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            "Servidor",
            "2024"
        );
        entorno.CrearCarpetaConFecha(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            "Servidor",
            "2026"
        );

        var progreso = new ProgresoSincronico();
        entorno.Indexador.Indexar(new[] { raiz }, CancellationToken.None, progreso);

        Assert.Equal(
            new[] { "Servidor", "2026", "2024", "2020" },
            progreso.CarpetasVisitadasEnOrden
        );
    }

    private sealed class ProgresoSincronico : IProgress<ProgresoIndexado>
    {
        public List<string> CarpetasVisitadasEnOrden { get; } = new();

        public void Report(ProgresoIndexado value)
        {
            if (!value.Omitida)
            {
                CarpetasVisitadasEnOrden.Add(Path.GetFileName(value.CarpetaActual));
            }
        }
    }
}
