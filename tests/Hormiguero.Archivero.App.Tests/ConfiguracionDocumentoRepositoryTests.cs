using Archivero.Datos;

namespace Archivero.Tests;

public class ConfiguracionDocumentoRepositoryTests : IDisposable
{
    private readonly string _rutaDbTemporal;

    public ConfiguracionDocumentoRepositoryTests()
    {
        _rutaDbTemporal = Path.Combine(Path.GetTempPath(), $"archivero-tests-{Guid.NewGuid():N}.db");
        BaseDeDatos.RutaArchivo = _rutaDbTemporal;
        BaseDeDatos.AsegurarEsquema();
    }

    [Fact]
    public void ExisteCoincidenciaExacta_ConEmisorYTipoYaGuardados_DevuelveTrue()
    {
        var repo = new ConfiguracionDocumentoRepository();
        repo.GuardarNueva("Banco Galicia", "Resumen de cuenta", @"C:\Destino", FormatoCarpeta.Directo, null, false, new List<Marca>());

        Assert.True(repo.ExisteCoincidenciaExacta("Banco Galicia", "Resumen de cuenta"));
    }

    [Fact]
    public void ExisteCoincidenciaExacta_ConEmisorDistinto_DevuelveFalse()
    {
        var repo = new ConfiguracionDocumentoRepository();
        repo.GuardarNueva("Banco Galicia", "Resumen de cuenta", @"C:\Destino", FormatoCarpeta.Directo, null, false, new List<Marca>());

        Assert.False(repo.ExisteCoincidenciaExacta("Banco Nacion", "Resumen de cuenta"));
    }

    [Fact]
    public void ExisteCoincidenciaExacta_ConTipoDistinto_DevuelveFalse()
    {
        var repo = new ConfiguracionDocumentoRepository();
        repo.GuardarNueva("Banco Galicia", "Resumen de cuenta", @"C:\Destino", FormatoCarpeta.Directo, null, false, new List<Marca>());

        Assert.False(repo.ExisteCoincidenciaExacta("Banco Galicia", "Factura"));
    }

    [Fact]
    public void ExisteCoincidenciaExacta_SinNingunaConfiguracionGuardada_DevuelveFalse()
    {
        var repo = new ConfiguracionDocumentoRepository();

        Assert.False(repo.ExisteCoincidenciaExacta("Cualquiera", "Cualquiera"));
    }

    [Fact]
    public void EliminarConfiguracion_BorraLaConfiguracionYSusPatronesYMarcas()
    {
        var repo = new ConfiguracionDocumentoRepository();
        var marcas = new List<Marca>
        {
            new(CampoMarca.Emisor, 0, 0.1, 0.1, 0.2, 0.05),
            new(CampoMarca.Tipo, 0, 0.1, 0.2, 0.2, 0.05)
        };
        var id = repo.GuardarNueva("Banco Galicia", "Resumen de cuenta", @"C:\Destino", FormatoCarpeta.Directo, null, false, marcas);

        repo.EliminarConfiguracion(id);

        Assert.Null(repo.BuscarPorEmisorYTipo("Banco Galicia", "Resumen de cuenta"));
        Assert.Empty(repo.ObtenerTodasConPatrones());
    }

    [Fact]
    public void GuardarNueva_SinIndicarAbrirDespuesDeGuardar_ArrancaEnFalse()
    {
        var repo = new ConfiguracionDocumentoRepository();
        repo.GuardarNueva("Banco Galicia", "Resumen de cuenta", @"C:\Destino", FormatoCarpeta.Directo, null, false, new List<Marca>());

        var configuracion = repo.BuscarPorEmisorYTipo("Banco Galicia", "Resumen de cuenta");

        Assert.NotNull(configuracion);
        Assert.False(configuracion!.AbrirDespuesDeGuardar);
    }

    [Fact]
    public void GuardarNueva_ConAbrirDespuesDeGuardarActivado_LoPersiste()
    {
        var repo = new ConfiguracionDocumentoRepository();
        repo.GuardarNueva("Banco Galicia", "Resumen de cuenta", @"C:\Destino", FormatoCarpeta.Directo, null, false, new List<Marca>(), abrirDespuesDeGuardar: true);

        var configuracion = repo.BuscarPorEmisorYTipo("Banco Galicia", "Resumen de cuenta");

        Assert.NotNull(configuracion);
        Assert.True(configuracion!.AbrirDespuesDeGuardar);
    }

