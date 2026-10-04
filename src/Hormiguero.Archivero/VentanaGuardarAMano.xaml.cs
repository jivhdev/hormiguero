using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Hormiguero.Archivero.Logica;
using Hormiguero.Diseno.Controles;
using Hormiguero.Nucleo.Utilidades;
using Microsoft.Win32;

namespace Hormiguero.Archivero;

public sealed partial class VentanaGuardarAMano : Window
{
    private readonly Archivador archivador;
    private readonly Pendiente pendiente;

    // Una por carpeta madre, sin repetir (comparación sin mayúsculas).
    private readonly List<ReglaDestino> elegibles = [];
    private readonly HashSet<string> creadas = new(StringComparer.OrdinalIgnoreCase);

    public VentanaGuardarAMano(
        Archivador archivador,
        Pendiente pendiente,
        IReadOnlyList<ReglaDestino> ubicaciones
    )
    {
        InitializeComponent();
        this.archivador = archivador;
        this.pendiente = pendiente;

        foreach (
            var grupo in ubicaciones.GroupBy(u => u.CarpetaMadre, StringComparer.OrdinalIgnoreCase)
        )
        {
            elegibles.Add(grupo.First());
        }

        foreach (ReglaDestino regla in elegibles)
        {
            _ = Ubicaciones.Items.Add(regla.CarpetaMadre);
        }

        Nombre.Text = Path.GetFileNameWithoutExtension(pendiente.Ruta);
        Fecha.SelectedDate = DateTime.Today;

        // Los atajos de la ventana se deciden antes que los del visor, que
        // también escucha Alt+F y Alt+G para imprimir.
        Loaded += (_, _) =>
        {
            PreviewKeyDown += Atajo;
            _ = AbrirPdfAsync();
        };
        Unloaded += (_, _) => PreviewKeyDown -= Atajo;

        ActualizarVista();
    }

    private async Task AbrirPdfAsync() => await Visor.AbrirAsync(pendiente.Ruta);

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

        if (e.SystemKey == Key.S && BtnGuardar.IsEnabled)
        {
            _ = GuardarAsync();
            e.Handled = true;
        }
    }

    private void Ubicaciones_Changed(object sender, SelectionChangedEventArgs e) =>
        ActualizarVista();

    private void Nombre_Changed(object sender, TextChangedEventArgs e) => ActualizarVista();

    private void Fecha_Changed(object sender, SelectionChangedEventArgs e) => ActualizarVista();

    // La forma elegida vale para las ubicaciones creadas aquí; las de las
    // configuraciones conservan la suya.
    private void Forma_Changed(object sender, RoutedEventArgs e)
    {
        if (
            !IsLoaded
            || Ubicaciones.SelectedItem is not string carpeta
            || !creadas.Contains(carpeta)
        )
        {
            return;
        }
        int indice = elegibles.FindIndex(u =>
            string.Equals(u.CarpetaMadre, carpeta, StringComparison.OrdinalIgnoreCase)
        );
        elegibles[indice] = elegibles[indice] with { Forma = FormaElegida() };
        ActualizarVista();
    }

    private void CrearUbicacion_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFolderDialog();
        if (dialogo.ShowDialog(this) != true)
        {
            return;
        }

        string ruta = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dialogo.FolderName));
        if (
            elegibles.Any(u =>
                string.Equals(u.CarpetaMadre, ruta, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            Ubicaciones.SelectedItem = ruta;
            return;
        }

        elegibles.Add(new ReglaDestino(ruta, FormaElegida(), [], ""));
        creadas.Add(ruta);
        _ = Ubicaciones.Items.Add(ruta);
        Ubicaciones.SelectedItem = ruta;
        PanelFormas.Visibility = Visibility.Visible;
    }

    private FormaCarpeta FormaElegida()
    {
        if (FormaSoloAnio.IsChecked == true)
        {
            return FormaCarpeta.SoloAnio;
        }
        if (FormaDirecto.IsChecked == true)
        {
            return FormaCarpeta.Directo;
        }
        return FormaCarpeta.AnioYMes;
    }

    private ReglaDestino? ReglaElegida =>
        Ubicaciones.SelectedItem is string carpeta
            ? elegibles.FirstOrDefault(u =>
                string.Equals(u.CarpetaMadre, carpeta, StringComparison.OrdinalIgnoreCase)
            )
            : null;

    private void ActualizarVista()
    {
        ReglaDestino? regla = ReglaElegida;
        if (regla is null || string.IsNullOrWhiteSpace(Nombre.Text))
        {
            Vista.Text = "";
            BtnGuardar.IsEnabled = false;
            return;
        }

        var fecha = DateOnly.FromDateTime(Fecha.SelectedDate ?? DateTime.Today);
        Vista.Text = Path.Combine(
            DestinoArchivo.Carpeta(regla, fecha),
            Limpio(Nombre.Text) + ".pdf"
        );
        BtnGuardar.IsEnabled = true;
    }

    // Al guardar se cambian por "-" los caracteres no válidos en un nombre.
    private static string Limpio(string nombre)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        return new string(nombre.Select(c => invalidos.Contains(c) ? '-' : c).ToArray());
    }

    private void Guardar_Click(object sender, RoutedEventArgs e) => _ = GuardarAsync();

    private async Task GuardarAsync()
    {
        if (ReglaElegida is not { CarpetaMadre: not null } regla)
        {
            return;
        }

        var fecha = DateOnly.FromDateTime(Fecha.SelectedDate ?? DateTime.Today);
        string destino = Path.Combine(
            DestinoArchivo.Carpeta(regla, fecha),
            Limpio(Nombre.Text) + ".pdf"
        );

        try
        {
            Traslado traslado = await archivador.GuardarAManoAsync(pendiente.Ruta, destino);
            switch (traslado.Resultado)
            {
                case ResultadoTraslado.Movido:
                    DialogResult = true;
                    Close();
                    break;

                case ResultadoTraslado.YaEstabaIgual:
                    Aviso.Text = "Ese documento ya está guardado en esa carpeta";
                    break;

                case ResultadoTraslado.DestinoConOtroContenido:
                    Aviso.Text = "Ya existe otro documento con ese nombre: elige otro nombre";
                    break;

                case ResultadoTraslado.OrigenEnUso:
                    Aviso.Text = "El archivo está abierto en otro programa";
                    break;

                default:
                    Aviso.Text = "No se pudo guardar: " + (traslado.Detalle ?? "");
                    break;
            }
        }
        catch (Exception error)
        {
            Aviso.Text = "No se pudo guardar: " + error.Message;
        }
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();
}
