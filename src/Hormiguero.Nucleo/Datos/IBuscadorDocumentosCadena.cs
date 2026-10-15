namespace Hormiguero.Nucleo.Datos;

public interface IBuscadorDocumentosCadena
{
    IReadOnlyList<long> Buscar(string tipoDiccionario, string numero, EsquemaDiccionario esquema);
}
