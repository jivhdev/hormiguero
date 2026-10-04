using Archivero.Datos;
using Archivero.Servicios.Pdf;

namespace Archivero.Servicios;

public static class CoincidenciaAutomaticaService
{
    /// <summary>
    /// Prueba, uno por uno, los patrones de reconocimiento de todas las configuraciones
    /// guardadas contra el PDF que llegó: si el texto que se extrae ahora en la coordenada de
    /// Emisor y la de Tipo de un patrón coincide exactamente con el texto que se extrajo de esa
    /// misma coordenada cuando se creó el patrón (TextoReferencia), ese patrón "reconoce" el
    /// documento. Se compara contra TextoReferencia y no contra el nombre de la entidad, porque
    /// el campo marcado no siempre es el nombre visible (ej. puede ser un RUT o un código, si el
    /// nombre es un logo/imagen no extraíble como texto). Para patrones viejos sin
    /// TextoReferencia guardado, se usa el nombre de la entidad como antes.
    /// Devuelve la configuración con Patrones reducido a únicamente el patrón que coincidió
    /// (para saber qué marca de Fecha y de Nombre de archivo usar), o null si ninguno coincide.
    /// </summary>
    public static ConfiguracionDocumento? BuscarConfiguracionQueCoincide(string rutaPdf, IEnumerable<ConfiguracionDocumento> configuraciones)
    {
        var totalPaginas = LectorPdf.ContarPaginas(rutaPdf);

        foreach (var configuracion in configuraciones)
        {
            foreach (var patron in configuracion.Patrones)
            {
                var marcaEmisor = patron.Marcas.FirstOrDefault(m => m.Campo == CampoMarca.Emisor);
                var marcaTipo = patron.Marcas.FirstOrDefault(m => m.Campo == CampoMarca.Tipo);

                if (marcaEmisor is null || marcaTipo is null)
                {
                    continue;
                }

                if (marcaEmisor.Pagina >= totalPaginas || marcaTipo.Pagina >= totalPaginas)
                {
                    continue;
                }

                var referenciaEmisor = marcaEmisor.TextoReferencia ?? configuracion.Emisor;
                var textoEmisor = LectorPdf.ExtraerTexto(rutaPdf, marcaEmisor.Pagina, ARect(marcaEmisor));
                if (!CoincideExacto(textoEmisor, referenciaEmisor))
                {
                    continue;
                }

                var referenciaTipo = marcaTipo.TextoReferencia ?? configuracion.Tipo;
                var textoTipo = LectorPdf.ExtraerTexto(rutaPdf, marcaTipo.Pagina, ARect(marcaTipo));
                if (!CoincideExacto(textoTipo, referenciaTipo))
                {
                    continue;
                }

                return configuracion with { Patrones = [patron] };
            }
        }

        return null;
    }

    private static bool CoincideExacto(string textoExtraido, string valorEsperado) =>
        string.Equals(textoExtraido.Trim(), valorEsperado.Trim(), StringComparison.OrdinalIgnoreCase);

    private static RectanguloFraccion ARect(Marca marca) => new(marca.X, marca.Y, marca.Ancho, marca.Alto);
}
