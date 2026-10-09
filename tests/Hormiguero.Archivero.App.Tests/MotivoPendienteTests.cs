using Archivero.Datos;

namespace Archivero.Tests;

public class MotivoPendienteTests
{
    [Fact]
    public void FaltaProveedor_DescripcionLegible_ContieneFaltaElProveedor()
    {
        var descripcion = MotivoPendiente.FaltaProveedor.DescripcionLegible();

        Assert.Contains("Falta el proveedor", descripcion);
    }
}
