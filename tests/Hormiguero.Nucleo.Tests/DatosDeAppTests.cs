using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Tests;

public sealed class DatosDeAppTests : IDisposable
{
    private readonly string raiz = Path.Combine(
        Path.GetTempPath(),
        "HormigueroDatosDeApp",
        Guid.NewGuid().ToString("N")
    );

    private string Comun => Path.Combine(raiz, "Hormiguero");
    private static readonly DateTime Ahora = new(2026, 10, 4, 10, 0, 0);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(raiz))
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    private string BaseAnterior(string valor)
    {
        string ruta = Path.Combine(raiz, "Archivero", "archivero.db");
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        using var conexion = new SqliteConnection($"Data Source={ruta};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "PRAGMA journal_mode=WAL; CREATE TABLE Configuracion(Clave TEXT, Valor TEXT); "
            + "INSERT INTO Configuracion VALUES('CarpetaObservada', $valor);";
        comando.Parameters.AddWithValue("$valor", valor);
        comando.ExecuteNonQuery();
        return ruta;
    }

    private static string? Leer(string ruta)
    {
        using var conexion = new SqliteConnection($"Data Source={ruta};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT Valor FROM Configuracion;";
        return comando.ExecuteScalar() as string;
    }

    [Fact]
    public void La_primera_vez_copia_los_datos_anteriores_y_no_los_toca()
    {
        string anterior = BaseAnterior(@"C:\Entrada");
        DateTime antes = File.GetLastWriteTimeUtc(anterior);

        string destino = DatosDeApp.Preparar(Comun, "archivero", anterior, Ahora);

        Assert.Equal(Path.Combine(Comun, "archivero.db"), destino);
        Assert.Equal(@"C:\Entrada", Leer(destino));
        Assert.True(File.Exists(anterior));
        Assert.Equal(antes, File.GetLastWriteTimeUtc(anterior));
    }

    [Fact]
    public void Si_ya_se_copio_no_vuelve_a_copiar_encima()
    {
        string anterior = BaseAnterior(@"C:\Entrada");
        string destino = DatosDeApp.Preparar(Comun, "archivero", anterior, Ahora);
        using (var conexion = new SqliteConnection($"Data Source={destino};Pooling=False"))
        {
            conexion.Open();
            using var comando = conexion.CreateCommand();
            comando.CommandText = "UPDATE Configuracion SET Valor = 'C:\\Nueva';";
            comando.ExecuteNonQuery();
        }

        DatosDeApp.Preparar(Comun, "archivero", anterior, Ahora.AddDays(1));

        Assert.Equal(@"C:\Nueva", Leer(destino));
    }

    [Fact]
    public void Sin_datos_anteriores_solo_devuelve_la_ruta_nueva()
    {
        string destino = DatosDeApp.Preparar(
            Comun,
            "buscadero",
            Path.Combine(raiz, "no-existe.db"),
            Ahora
        );

        Assert.Equal(Path.Combine(Comun, "buscadero.db"), destino);
        Assert.False(File.Exists(destino));
    }

    [Fact]
    public void Respalda_con_el_nombre_de_la_app()
    {
        string anterior = BaseAnterior(@"C:\Entrada");

        DatosDeApp.Preparar(Comun, "archivero", anterior, Ahora);

        string respaldo = Assert.Single(
            Directory.GetFiles(Path.Combine(Comun, "respaldos"), "archivero-*.db")
        );
        Assert.Equal(@"C:\Entrada", Leer(respaldo));
    }

    [Fact]
    public void La_marca_de_reinicio_impide_copiar_datos_anteriores()
    {
        string anterior = BaseAnterior(@"C:\Entrada");
        DatosDeApp.MarcarReinicio(Comun, Ahora);

        string destino = DatosDeApp.Preparar(Comun, "archivero", anterior, Ahora);

        Assert.False(File.Exists(destino));
        Assert.True(File.Exists(Path.Combine(Comun, "reiniciado.txt")));
        Assert.True(File.Exists(anterior));
    }
}
