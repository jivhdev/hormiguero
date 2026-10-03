using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;

namespace Hormiguero.Diseno.Tests;

public class TemaTests
{
    // WPF admite una sola Application por proceso y cada objeto de WPF pertenece al
    // hilo que lo crea: todas las pruebas se atienden en un único hilo STA.
    private static readonly BlockingCollection<Action> pendientes = new();
    private static readonly Thread hiloSta = CrearHiloSta();
    private static Application? aplicacion;

    [Theory]
    [InlineData(ModoTema.Sistema, true, "Oscuro")]
    [InlineData(ModoTema.Sistema, false, "Claro")]
    [InlineData(ModoTema.Claro, true, "Claro")]
    [InlineData(ModoTema.Claro, false, "Claro")]
    [InlineData(ModoTema.Oscuro, true, "Oscuro")]
    [InlineData(ModoTema.Oscuro, false, "Oscuro")]
    public void Elige_el_diccionario_correcto(ModoTema modo, bool sistemaOscuro, string esperado)
    {
        Uri uri = Tema.DiccionarioPara(modo, sistemaOscuro);

        Assert.Equal(
            $"pack://application:,,,/Hormiguero.Diseno;component/Temas/{esperado}.xaml",
            uri.ToString()
        );
    }

    [Fact]
    public void Ambos_temas_tienen_las_mismas_claves()
    {
        List<string> claro = EnSta(_ => ClavesDe(Tema.DiccionarioPara(ModoTema.Claro, false)));
        List<string> oscuro = EnSta(_ => ClavesDe(Tema.DiccionarioPara(ModoTema.Oscuro, false)));

        Assert.Equal(claro, oscuro);
        Assert.Contains("Hormiguero.Principal", claro);
        // El estilo base de Button no lleva clave: se registra por tipo.
        Assert.Contains(typeof(Button).FullName!, claro);
    }

    [Fact]
    public void Aplicar_dos_veces_no_duplica()
    {
        EnSta(app =>
        {
            app.Resources.MergedDictionaries.Add(new ResourceDictionary());

            Tema.Aplicar(app, ModoTema.Claro);
            Tema.Aplicar(app, ModoTema.Oscuro);

            List<ResourceDictionary> temas = app
                .Resources.MergedDictionaries.OfType<ResourceDictionary>()
                .Where(diccionario => diccionario.Source != null)
                .ToList();
            Assert.Single(temas);
            Assert.Equal(Tema.DiccionarioPara(ModoTema.Oscuro, false), temas[0].Source);
        });
    }

    private static List<string> ClavesDe(Uri uri) =>
        new ResourceDictionary { Source = uri }
            .Keys.Cast<object>()
            .Select(clave => clave.ToString() ?? string.Empty)
            .OrderBy(clave => clave, StringComparer.Ordinal)
            .ToList();

    private static T EnSta<T>(Func<Application, T> funcion)
    {
        T? resultado = default;
        EnSta(app =>
        {
            resultado = funcion(app);
        });
        return resultado!;
    }

    private static void EnSta(Action<Application> accion)
    {
        Exception? fallo = null;
        var terminado = new ManualResetEventSlim();
        pendientes.Add(() =>
        {
            try
            {
                accion(Aplicacion());
            }
            catch (Exception ex)
            {
                fallo = ex;
            }
            finally
            {
                terminado.Set();
            }
        });

        Assert.True(terminado.Wait(TimeSpan.FromSeconds(30)), "El hilo de WPF no respondió.");
        if (fallo is not null)
        {
            ExceptionDispatchInfo.Capture(fallo).Throw();
        }
    }

    private static Thread CrearHiloSta()
    {
        var hilo = new Thread(() =>
        {
            foreach (Action accion in pendientes.GetConsumingEnumerable())
            {
                accion();
            }
        })
        {
            IsBackground = true,
            Name = "pruebas-wpf",
        };
        hilo.SetApartmentState(ApartmentState.STA);
        hilo.Start();
        return hilo;
    }

    private static Application Aplicacion() => aplicacion ??= new Application();
}