    [Fact]
    public void ObtenerTodasConPatrones_ConUnPatronSinMarcas_LoDevuelveComoPatronVacio()
    {
        // Caso-1, punto 1: un "patron sin texto" (documento sin texto extraible) no tiene
        // ninguna marca. Antes se perdia al leerlo de vuelta (INNER JOIN desde Marcas).
        var repo = new ConfiguracionDocumentoRepository();
        repo.GuardarNueva("Proveedor Escaneado", "Recibo escaneado", @"C:\Destino", FormatoCarpeta.Directo, null, false, new List<Marca>());

        var configuraciones = repo.ObtenerTodasConPatrones();

        var configuracion = Assert.Single(configuraciones);
        var patron = Assert.Single(configuracion.Patrones);
        Assert.Empty(patron.Marcas);
    }

    [Fact]
    public void BuscarPorEmisorYTipo_DevuelveElPatronConLasMarcasGuardadas()
    {
        var repo = new ConfiguracionDocumentoRepository();
        var marcas = new List<Marca>
        {
            new(CampoMarca.Emisor, 0, 0.1, 0.1, 0.2, 0.05),
            new(CampoMarca.Tipo, 0, 0.1, 0.2, 0.2, 0.05)
        };
        repo.GuardarNueva("Banco Galicia", "Resumen de cuenta", @"C:\Destino", FormatoCarpeta.Anio, "yyyy", true, marcas);

        var configuracion = repo.BuscarPorEmisorYTipo("Banco Galicia", "Resumen de cuenta");

        Assert.NotNull(configuracion);
        var patron = Assert.Single(configuracion!.Patrones);
        Assert.Equal(2, patron.Marcas.Count);
        Assert.Contains(patron.Marcas, m => m.Campo == CampoMarca.Emisor);
        Assert.Contains(patron.Marcas, m => m.Campo == CampoMarca.Tipo);
    }

    [Fact]
    public void AgregarPatronAConfiguracionExistente_SumaUnSegundoPatron()
    {
        var repo = new ConfiguracionDocumentoRepository();
        var configuracionId = repo.GuardarNueva(
            "Banco Galicia", "Resumen de cuenta", @"C:\Destino", FormatoCarpeta.Directo, null, false,
            [new Marca(CampoMarca.Emisor, 0, 0.1, 0.1, 0.2, 0.05), new Marca(CampoMarca.Tipo, 0, 0.1, 0.2, 0.2, 0.05)]);

        repo.AgregarPatronAConfiguracionExistente(
            configuracionId,
            [new Marca(CampoMarca.Emisor, 0, 0.3, 0.3, 0.2, 0.05), new Marca(CampoMarca.Tipo, 0, 0.3, 0.4, 0.2, 0.05)]);

        var configuracion = repo.BuscarPorEmisorYTipo("Banco Galicia", "Resumen de cuenta");

        Assert.NotNull(configuracion);
        Assert.Equal(2, configuracion!.Patrones.Count);
    }

    [Fact]
    public void ActualizarDestino_CambiaCarpetaFormatoRenombrarYAbrirDespuesDeGuardar_SinTocarElEmisorNiElTipo()
    {
        var repo = new ConfiguracionDocumentoRepository();
        var configuracionId = repo.GuardarNueva(
            "Banco Galicia", "Resumen de cuenta", @"C:\Destino", FormatoCarpeta.Directo, null, false,
            [new Marca(CampoMarca.Emisor, 0, 0.1, 0.1, 0.2, 0.05), new Marca(CampoMarca.Tipo, 0, 0.1, 0.2, 0.2, 0.05)]);

        repo.ActualizarDestino(configuracionId, @"C:\OtroDestino", FormatoCarpeta.Anio, "yyyy", true, true, false);

        var configuracion = repo.BuscarPorEmisorYTipo("Banco Galicia", "Resumen de cuenta");

        Assert.NotNull(configuracion);
        Assert.Equal(@"C:\OtroDestino", configuracion!.CarpetaDestino);
        Assert.Equal(FormatoCarpeta.Anio, configuracion.FormatoCarpeta);
        Assert.Equal("yyyy", configuracion.PatronCarpeta);
        Assert.True(configuracion.Renombrar);
        Assert.True(configuracion.AbrirDespuesDeGuardar);
    }

    // ----- Caso-11, punto 1: "Preguntar el nombre cada vez" -----

