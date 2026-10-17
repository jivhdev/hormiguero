using System.Text.Json;
using Hormiguero.Nucleo.Datos;

namespace Archivero.Servicios;

public sealed record OpcionCadena(string Proveedor, IReadOnlyList<string> DatosRequeridos);

public static class OpcionesCadenaService
{
    public static IReadOnlyList<OpcionCadena> ParaTipo(string tipoId)
    {
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        return new RepositorioEsquemasDiccionario(conexion)
            .Listar()
            .Where(esquema => esquema.Activo)
            .SelectMany(esquema =>
                esquema
                    .Lugares.Where(lugar => lugar.DatoTipoId == tipoId)
                    .Select(lugar => new OpcionCadena(
                        esquema.Proveedor,
                        JsonSerializer.Deserialize<string[]>(lugar.DatosRequeridosJson) ?? []
                    ))
            )
            .ToArray();
    }
}
