using System.Windows;
using System.Windows.Controls;

namespace Hormiguero.Mensajero.App;

public partial class VentanaCodigosFlotante : Window
{
    private readonly Action<string> copiar;
    private string[] codigos;
    private int siguiente;

    public VentanaCodigosFlotante(string[] codigos, Action<string> copiar)
    {
        InitializeComponent();
        this.codigos = codigos;
        this.copiar = copiar;
        ListaCodigos.ItemsSource = codigos;
        ActualizarProgreso();
    }

    public void Actualizar(string[] nuevos)
    {
        codigos = nuevos;
        siguiente = 0;
        ListaCodigos.ItemsSource = codigos;
        ActualizarProgreso();
    }

    private void Copiar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string codigo })
            return;
        copiar(codigo);
        int indice = Array.IndexOf(codigos, codigo, siguiente);
        siguiente = indice < 0 ? 0 : (indice + 1) % codigos.Length;
        ActualizarProgreso();
    }

    private void ActualizarProgreso() =>
        TextoProgreso.Text =
            codigos.Length == 0
                ? "Sin códigos"
                : $"Siguiente: {codigos[siguiente]} ({siguiente + 1}/{codigos.Length})";
}
