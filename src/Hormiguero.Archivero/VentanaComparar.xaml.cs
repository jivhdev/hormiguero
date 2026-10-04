using System.Windows;
using System.Windows.Input;
using Hormiguero.Archivero.Logica;
using Hormiguero.Diseno.Controles;

namespace Hormiguero.Archivero;

public enum ResultadoComparar
{
    Nada,
    Descartado,
    GuardarConOtroNombre,
}

public sealed partial class VentanaComparar : Window
{
    private readonly Archivador archivador;
    private readonly Pendiente pendiente;

    public ResultadoComparar Resultado { get; private set; } = ResultadoComparar.Nada;

    public VentanaComparar(Archivador archivador, Pendiente pendiente)
    {
        InitializeComponent();
        this.archivador = archivador;
        this.pendiente = pendiente;

        Mensaje.Text = pendiente.Bandeja switch
        {
            Bandeja.YaGuardado => "Son idénticos: el que llegó ya está guardado.",
            _ =>
                "Tienen el mismo nombre pero distinto contenido. Nada se sobrescribe: elige qué hacer.",
        };
        BtnGuardarOtro.Visibility =
            pendiente.Bandeja == Bandeja.MismoNombre ? Visibility.Visible : Visibility.Collapsed;

        RutaLlego.Text = pendiente.Ruta;
        RutaEstaba.Text = pendiente.Destino ?? "";

        // Los atajos de la ventana se deciden antes que los del visor, que
        // también escucha Alt+F y Alt+G para imprimir.
        PreviewKeyDown += Atajo;

        _ = AbrirDocumentosAsync();
    }

    private async Task AbrirDocumentosAsync()
    {
        await VisorLlego.AbrirAsync(pendiente.Ruta);
        if (pendiente.Destino is not null)
        {
            await VisorEstaba.AbrirAsync(pendiente.Destino);
        }
    }

    // Misma forma que PantallaCarpetas: atajos en la ventana con Alt y SystemKey.
    private void Atajo(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Alt)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            return;
        }

        if (e.SystemKey == Key.F)
        {
            Descartar();
            e.Handled = true;
        }
        else if (e.SystemKey == Key.G && BtnGuardarOtro.Visibility == Visibility.Visible)
        {
            GuardarConOtroNombre();
            e.Handled = true;
        }
    }

    private void Descartar_Click(object sender, RoutedEventArgs e) => Descartar();

    private async void Descartar()
    {
        var opciones = MessageBox.Show(
            "¿Enviar a la Papelera el documento que llegó?",
            "Archivero",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );
        if (opciones != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await archivador.DescartarAsync(pendiente.Ruta);
            Resultado = ResultadoComparar.Descartado;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se pudo enviar a la Papelera:\n{ex.Message}",
                "Archivero",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void GuardarOtro_Click(object sender, RoutedEventArgs e) => GuardarConOtroNombre();

    private void GuardarConOtroNombre()
    {
        Resultado = ResultadoComparar.GuardarConOtroNombre;
        Close();
    }
}
