using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Hormiguero.Archivero.Logica;
using Hormiguero.Nucleo.Datos;

namespace Hormiguero.Archivero;

public sealed partial class PantallaPrincipal : UserControl
{
    private readonly Archivador archivador;
    private readonly ObservableCollection<CarpetaEnLista> carpetas = [];
    private readonly ObservableCollection<GrupoAvisos> avisos = [];
    private readonly ObservableCollection<RecienteEnLista> recientes = [];
    private readonly DispatcherTimer refresco;
    private volatile bool hayCambios;
    private Window? ventana;

    public event EventHandler<Pendiente>? Identificar;
    public event EventHandler<Pendiente>? GuardarAMano;
    public event EventHandler<Pendiente>? Comparar;
    public event EventHandler? CambiarCarpetas;

    public PantallaPrincipal(Archivador archivador)
    {
        InitializeComponent();
        this.archivador = archivador;

        ListaCarpetas.ItemsSource = carpetas;
        ListaAvisos.ItemsSource = avisos;
        ListaRecientes.ItemsSource = recientes;

        // Cambio llega desde otro hilo: aquí solo se marca la novedad; el
        // refresco real ocurre en este hilo con el temporizador.
        archivador.Cambio += MarcarCambio;
        refresco = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        refresco.Tick += (_, _) => RefrescarSiHuboCambios();

        Loaded += (_, _) =>
        {
            ventana = Window.GetWindow(this);
            if (ventana is not null)
            {
                ventana.PreviewKeyDown += Atajo;
            }
            refresco.Start();
            Refrescar();
        };
        Unloaded += (_, _) =>
        {
            if (ventana is not null)
            {
                ventana.PreviewKeyDown -= Atajo;
            }
            refresco.Stop();
            archivador.Cambio -= MarcarCambio;
        };

        Refrescar();
    }

    private void MarcarCambio() => hayCambios = true;

    private void RefrescarSiHuboCambios()
    {
        if (hayCambios)
        {
            hayCambios = false;
            Refrescar();
        }
    }

    private void Atajo(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Alt)
        {
            return;
        }

