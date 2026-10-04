using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Buscadero.Logica;

public enum ModoImpresion
{
    Directa,
    ImpresoraFija,
    CuadroDeWindows,
}

// Cómo imprime el visor (REQ-006, D-60): directo en la predeterminada de
// Windows por defecto; en Configuración se elige otra fija o el cuadro.
public sealed class PreferenciaImpresion(SqliteConnection conexion)
{
    private const string ClaveModo = "buscadero.impresion.modo";
    private const string ClaveImpresora = "buscadero.impresion.impresora";

    private readonly Configuracion configuracion = new(conexion);

    public ModoImpresion Modo =>
        Enum.TryParse(configuracion.Leer(ClaveModo), out ModoImpresion modo)
            ? modo
            : ModoImpresion.Directa;

    public string? Impresora =>
        Modo == ModoImpresion.ImpresoraFija ? configuracion.Leer(ClaveImpresora) : null;

    public void Guardar(ModoImpresion modo, string? impresora = null)
    {
        if (modo == ModoImpresion.ImpresoraFija)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(impresora);
            configuracion.Guardar(ClaveImpresora, impresora);
        }

        configuracion.Guardar(ClaveModo, modo.ToString());
    }
}
