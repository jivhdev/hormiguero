using System.Windows;
using Archivero.Datos;

namespace Archivero.Vistas;

/// <summary>
/// Pregunta del final de "Crear ubicación nueva" (Caso-11, punto 4). Solo elige el nombre: el
/// atajo se persiste recién después de que el documento se guardó bien, no acá.
/// </summary>
public partial class GuardarAtajoWindow : Window
{
    private readonly AtajoGuardadoRapidoRepository _atajos;

    public string? NombreElegido { get; private set; }

    public GuardarAtajoWindow(string nombreSugerido, AtajoGuardadoRapidoRepository atajos)
    {
        InitializeComponent();
        _atajos = atajos;
        TxtNombreAtajo.Text = nombreSugerido;
        Loaded += (_, _) =>
        {
            TxtNombreAtajo.Focus();
            TxtNombreAtajo.SelectAll();
        };
    }

    private void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        var nombre = TxtNombreAtajo.Text.Trim();
        if (nombre.Length == 0)
        {
            TxtError.Text = "Escribir un nombre para el acceso rápido.";
            TxtError.Visibility = Visibility.Visible;
            return;
        }

        if (_atajos.ExisteNombre(nombre))
        {
            var reemplazar = System.Windows.MessageBox.Show(
                this, $"Ya existe un acceso rápido llamado \"{nombre}\". ¿Reemplazarlo por este?",
                "Archivero", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (reemplazar != MessageBoxResult.Yes)
            {
                return;
            }
        }

        NombreElegido = nombre;
        DialogResult = true;
    }
}
