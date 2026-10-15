using System.IO;
using Archivero.Datos;
using Hormiguero.Nucleo.Datos;

namespace Archivero.Servicios;

public sealed class BuscadorDocumentosEnCarpetaService : IBuscadorDocumentosCadena
{
    private readonly Func<IReadOnlyList<ConfiguracionDocumento>> configuraciones;

    public BuscadorDocumentosEnCarpetaService(
        Func<IReadOnlyList<ConfiguracionDocumento>>? configuraciones = null
    ) =>
        this.configuraciones =
            configuraciones
            ?? (() => new ConfiguracionDocumentoRepository().ObtenerTodasConPatrones());

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

        var versiones = new List<long>();
        if (tipoDiccionario == "guia_firmada_proveedor")
        {
            foreach (var ubicacion in new UbicacionSinTextoRepository().ObtenerTodas())
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
                        esquema.Proveedor,
                        revisarEnlaces: false
                    );
                    versiones.Add(LeerVersion(ruta));
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
                var valores = PublicadorDatosDocumentoService.ExtraerValoresObservados(
                    ruta,
                    configuracion
                );
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
                versiones.Add(LeerVersion(ruta));
            }
        }
        return versiones.Distinct().ToArray();
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
