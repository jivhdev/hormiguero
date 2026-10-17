using System.IO;
using System.Text.RegularExpressions;
using Archivero.Datos;
using Hormiguero.Nucleo.Datos;

namespace Archivero.Servicios;

public sealed class BuscadorDocumentosEnCarpetaService : IBuscadorDocumentosCadena
{
    private readonly Func<IReadOnlyList<ConfiguracionDocumento>> configuraciones;
    private readonly Func<
        string,
        ConfiguracionDocumento,
        IReadOnlyList<ValorDocumentoLeido>
    > extraerValores;

    public BuscadorDocumentosEnCarpetaService(
        Func<IReadOnlyList<ConfiguracionDocumento>>? configuraciones = null,
        Func<string, ConfiguracionDocumento, IReadOnlyList<ValorDocumentoLeido>>? extraerValores =
            null
    )
    {
        this.configuraciones =
            configuraciones
            ?? (() => new ConfiguracionDocumentoRepository().ObtenerTodasConPatrones());
        this.extraerValores =
            extraerValores ?? PublicadorDatosDocumentoService.ExtraerValoresObservados;
    }

    public IReadOnlyList<long> Buscar(
        string tipoDiccionario,
        string numero,
        EsquemaDiccionario esquema
    )
    {
        string numeroClave = new(numero.Where(char.IsAsciiDigit).ToArray());
        numeroClave = numeroClave.TrimStart('0');
        if (numeroClave.Length == 0)
            return [];

        var lugarEsquema = esquema.Lugares.FirstOrDefault(l => l.DatoTipoId == tipoDiccionario);
        if (
            !string.IsNullOrWhiteSpace(lugarEsquema?.CarpetaBusqueda)
            && Directory.Exists(lugarEsquema.CarpetaBusqueda)
        )
        {
            foreach (string ruta in ArchivosPorPeriodo(lugarEsquema.CarpetaBusqueda))
            {
                if (!NombreContieneNumero(ruta, numeroClave))
                    continue;
                var coincidencia = configuraciones()
                    .Where(c => c.Patrones.Count > 0 && EsTipo(c, tipoDiccionario))
                    .Where(c =>
                        !EsTipoProveedor(tipoDiccionario)
                        || EsEmisorReconocido(
                            c.Emisor,
                            esquema.Proveedor,
                            Reconocimientos(esquema.Id)
                        )
                    )
                    .FirstOrDefault();
                if (coincidencia is null)
                    RegistrarSinClasificacion(
                        ruta,
                        tipoDiccionario,
                        esquema.Proveedor,
                        numeroClave
                    );
                else
                {
                    var valores = extraerValores(ruta, coincidencia);
                    if (
                        !valores.Any(v =>
                            v.DatoDiccionarioId == tipoDiccionario
                            && ClaveNumero(v.ValorOriginal) == numeroClave
                        )
                    )
                        RegistrarSinClasificacion(
                            ruta,
                            tipoDiccionario,
                            esquema.Proveedor,
                            numeroClave
                        );
                    else
                        PublicadorDatosDocumentoService.Publicar(
                            ruta,
                            coincidencia,
                            valores,
                            "busqueda_carpeta",
                            revisarEnlaces: false
                        );
                }
                return [LeerVersion(ruta)];
            }
        }

        var versiones = new List<long>();
        if (tipoDiccionario == "guia_firmada_proveedor")
        {
            foreach (
                var (
                    ubicacion,
                    _,
                    proveedor
                ) in new UbicacionSinTextoRepository().ObtenerParaCadena(
                    tipoDiccionario,
                    esquema.Proveedor
                )
            )
            {
                if (!Directory.Exists(ubicacion.CarpetaMadre))
                    continue;
                foreach (
                    string ruta in Directory.EnumerateFiles(
                        ubicacion.CarpetaMadre,
                        "*.pdf",
                        SearchOption.AllDirectories
                    )
                )
                {
                    string digitos = new(
                        Path.GetFileNameWithoutExtension(ruta).Where(char.IsAsciiDigit).ToArray()
                    );
                    if (!digitos.TrimStart('0').Contains(numeroClave, StringComparison.Ordinal))
                        continue;
                    PublicadorDatosDocumentoService.PublicarGuiaFirmada(
                        ruta,
                        tipoDiccionario,
                        proveedor,
                        revisarEnlaces: false
                    );
                    return [LeerVersion(ruta)];
                }
            }
        }

        IReadOnlyList<ReconocimientoProveedor> reconocimiento;
        using (var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun))
            reconocimiento = new RepositorioEsquemasDiccionario(conexion).ListarReconocimientos(
                esquema.Id
            );
        var candidatas = configuraciones()
            .Where(c => c.Patrones.Count > 0 && Directory.Exists(c.CarpetaDestino))
            .Where(c => EsTipo(c, tipoDiccionario))
            .Where(c =>
                !EsTipoProveedor(tipoDiccionario)
                || EsEmisorReconocido(c.Emisor, esquema.Proveedor, reconocimiento)
            )
            .ToArray();
        foreach (var configuracion in candidatas)
        {
            foreach (
                string ruta in Directory.EnumerateFiles(
                    configuracion.CarpetaDestino,
                    "*.pdf",
                    SearchOption.AllDirectories
                )
            )
            {
                string digitos = new(
                    Path.GetFileNameWithoutExtension(ruta).Where(char.IsAsciiDigit).ToArray()
                );
                if (!digitos.TrimStart('0').Contains(numeroClave, StringComparison.Ordinal))
                    continue;
                var valores = extraerValores(ruta, configuracion);
                if (
                    !valores.Any(v =>
                        v.DatoDiccionarioId == tipoDiccionario
                        && ClaveNumero(v.ValorOriginal) == numeroClave
                    )
                )
                    continue;
                PublicadorDatosDocumentoService.Publicar(
                    ruta,
                    configuracion,
                    valores,
                    "busqueda_carpeta",
                    revisarEnlaces: false
                );
                return [LeerVersion(ruta)];
            }
        }
        return versiones.Distinct().Take(1).ToArray();
    }

    private IReadOnlyList<ReconocimientoProveedor> Reconocimientos(long esquemaId)
    {
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        return new RepositorioEsquemasDiccionario(conexion).ListarReconocimientos(esquemaId);
    }

    private static void RegistrarSinClasificacion(
        string ruta,
        string tipo,
        string proveedor,
        string numero
    )
    {
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        new RegistroDocumentoEnCadena(conexion).RegistrarSinClasificacion(
            ruta,
            tipo,
            proveedor,
            numero
        );
    }

    private static IEnumerable<string> ArchivosPorPeriodo(string carpeta)
    {
        var directorios = new[] { carpeta }.Concat(
            Directory.EnumerateDirectories(carpeta, "*", SearchOption.AllDirectories)
        );
        var ordenados = directorios
            .Select(d => new DirectoryInfo(d))
            .OrderByDescending(d => Periodo(d.Name))
            .ThenByDescending(d => d.LastWriteTimeUtc)
            .ToArray();
        foreach (var directorio in ordenados)
        {
            IEnumerable<string> archivos;
            try
            {
                archivos = Directory
                    .EnumerateFiles(directorio.FullName, "*.pdf", SearchOption.TopDirectoryOnly)
                    .ToArray();
            }
            catch (IOException)
            {
                continue;
            }
            foreach (
                string archivo in archivos.OrderBy(
                    Path.GetFileName,
                    StringComparer.OrdinalIgnoreCase
                )
            )
                yield return archivo;
        }
    }

    private static long Periodo(string nombre)
    {
        string digitos = string.Concat(nombre.Where(char.IsAsciiDigit));
        return
            Regex.IsMatch(digitos, @"^20\d{2}(0[1-9]|1[0-2])?$")
            && long.TryParse(digitos, out long periodo)
            ? periodo
            : 0;
    }

    private static bool NombreContieneNumero(string ruta, string numeroClave)
    {
        string digitos = string.Concat(
            Path.GetFileNameWithoutExtension(ruta).Where(char.IsAsciiDigit)
        );
        return digitos.TrimStart('0').Contains(numeroClave, StringComparison.Ordinal);
    }

    private static bool EsTipo(ConfiguracionDocumento configuracion, string tipo) =>
        configuracion.Patrones.Any(p =>
            DatosEnlazantesConfiguracionService
                .Leer(configuracion.Emisor, configuracion.Tipo, p.Id)
                .Any(d => d.DefineTipo && d.Id == tipo)
        );

    private static bool EsTipoProveedor(string tipo) =>
        DiccionarioDatosEnlazantes.Todos.Single(d => d.Id == tipo).Grupo == "Del proveedor";

    private static bool EsEmisorReconocido(
        string emisor,
        string proveedorEsquema,
        IReadOnlyList<ReconocimientoProveedor> reconocimientos
    ) =>
        reconocimientos.Count == 0
            ? string.Equals(
                ProveedorDeDocumento.NormalizarProveedor(emisor),
                ProveedorDeDocumento.NormalizarProveedor(proveedorEsquema),
                StringComparison.OrdinalIgnoreCase
            )
            : reconocimientos.Any(r =>
                string.Equals(
                    r.ValorNormalizado,
                    ProveedorDeDocumento.NormalizarProveedor(emisor),
                    StringComparison.OrdinalIgnoreCase
                )
            );

    private static string ClaveNumero(string valor) =>
        string.Concat(valor.Where(char.IsAsciiDigit)).TrimStart('0');

    private static long LeerVersion(string ruta)
    {
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        using var q = conexion.CreateCommand();
        q.CommandText =
            "SELECT id FROM versiones_documento WHERE documento_id=(SELECT id FROM documentos WHERE ruta=$r COLLATE NOCASE) AND estado='vigente' ORDER BY id DESC LIMIT 1;";
        q.Parameters.AddWithValue("$r", Path.GetFullPath(ruta));
        return Convert.ToInt64(q.ExecuteScalar());
    }
}
