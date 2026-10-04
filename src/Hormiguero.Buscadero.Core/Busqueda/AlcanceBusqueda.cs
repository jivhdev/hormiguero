namespace Buscadero.Core.Busqueda;

/// <summary>
/// Decide EN QUE CARPETAS se busca. Es un concepto distinto de <see cref="ModoBusqueda"/>
/// (que decide COMO se compara el texto contra el nombre de archivo) - ver Caso-11.
/// </summary>
public enum AlcanceBusqueda
{
    /// <summary>
    /// Por defecto: recorre las carpetas madre configuradas y se detiene apenas
    /// encuentra una con al menos una coincidencia.
    /// </summary>
    PrimeraCoincidencia,

    /// <summary>
    /// Recorre todas las carpetas madre configuradas sin detenerse, y reune todas
    /// las coincidencias encontradas en cualquiera de ellas.
    /// </summary>
    TodasLasCarpetas,

    /// <summary>
    /// Restringe la busqueda a una unica carpeta (madre o subcarpeta), elegida con
    /// el filtro de carpeta existente.
    /// </summary>
    CarpetaEspecifica,
}
