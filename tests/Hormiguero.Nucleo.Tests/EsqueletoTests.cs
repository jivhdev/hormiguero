using System.Reflection;

namespace Hormiguero.Nucleo.Tests;

public class EsqueletoTests
{
    [Fact]
    public void El_esqueleto_compila_y_corre()
    {
        Assembly ensamblado = Assembly.Load("Hormiguero.Nucleo");

        Assert.NotNull(ensamblado);
    }
}
