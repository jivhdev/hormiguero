using Buscadero.Core.Busqueda;
using Buscadero.Core.Carpetas;
using Buscadero.Core.Indexado;
using Buscadero.Core.Lineas;
using Buscadero.Core.Marcas;
using Microsoft.Data.Sqlite;

namespace Buscadero.Core.Tests;

public sealed class EntornoDePrueba : IDisposable
{
    public string Raiz { get; }
    public string RutaBaseDeDatos { get; }
    public string RutaBaseComun { get; }
    public RepositorioIndice RepositorioIndice { get; }
    public RepositorioMarcas RepositorioMarcas { get; }
    public RepositorioLineas RepositorioLineas { get; }
    public ServicioLineas ServicioLineas { get; }
    public Indexador Indexador { get; }
    public ServicioCarpetas ServicioCarpetas { get; }
    public ServicioBusqueda ServicioBusqueda { get; }

    public EntornoDePrueba()
    {
        Raiz = Path.Combine(Path.GetTempPath(), "buscadero-prueba-" + Guid.NewGuid().ToString("N"));
        var carpetaDatos = Path.Combine(Raiz, "datos");
        Directory.CreateDirectory(carpetaDatos);
        RutaBaseDeDatos = Path.Combine(carpetaDatos, "buscadero.db");
        RutaBaseComun = Path.Combine(carpetaDatos, "hormiguero.db");

        ServicioCarpetas = new ServicioCarpetas(new RepositorioCarpetas(RutaBaseDeDatos));
        RepositorioIndice = new RepositorioIndice(RutaBaseDeDatos);
        RepositorioMarcas = new RepositorioMarcas(RutaBaseComun);
        RepositorioLineas = new RepositorioLineas(RutaBaseComun);
        ServicioLineas = new ServicioLineas(RepositorioLineas);
        Indexador = new Indexador(RepositorioIndice, TimeSpan.Zero, _ => { });
        ServicioBusqueda = new ServicioBusqueda(ServicioCarpetas, Indexador, RepositorioIndice);
    }

    public string CrearCarpeta(params string[] partes)
    {
        var ruta = Path.Combine(new[] { Raiz }.Concat(partes).ToArray());
        Directory.CreateDirectory(ruta);
        return ruta;
    }

    public string CrearCarpetaConFecha(DateTime modificacion, params string[] partes)
    {
        var ruta = CrearCarpeta(partes);
        Directory.SetLastWriteTimeUtc(ruta, modificacion);
        return ruta;
    }

    public string CrearArchivo(string carpeta, string nombre, DateTime? modificacion = null)
    {
        var ruta = Path.Combine(carpeta, nombre);
        File.WriteAllText(ruta, "contenido simulado");
        if (modificacion is not null)
        {
            File.SetLastWriteTimeUtc(ruta, modificacion.Value);
        }

        return ruta;
    }

    public string CrearDocumento(string nombre, string contenido = "PDF de prueba")
    {
        var ruta = Path.Combine(Raiz, nombre);
        File.WriteAllText(ruta, contenido);
        return ruta;
    }

    public void Dispose()
    {
        RepositorioMarcas.Dispose();
        RepositorioLineas.Dispose();
        SqliteConnection.ClearAllPools();
        for (var intento = 0; ; intento++)
        {
            try
            {
                Directory.Delete(Raiz, true);
                break;
            }
            catch (IOException) when (intento < 19)
            {
                SqliteConnection.ClearAllPools();
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (intento < 19)
            {
                SqliteConnection.ClearAllPools();
                Thread.Sleep(100);
            }
        }
    }
}
