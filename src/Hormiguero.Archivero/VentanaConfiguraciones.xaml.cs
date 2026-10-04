using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Hormiguero.Archivero.Logica;
using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Archivero;

public sealed partial class VentanaConfiguraciones : Window
{
    private readonly Identificaciones identificaciones;
    private readonly ObservableCollection<Grupo> grupos = [];

    public event EventHandler<Identificacion>? Editar;

    public VentanaConfiguraciones(SqliteConnection conexion)
    {
        InitializeComponent();
        identificaciones = new Identificaciones(conexion);
        ListaGrupos.ItemsSource = grupos;

        // Misma forma que PantallaCarpetas: los atajos se escuchan en toda la
        // ventana, no solo cuando el foco está aquí.
        Loaded += (_, _) => PreviewKeyDown += Atajo;
        Unloaded += (_, _) => PreviewKeyDown -= Atajo;

        Refrescar();
    }

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
    }

    public void Refrescar()
    {
        grupos.Clear();
        foreach (Identificacion identificacion in identificaciones.Listar())
        {
            Fila fila = Fila.De(identificacion);
            Grupo? grupo = grupos.FirstOrDefault(g => g.Emisor == fila.Emisor);
            if (grupo is null)
            {
                grupo = new Grupo(fila.Emisor);
                grupos.Add(grupo);
            }
            grupo.Filas.Add(fila);
        }
        SinConfiguraciones.Visibility =
            grupos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Editar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Fila fila })
        {
            Editar?.Invoke(this, fila.Identificacion);
        }
    }

    private void Quitar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Fila fila })
        {
            return;
        }

        var opciones = MessageBox.Show(
            $"¿Quitar la configuración {fila.Tipo} de {fila.Emisor}? Los documentos ya guardados no se tocan.",
            "Archivero",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );
        if (opciones != MessageBoxResult.Yes)
        {
            return;
        }

        _ = identificaciones.Quitar(fila.Identificacion.Id);
        Refrescar();
    }

    private void Cerrar_Click(object sender, RoutedEventArgs e) => Close();

    private sealed class Grupo(string emisor)
    {
        public string Emisor { get; } = emisor;

        public ObservableCollection<Fila> Filas { get; } = [];
    }

    private sealed class Fila(Identificacion identificacion, ConfiguracionArchivo? configuracion)
    {
        public Identificacion Identificacion { get; } = identificacion;

        public string Tipo { get; } = identificacion.Tipo;

        public string Emisor { get; } = identificacion.Emisor;

        public string TextoDestino =>
            configuracion is null
                ? "Configuración dañada"
                : $"Se guarda en: {configuracion.Destino.CarpetaMadre}\n{TextoForma(configuracion.Destino.Forma)}";

        public Brush ColorDestino =>
            (Brush)
                Application.Current.Resources[
                    configuracion is null ? "Hormiguero.Error" : "Hormiguero.TextoSecundario"
                ];

        public Visibility VisibilidadBotonEditar =>
            configuracion is null ? Visibility.Collapsed : Visibility.Visible;

        private static string TextoForma(FormaCarpeta forma) =>
            forma switch
            {
                FormaCarpeta.AnioYMes => "año y mes",
                FormaCarpeta.SoloAnio => "solo año",
                _ => "directo",
            };

        public static Fila De(Identificacion identificacion)
        {
            ConfiguracionArchivo? configuracion = null;
            try
            {
                configuracion = ConfiguracionArchivo.DeJson(identificacion.Datos);
            }
            catch (Exception)
            {
                // La fila queda en "Configuración dañada" y solo permite quitarla.
            }
            return new(identificacion, configuracion);
        }
    }
}
