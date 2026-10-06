using Archivero.Datos;
using Hormiguero.Nucleo.Datos;

namespace Archivero.Servicios;

public static class DatosEnlazantesConfiguracionService
{
    public static IReadOnlyList<DatoEnlazanteConfigurado> Leer(
        string emisor,
        string tipo,
        int patronId
    )
    {
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        var identificacion = new Identificaciones(conexion)
            .Listar()
            .FirstOrDefault(i => i.Emisor == emisor && i.Tipo == tipo);
        var diccionario = new RepositorioDatosEnlazantes(conexion).LeerDiccionario();
        var zonas = identificacion is null
            ? []
            : new RepositorioDatosEnlazantes(conexion).ListarDatosTipo(
                identificacion.Id,
                patronId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
        return diccionario
            .Select(d =>
            {
                var zona = zonas.FirstOrDefault(z => z.DatoId == d.Id && z.Activo);
                return new DatoEnlazanteConfigurado(
                    d.Id,
                    d.Nombre,
                    d.Grupo,
                    zona is not null,
                    zona is not null,
                    zona is null ? 0 : zona.Pagina - 1,
                    zona?.X ?? 0,
                    zona?.Y ?? 0,
                    zona?.Ancho ?? 0,
                    zona?.Alto ?? 0,
                    string.Empty
                );
            })
            .ToList();
    }

    public static void Guardar(
        string emisor,
        string tipo,
        string grupo,
        string nombreEstandar,
        int patronId,
        IReadOnlyList<DatoEnlazanteConfigurado> datos
    )
    {
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        var repositorioIdentificaciones = new Identificaciones(conexion);
        var identificacion = repositorioIdentificaciones
            .Listar()
            .FirstOrDefault(i => i.Emisor == emisor && i.Tipo == tipo);
        long identificacionId = repositorioIdentificaciones.Guardar(
            new Identificacion(
                identificacion?.Id ?? 0,
                tipo,
                emisor,
                identificacion?.Datos ?? "{}",
                grupo,
                nombreEstandar
            )
        );

        var repositorioDatos = new RepositorioDatosEnlazantes(conexion);
        string diseno = patronId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var guardados = repositorioDatos.ListarDatosTipo(identificacionId, diseno, true);
        foreach (var dato in datos)
        {
            var existente = guardados.FirstOrDefault(z => z.DatoId == dato.Id && z.Activo);
            if (!dato.Incluido)
            {
                if (existente is not null)
                    repositorioDatos.AnularDatoTipo(existente.Id);
                continue;
            }

            if (!dato.Marcado)
                throw new InvalidOperationException($"Marca una zona para «{dato.Nombre}».");
            if (existente is null && guardados.Any(z => z.DatoId == dato.Id && !z.Activo))
                throw new InvalidOperationException(
                    $"No se puede volver a marcar «{dato.Nombre}» en este diseño: el repositorio del Núcleo no permite reactivar la zona anulada sin borrar su historial."
                );
            repositorioDatos.GuardarDatoTipo(
                new DatoTipoDocumento(
                    existente?.Id ?? 0,
                    identificacionId,
                    dato.Id,
                    diseno,
                    null,
                    dato.Pagina + 1,
                    dato.X,
                    dato.Y,
                    dato.Ancho,
                    dato.Alto,
                    true
                )
            );
        }
    }

    public static void VincularCamposAnteriores(
        string emisor,
        string tipo,
        int patronId,
        IReadOnlyList<(string NombreEstable, string DatoId)> vinculos
    )
    {
        if (vinculos.Count == 0)
            return;
        using var conexion = BaseComun.Abrir(DocumentosGuardados.RutaBaseComun);
        var identificacion = new Identificaciones(conexion)
            .Listar()
            .FirstOrDefault(i => i.Emisor == emisor && i.Tipo == tipo);
        if (identificacion is null)
            return;
        var repositorioDocumentos = new RepositorioDocumentosDatos(conexion);
        var campos = repositorioDocumentos.ListarCampos(identificacion.Id, incluirInactivos: true);
        var repositorioEnlazantes = new RepositorioDatosEnlazantes(conexion);
        var zonas = repositorioEnlazantes.ListarDatosTipo(
            identificacion.Id,
            patronId.ToString(System.Globalization.CultureInfo.InvariantCulture)
        );
        foreach (var vinculo in vinculos)
        {
            var campo = campos.FirstOrDefault(c => c.NombreEstable == vinculo.NombreEstable);
            var zona = zonas.FirstOrDefault(z => z.DatoId == vinculo.DatoId && z.Activo);
            if (campo is null || zona is null)
                continue;
            repositorioDocumentos.GuardarCampo(campo with { DatoDiccionarioId = vinculo.DatoId });
            repositorioEnlazantes.GuardarDatoTipo(zona with { CampoId = campo.Id });
        }
    }
}
