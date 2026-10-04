using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Hormiguero.Buscadero.Logica;

namespace Hormiguero.Buscadero;

public partial class VentanaPrincipal : Window
{
    private readonly ControlIndice _controlIndice = new();
    private readonly IndiceEnSegundoPlano _indice;
    private readonly CarpetasConfiguradas _carpetas;

    // El índice avisa por cada archivo: la barra se refresca a su ritmo, no al del índice.
    private readonly DispatcherTimer _refresco = new()
    {
        Interval = TimeSpan.FromMilliseconds(500),
    };

    public VentanaPrincipal()
    {
        InitializeComponent();

        _carpetas = new CarpetasConfiguradas(App.Base!);
        _indice = new IndiceEnSegundoPlano(App.RutaBase, _controlIndice);
        _refresco.Tick += (_, _) => MostrarEstadoIndice();
        _refresco.Start();
        Closed += (_, _) =>
        {
            _refresco.Stop();
            _indice.Dispose();
        };

        if (_carpetas.HayAlguna)
        {
            _indice.Revisar();
            MostrarBuscador();
        }
        else
        {
            MostrarPrimeraConfiguracion();
        }
    }

    private void MostrarPrimeraConfiguracion()
    {
        BtnConfiguracion.Visibility = Visibility.Collapsed;
        var pantalla = new PantallaCarpetas(_carpetas);
        pantalla.Listo += (_, _) =>
        {
            _indice.Revisar();
            MostrarBuscador();
        };
        Contenido.Content = pantalla;
    }

    private void MostrarBuscador()
    {
        BtnConfiguracion.Visibility = Visibility.Visible;
        var pantalla = new PantallaBuscar(App.Base!, _controlIndice);
        pantalla.VisorDocumento.Impresora = ElegirImpresora;
        Contenido.Content = pantalla;
    }

    private void Configuracion_Click(object sender, RoutedEventArgs e)
    {
        var ventana = new VentanaConfiguracion(App.Base!) { Owner = this };
        ventana.ShowDialog();

        if (!ventana.CarpetasCambiaron)
        {
            return;
        }

        if (_carpetas.HayAlguna)
        {
            _indice.Revisar();
            // Se vuelve a crear para que la lista de carpetas del alcance quede al día.
            MostrarBuscador();
        }
        else
        {
            MostrarPrimeraConfiguracion();
        }
    }

    // REQ-006 y D-60: directa por defecto; en Configuración, otra fija o el cuadro de Windows.
    private (bool Seguir, string? Nombre) ElegirImpresora()
    {
        var preferencia = new PreferenciaImpresion(App.Base!);
        switch (preferencia.Modo)
        {
            case ModoImpresion.ImpresoraFija:
                return (true, preferencia.Impresora);

            case ModoImpresion.CuadroDeWindows:
                var cuadro = new PrintDialog { UserPageRangeEnabled = false };
                return cuadro.ShowDialog() == true
                    ? (true, cuadro.PrintQueue.FullName)
                    : (false, null);

            default:
                return (true, null);
        }
    }

    private void MostrarEstadoIndice()
    {
        string texto = _indice.Estado switch
        {
            EstadoIndice.Armandose when _indice.Porcentaje is int porcentaje =>
                $"Índice: armándose, {porcentaje} %",
            EstadoIndice.Armandose =>
                $"Índice: armándose · {_controlIndice.ArchivosRevisados} documentos revisados",
            EstadoIndice.AlDia => $"Índice al día · {_indice.DocumentosIndexados} documentos",
            _ when _indice.UltimoError is Exception error => $"Índice detenido: {error.Message}",
            _ => "",
        };

        int noDisponibles = _indice.NoDisponibles.Count;
        if (noDisponibles == 1)
        {
            texto += " · 1 carpeta no disponible";
        }
        else if (noDisponibles > 1)
        {
            texto += $" · {noDisponibles} carpetas no disponibles";
        }

        TextoIndice.Text = texto;
    }
}
