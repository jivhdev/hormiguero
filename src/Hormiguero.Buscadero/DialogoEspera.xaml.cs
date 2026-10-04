using System.Windows;
using System.Windows.Threading;

namespace Buscadero.App;

public partial class DialogoEspera : Window
{
    private readonly DispatcherTimer _temporizador;
    private int _restante = 5;

    public DialogoEspera()
    {
        InitializeComponent();
        _temporizador = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _temporizador.Tick += (_, _) =>
        {
            _restante--;
            if (_restante <= 0)
            {
                _temporizador.Stop();
                BotonConfirmar.Content = "Confirmar";
                BotonConfirmar.IsEnabled = true;
            }
            else
            {
                Actualizar();
            }
        };

        Loaded += (_, _) =>
        {
            Actualizar();
            _temporizador.Start();
        };
        Closed += (_, _) => _temporizador.Stop();
    }

    public static bool Pedir(Window propietario, string mensaje)
    {
        var dialogo = new DialogoEspera { Owner = propietario };
        dialogo.Mensaje.Text = mensaje;
        return dialogo.ShowDialog() == true;
    }

    private void Actualizar() => BotonConfirmar.Content = $"Confirmar ({_restante})";

    private void Confirmar_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
