using System.IO.Packaging;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Hormiguero.Diseno;

public enum ModoTema
{
    Sistema,
    Claro,
    Oscuro,
}

public static class Tema
{
    private const string RutaPersonalizar =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ValorAppsClaro = "AppsUseLightTheme";

    private static readonly Uri UriClaro = UriDe("Claro");
    private static readonly Uri UriOscuro = UriDe("Oscuro");

    // Estilos de los controles: van siempre después del tema de colores para
    // ganarle a los estilos de Fluent y tomar sus colores con DynamicResource.
    private static readonly Uri UriControles = UriDe("Controles");

    private static UserPreferenceChangedEventHandler? alCambiarWindows;
    private static bool ventanasRegistradas;
    private static bool temaActualOscuro;

    private static Uri UriDe(string nombre)
    {
        // El esquema "pack" solo se entiende si WPF registró su analizador; sin
        // esto, construir la Uri falla al leer los signos de coma.
        _ = PackUriHelper.UriSchemePack;

        return new Uri($"pack://application:,,,/Hormiguero.Diseno;component/Temas/{nombre}.xaml");
    }

    public static bool WindowsEstaEnOscuro()
    {
        using RegistryKey? clave = Registry.CurrentUser.OpenSubKey(RutaPersonalizar);

        if (clave?.GetValue(ValorAppsClaro) is int usaClaro)
        {
            return usaClaro == 0;
        }

        // Si Windows no dice nada, se supone el modo claro.
        return false;
    }

    public static Uri DiccionarioPara(ModoTema modo, bool sistemaOscuro) =>
        modo switch
        {
            ModoTema.Sistema when sistemaOscuro => UriOscuro,
            ModoTema.Oscuro => UriOscuro,
            _ => UriClaro,
        };

    public static void Aplicar(Application app, ModoTema modo)
    {
        ArgumentNullException.ThrowIfNull(app);

        // HORMIGUERO_TEMA=claro|oscuro fuerza un tema para probar sin cambiar Windows.
        modo = Environment.GetEnvironmentVariable("HORMIGUERO_TEMA")?.ToLowerInvariant() switch
        {
            "claro" => ModoTema.Claro,
            "oscuro" => ModoTema.Oscuro,
            _ => modo,
        };

        Quitar(app);
        Uri diccionario = DiccionarioPara(modo, WindowsEstaEnOscuro());
        temaActualOscuro = diccionario == UriOscuro;
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = diccionario });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = UriControles });
        PintarTodasLasVentanas();
        // Las ventanas ya abiertas también cambian su barra de título al cambiar Windows.
        foreach (Window abierta in app.Windows)
        {
            BarraDeTitulo(abierta);
        }

        if (modo == ModoTema.Sistema)
        {
            Escuchar(app);
        }
        else
        {
            DejarDeEscuchar();
        }
    }

    // Un estilo implícito de Window no se aplica a las ventanas propias de cada app
    // (MainWindow y diálogos derivan de Window): sin esto quedan con fondo blanco y
    // texto negro en el modo oscuro. Se pinta cada ventana al cargarse, salvo que
    // ya traiga su propio fondo o color de texto.
    private static void PintarTodasLasVentanas()
    {
        if (ventanasRegistradas)
        {
            return;
        }
        ventanasRegistradas = true;
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((origen, _) => Pintar((Window)origen))
        );
    }

    public static void Pintar(Window ventana)
    {
        ArgumentNullException.ThrowIfNull(ventana);
        if (ventana.ReadLocalValue(Control.BackgroundProperty) == DependencyProperty.UnsetValue)
        {
            ventana.SetResourceReference(Control.BackgroundProperty, "Hormiguero.Fondo");
        }
        if (ventana.ReadLocalValue(Control.ForegroundProperty) == DependencyProperty.UnsetValue)
        {
            ventana.SetResourceReference(Control.ForegroundProperty, "Hormiguero.Texto");
        }
        BarraDeTitulo(ventana);
    }

    // La barra de título la dibuja Windows: se le pide el modo oscuro cuando el
    // tema activo lo es (Windows 10 2004 en adelante; en versiones viejas no hace nada).
    private static void BarraDeTitulo(Window ventana)
    {
        IntPtr ventanaWin32 = new System.Windows.Interop.WindowInteropHelper(ventana).Handle;
        if (ventanaWin32 == IntPtr.Zero)
        {
            return;
        }
        int oscuro = temaActualOscuro ? 1 : 0;
        _ = DwmSetWindowAttribute(ventanaWin32, UsarModoOscuro, ref oscuro, sizeof(int));
    }

    private const int UsarModoOscuro = 20;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr ventana,
        int atributo,
        ref int valor,
        int tamano
    );

    private static void Quitar(Application app)
    {
        List<ResourceDictionary> cargados = app
            .Resources.MergedDictionaries.OfType<ResourceDictionary>()
            .ToList();

        foreach (ResourceDictionary diccionario in cargados)
        {
            if (
                diccionario.Source is Uri origen
                && (origen == UriClaro || origen == UriOscuro || origen == UriControles)
            )
            {
                app.Resources.MergedDictionaries.Remove(diccionario);
            }
        }
    }

    private static void Escuchar(Application app)
    {
        if (alCambiarWindows is not null)
        {
            return;
        }

        alCambiarWindows = (_, _) =>
            // SystemEvents avisa desde su propio hilo: el cambio se hace en el de la interfaz.
            app.Dispatcher.BeginInvoke(() => Aplicar(app, ModoTema.Sistema));
        SystemEvents.UserPreferenceChanged += alCambiarWindows;
    }

    private static void DejarDeEscuchar()
    {
        if (alCambiarWindows is null)
        {
            return;
        }

        SystemEvents.UserPreferenceChanged -= alCambiarWindows;
        alCambiarWindows = null;
    }
}
