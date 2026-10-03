using System.IO.Packaging;
using System.Windows;
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

    private static UserPreferenceChangedEventHandler? alCambiarWindows;

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

        Quitar(app);
        app.Resources.MergedDictionaries.Add(
            new ResourceDictionary { Source = DiccionarioPara(modo, WindowsEstaEnOscuro()) }
        );

        if (modo == ModoTema.Sistema)
        {
            Escuchar(app);
        }
        else
        {
            DejarDeEscuchar();
        }
    }

    private static void Quitar(Application app)
    {
        List<ResourceDictionary> cargados = app
            .Resources.MergedDictionaries.OfType<ResourceDictionary>()
            .ToList();

        foreach (ResourceDictionary diccionario in cargados)
        {
            if (diccionario.Source is Uri origen && (origen == UriClaro || origen == UriOscuro))
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
