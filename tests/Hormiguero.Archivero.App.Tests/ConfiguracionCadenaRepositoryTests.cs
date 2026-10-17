using Archivero.Datos;
using Microsoft.Data.Sqlite;

namespace Archivero.Tests;

public sealed class ConfiguracionCadenaRepositoryTests
{
    [Fact]
    public void Guarda_actualiza_y_elimina_el_alcance_de_cadena()
    {
        string anterior = BaseDeDatos.RutaArchivo;
        string raiz = Path.Combine(
            Path.GetTempPath(),
            "ArchiveroTests",
            Guid.NewGuid().ToString("N")
        );
        BaseDeDatos.RutaArchivo = Path.Combine(raiz, "archivero.db");
        try
        {
            BaseDeDatos.AsegurarEsquema();
            int id = new ConfiguracionDocumentoRepository().GuardarNueva(
                "Proveedor",
                "Factura",
                raiz,
                FormatoCarpeta.Directo,
                null,
                false,
                []
            );
            var repo = new ConfiguracionCadenaRepository();
            repo.Guardar(id, "esquema", "Hoffens");
            Assert.Equal(new ConfiguracionCadena("esquema", "Hoffens"), repo.Obtener(id));
            repo.Guardar(id, "todas", null);
            Assert.Equal(new ConfiguracionCadena("todas", null), repo.Obtener(id));
            repo.Eliminar(id);
            Assert.Null(repo.Obtener(id));
        }
        finally
        {
            BaseDeDatos.RutaArchivo = anterior;
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(raiz))
                Directory.Delete(raiz, recursive: true);
        }
    }

    [Fact]
    public void Rechaza_esquema_sin_proveedor()
    {
        var repo = new ConfiguracionCadenaRepository();
        Assert.Throws<ArgumentException>(() => repo.Guardar(1, "esquema", null));
    }
}
