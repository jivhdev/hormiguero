using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Pdf;
using Hormiguero.Nucleo.Utilidades;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Buscadero.Logica;

public record ResumenIndexado(int Nuevos, int Actualizados, int Quitados, int SinCambios);

public sealed class Indexador
{
    // Un archivo "solo en línea" (Drive) no está en el equipo: leerlo sería
    // descargar toda la carpeta, así que se indexa solo por su nombre (D-60).
    // .NET no expone RecallOnDataAccess (0x400000) en FileAttributes.
    private const FileAttributes SoloEnLinea = (FileAttributes)0x400000 | FileAttributes.Offline;

    private readonly Documentos _documentos;

    public Indexador(SqliteConnection conexion) => _documentos = new Documentos(conexion);

    public ResumenIndexado Revisar(
        string carpetaRaiz,
        CancellationToken cancelar,
        ControlIndice? control = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carpetaRaiz);
        cancelar.ThrowIfCancellationRequested();

        string raiz = Path.TrimEndingDirectorySeparator(Path.GetFullPath(carpetaRaiz));
        IReadOnlyDictionary<string, (long Tamano, DateTime Modificado)> firmas = _documentos.Firmas(
            raiz
        );

        int nuevos = 0;
        int actualizados = 0;
        int sinCambios = 0;
        var enDisco = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sinLeer = new List<string>();

        foreach (string ruta in PdfsEn(raiz, sinLeer, control, cancelar))
        {
            cancelar.ThrowIfCancellationRequested();

            var archivo = new FileInfo(ruta);
            if (!archivo.Exists)
            {
                continue;
            }

            enDisco.Add(ruta);
            control?.ContarArchivo();

            if (
                firmas.TryGetValue(ruta, out var firma)
                && firma.Tamano == archivo.Length
                && firma.Modificado == archivo.LastWriteTime
            )
            {
                sinCambios++;
                continue;
            }

            _documentos.Guardar(Describir(raiz, archivo));

            if (firmas.ContainsKey(ruta))
            {
                actualizados++;
            }
            else
            {
                nuevos++;
            }
        }

        // Una carpeta que no se pudo leer (red caída, sin permiso) no significa
        // que sus archivos se borraron: se conservan hasta poder revisarla (REQ-002).
        if (sinLeer.Contains(raiz, StringComparer.OrdinalIgnoreCase))
        {
            throw new DirectoryNotFoundException($"No se pudo leer la carpeta {raiz}");
        }

        int quitados = 0;
        foreach (string ruta in firmas.Keys)
        {
            if (!enDisco.Contains(ruta) && !DentroDeAlguna(ruta, sinLeer))
            {
                _documentos.Quitar(ruta);
                quitados++;
            }
        }

        return new ResumenIndexado(nuevos, actualizados, quitados, sinCambios);
    }

    private DocumentoIndexado Describir(string raiz, FileInfo archivo)
    {
        var numeros = new List<(string, string, string, string)>();
        foreach (NumeroEnNombre numero in NombreArchivo.Extraer(archivo.Name))
        {
            numeros.Add((numero.Numero, numero.Prefijo, numero.Sufijo, "nombre"));
        }

        if ((archivo.Attributes & SoloEnLinea) != 0)
        {
            return new DocumentoIndexado(
                archivo.FullName,
                raiz,
                archivo.Name,
                archivo.Length,
                archivo.LastWriteTime,
                null,
                "solo_en_linea",
                false,
                numeros
            );
        }

        // Se lee el archivo una sola vez (vecino silencioso: en red, leerlo dos veces duplica el tráfico).
        byte[] contenido;
        using (
            var flujo = new FileStream(
                archivo.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete
            )
        )
        {
            using var memoria = new MemoryStream();
            flujo.CopyTo(memoria);
            contenido = memoria.ToArray();
        }

        string huella = Huella.DeContenido(contenido);
        InfoPdf info = LectorPdf.Leer(new MemoryStream(contenido, writable: false));

        if (info.TieneTexto)
        {
            foreach (string numero in NumerosDelTexto(info))
            {
                numeros.Add((numero, "", "", "texto"));
            }
        }

        return new DocumentoIndexado(
            archivo.FullName,
            raiz,
            archivo.Name,
            archivo.Length,
            archivo.LastWriteTime,
            huella,
            Estado(info.Estado),
            info.TieneTexto,
            numeros
        );
    }

    private static IEnumerable<string> PdfsEn(
        string raiz,
        List<string> sinLeer,
        ControlIndice? control,
        CancellationToken cancelar
    )
    {
        var pendientes = new Stack<string>();
        pendientes.Push(raiz);

        while (pendientes.Count > 0)
        {
            cancelar.ThrowIfCancellationRequested();
            control?.EsperarTurno(cancelar);

            string carpeta = pendientes.Pop();
            control?.ContarCarpeta();

            string[] archivos;
            string[] subcarpetas;
            try
            {
                archivos = Directory.GetFiles(carpeta);
                subcarpetas = Directory.GetDirectories(carpeta);
            }
            catch (Exception error) when (error is UnauthorizedAccessException or IOException)
            {
                // Una subcarpeta sin permiso, caída o que ya no existe no puede
                // detener el índice: se sigue con las demás (REQ-002).
                sinLeer.Add(carpeta);
                continue;
            }

            foreach (string subcarpeta in subcarpetas)
            {
                pendientes.Push(subcarpeta);
            }

            foreach (string archivo in archivos)
            {
                if (
                    string.Equals(
                        Path.GetExtension(archivo),
                        ".pdf",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    yield return Path.GetFullPath(archivo);
                }
            }
        }
    }

    private static bool DentroDeAlguna(string ruta, List<string> carpetas) =>
        carpetas.Exists(carpeta =>
            ruta.StartsWith(
                carpeta + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            )
        );

    private static IEnumerable<string> NumerosDelTexto(InfoPdf info)
    {
        var vistos = new HashSet<string>();

        foreach (PalabraPdf palabra in info.Palabras)
        {
            string texto = palabra.Texto;

            int posicion = 0;
            while (posicion < texto.Length)
            {
                if (!char.IsDigit(texto[posicion]))
                {
                    posicion++;
                    continue;
                }

                int inicio = posicion;
                while (posicion < texto.Length && char.IsDigit(texto[posicion]))
                {
                    posicion++;
                }

                if (posicion - inicio < 3)
                {
                    continue;
                }

                int primero = inicio;
                while (primero < posicion - 1 && texto[primero] == '0')
                {
                    primero++;
                }

                string numero = texto[primero..posicion];
                if (vistos.Add(numero))
                {
                    yield return numero;
                }
            }
        }
    }

    private static string Estado(EstadoPdf estado) =>
        estado switch
        {
            EstadoPdf.Danado => "danado",
            EstadoPdf.Protegido => "protegido",
            _ => "correcto",
        };
}