    [Fact]
    public void GuardarNueva_SinIndicarPreguntarNombre_ArrancaEnFalse()
    {
        var repo = new ConfiguracionDocumentoRepository();
        repo.GuardarNueva("Banco Galicia", "Resumen de cuenta", @"C:\Destino", FormatoCarpeta.Directo, null, false, new List<Marca>());

        Assert.False(repo.BuscarPorEmisorYTipo("Banco Galicia", "Resumen de cuenta")!.PreguntarNombre);
    }

    [Fact]
    public void GuardarNueva_ConPreguntarNombre_LoPersisteEnTodasLasLecturas()
    {
        var repo = new ConfiguracionDocumentoRepository();
        repo.GuardarNueva("Banco Galicia", "Resumen de cuenta", @"C:\Destino", FormatoCarpeta.Directo, null, false,
            new List<Marca>(), preguntarNombre: true);

        Assert.True(repo.BuscarPorEmisorYTipo("Banco Galicia", "Resumen de cuenta")!.PreguntarNombre);
        Assert.True(Assert.Single(repo.ObtenerTodas()).PreguntarNombre);
        Assert.True(Assert.Single(repo.ObtenerTodasConPatrones()).PreguntarNombre);
    }

    [Fact]
    public void ActualizarDestino_ActivaYDesactivaPreguntarNombre()
    {
        var repo = new ConfiguracionDocumentoRepository();
        var id = repo.GuardarNueva("Banco Galicia", "Resumen de cuenta", @"C:\Destino", FormatoCarpeta.Directo, null, false, new List<Marca>());

        repo.ActualizarDestino(id, @"C:\Destino", FormatoCarpeta.Directo, null, false, false, true);
        Assert.True(repo.BuscarPorEmisorYTipo("Banco Galicia", "Resumen de cuenta")!.PreguntarNombre);

        repo.ActualizarDestino(id, @"C:\Destino", FormatoCarpeta.Directo, null, false, false, false);
        Assert.False(repo.BuscarPorEmisorYTipo("Banco Galicia", "Resumen de cuenta")!.PreguntarNombre);
    }

    [Fact]
    public void ActualizarPatron_ReemplazaLasMarcasDeEsePatron()
    {
        var repo = new ConfiguracionDocumentoRepository();
        var configuracionId = repo.GuardarNueva(
            "Banco Galicia", "Resumen de cuenta", @"C:\Destino", FormatoCarpeta.Directo, null, false,
            [new Marca(CampoMarca.Emisor, 0, 0.1, 0.1, 0.2, 0.05, "12.345.678-9"), new Marca(CampoMarca.Tipo, 0, 0.1, 0.2, 0.2, 0.05, "Resumen")]);

        var configuracionAntes = repo.BuscarPorEmisorYTipo("Banco Galicia", "Resumen de cuenta")!;
        var patronId = Assert.Single(configuracionAntes.Patrones).Id;

        repo.ActualizarPatron(patronId,
            [new Marca(CampoMarca.Emisor, 0, 0.5, 0.5, 0.2, 0.05, "otro-rut"), new Marca(CampoMarca.Tipo, 0, 0.5, 0.6, 0.2, 0.05, "Resumen v2")]);

        var configuracionDespues = repo.BuscarPorEmisorYTipo("Banco Galicia", "Resumen de cuenta")!;
        var patron = Assert.Single(configuracionDespues.Patrones);

        Assert.Equal(2, patron.Marcas.Count);
        Assert.Contains(patron.Marcas, m => m.Campo == CampoMarca.Emisor && m.TextoReferencia == "otro-rut");
        Assert.Contains(patron.Marcas, m => m.Campo == CampoMarca.Tipo && m.TextoReferencia == "Resumen v2");
    }

    [Fact]
    public void ObtenerTodas_DevuelveTodasLasConfiguracionesGuardadas()
    {
        var repo = new ConfiguracionDocumentoRepository();
        repo.GuardarNueva("Banco Galicia", "Resumen de cuenta", @"C:\Destino1", FormatoCarpeta.Directo, null, false, []);
        repo.GuardarNueva("Banco Nacion", "Factura", @"C:\Destino2", FormatoCarpeta.Directo, null, false, []);

        var todas = repo.ObtenerTodas();

        Assert.Equal(2, todas.Count);
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite reutiliza handles nativos por cadena de conexion (pooling);
        // hay que vaciar el pool antes de borrar el archivo o queda "en uso".
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (File.Exists(_rutaDbTemporal))
        {
            File.Delete(_rutaDbTemporal);
        }
    }
}
