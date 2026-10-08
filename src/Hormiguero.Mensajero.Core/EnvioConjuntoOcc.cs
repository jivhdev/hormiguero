namespace Hormiguero.Mensajero.Core;

public sealed record OccEnvioConjunto(
    string Occ,
    string Nvv,
    string Ocl,
    string Proveedor,
    string RutaPdf
);

public static class EnvioConjuntoOcc
{
    public static string FormatearAsunto(IReadOnlyList<OccEnvioConjunto> ordenes)
    {
        ArgumentNullException.ThrowIfNull(ordenes);

        var partes = new List<string>();
        AgregarGrupo(partes, "OCC", ordenes.Select(orden => orden.Occ), quitarDuplicados: false);
        AgregarGrupo(partes, "NVV", ordenes.Select(orden => orden.Nvv), quitarDuplicados: true);
        AgregarGrupo(partes, "OCL", ordenes.Select(orden => orden.Ocl), quitarDuplicados: true);
        return string.Join(" ", partes);
    }

    public static string GenerarCuerpo(string cuerpo, IReadOnlyList<OccEnvioConjunto> ordenes)
    {
        ArgumentNullException.ThrowIfNull(cuerpo);
        ArgumentNullException.ThrowIfNull(ordenes);

        string detalle = string.Join("\r\n", ordenes.Select(FormatearDetalle));
        return string.IsNullOrEmpty(detalle) ? cuerpo : $"{cuerpo.TrimEnd()}\r\n\r\n{detalle}";
    }

    public static string FormatearDetalle(OccEnvioConjunto orden)
    {
        ArgumentNullException.ThrowIfNull(orden);

        var partes = new List<string>();
        AgregarDetalle(partes, "OCC", orden.Occ);
        AgregarDetalle(partes, "NVV", orden.Nvv);
        AgregarDetalle(partes, "OCL", orden.Ocl);
        return string.Join(" · ", partes);
    }

    public static bool PuedeAgregar(IReadOnlyList<OccEnvioConjunto> ordenes, OccEnvioConjunto nueva)
    {
        ArgumentNullException.ThrowIfNull(ordenes);
        ArgumentNullException.ThrowIfNull(nueva);
        return !ordenes.Any(orden => orden.Occ == nueva.Occ)
            && (ordenes.Count == 0 || ordenes[0].Proveedor == nueva.Proveedor);
    }

    private static void AgregarGrupo(
        List<string> partes,
        string etiqueta,
        IEnumerable<string> valores,
        bool quitarDuplicados
    )
    {
        IEnumerable<string> datos = valores.Where(valor => !string.IsNullOrWhiteSpace(valor));
        if (quitarDuplicados)
            datos = datos.Distinct(StringComparer.Ordinal);
        string numeros = string.Join(" ", datos);
        if (numeros.Length > 0)
            partes.Add($"{etiqueta} {numeros}");
    }

    private static void AgregarDetalle(List<string> partes, string etiqueta, string valor)
    {
        if (!string.IsNullOrWhiteSpace(valor))
            partes.Add($"{etiqueta} {valor}");
    }
}
