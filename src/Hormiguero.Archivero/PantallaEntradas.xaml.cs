using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Hormiguero.Archivero.Logica;
using Microsoft.Win32;

namespace Hormiguero.Archivero;

public sealed partial class PantallaEntradas : UserControl
{
    private readonly CarpetasEntrada entradas;
    private readonly ObservableCollection<CarpetaEnLista> lista = [];
    private Window? ventana;

    public event EventHandler? Listo;

    public PantallaEntradas(CarpetasEntrada entradas)
    {
        InitializeComponent();
        this.entradas = entradas;
        ListaCarpetas.ItemsSource = lista;

        // Los atajos se escuchan en toda la ventana, no solo cuando el foco está aquí.
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

        AgregarRutas(entradas.Listar());
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
            Confirmar();
            e.Handled = true;
        }
    }

    private void Agregar_Click(object sender, RoutedEventArgs e) => AgregarCarpetas();

    private void Quitar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CarpetaEnLista carpeta })
        {
            lista.Remove(carpeta);
            BtnListo.IsEnabled = lista.Count > 0;
        }
    }

    private void Listo_Click(object sender, RoutedEventArgs e) => Confirmar();

    private void AgregarCarpetas()
    {
        var dialogo = new OpenFolderDialog { Multiselect = true };
        if (dialogo.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        AgregarRutas(dialogo.FolderNames);
    }

    private void AgregarRutas(IEnumerable<string> rutas)
    {
        foreach (string ruta in rutas)
        {
            if (
                lista.Any(otra =>
                    string.Equals(otra.Ruta, ruta, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                continue;
            }

            var carpeta = new CarpetaEnLista(ruta);
            lista.Add(carpeta);
            _ = RevisarDisponibleAsync(carpeta);
        }

        BtnListo.IsEnabled = lista.Count > 0;
    }

    private void Confirmar()
    {
        entradas.Guardar(lista.Select(c => c.Ruta));
        Listo?.Invoke(this, EventArgs.Empty);
    }

    // Una carpeta de red que no responde deja Directory.Exists bloqueado: se revisa
    // en otro hilo y, si no contesta en 3 segundos, se marca "No disponible" (REQ-001).
    private static async Task RevisarDisponibleAsync(CarpetaEnLista carpeta)
    {
        carpeta.Disponible = await EstaDisponibleAsync(carpeta.Ruta, TimeSpan.FromSeconds(3));
    }

    private static async Task<bool> EstaDisponibleAsync(string ruta, TimeSpan limite)
    {
        var tarea = Task.Run(() => Directory.Exists(ruta));
        var primera = await Task.WhenAny(tarea, Task.Delay(limite));
        return ReferenceEquals(primera, tarea) && await tarea;
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
