using System.Windows;
using Hormiguero.Diseno;

namespace Buscadero.App;

public partial class App : Application
{
    // Fase B-1 (D-66): diseño común de Hormiguero, claro u oscuro según Windows.
    protected override void OnStartup(StartupEventArgs e)
    {
        Tema.Aplicar(this, ModoTema.Sistema);
        base.OnStartup(e);
    }
}
