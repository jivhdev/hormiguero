using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Buscadero.Core.Indexado;

public sealed class ImportadorDeGuardados(
    RepositorioIndice repositorio,
    Func<IReadOnlyList<string>> carpetasMadre,
    string rutaBaseComun
)
{
    public int Importar()
    {
        if (!File.Exists(rutaBaseComun))
        {
            return 0;
        }

        try
        {
            using var conexion = BaseComun.Abrir(rutaBaseComun);
            var guardados = new DocumentosGuardados(conexion).Despues(
                repositorio.LeerUltimoGuardadoImportado()
            );
            var carpetas = carpetasMadre().Select(RepositorioIndice.ClaveRuta).ToArray();
            var agregados = 0;

            foreach (var guardado in guardados)
            {
                if (
                    string.Equals(
                        Path.GetExtension(guardado.Ruta),
                        ".pdf",
                        StringComparison.OrdinalIgnoreCase
                    )
                    && File.Exists(guardado.Ruta)
                    && EstaDentroDeCarpetaMadre(guardado.Ruta, carpetas)
                )
                {
                    repositorio.AgregarArchivo(guardado.Ruta);
                    agregados++;
                }
            }

            if (guardados.Count > 0)
            {
                repositorio.GuardarUltimoGuardadoImportado(guardados.Max(guardado => guardado.Id));
            }

            return agregados;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
        catch (SqliteException)
        {
            return 0;
        }
    }

    private static bool EstaDentroDeCarpetaMadre(string ruta, IReadOnlyList<string> carpetas)
    {
        var claveRuta = RepositorioIndice.ClaveRuta(Path.GetDirectoryName(ruta) ?? ruta);
        return carpetas.Any(carpeta =>
            claveRuta.Equals(carpeta, StringComparison.OrdinalIgnoreCase)
            || claveRuta.StartsWith(carpeta + "\\", StringComparison.OrdinalIgnoreCase)
        );
    }
}
