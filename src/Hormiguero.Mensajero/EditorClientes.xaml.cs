using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Hormiguero.Mensajero.Core;

namespace Hormiguero.Mensajero.App;

public partial class EditorClientes : Window
{
    private readonly Action<IReadOnlyList<string>> guardar;
    private bool modificado;
    private bool guardado;
    private bool cerrando;
    private bool cargado;

    public EditorClientes(IReadOnlyList<string> clientes, Action<IReadOnlyList<string>> guardar)
    {
        InitializeComponent();
        this.guardar = guardar;
        CampoClientes.Text = string.Join(Environment.NewLine, clientes);
        CampoClientes.CaretIndex = 0;
        cargado = true;
    }

    private void CampoClientes_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!cargado || guardado)
            return;
        modificado = true;
        TextoAviso.Text = "⚠️ Hay cambios sin guardar";
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string[] nuevos = CampoClientes
                .Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(linea => linea.Trim())
                .Where(linea => linea.Length > 0)
                .ToArray();
            guardar(nuevos);
            guardado = true;
            modificado = false;
            TextoAviso.Text = "✅ Guardado correctamente";
            Dispatcher.BeginInvoke(Close, System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (Exception excepcion)
        {
            MessageBox.Show(
                this,
                $"No se pudieron guardar los clientes: {excepcion.Message}",
                "Mensajero",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();

    private void Ventana_Closing(object? sender, CancelEventArgs e)
    {
        if (cerrando || !modificado || guardado)
            return;
        MessageBoxResult respuesta = MessageBox.Show(
            this,
            "¿Salir sin guardar los cambios?",
            "Cambios sin guardar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );
        if (respuesta != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        cerrando = true;
    }
}
