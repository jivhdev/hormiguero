using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Hormiguero.Buscadero.Logica;
using Microsoft.Win32;

namespace Hormiguero.Buscadero;

public sealed partial class PantallaCarpetas : UserControl
{
    private readonly CarpetasConfiguradas carpetas;
    private readonly ObservableCollection<CarpetaEnLista> lista = [];
    private Window? ventana;

    public event EventHandler? Listo;

    public PantallaCarpetas(CarpetasConfiguradas carpetas)
    {
        InitializeComponent();
        this.carpetas = carpetas;
        ListaCarpetas.ItemsSource = lista;

        // Los atajos (D-55) se escuchan en toda la ventana, no solo cuando el foco está aquí.
        Loaded += (_, _) =>
        {
            ventana = Window.GetWindow(this);
            if (ventana is not null)
            {
                ventana.PreviewKeyDown += Atajo;
            }
        };
        Unloaded += (_, _) =>
        {
            if (ventana is not null)
            {
                ventana.PreviewKeyDown -= Atajo;
            }
        };

        Refrescar();
    }

    private void Atajo(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Alt)
        {
            return;
        }

        if (e.SystemKey == Key.A)
        {
            AgregarCarpetas();
            e.Handled = true;
        }
        else if (e.SystemKey == Key.S && BtnListo.IsEnabled)
        {
            Listo?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void Agregar_Click(object sender, RoutedEventArgs e) => AgregarCarpetas();

    private void Quitar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string ruta })
        {
            carpetas.Quitar(ruta);
            Refrescar();
        }
    }

    private void Listo_Click(object sender, RoutedEventArgs e) =>
        Listo?.Invoke(this, EventArgs.Empty);

    private void AgregarCarpetas()
    {
        var dialogo = new OpenFolderDialog { Multiselect = true };
        if (dialogo.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        foreach (string ruta in dialogo.FolderNames)
        {
            carpetas.Agregar(ruta);
        }

        Refrescar();
    }

    private void Refrescar()
    {
        lista.Clear();
        foreach (string ruta in carpetas.Listar())
        {
            var carpeta = new CarpetaEnLista(ruta);
            lista.Add(carpeta);
            _ = RevisarDisponibleAsync(carpeta);
        }

        BtnListo.IsEnabled = lista.Count > 0;
    }

    // Una carpeta de red caída no congela la pantalla: se revisa aparte y se
    // marca "No disponible" si no responde en 3 segundos (REQ-001).
    private static async Task RevisarDisponibleAsync(CarpetaEnLista carpeta)
    {
        carpeta.Disponible = await CarpetasConfiguradas.EstaDisponibleAsync(
            carpeta.Ruta,
            TimeSpan.FromSeconds(3)
        );
    }

    private sealed class CarpetaEnLista(string ruta) : INotifyPropertyChanged
    {
        private bool disponible = true;

        public string Ruta { get; } = ruta;

        public bool Disponible
        {
            get => disponible;
            set
            {
                if (disponible != value)
                {
                    disponible = value;
                    PropertyChanged?.Invoke(this, new(nameof(Disponible)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
