using System.IO;
using Archivero.Datos;
using Archivero.Servicios.Pdf;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Utilidades;

namespace Archivero.Servicios;

public static class PublicadorDatosDocumentoService
{
    public static bool PublicacionAutomaticaActiva { get; set; }
    public static Task? UltimaRevisionEnlaces { get; private set; }

    public static void PublicarGuardado(
        string ruta,
        ConfiguracionDocumento configuracion,
        CamposExtraidos campos
    )
    {
        var valores = CrearValores(ruta, configuracion, campos);
        Publicar(ruta, configuracion, valores, "marca");
    }

    public static void PublicarObservado(
        string ruta,
        ConfiguracionDocumento configuracion,
        IReadOnlyList<ValorDocumentoLeido> valoresLeidos
    ) => Publicar(ruta, configuracion, valoresLeidos, "observador");

    public static void Publicar(
        string ruta,
        ConfiguracionDocumento configuracion,
        IReadOnlyList<ValorDocumentoLeido> valoresLeidos,
        string procedencia
    )
    {
        var infoAntes = new FileInfo(ruta);
        infoAntes.Refresh();
        if (!infoAntes.Exists)
            throw new FileNotFoundException("No se encontró el documento para publicarlo.", ruta);
        var huellaAntes = Huella.Calcular(ruta);
        long tamano = infoAntes.Length;
        DateTime modificado = infoAntes.LastWriteTimeUtc;
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        var publicado = new RepositorioDocumentosDatos(conexion).PublicarDocumento(
            ruta,
            tamano,
            modificado,
            huellaAntes,
            configuracion.Emisor,
            configuracion.Tipo,
            ConCamposBase(valoresLeidos),
            procedencia
        );
        infoAntes.Refresh();
        if (
            !infoAntes.Exists
            || huellaAntes != Huella.Calcular(ruta)
            || tamano != infoAntes.Length
            || modificado != infoAntes.LastWriteTimeUtc
        )
            throw new IOException("El documento cambió mientras se publicaba.");
        UltimaRevisionEnlaces = Task.Run(() => RevisarEnlaces(publicado.Version.Id));
    }

    public static Task RevisarEnlacesPendientesAsync() => Task.Run(() => RevisarEnlaces(null));

    private static void RevisarEnlaces(long? versionId)
    {
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        var motor = new MotorEnlaceAutomatico(conexion);
        try
        {
            motor.Ejecutar(versionId);
        }
        catch (Exception error)
        {
            try
            {
                motor.RegistrarError(error, versionId);
            }
            catch
            { /* Un fallo de auditoría no invalida el documento publicado. */
            }
        }
    }

    private static IReadOnlyList<ValorDocumentoLeido> CrearValores(
        string ruta,
        ConfiguracionDocumento configuracion,
        CamposExtraidos campos
    )
    {
        var valores = new List<ValorDocumentoLeido>
        {
            new("Emisor", "emisor", configuracion.Emisor, Clave(configuracion.Emisor), "marca"),
            new("Tipo", "tipo", configuracion.Tipo, Clave(configuracion.Tipo), "marca"),
            new(
                "Fecha",
                "fecha",
                campos.Fecha?.ToString("yyyy-MM-dd") ?? "",
                campos.Fecha?.ToString("yyyy-MM-dd") ?? "",
                "marca",
                "fecha"
            ),
            new(
                "Nombre de archivo",
                "nombre_archivo",
                campos.NombreExtraido ?? Path.GetFileNameWithoutExtension(ruta),
                Clave(campos.NombreExtraido ?? Path.GetFileNameWithoutExtension(ruta)),
                "marca"
            ),
        };
        var patron = configuracion.Patrones.FirstOrDefault();
        if (patron is not null)
            foreach (
                var marca in patron.Marcas.Where(m =>
                    m.Campo
                        is not (
                            CampoMarca.Emisor
                            or CampoMarca.Tipo
                            or CampoMarca.Fecha
                            or CampoMarca.NombreArchivo
                        )
                )
            )
                valores.Add(
                    new(
                        marca.Campo.ToString(),
                        NombreEstable(marca.Campo.ToString()),
                        LectorPdf.ExtraerTexto(
                            ruta,
                            marca.Pagina,
                            new(marca.X, marca.Y, marca.Ancho, marca.Alto)
                        ),
                        Clave(
                            LectorPdf.ExtraerTexto(
                                ruta,
                                marca.Pagina,
                                new(marca.X, marca.Y, marca.Ancho, marca.Alto)
                            )
                        ),
                        "marca"
                    )
                );
        foreach (var campo in configuracion.CamposPropios)
        {
            string original = LectorPdf.ExtraerTexto(
                ruta,
                campo.Pagina,
                new(campo.X, campo.Y, campo.Ancho, campo.Alto)
            );
            valores.Add(new(campo.Nombre, campo.NombreEstable, original, Clave(original), "marca"));
        }
        return valores;
    }

    private static IReadOnlyList<ValorDocumentoLeido> ConCamposBase(
        IReadOnlyList<ValorDocumentoLeido> valores
    )
    {
        var lista = valores.ToList();
        foreach (
            var (nombre, estable, tipo) in new[]
            {
                ("Emisor", "emisor", "texto"),
                ("Tipo", "tipo", "texto"),
                ("Fecha", "fecha", "fecha"),
                ("Nombre de archivo", "nombre_archivo", "texto"),
            }
        )
            if (!lista.Any(v => v.NombreEstable == estable))
                lista.Add(new(nombre, estable, "", "", "marca", tipo));
        return lista;
    }

    private static string Clave(string valor) =>
        string.Join(' ', valor.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string NombreEstable(string nombre) =>
        string.Concat(
                nombre.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_')
            )
            .Trim('_');
}
