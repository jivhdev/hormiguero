using Hormiguero.Nucleo.Datos;

namespace Hormiguero.Buscadero.Logica;

public record VersionDocumento(
    string Etiqueta,
    IReadOnlyList<string> Rutas,
    DocumentoIndexado Principal,
    DateTime Modificado
);

public record ResultadoBusqueda(
    string Titulo,
    string Numero,
    string Tipo,
    IReadOnlyList<VersionDocumento> Versiones,
    IReadOnlyList<string> Carpetas,
    DateTime Modificado
);

public static class Agrupador
{
    public static IReadOnlyList<ResultadoBusqueda> Agrupar(
        IReadOnlyList<DocumentoIndexado> documentos,
        string numero
    )
    {
        if (documentos.Count == 0)
        {
            return [];
        }

        string buscado = Buscador.Normalizar(numero) ?? numero;
        var gruposPorHuella = AgruparPorHuella(documentos);
        var versiones = CrearVersiones(gruposPorHuella);
        var resultados = AgruparResultados(versiones, buscado);
        return OrdenarResultados(resultados);
    }

    private static IReadOnlyList<IReadOnlyList<DocumentoIndexado>> AgruparPorHuella(
        IReadOnlyList<DocumentoIndexado> documentos
    )
    {
        var grupos = new Dictionary<string, List<DocumentoIndexado>>(
            StringComparer.OrdinalIgnoreCase
        );
        var sinHuella = new List<DocumentoIndexado>();

        foreach (var doc in documentos)
        {
            if (doc.Huella != null)
            {
                if (!grupos.ContainsKey(doc.Huella))
                {
                    grupos[doc.Huella] = [];
                }
                grupos[doc.Huella].Add(doc);
            }
            else
            {
                sinHuella.Add(doc);
            }
        }

        var resultado = grupos.Values.Select(g => (IReadOnlyList<DocumentoIndexado>)g).ToList();

        foreach (var doc in sinHuella)
        {
            resultado.Add([doc]);
        }

        return resultado;
    }

    private static IReadOnlyList<VersionDocumento> CrearVersiones(
        IReadOnlyList<IReadOnlyList<DocumentoIndexado>> gruposPorHuella
    )
    {
        var versiones = new List<VersionDocumento>();

        foreach (var grupo in gruposPorHuella)
        {
            var principal = grupo[0];
            var rutas = grupo.Select(d => d.Ruta).ToList();
            var etiqueta = DeterminarEtiqueta(principal);
            var modificado = grupo.Max(d => d.Modificado);

            versiones.Add(new VersionDocumento(etiqueta, rutas, principal, modificado));
        }

        return versiones;
    }

    private static string DeterminarEtiqueta(DocumentoIndexado doc)
    {
        string nombreSinExtension = Path.GetFileNameWithoutExtension(doc.Nombre);
        if (nombreSinExtension.EndsWith("CEDIBLE", StringComparison.OrdinalIgnoreCase))
        {
            return "cedible";
        }

        if (!doc.TieneTexto && doc.Estado == "correcto")
        {
            return "escaneado";
        }

        return "original";
    }

    private static IReadOnlyList<ResultadoBusqueda> AgruparResultados(
        IReadOnlyList<VersionDocumento> versiones,
        string buscado
    )
    {
        var grupos = new Dictionary<(string Numero, string Tipo), List<VersionDocumento>>();

        foreach (var version in versiones)
        {
            string tipo = TipoDe(version.Principal, buscado);
            var clave = (buscado, tipo);
            if (!grupos.ContainsKey(clave))
            {
                grupos[clave] = [];
            }
            grupos[clave].Add(version);
        }

        var resultados = new List<ResultadoBusqueda>();

        foreach (var grupo in grupos)
        {
            var (numero, tipo) = grupo.Key;
            var versionesGrupo = grupo.Value;

            var carpetas = versionesGrupo
                .SelectMany(v => v.Rutas)
                .Select(r => Path.GetDirectoryName(r)!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var modificado = versionesGrupo.Max(v => v.Modificado);

            var versionesOrdenadas = OrdenarVersiones(versionesGrupo);

            string titulo = string.IsNullOrEmpty(tipo) ? numero : $"{tipo} {numero}";

            resultados.Add(
                new ResultadoBusqueda(
                    titulo,
                    numero,
                    tipo,
                    versionesOrdenadas,
                    carpetas,
                    modificado
                )
            );
        }

        return resultados;
    }

    // El tipo es el prefijo que acompaña al número buscado en el nombre
    // (OCC, FCV...). Si el número solo aparece en el texto, no hay tipo.
    private static string TipoDe(DocumentoIndexado doc, string buscado)
    {
        foreach (var numero in doc.Numeros)
        {
            if (numero.Origen == "nombre" && Buscador.Normalizar(numero.Numero) == buscado)
            {
                return numero.Prefijo.ToUpperInvariant();
            }
        }

        return "";
    }

    private static IReadOnlyList<VersionDocumento> OrdenarVersiones(
        IReadOnlyList<VersionDocumento> versiones
    )
    {
        var orden = new Dictionary<string, int>
        {
            ["original"] = 0,
            ["escaneado"] = 1,
            ["cedible"] = 2,
        };

        return versiones.OrderBy(v => orden.GetValueOrDefault(v.Etiqueta, 3)).ToList();
    }

    private static IReadOnlyList<ResultadoBusqueda> OrdenarResultados(
        IReadOnlyList<ResultadoBusqueda> resultados
    )
    {
        return resultados.OrderByDescending(r => r.Modificado).ToList();
    }
}
