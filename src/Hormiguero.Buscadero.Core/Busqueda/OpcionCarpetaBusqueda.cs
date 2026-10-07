namespace Buscadero.Core.Busqueda;

public sealed record OpcionCarpetaBusqueda(string Ruta, bool NoDisponible)
{
    public string Texto => NoDisponible ? $"{Ruta} (no disponible)" : Ruta;

    // El ComboBox editable escribe en su cuadro el texto del elemento elegido.
    public override string ToString() => Texto;
}
