using System.Globalization;
using System.Numerics;
using ClosedXML.Excel;

namespace Hormiguero.Mensajero.Core.ClickFactura;

public sealed class ErrorLectorExcelFactura(string mensaje, Exception? inner = null)
    : Exception(mensaje, inner);

public static class LectorExcelFactura
{
    public static IReadOnlyList<DocumentoFactura> Leer(string ruta)
    {
        if (!File.Exists(ruta))
            throw new ErrorLectorExcelFactura($"El archivo no existe: {ruta}");

        using var libro = Abrir(ruta);
        IXLWorksheet hoja = libro.Worksheet(1);
        int filaEncabezado = 0;
        int indiceTipo = -1;
        int indiceNumero = -1;
        int indiceEntidad = -1;
        for (int fila = 1; fila <= 10; fila++)
        {
            var valores = hoja.Row(fila)
                .CellsUsed()
                .ToDictionary(
                    celda => celda.Address.ColumnNumber,
                    celda => celda.GetString().Trim()
                );
            if (
                !valores.Values.Contains("TD")
                || !valores.Values.Contains("Número")
                || !valores.Values.Contains("Entidad")
            )
                continue;

            filaEncabezado = fila;
            indiceTipo = valores.Single(par => par.Value == "TD").Key;
            indiceNumero = valores.Single(par => par.Value == "Número").Key;
            indiceEntidad = valores.Single(par => par.Value == "Entidad").Key;
            break;
        }

        if (filaEncabezado == 0)
            throw new ErrorLectorExcelFactura(
                "No se encontraron las columnas TD, Número, Entidad en las primeras 10 filas."
            );

        var documentos = new List<DocumentoFactura>();
        int ultimaFila = hoja.LastRowUsed()?.RowNumber() ?? filaEncabezado;
        for (int numeroFila = filaEncabezado + 1; numeroFila <= ultimaFila; numeroFila++)
        {
            IXLRow fila = hoja.Row(numeroFila);
            IXLCell celdaTipo = fila.Cell(indiceTipo);
            IXLCell celdaNumero = fila.Cell(indiceNumero);
            IXLCell celdaEntidad = fila.Cell(indiceEntidad);
            if (celdaTipo.IsEmpty() && celdaNumero.IsEmpty())
                continue;

            string numero = FormatearNumero(celdaNumero);
            documentos.Add(
                new DocumentoFactura(
                    celdaTipo.IsEmpty() ? "" : celdaTipo.GetString().Trim().ToUpperInvariant(),
                    numero,
                    celdaEntidad.IsEmpty() ? "" : celdaEntidad.GetString().Trim()
                )
            );
        }

        if (documentos.Count == 0)
            throw new ErrorLectorExcelFactura("El archivo no contiene datos.");
        return documentos;
    }

    private static XLWorkbook Abrir(string ruta)
    {
        try
        {
            return new XLWorkbook(ruta);
        }
        catch (Exception error)
        {
            throw new ErrorLectorExcelFactura(
                $"No se pudo abrir el archivo: {error.Message}",
                error
            );
        }
    }

    private static string FormatearNumero(IXLCell celda)
    {
        if (celda.IsEmpty())
            return "";

        string numero;
        if (celda.DataType == XLDataType.Number && celda.TryGetValue<double>(out double valor))
        {
            numero = new BigInteger(valor).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            string texto = celda.GetString().Trim();
            numero = BigInteger.TryParse(
                texto,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out BigInteger entero
            )
                ? entero.ToString(CultureInfo.InvariantCulture)
                : texto;
        }

        if (numero.StartsWith("-", StringComparison.Ordinal))
            return "-" + numero[1..].PadLeft(9, '0');
        if (numero.StartsWith("+", StringComparison.Ordinal))
            return "+" + numero[1..].PadLeft(9, '0');
        return numero.PadLeft(10, '0');
    }
}
