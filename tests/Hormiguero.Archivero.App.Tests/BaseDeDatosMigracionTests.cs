using Archivero.Datos;
using Microsoft.Data.Sqlite;

namespace Archivero.Tests;

/// <summary>
/// Caso-3: la migración que recrea la tabla Configuraciones (el CHECK viejo solo aceptaba
/// 'Directo'/'Anio'/'AnioMes') tiene que conservar intactos los datos ya guardados y permitir
/// los tipos de organización nuevos, sin romperse al correr varias veces.
/// </summary>
public class BaseDeDatosMigracionTests : IDisposable
{
    private readonly string _rutaDbTemporal;

    public BaseDeDatosMigracionTests()
    {
        _rutaDbTemporal = Path.Combine(Path.GetTempPath(), $"archivero-tests-{Guid.NewGuid():N}.db");
        BaseDeDatos.RutaArchivo = _rutaDbTemporal;
        CrearBaseConEsquemaAnteriorACaso3();
    }

    /// <summary>Una base como las de antes de Caso-3: CHECK viejo, y sin las columnas que se
    /// agregaron por migraciones previas (TextoReferencia, Motivo, AbrirDespuesDeGuardar).</summary>
    private void CrearBaseConEsquemaAnteriorACaso3()
    {
        using var conexion = new SqliteConnection($"Data Source={_rutaDbTemporal}");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            CREATE TABLE Configuracion (
                Clave TEXT PRIMARY KEY,
                Valor TEXT NOT NULL
            );

            CREATE TABLE EntidadesConocidas (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Categoria TEXT NOT NULL CHECK (Categoria IN ('Emisor', 'Tipo')),
                Nombre TEXT NOT NULL,
                UNIQUE (Categoria, Nombre)
            );

            CREATE TABLE Configuraciones (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EmisorId INTEGER NOT NULL REFERENCES EntidadesConocidas (Id),
                TipoId INTEGER NOT NULL REFERENCES EntidadesConocidas (Id),
                CarpetaDestino TEXT NOT NULL,
                FormatoCarpeta TEXT NOT NULL CHECK (FormatoCarpeta IN ('Directo', 'Anio', 'AnioMes')),
                PatronCarpeta TEXT NULL,
                Renombrar INTEGER NOT NULL DEFAULT 0,
                UNIQUE (EmisorId, TipoId)
            );

            CREATE TABLE PatronesReconocimiento (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ConfiguracionId INTEGER NOT NULL REFERENCES Configuraciones (Id)
            );

            CREATE TABLE Marcas (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PatronId INTEGER NOT NULL REFERENCES PatronesReconocimiento (Id),
                Campo TEXT NOT NULL CHECK (Campo IN ('Emisor', 'Tipo', 'Fecha', 'NombreArchivo')),
                Pagina INTEGER NOT NULL,
                X REAL NOT NULL,
                Y REAL NOT NULL,
                Ancho REAL NOT NULL,
                Alto REAL NOT NULL,
                UNIQUE (PatronId, Campo)
            );

            CREATE TABLE Pendientes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RutaArchivo TEXT NOT NULL UNIQUE,
                FechaDetectado TEXT NOT NULL
            );

            CREATE TABLE Borradores (
                RutaArchivo TEXT PRIMARY KEY,
                Datos TEXT NOT NULL
            );

            INSERT INTO EntidadesConocidas (Categoria, Nombre) VALUES ('Emisor', 'Proveedor X');
            INSERT INTO EntidadesConocidas (Categoria, Nombre) VALUES ('Tipo', 'Factura');
            INSERT INTO Configuraciones (EmisorId, TipoId, CarpetaDestino, FormatoCarpeta, PatronCarpeta, Renombrar)
            VALUES (1, 2, 'C:\Destino', 'AnioMes', 'yyyy\MM', 1);
            INSERT INTO PatronesReconocimiento (ConfiguracionId) VALUES (1);
            INSERT INTO Marcas (PatronId, Campo, Pagina, X, Y, Ancho, Alto) VALUES (1, 'Fecha', 0, 0.1, 0.2, 0.3, 0.05);
            INSERT INTO Pendientes (RutaArchivo, FechaDetectado) VALUES ('C:\Observada\doc.pdf', '2026-09-01');
            """;
        comando.ExecuteNonQuery();
    }

    [Fact]
    public void AsegurarEsquema_ConBaseVieja_ConservaLosDatosYAceptaLosTiposNuevos()
    {
        BaseDeDatos.AsegurarEsquema();

        var repo = new ConfiguracionDocumentoRepository();
        var config = repo.BuscarPorEmisorYTipo("Proveedor X", "Factura");

        Assert.NotNull(config);
        Assert.Equal(FormatoCarpeta.AnioMes, config.FormatoCarpeta);
        Assert.Equal(@"yyyy\MM", config.PatronCarpeta);
        Assert.True(config.Renombrar);
        Assert.False(config.AbrirDespuesDeGuardar);
        Assert.False(config.PreguntarNombre);
        Assert.Single(config.Patrones);
        Assert.Equal(CampoMarca.Fecha, Assert.Single(config.Patrones[0].Marcas).Campo);

        // El CHECK nuevo acepta los tipos de Caso-3, y la columna de Caso-11 sobrevive a la
        // recreación de la tabla (se agrega después de reconstruirla, no antes).
        repo.ActualizarDestino(config.Id, @"C:\Destino", FormatoCarpeta.SemanaDelMes, @"MM\'Semana 'N", true, true, true);
        var actualizada = repo.BuscarPorEmisorYTipo("Proveedor X", "Factura")!;
        Assert.Equal(FormatoCarpeta.SemanaDelMes, actualizada.FormatoCarpeta);
        Assert.Equal(@"MM\'Semana 'N", actualizada.PatronCarpeta);
        Assert.True(actualizada.AbrirDespuesDeGuardar);
        Assert.True(actualizada.PreguntarNombre);

        // Y crear configuraciones nuevas sigue funcionando tras la recreación de la tabla
        // (AUTOINCREMENT intacto).
        var idNueva = repo.GuardarNueva("Otro", "Otro Tipo", @"C:\Otro", FormatoCarpeta.AnioTrimestre, @"yyyy\'T'T", false, new List<Marca>());
        Assert.True(idNueva > actualizada.Id);
        Assert.NotNull(repo.BuscarPorEmisorYTipo("Otro", "Otro Tipo"));
    }

    [Fact]
    public void AsegurarEsquema_DosVeces_EsIdempotente()
    {
        BaseDeDatos.AsegurarEsquema();
        BaseDeDatos.AsegurarEsquema();

        var repo = new ConfiguracionDocumentoRepository();
        var config = repo.BuscarPorEmisorYTipo("Proveedor X", "Factura");

        Assert.NotNull(config);
        Assert.Equal(FormatoCarpeta.AnioMes, config.FormatoCarpeta);
        Assert.Single(config.Patrones);
    }

    [Fact]
    public void AsegurarEsquema_ConBaseNueva_CreaTodoDirectoConElCheckNuevo()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_rutaDbTemporal);

        BaseDeDatos.AsegurarEsquema();

        var repo = new ConfiguracionDocumentoRepository();
        repo.GuardarNueva("Proveedor Y", "Guía", @"C:\Destino", FormatoCarpeta.AnioSemana, @"yyyy\'Semana 'WW", false, new List<Marca>());

        Assert.Equal(FormatoCarpeta.AnioSemana, repo.BuscarPorEmisorYTipo("Proveedor Y", "Guía")!.FormatoCarpeta);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_rutaDbTemporal);
    }
}
