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
    public static event Action<long?, string>? RevisionEnlacesFallida;

    public static void PublicarGuardado(
        string ruta,
        ConfiguracionDocumento configuracion,
        CamposExtraidos campos,
        string? proveedorManual = null
    )
    {
        var valores = CrearValores(ruta, configuracion, campos).ToList();
        if (!string.IsNullOrWhiteSpace(proveedorManual))
            valores.Add(
                new(
                    "Nombre o razón social del proveedor",
                    "nombre_proveedor",
                    proveedorManual.Trim(),
                    DiccionarioDatosEnlazantes.ClaveDeEnlace("nombre_proveedor", proveedorManual),
                    "manual",
                    DatoDiccionarioId: "nombre_proveedor"
                )
            );
        Publicar(ruta, configuracion, valores, "marca");
    }

    public static void PublicarObservado(
        string ruta,
        ConfiguracionDocumento configuracion,
        IReadOnlyList<ValorDocumentoLeido> valoresLeidos,
        bool cedible = false
    ) =>
        Publicar(
            ruta,
            configuracion,
            cedible
                ? valoresLeidos
                    .Select(v => v.DatoDiccionarioId is null ? v : v with { Origen = "cedible" })
                    .ToArray()
                : valoresLeidos,
            "observador"
        );

    public static void PublicarObservado(
        string ruta,
        ConfiguracionDocumento configuracion,
        DatoEnlazante datoIdentificador,
        string valor
    )
    {
        var valores = new List<ValorDocumentoLeido>
        {
            new("Emisor", "emisor", configuracion.Emisor, configuracion.Emisor, "observador"),
            new("Tipo", "tipo", configuracion.Tipo, configuracion.Tipo, "observador"),
            new("Fecha", "fecha", "", "", "observador", "fecha"),
            new(
                "Nombre de archivo",
                "nombre_archivo",
                Path.GetFileNameWithoutExtension(ruta),
                Path.GetFileNameWithoutExtension(ruta),
                "observador"
            ),
            new(
                datoIdentificador.Nombre,
                datoIdentificador.Id,
                valor,
                DiccionarioDatosEnlazantes.ClaveDeEnlace(datoIdentificador.Id, valor),
                "observador"
            ),
        };
        Publicar(ruta, configuracion, valores, "observador", datoIdentificador);
    }

    public static IReadOnlyList<ValorDocumentoLeido> ExtraerValoresObservados(
        string ruta,
        ConfiguracionDocumento configuracion
    ) => CrearValores(ruta, configuracion, new CamposExtraidos(null, null));

    public static void Publicar(
        string ruta,
        ConfiguracionDocumento configuracion,
        IReadOnlyList<ValorDocumentoLeido> valoresLeidos,
        string procedencia,
        DatoEnlazante? datoIdentificador = null
    )
    {
        var infoAntes = new FileInfo(ruta);
        infoAntes.Refresh();
        if (!infoAntes.Exists)
            throw new FileNotFoundException("No se encontró el documento para publicarlo.", ruta);
        var huellaAntes = Huella.Calcular(ruta);
        long tamano = infoAntes.Length;
        DateTime modificado = infoAntes.LastWriteTimeUtc;
        var patronConfigurado = configuracion.Patrones.FirstOrDefault();
        var datosConfigurados = patronConfigurado is null
            ? []
            : DatosEnlazantesConfiguracionService
                .Leer(configuracion.Emisor, configuracion.Tipo, patronConfigurado.Id)
                .Where(d => d.Incluido && d.Marcado)
                .ToList();
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        var identificaciones = new Identificaciones(conexion);
        var identificacion = identificaciones
            .Listar()
            .FirstOrDefault(i => i.Emisor == configuracion.Emisor && i.Tipo == configuracion.Tipo);
        long identificacionId = identificaciones.Guardar(
            new Identificacion(
                identificacion?.Id ?? 0,
                configuracion.Tipo,
                configuracion.Emisor,
                identificacion?.Datos ?? "{}",
                configuracion.GrupoDocumento,
                string.IsNullOrWhiteSpace(configuracion.NombreEstandar)
                    ? $"{configuracion.Tipo} · {configuracion.Emisor}"
                    : configuracion.NombreEstandar
            )
        );
        var repositorioDocumentos = new RepositorioDocumentosDatos(conexion);
        var camposExistentes = repositorioDocumentos.ListarCampos(identificacionId);
        foreach (var dato in datosConfigurados)
        {
            var campoExistente = camposExistentes.FirstOrDefault(c => c.NombreEstable == dato.Id);
            repositorioDocumentos.GuardarCampo(
                new CampoDocumento(
                    campoExistente?.Id ?? 0,
                    identificacionId,
                    dato.Nombre,
                    dato.Id,
                    "texto",
                    true,
                    "marca",
                    dato.Id
                )
            );
        }
        if (
            datoIdentificador is not null
            && !camposExistentes.Any(c => c.NombreEstable == datoIdentificador.Id)
        )
            new RepositorioDocumentosDatos(conexion).GuardarCampo(
                new CampoDocumento(
                    0,
                    identificacionId,
                    datoIdentificador.Nombre,
                    datoIdentificador.Id,
                    "texto",
                    true,
                    "observador",
                    datoIdentificador.Id
                )
            );
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
        if (patronConfigurado is not null)
        {
            var zonasInformativas = new RepositorioDatosInformativos(conexion).LeerZonas(
                identificacionId,
                patronConfigurado.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
            var valoresInformativos = zonasInformativas
                .Select(zona =>
                {
                    string valor = LectorPdf.ExtraerTexto(
                        ruta,
                        zona.Pagina - 1,
                        new(zona.X, zona.Y, zona.Ancho, zona.Alto)
                    );
                    return zona.Dato == "fecha_documento"
                        ? FechaDocumentoParser.InterpretarFecha(valor)
                        : new ValorInformativo(zona.Dato, valor, null, false);
                })
                .ToArray();
            new RepositorioDatosInformativos(conexion).GuardarValores(
                publicado.Version.Id,
                valoresInformativos
            );
        }
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
        var motor = new MotorCadenasSimples(conexion);
        try
        {
            motor.Procesar(versionId);
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
            RevisionEnlacesFallida?.Invoke(
                versionId,
                $"No se pudieron revisar los enlaces autom\u00e1ticos del documento publicado (versi\u00f3n {versionId?.ToString() ?? "pendiente"}): {error.Message}"
            );
        }
        try
        {
            new EvaluadorAlertas(conexion).Evaluar();
        }
        catch (Exception error)
        {
            RevisionEnlacesFallida?.Invoke(
                versionId,
                $"No se pudieron evaluar las alertas después de publicar el documento: {error.Message}"
            );
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
        if (patron is not null)
            foreach (
                var dato in DatosEnlazantesConfiguracionService
                    .Leer(configuracion.Emisor, configuracion.Tipo, patron.Id)
                    .Where(d => d.Incluido && d.Marcado)
            )
            {
                var original = LectorPdf.ExtraerTexto(
                    ruta,
                    dato.Pagina,
                    new(dato.X, dato.Y, dato.Ancho, dato.Alto)
                );
                valores.Add(
                    new(
                        dato.Nombre,
                        dato.Id,
                        original,
                        DiccionarioDatosEnlazantes.ClaveDeEnlace(dato.Id, original),
                        "marca",
                        DatoDiccionarioId: dato.Id
                    )
                );
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
