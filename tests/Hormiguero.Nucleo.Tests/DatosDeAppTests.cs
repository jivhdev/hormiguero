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
    public void Nunca_copia_datos_de_la_ubicacion_anterior()
    {
        string anterior = BaseAnterior(@"C:\Entrada");
        DateTime antes = File.GetLastWriteTimeUtc(anterior);

        string destino = DatosDeApp.Preparar(Comun, "archivero", anterior, Ahora);

        Assert.Equal(Path.Combine(Comun, "archivero.db"), destino);
        Assert.False(File.Exists(destino));
        Assert.True(File.Exists(anterior));
        Assert.Equal(antes, File.GetLastWriteTimeUtc(anterior));
    }

    [Fact]
    public void No_reemplaza_la_base_comun_existente_con_una_ubicacion_anterior()
    {
        string anterior = BaseAnterior(@"C:\Entrada");
        string destino = DatosDeApp.Preparar(Comun, "archivero", anterior, Ahora);
        Assert.False(File.Exists(destino));
        using (var conexion = new SqliteConnection($"Data Source={destino};Pooling=False"))
        {
            conexion.Open();
            using var comando = conexion.CreateCommand();
            comando.CommandText =
                "CREATE TABLE Configuracion(Clave TEXT, Valor TEXT); INSERT INTO Configuracion VALUES('x','C:\\Nueva');";
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
    public void Respalda_la_base_existente_con_el_nombre_de_la_app()
    {
        string destino = Path.Combine(Comun, "archivero.db");
        Directory.CreateDirectory(Comun);
        using (var conexion = new SqliteConnection($"Data Source={destino};Pooling=False"))
        {
            conexion.Open();
            using var crear = conexion.CreateCommand();
            crear.CommandText =
                "CREATE TABLE Configuracion(Clave TEXT, Valor TEXT); INSERT INTO Configuracion VALUES('x','dato');";
            crear.ExecuteNonQuery();
        }

        DatosDeApp.Preparar(Comun, "archivero", rutaAnterior: null, Ahora);

        string respaldo = Assert.Single(
            Directory.GetFiles(Path.Combine(Comun, "respaldos"), "archivero-*.db")
        );
        Assert.Equal("dato", Leer(respaldo));
    }

    [Fact]
    public void La_marca_de_reinicio_no_habilita_ni_requiere_copiar_datos_anteriores()
    {
        string anterior = BaseAnterior(@"C:\Entrada");
        DatosDeApp.MarcarReinicio(Comun, Ahora);

        string destino = DatosDeApp.Preparar(Comun, "archivero", anterior, Ahora);

        Assert.False(File.Exists(destino));
        Assert.True(File.Exists(Path.Combine(Comun, "reiniciado.txt")));
        Assert.True(File.Exists(anterior));
    }

    [Fact]
    public void Guarda_preferencias_separadas_por_app_y_las_lee()
    {
        DatosDeApp.GuardarPreferencia("tema.archivero", "Claro", Comun);
        DatosDeApp.GuardarPreferencia("tema.buscadero", "Oscuro", Comun);

        Assert.Equal("Claro", DatosDeApp.LeerPreferencia("tema.archivero", Comun));
        Assert.Equal("Oscuro", DatosDeApp.LeerPreferencia("tema.buscadero", Comun));
        Assert.Equal(
            "{\"tema.archivero\":\"Claro\",\"tema.buscadero\":\"Oscuro\"}",
            File.ReadAllText(Path.Combine(Comun, "preferencias.json"))
        );
    }

    [Fact]
    public void Preferencias_ausentes_o_danadas_se_ignoran()
    {
        Directory.CreateDirectory(Comun);
        Assert.Null(DatosDeApp.LeerPreferencia("tema.archivero", Comun));
        File.WriteAllText(Path.Combine(Comun, "preferencias.json"), "{");

        Assert.Null(DatosDeApp.LeerPreferencia("tema.archivero", Comun));

        DatosDeApp.GuardarPreferencia("tema.archivero", "Sistema", Comun);
        Assert.Equal("Sistema", DatosDeApp.LeerPreferencia("tema.archivero", Comun));
    }
}