        if (e.SystemKey == Key.D)
        {
            RevisarAhora();
            e.Handled = true;
        }
    }

    private void RevisarAhora_Click(object sender, RoutedEventArgs e) => RevisarAhora();

    private void RevisarAhora() => archivador.RevisarAhora();

    private void CambiarCarpetas_Click(object sender, RoutedEventArgs e) =>
        CambiarCarpetas?.Invoke(this, EventArgs.Empty);

    // El mismo botón sirve en todas las bandejas: el texto depende del dato y
    // aquí se reparte al evento que corresponde.
    private void Pendiente_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Pendiente pendiente })
        {
            EventHandler<Pendiente>? evento = pendiente.Bandeja switch
            {
                Bandeja.PorReconocer => Identificar,
                Bandeja.SinTexto => GuardarAMano,
                Bandeja.YaGuardado or Bandeja.MismoNombre => Comparar,
                _ => null,
            };
            evento?.Invoke(this, pendiente);
        }
    }

    private void Abrir_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: RecienteEnLista reciente })
        {
            if (!File.Exists(reciente.Destino))
            {
                MessageBox.Show("El archivo ya no está en esa carpeta");
                return;
            }
            Process.Start(new ProcessStartInfo(reciente.Destino) { UseShellExecute = true });
        }
    }

    private void Carpeta_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: RecienteEnLista reciente })
        {
            if (!File.Exists(reciente.Destino))
            {
                MessageBox.Show("El archivo ya no está en esa carpeta");
                return;
            }
            Process.Start("explorer.exe", $"/select,\"{reciente.Destino}\"");
        }
    }

    private void Refrescar()
    {
        carpetas.Clear();
        foreach (KeyValuePair<string, EstadoCarpeta> par in archivador.Carpetas)
        {
            carpetas.Add(new CarpetaEnLista(par.Key, par.Value));
        }

        avisos.Clear();
        var grupos = new Dictionary<Bandeja, GrupoAvisos>();
        foreach (Pendiente pendiente in archivador.Pendientes)
        {
            if (!grupos.TryGetValue(pendiente.Bandeja, out GrupoAvisos? grupo))
            {
                grupo = pendiente.Bandeja switch
                {
                    Bandeja.PorReconocer => new GrupoAvisos("Por reconocer"),
                    Bandeja.SinTexto => new GrupoAvisos("Sin texto o dañados"),
                    Bandeja.YaGuardado => new GrupoAvisos("Ya guardados antes"),
                    Bandeja.MismoNombre => new GrupoAvisos("Mismo nombre, distinto contenido"),
                    Bandeja.EnUso => new GrupoAvisos("En uso por otro programa"),
                    _ => new GrupoAvisos("No se pudo guardar"),
                };
                avisos.Add(grupo);
            }
            grupo.Filas.Add(new PendienteEnLista(pendiente));
        }
        bool conAvisos = avisos.Count > 0;
        SinAvisos.Visibility = conAvisos ? Visibility.Collapsed : Visibility.Visible;

        recientes.Clear();
        foreach (Movimiento movimiento in archivador.Recientes)
        {
            recientes.Add(new RecienteEnLista(movimiento));
        }
        SinRecientes.Visibility = recientes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private sealed class CarpetaEnLista(string ruta, EstadoCarpeta estado)
    {
        public string Ruta { get; } = ruta;

        public string TextoEstado =>
            estado switch
            {
                EstadoCarpeta.Vigilando => "Vigilando",
                EstadoCarpeta.SinAvisos => "Sin avisos de Windows: revisa cada 30 s",
                _ => "No disponible",
            };

        public Brush ColorEstado =>
            estado switch
            {
                EstadoCarpeta.Vigilando => LeerRecurso("Hormiguero.Exito"),
                EstadoCarpeta.SinAvisos => LeerRecurso("Hormiguero.Aviso"),
                _ => LeerRecurso("Hormiguero.Error"),
            };
    }

    private sealed class GrupoAvisos(string titulo)
    {
        public string Titulo => string.Format("{0} ({1})", titulo, Filas.Count);

        public ObservableCollection<PendienteEnLista> Filas { get; } = [];
    }

    private sealed class PendienteEnLista(Pendiente pendiente)
    {
        public string Nombre => Path.GetFileName(pendiente.Ruta);

        public string Detalle => pendiente.Detalle ?? "";

        public Visibility VisibilidadDetalle =>
            string.IsNullOrEmpty(pendiente.Detalle) ? Visibility.Collapsed : Visibility.Visible;

        public string TextoBoton =>
            pendiente.Bandeja switch
            {
                Bandeja.PorReconocer => "Identificar",
                Bandeja.SinTexto => "Guardar a mano",
                _ => "Comparar",
            };

        public Visibility VisibilidadBoton =>
            pendiente.Bandeja
                is Bandeja.PorReconocer
                    or Bandeja.SinTexto
                    or Bandeja.YaGuardado
                    or Bandeja.MismoNombre
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private sealed class RecienteEnLista(Movimiento movimiento)
    {
        private readonly bool seGuardo = movimiento.Accion is "guardar" or "guardar a mano";

        public string Destino { get; } = movimiento.Destino ?? movimiento.Origen;

        public string Titulo { get; } =
            movimiento.Accion is "guardar" or "guardar a mano"
                ? Path.GetFileName(movimiento.Destino ?? movimiento.Origen)
                : $"Descartado: {Path.GetFileName(movimiento.Origen)}";

        public Brush ColorTitulo =>
            empiezaConError ? LeerRecurso("Hormiguero.Error") : LeerRecurso("Hormiguero.Texto");

        public string Detalle
        {
            get
            {
                string? carpeta =
                    seGuardo && movimiento.Destino is null
                        ? null
                        : Path.GetDirectoryName(movimiento.Destino);
                return carpeta is { Length: > 0 }
                    ? $"{carpeta} · {movimiento.Fecha:dd-MM-yyyy HH:mm}"
                    : movimiento.Fecha.ToString("dd-MM-yyyy HH:mm");
            }
        }

        public Visibility VisibilidadDetalle =>
            seGuardo ? Visibility.Visible : Visibility.Collapsed;

        public Visibility VisibilidadBotones =>
            seGuardo ? Visibility.Visible : Visibility.Collapsed;

        private bool empiezaConError =>
            movimiento.Resultado.StartsWith("error", StringComparison.OrdinalIgnoreCase);
    }

    private static Brush LeerRecurso(string clave) => (Brush)Application.Current.Resources[clave];
}
