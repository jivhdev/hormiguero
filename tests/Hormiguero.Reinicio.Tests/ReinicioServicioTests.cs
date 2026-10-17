using Hormiguero.Reinicio;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Reinicio.Tests;

public sealed class ReinicioServicioTests : IDisposable
{
    private readonly string raiz = Path.Combine(
        Path.GetTempPath(),
        "HormigueroReinicioTests",
        Guid.NewGuid().ToString("N")
    );
    private string Carpeta => Path.Combine(raiz, "Hormiguero");
    private static readonly DateTime Ahora = new(2026, 10, 6, 12, 34, 56);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(raiz))
            Directory.Delete(raiz, recursive: true);
    }

    [Fact]
    public void Respalda_completo_y_limpia_datos_de_trabajo_sin_tocar_mensajero()
    {
        CrearDatos();
        File.WriteAllText(Path.Combine(Carpeta, "preferencias.json"), "preferencias");
        File.WriteAllText(Path.Combine(Carpeta, "archivero-auditoria.log"), "registro");

        ResultadoReinicio resultado = new ReinicioServicio().Ejecutar(Carpeta, Ahora);

        Assert.True(resultado.Exitoso, resultado.Mensaje);
        Assert.Equal(Carpeta + "-respaldo-reinicio-20261006-123456", resultado.RutaRespaldo);
        Assert.Equal(
            "preferencias",
            File.ReadAllText(Path.Combine(resultado.RutaRespaldo, "preferencias.json"))
        );
        Assert.Equal(
            "registro",
            File.ReadAllText(Path.Combine(resultado.RutaRespaldo, "archivero-auditoria.log"))
        );
        Assert.True(File.Exists(Path.Combine(Carpeta, "reiniciado.txt")));
        Assert.Equal(1L, Contar(Path.Combine(Carpeta, "hormiguero.db"), "diccionario_datos"));
        Assert.Equal(1L, Contar(Path.Combine(Carpeta, "hormiguero.db"), "identificaciones"));
        Assert.Equal(1L, Contar(Path.Combine(Carpeta, "hormiguero.db"), "esquemas_diccionario"));
        Assert.Equal(0L, Contar(Path.Combine(Carpeta, "hormiguero.db"), "documentos"));
        Assert.Equal(1L, Contar(Path.Combine(Carpeta, "archivero.db"), "Configuraciones"));
        Assert.Equal(0L, Contar(Path.Combine(Carpeta, "archivero.db"), "Pendientes"));
        Assert.Equal(1L, Contar(Path.Combine(Carpeta, "buscadero.db"), "Carpetas"));
        Assert.Equal(0L, Contar(Path.Combine(Carpeta, "buscadero.db"), "ArchivosIndexados"));
        Assert.Equal(1L, Contar(Path.Combine(Carpeta, "mensajero.db"), "clientes_factura"));
        Assert.Equal(1L, Contar(Path.Combine(Carpeta, "mensajero.db"), "estados_envio_factura"));
        Assert.Equal(
            1L,
            Contar(Path.Combine(resultado.RutaRespaldo, "hormiguero.db"), "documentos")
        );
    }

    [Fact]
    public void Opcion_de_inicio_cero_vacia_clasificaciones_y_conserva_esquemas_y_mensajero()
    {
        CrearDatos();

        ResultadoReinicio resultado = new ReinicioServicio().Ejecutar(
            Carpeta,
            Ahora,
            borrarClasificaciones: true
        );

        Assert.True(resultado.Exitoso, resultado.Mensaje);
        Assert.Equal(0L, Contar(Path.Combine(Carpeta, "hormiguero.db"), "identificaciones"));
        Assert.Equal(1L, Contar(Path.Combine(Carpeta, "hormiguero.db"), "esquemas_diccionario"));
        Assert.Equal(0L, Contar(Path.Combine(Carpeta, "archivero.db"), "EntidadesConocidas"));
        Assert.Equal(1L, Contar(Path.Combine(Carpeta, "mensajero.db"), "clientes_factura"));
    }

    [Fact]
    public void Si_ya_existe_el_respaldo_no_modifica_las_bases()
    {
        CrearDatos();
        string respaldo = ReinicioServicio.RutaRespaldo(Carpeta, Ahora);
        Directory.CreateDirectory(respaldo);

        ResultadoReinicio resultado = new ReinicioServicio().Ejecutar(Carpeta, Ahora);

        Assert.False(resultado.Exitoso);
        Assert.Equal(1L, Contar(Path.Combine(Carpeta, "hormiguero.db"), "documentos"));
        Assert.False(File.Exists(Path.Combine(Carpeta, "reiniciado.txt")));
    }

    private void CrearDatos()
    {
        Directory.CreateDirectory(Carpeta);
        CrearBase(
            "hormiguero.db",
            "CREATE TABLE diccionario_datos(id TEXT PRIMARY KEY); INSERT INTO diccionario_datos VALUES('x'); CREATE TABLE migraciones(version INTEGER PRIMARY KEY); INSERT INTO migraciones VALUES(1); CREATE TABLE calendarios_feriados(id INTEGER); INSERT INTO calendarios_feriados VALUES(1); CREATE TABLE feriados(id INTEGER); INSERT INTO feriados VALUES(1); CREATE TABLE identificaciones(id INTEGER); INSERT INTO identificaciones VALUES(1); CREATE TABLE campos_documento(id INTEGER); INSERT INTO campos_documento VALUES(1); CREATE TABLE tipos_documento_datos(id INTEGER); INSERT INTO tipos_documento_datos VALUES(1); CREATE TABLE tipos_documento_informativos(id INTEGER); INSERT INTO tipos_documento_informativos VALUES(1); CREATE TABLE esquemas_diccionario(id INTEGER); INSERT INTO esquemas_diccionario VALUES(1); CREATE TABLE lugares_esquema_diccionario(id INTEGER); INSERT INTO lugares_esquema_diccionario VALUES(1); CREATE TABLE reglas_propuesta_modo_esquema_diccionario(id INTEGER); INSERT INTO reglas_propuesta_modo_esquema_diccionario VALUES(1); CREATE TABLE documentos(id INTEGER); INSERT INTO documentos VALUES(1);"
        );
        CrearBase(
            "archivero.db",
            "CREATE TABLE Configuraciones(id INTEGER); INSERT INTO Configuraciones VALUES(1); CREATE TABLE PatronesReconocimiento(id INTEGER); INSERT INTO PatronesReconocimiento VALUES(1); CREATE TABLE EntidadesConocidas(id INTEGER); INSERT INTO EntidadesConocidas VALUES(1); CREATE TABLE Pendientes(id INTEGER); INSERT INTO Pendientes VALUES(1); CREATE TABLE Borradores(id INTEGER); INSERT INTO Borradores VALUES(1); CREATE TABLE GuardadosRecientes(id INTEGER); INSERT INTO GuardadosRecientes VALUES(1); CREATE TABLE PendientesDuplicados(id INTEGER); INSERT INTO PendientesDuplicados VALUES(1);"
        );
        CrearBase(
            "buscadero.db",
            "CREATE TABLE Carpetas(id INTEGER); INSERT INTO Carpetas VALUES(1); CREATE TABLE CarpetasIndexadas(id INTEGER); INSERT INTO CarpetasIndexadas VALUES(1); CREATE TABLE ArchivosIndexados(id INTEGER); INSERT INTO ArchivosIndexados VALUES(1); CREATE TABLE FrecuenciaCarpetas(id INTEGER); INSERT INTO FrecuenciaCarpetas VALUES(1); CREATE TABLE ImportacionGuardados(id INTEGER); INSERT INTO ImportacionGuardados VALUES(1);"
        );
        CrearBase(
            "mensajero.db",
            "CREATE TABLE clientes_factura(rut TEXT); INSERT INTO clientes_factura VALUES('76000000-1'); CREATE TABLE estados_envio_factura(rut TEXT); INSERT INTO estados_envio_factura VALUES('76000000-1'); CREATE TABLE clientes_nvv(linea TEXT); INSERT INTO clientes_nvv VALUES('cliente');"
        );
    }

    private void CrearBase(string nombre, string sql)
    {
        using var conexion = new SqliteConnection(
            $"Data Source={Path.Combine(Carpeta, nombre)};Pooling=False"
        );
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        comando.ExecuteNonQuery();
    }

    private static long Contar(string ruta, string tabla)
    {
        using var conexion = new SqliteConnection($"Data Source={ruta};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = $"SELECT COUNT(*) FROM \"{tabla}\";";
        return (long)comando.ExecuteScalar()!;
    }
}
