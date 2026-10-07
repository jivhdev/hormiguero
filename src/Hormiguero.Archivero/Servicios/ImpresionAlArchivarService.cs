using System.Diagnostics;
using System.Drawing.Printing;
using System.IO;
using Archivero.Datos;
using Hormiguero.Nucleo.Pdf;

namespace Archivero.Servicios;

public interface IAccionImpresion
{
    string ImpresoraPredeterminada { get; }
    IReadOnlyList<string> ImpresorasInstaladas { get; }
    void Imprimir(byte[] pdf, bool soloPrimeraPagina, string? impresora);
    void AbrirVisor(string rutaPdf);
}

public sealed class AccionImpresionWindows : IAccionImpresion
{
    public string ImpresoraPredeterminada => new PrinterSettings().PrinterName;

    public IReadOnlyList<string> ImpresorasInstaladas =>
        PrinterSettings.InstalledPrinters.Cast<string>().ToArray();

    public void Imprimir(byte[] pdf, bool soloPrimeraPagina, string? impresora) =>
        ImpresionPdf.Imprimir(pdf, soloPrimeraPagina ? 1 : int.MaxValue, impresora);

    public void AbrirVisor(string rutaPdf) =>
        Process.Start(new ProcessStartInfo(rutaPdf) { UseShellExecute = true });
}

public sealed class ImpresionAlArchivarService(IAccionImpresion accion)
{
    public string? Procesar(string rutaPdf, ConfiguracionDocumento configuracion)
    {
        if (configuracion.ModoImpresion == ModoImpresion.No)
            return null;

        try
        {
            if (configuracion.ModoImpresion == ModoImpresion.PreguntarCadaVez)
            {
                accion.AbrirVisor(rutaPdf);
                return null;
            }

            string? impresora = configuracion.Impresora;
            string? aviso = null;
            if (
                !string.IsNullOrWhiteSpace(impresora)
                && !accion.ImpresorasInstaladas.Contains(
                    impresora,
                    StringComparer.OrdinalIgnoreCase
                )
            )
            {
                impresora = null;
                aviso =
                    "La impresora guardada ya no está instalada. Se usó la predeterminada de Windows.";
            }

            byte[] pdf = File.ReadAllBytes(rutaPdf);
            bool soloPrimeraPagina = configuracion.ModoImpresion == ModoImpresion.PrimeraPagina;
            accion.Imprimir(pdf, soloPrimeraPagina, impresora);
            AuditoriaService.Registrar(
                "IMPRESION_AUTOMATICA",
                $"Tipo={configuracion.Tipo}; Ruta={rutaPdf}; Impresora={impresora ?? accion.ImpresoraPredeterminada}"
            );
            return aviso;
        }
        catch (Exception error)
        {
            AuditoriaService.Registrar(
                "IMPRESION_AUTOMATICA_FALLIDA",
                $"Tipo={configuracion.Tipo}; Ruta={rutaPdf}; Error={error.Message}"
            );
            return "El documento se guardó, pero no se pudo imprimir.";
        }
    }
}
