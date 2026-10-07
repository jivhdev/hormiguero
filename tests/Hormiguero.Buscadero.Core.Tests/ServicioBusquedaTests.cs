using Buscadero.Core.Busqueda;

namespace Buscadero.Core.Tests;

public sealed class ServicioBusquedaTests
{
    [Fact]
    public void ObtenerCarpetasParaSelector_IncluyeMadreAunqueNoEsteIndexada()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearCarpeta("Documentos", "Subcarpeta");
        entorno.ServicioCarpetas.Agregar(raiz);

        var carpetas = entorno.ServicioBusqueda.ObtenerCarpetasParaSelector();

        Assert.Equal(raiz, carpetas[0].Ruta);
        Assert.False(carpetas[0].NoDisponible);
    }

    [Fact]
    public void ObtenerCarpetasParaSelector_IncluyeSubcarpetasEnDiscoAunSinIndexar()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        var subcarpeta = entorno.CrearCarpeta("Documentos", "2026", "Marzo");
        entorno.ServicioCarpetas.Agregar(raiz);

        var carpetas = entorno.ServicioBusqueda.ObtenerCarpetasParaSelector();

        Assert.Contains(carpetas, carpeta => carpeta.Ruta == subcarpeta);
    }

    [Fact]
    public void ObtenerCarpetasParaSelector_MarcaMadreNoDisponible()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = Path.Combine(entorno.Raiz, "FueraDeLinea");
        entorno.ServicioCarpetas.Agregar(raiz);

        var carpeta = Assert.Single(entorno.ServicioBusqueda.ObtenerCarpetasParaSelector());

        Assert.Equal(raiz, carpeta.Ruta);
        Assert.True(carpeta.NoDisponible);
        Assert.Contains("(no disponible)", carpeta.Texto);
    }

    [Fact]
    public void FiltrarCarpetasParaSelector_BuscaEnCualquierParteDeLaRuta()
    {
        var opciones = new[]
        {
            new OpcionCarpetaBusqueda(@"C:\\Archivos\\2026\\Enero", false),
            new OpcionCarpetaBusqueda(@"C:\\Archivos\\2025\\Marzo", false),
        };

        var filtradas = ServicioBusqueda.FiltrarCarpetasParaSelector(opciones, "2026");

        Assert.Equal(@"C:\\Archivos\\2026\\Enero", Assert.Single(filtradas).Ruta);
    }

    [Fact]
    public void Buscar_CoincidenciaExacta_DevuelveElDocumento()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearArchivo(raiz, "12345 - Proveedor.pdf");
        entorno.ServicioCarpetas.Agregar(raiz);

        var resultados = entorno.ServicioBusqueda.Buscar("12345");

        var resultado = Assert.Single(resultados);
        Assert.Equal("12345 - Proveedor.pdf", resultado.Nombre);
    }

    [Fact]
    public void Buscar_CoincidenciasMultiples_DevuelveTodas()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        var sub = entorno.CrearCarpeta("Documentos", "2026");
        entorno.CrearArchivo(raiz, "12345.pdf");
        entorno.CrearArchivo(sub, "12345.pdf");
        entorno.ServicioCarpetas.Agregar(raiz);

        var resultados = entorno.ServicioBusqueda.Buscar("12345");

        Assert.Equal(2, resultados.Count);
    }

    [Fact]
    public void Buscar_OrdenaPorFechaDeModificacionDescendente()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        var antiguo = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var reciente = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        entorno.CrearArchivo(raiz, "12345 - Antiguo.pdf", antiguo);
        entorno.CrearArchivo(raiz, "12345 - Reciente.pdf", reciente);
        entorno.ServicioCarpetas.Agregar(raiz);

        var resultados = entorno.ServicioBusqueda.Buscar("12345");

        Assert.Equal(2, resultados.Count);
        Assert.Equal("12345 - Reciente.pdf", resultados[0].Nombre);
        Assert.Equal("12345 - Antiguo.pdf", resultados[1].Nombre);
    }

    [Fact]
    public void Buscar_ConFiltroDeCarpeta_AcotaLosResultados()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        var marzo = entorno.CrearCarpeta("Documentos", "2026", "Marzo");
        entorno.CrearArchivo(raiz, "12345 - General.pdf");
        entorno.CrearArchivo(marzo, "12345 - Marzo.pdf");
        entorno.ServicioCarpetas.Agregar(raiz);

        var resultados = entorno.ServicioBusqueda.Buscar("12345", marzo);

        var resultado = Assert.Single(resultados);
        Assert.Equal("12345 - Marzo.pdf", resultado.Nombre);
    }

    [Fact]
    public void Buscar_SinCoincidencias_DevuelveVacio()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearArchivo(raiz, "12345.pdf");
        entorno.ServicioCarpetas.Agregar(raiz);

        Assert.Empty(entorno.ServicioBusqueda.Buscar("99999"));
    }

    [Fact]
    public void Buscar_ModoExacto_NoConfundeNumerosPegados()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearArchivo(raiz, "123456.pdf");
        entorno.CrearArchivo(raiz, "12345F.pdf");
        entorno.ServicioCarpetas.Agregar(raiz);

        var resultados = entorno.ServicioBusqueda.Buscar("12345", null, ModoBusqueda.Exacto);

        Assert.Empty(resultados);
    }

    [Fact]
    public void Buscar_ModoTodos_EncuentraCodigoConPrefijoYCerosALaIzquierda()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearArchivo(raiz, "OCC0000020339.pdf");
        entorno.ServicioCarpetas.Agregar(raiz);

        var resultados = entorno.ServicioBusqueda.Buscar("20339");

        var resultado = Assert.Single(resultados);
        Assert.Equal("OCC0000020339.pdf", resultado.Nombre);
    }

    [Fact]
    public void Buscar_ModoTodos_EncuentraConEspacios()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearArchivo(raiz, "OCC0000020339.pdf");
        entorno.ServicioCarpetas.Agregar(raiz);

        var resultados = entorno.ServicioBusqueda.Buscar("occ 20339");

        var resultado = Assert.Single(resultados);
        Assert.Equal("OCC0000020339.pdf", resultado.Nombre);
    }

    [Fact]
    public void Buscar_ModoTodos_PriorizaExactoAntesQueSoloNumero()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearArchivo(raiz, "12345.pdf");
        entorno.CrearArchivo(raiz, "12345F.pdf");
        entorno.ServicioCarpetas.Agregar(raiz);

        var resultados = entorno.ServicioBusqueda.Buscar("12345");

        var resultado = Assert.Single(resultados);
        Assert.Equal("12345.pdf", resultado.Nombre);
    }

    [Fact]
    public void Buscar_ModoForzadoSoloNumero_IgnoraLetras()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearArchivo(raiz, "12345.pdf");
        entorno.CrearArchivo(raiz, "12345F.pdf");
        entorno.ServicioCarpetas.Agregar(raiz);

        var resultados = entorno.ServicioBusqueda.Buscar("12345", null, ModoBusqueda.SoloNumero);

        Assert.Equal(2, resultados.Count);
    }

    [Fact]
    public void Buscar_ModoForzadoAlfanumerico_IgnoraSeparadoresPeroNoCeros()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearArchivo(raiz, "OCC20339.pdf");
        entorno.CrearArchivo(raiz, "OCC0000020339.pdf");
        entorno.ServicioCarpetas.Agregar(raiz);

        var resultados = entorno.ServicioBusqueda.Buscar(
            "occ 20339",
            null,
            ModoBusqueda.Alfanumerico
        );

        var resultado = Assert.Single(resultados);
        Assert.Equal("OCC20339.pdf", resultado.Nombre);
    }

    [Fact]
    public void Buscar_ModoForzadoSoloLetras_IgnoraNumeros()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearArchivo(raiz, "OCC123.pdf");
        entorno.ServicioCarpetas.Agregar(raiz);

        var enModoExacto = entorno.ServicioBusqueda.Buscar("occ", null, ModoBusqueda.Exacto);
        var enModoSoloLetras = entorno.ServicioBusqueda.Buscar(
            "occ",
            null,
            ModoBusqueda.SoloLetras
        );

        Assert.Empty(enModoExacto);
        Assert.Single(enModoSoloLetras);
    }

    [Fact]
    public void ObtenerSugerenciasCarpeta_PriorizaLasMasUsadas()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        var marzo = entorno.CrearCarpeta("Documentos", "2026", "Marzo");
        var abril = entorno.CrearCarpeta("Documentos", "2026", "Abril");
        entorno.CrearArchivo(marzo, "12345.pdf");
        entorno.CrearArchivo(abril, "67890.pdf");
        entorno.ServicioCarpetas.Agregar(raiz);

        entorno.ServicioBusqueda.Buscar("12345", marzo);
        entorno.ServicioBusqueda.Buscar("99999", marzo);
        entorno.ServicioBusqueda.Buscar("67890", abril);

        var sugerencias = entorno.ServicioBusqueda.ObtenerSugerenciasCarpeta(4);

        Assert.Equal(marzo, sugerencias[0]);
    }

    [Fact]
    public void ObtenerSugerenciasCarpeta_SiempreIncluyeCarpetasMadre_AunConMuchosRecientes()
    {
        using var entorno = new EntornoDePrueba();
        var madreA = entorno.CrearCarpeta("MadreA");
        var madreB = entorno.CrearCarpeta("MadreB");
        entorno.ServicioCarpetas.Agregar(madreA);
        entorno.ServicioCarpetas.Agregar(madreB);

        // Se generan varias subcarpetas "recientes" muy usadas bajo MadreA, para que
        // compitan por los primeros puestos del historial de frecuencia.
        for (var i = 1; i <= 5; i++)
        {
            var sub = entorno.CrearCarpeta("MadreA", $"Sub{i}");
            entorno.CrearArchivo(sub, "1.pdf");
            entorno.ServicioBusqueda.Buscar("1", sub);
        }

        var sugerencias = entorno.ServicioBusqueda.ObtenerSugerenciasCarpeta(4);

        Assert.Contains(madreA, sugerencias);
        Assert.Contains(madreB, sugerencias);
    }

    [Fact]
    public void BuscarCarpetasPorNombre_CoincideConElInicioDelNombre()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearCarpeta("Documentos", "Facturas");
        entorno.CrearCarpeta("Documentos", "Fact-Anuladas");
        entorno.CrearCarpeta("Documentos", "Recibos");
        entorno.ServicioCarpetas.Agregar(raiz);
        entorno.Indexador.Indexar(new[] { raiz });

        var sugerencias = entorno.ServicioBusqueda.BuscarCarpetasPorNombre("fac");

        Assert.Equal(2, sugerencias.Count);
        Assert.DoesNotContain(
            sugerencias,
            s => s.EndsWith("Recibos", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void BuscarCarpetasPorNombre_CoincideEnCualquierParteDelNombre()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearCarpeta("Documentos", "Facturas");
        entorno.CrearCarpeta("Documentos", "Recibos");
        entorno.ServicioCarpetas.Agregar(raiz);
        entorno.Indexador.Indexar(new[] { raiz });

        var sugerencias = entorno.ServicioBusqueda.BuscarCarpetasPorNombre("tura");

        var sugerencia = Assert.Single(sugerencias);
        Assert.EndsWith("Facturas", sugerencia);
    }

    [Fact]
    public void BuscarCarpetasPorNombre_SeAcotaConMasLetras()
    {
        using var entorno = new EntornoDePrueba();
        var raiz = entorno.CrearCarpeta("Documentos");
        entorno.CrearCarpeta("Documentos", "Facturas2025");
        entorno.CrearCarpeta("Documentos", "Facturas2026");
        entorno.ServicioCarpetas.Agregar(raiz);
        entorno.Indexador.Indexar(new[] { raiz });

        var conTresLetras = entorno.ServicioBusqueda.BuscarCarpetasPorNombre("fac");
        var conMasLetras = entorno.ServicioBusqueda.BuscarCarpetasPorNombre("facturas2026");

        Assert.Equal(2, conTresLetras.Count);
        Assert.Single(conMasLetras);
    }

    [Fact]
    public void Buscar_AlcancePrimeraCoincidencia_SeDetieneEnLaPrimeraCarpetaConCoincidencia()
    {
        using var entorno = new EntornoDePrueba();
        var carpetaA = entorno.CrearCarpeta("CarpetaA");
        var carpetaB = entorno.CrearCarpeta("CarpetaB");
        var carpetaC = entorno.CrearCarpeta("CarpetaC");
        entorno.CrearArchivo(carpetaB, "12345.pdf");
        entorno.CrearArchivo(carpetaC, "12345.pdf");
        entorno.ServicioCarpetas.Agregar(carpetaA);
        entorno.ServicioCarpetas.Agregar(carpetaB);
        entorno.ServicioCarpetas.Agregar(carpetaC);

        var resultados = entorno.ServicioBusqueda.Buscar(
            "12345",
            null,
            ModoBusqueda.Todos,
            AlcanceBusqueda.PrimeraCoincidencia
        );

        var resultado = Assert.Single(resultados);
        Assert.Equal(carpetaB, resultado.Carpeta);
    }

    [Fact]
    public void Buscar_AlcanceTodasLasCarpetas_ReuneCoincidenciasDeCarpetasDistintas()
    {
        using var entorno = new EntornoDePrueba();
        var carpetaA = entorno.CrearCarpeta("CarpetaA");
        var carpetaB = entorno.CrearCarpeta("CarpetaB");
        entorno.CrearArchivo(carpetaA, "12345.pdf");
        entorno.CrearArchivo(carpetaB, "12345.pdf");
        entorno.ServicioCarpetas.Agregar(carpetaA);
        entorno.ServicioCarpetas.Agregar(carpetaB);

        var resultados = entorno.ServicioBusqueda.Buscar(
            "12345",
            null,
            ModoBusqueda.Todos,
            AlcanceBusqueda.TodasLasCarpetas
        );

        Assert.Equal(2, resultados.Count);
        Assert.Contains(resultados, r => r.Carpeta == carpetaA);
        Assert.Contains(resultados, r => r.Carpeta == carpetaB);
    }

    [Fact]
    public void Buscar_AlcanceDefault_EsPrimeraCoincidencia()
    {
        using var entorno = new EntornoDePrueba();
        var carpetaA = entorno.CrearCarpeta("CarpetaA");
        var carpetaB = entorno.CrearCarpeta("CarpetaB");
        entorno.CrearArchivo(carpetaA, "12345.pdf");
        entorno.CrearArchivo(carpetaB, "12345.pdf");
        entorno.ServicioCarpetas.Agregar(carpetaA);
        entorno.ServicioCarpetas.Agregar(carpetaB);

        var resultados = entorno.ServicioBusqueda.Buscar("12345");

        Assert.Single(resultados);
    }

    [Fact]
    public void Buscar_ModoTodosPorDefecto_MultiplesCarpetasMadre_EncuentraPrefijoConCeros()
    {
        using var entorno = new EntornoDePrueba();
        var carpetaA = entorno.CrearCarpeta("CarpetaA");
        var carpetaB = entorno.CrearCarpeta("CarpetaB");
        entorno.CrearArchivo(carpetaA, "algo-sin-relacion.pdf");
        entorno.CrearArchivo(carpetaB, "FCV0000025253.pdf");
        entorno.ServicioCarpetas.Agregar(carpetaA);
        entorno.ServicioCarpetas.Agregar(carpetaB);

        // Modo y alcance por defecto, tal como haria el usuario sin tocar los selectores.
        var resultados = entorno.ServicioBusqueda.Buscar("25253");

        var resultado = Assert.Single(resultados);
        Assert.Equal("FCV0000025253.pdf", resultado.Nombre);
    }

    [Theory]
    [InlineData(AlcanceBusqueda.PrimeraCoincidencia)]
    [InlineData(AlcanceBusqueda.TodasLasCarpetas)]
    public void Buscar_MatrizModosPorAlcance_EncuentraPrefijoConCerosEnCualquierCarpetaMadre(
        AlcanceBusqueda alcance
    )
    {
        using var entorno = new EntornoDePrueba();
        var carpetaA = entorno.CrearCarpeta("CarpetaA");
        var carpetaB = entorno.CrearCarpeta("CarpetaB");
        entorno.CrearArchivo(carpetaA, "sin-relacion.pdf");
        entorno.CrearArchivo(carpetaB, "OCC0000020474.pdf");
        entorno.ServicioCarpetas.Agregar(carpetaA);
        entorno.ServicioCarpetas.Agregar(carpetaB);

        var resultados = entorno.ServicioBusqueda.Buscar(
            "20474",
            null,
            ModoBusqueda.Todos,
            alcance
        );

        var resultado = Assert.Single(resultados);
        Assert.Equal("OCC0000020474.pdf", resultado.Nombre);
    }

    [Fact]
    public void Buscar_ConFiltroDeCarpeta_IgnoraElAlcance()
    {
        using var entorno = new EntornoDePrueba();
        var carpetaA = entorno.CrearCarpeta("CarpetaA");
        var carpetaB = entorno.CrearCarpeta("CarpetaB");
        entorno.CrearArchivo(carpetaA, "12345.pdf");
        entorno.CrearArchivo(carpetaB, "12345.pdf");
        entorno.ServicioCarpetas.Agregar(carpetaA);
        entorno.ServicioCarpetas.Agregar(carpetaB);

        var resultados = entorno.ServicioBusqueda.Buscar(
            "12345",
            carpetaB,
            ModoBusqueda.Todos,
            AlcanceBusqueda.TodasLasCarpetas
        );

        var resultado = Assert.Single(resultados);
        Assert.Equal(carpetaB, resultado.Carpeta);
    }
}
