using System.Collections.Concurrent;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Pdf;
using Hormiguero.Nucleo.Utilidades;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Archivero.Logica;

public enum Bandeja
{
    PorReconocer,
    SinTexto,
    YaGuardado,
    MismoNombre,
    EnUso,
    NoSeGuardo,
}

public record Pendiente(string Ruta, Bandeja Bandeja, string? Detalle, string? Destino);

// Une todo Archivero: vigila, reconoce, guarda sin perder nada y deja a la vista
// lo que necesita al usuario (SPEC REQ-001 a REQ-007). Todo el trabajo con la
// base y los archivos pasa por un solo hilo, de a un documento por vez.
public sealed class Archivador : IDisposable
{
    public const string App = "Archivero";

    private readonly string rutaBase;
    private readonly Func<DateOnly> hoy;
    private readonly Action<string> aPapelera;
    private readonly TimeSpan reintento;
    private readonly VigilanteEntrada vigilante;
    private readonly BlockingCollection<Action<SqliteConnection>> trabajos = new();
    private readonly ConcurrentDictionary<string, byte> enCola = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly ConcurrentDictionary<string, Pendiente> pendientes = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly Thread hilo;
    private readonly Timer reloj;
    private IReadOnlyList<Movimiento> recientes = [];

    public Archivador(
        string rutaBase,
        IReadOnlyList<string> entradas,
        Func<DateOnly>? hoy = null,
        Action<string>? aPapelera = null,
        TimeSpan? reintento = null
    )
    {
        this.rutaBase = rutaBase;
        this.hoy = hoy ?? (() => DateOnly.FromDateTime(DateTime.Now));
        this.aPapelera = aPapelera ?? MovedorSeguro.APapelera;
        this.reintento = reintento ?? TimeSpan.FromSeconds(5);

        vigilante = new VigilanteEntrada(entradas);
        vigilante.Llego += Encolar;
        vigilante.CambioEstado += () => Cambio?.Invoke();

        hilo = new Thread(Trabajar) { IsBackground = true, Name = "Archivador" };
        hilo.Start();

        // Lo que no se pudo guardar por estar en uso o por un destino caído se
        // reintenta solo, sin consultar las carpetas (vecino silencioso).
        reloj = new Timer(_ => Reintentar(), null, this.reintento, this.reintento);
    }

    public event Action? Cambio;

    public IReadOnlyList<Pendiente> Pendientes =>
        [.. pendientes.Values.OrderBy(p => p.Bandeja).ThenBy(p => p.Ruta)];

    public IReadOnlyList<Movimiento> Recientes => Volatile.Read(ref recientes);

    public IReadOnlyDictionary<string, EstadoCarpeta> Carpetas => vigilante.Estados;

    public void Iniciar()
    {
        trabajos.Add(ActualizarRecientes);
        vigilante.Iniciar();
    }

    public void RevisarAhora() => vigilante.RevisarAhora();

    // Después de crear o editar una configuración, se revisan solos los que
    // estaban por reconocer (REQ-003).
    public void ReconocerDeNuevo()
    {
        foreach (Pendiente pendiente in pendientes.Values)
        {
            if (pendiente.Bandeja is Bandeja.PorReconocer)
            {
                Encolar(pendiente.Ruta);
            }
        }
    }

    public Task<Traslado> GuardarAManoAsync(string ruta, string destino) =>
        EnElHilo(conexion =>
        {
            Traslado traslado = MovedorSeguro.Mover(ruta, destino);
            Registrar(conexion, ruta, destino, traslado, "guardar a mano");
            return traslado;
        });

    public Task DescartarAsync(string ruta) =>
        EnElHilo(conexion =>
        {
            string? huella = File.Exists(ruta) ? Huella.Calcular(ruta) : null;
            aPapelera(ruta);
            pendientes.TryRemove(ruta, out _);
            new Auditoria(conexion).Registrar(App, "descartar", ruta, "Papelera", huella, "ok");
            ActualizarRecientes(conexion);
            return true;
        });

    public void Dispose()
    {
        reloj.Dispose();
        vigilante.Dispose();
        trabajos.CompleteAdding();
        hilo.Join(TimeSpan.FromSeconds(5));
        trabajos.Dispose();
    }

    private void Encolar(string ruta)
    {
        if (!trabajos.IsAddingCompleted && enCola.TryAdd(ruta, 0))
        {
            try
            {
                trabajos.Add(conexion => Procesar(conexion, ruta));
            }
            catch (InvalidOperationException)
            {
                // La app se está cerrando.
            }
        }
    }

    private void Reintentar()
    {
        foreach (Pendiente pendiente in pendientes.Values)
        {
            if (pendiente.Bandeja is Bandeja.EnUso or Bandeja.NoSeGuardo)
            {
                Encolar(pendiente.Ruta);
            }
        }
    }

    private Task<T> EnElHilo<T>(Func<SqliteConnection, T> accion)
    {
        var resultado = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        trabajos.Add(conexion =>
        {
            try
            {
                resultado.SetResult(accion(conexion));
            }
            catch (Exception error)
            {
                resultado.SetException(error);
            }
            Cambio?.Invoke();
        });
        return resultado.Task;
    }

    private void Trabajar()
    {
        using SqliteConnection conexion = BaseComun.Abrir(rutaBase);
        foreach (Action<SqliteConnection> trabajo in trabajos.GetConsumingEnumerable())
        {
            try
            {
                trabajo(conexion);
            }
            catch (Exception)
            {
                // Un documento con problemas no detiene a los demás: cada
                // camino de error ya lo deja en una bandeja visible.
            }
        }
    }

    private void Procesar(SqliteConnection conexion, string ruta)
    {
        enCola.TryRemove(ruta, out _);
        try
        {
            ProcesarUno(conexion, ruta);
        }
        catch (Exception error)
        {
            Apartar(ruta, Bandeja.NoSeGuardo, error.Message, null);
        }
        Cambio?.Invoke();
    }

    private void ProcesarUno(SqliteConnection conexion, string ruta)
    {
        if (!File.Exists(ruta))
        {
            pendientes.TryRemove(ruta, out _);
            return;
        }

        byte[] contenido;
        try
        {
            // Sin compartir la escritura: si todavía se está copiando, se espera.
            using var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var memoria = new MemoryStream();
            flujo.CopyTo(memoria);
            contenido = memoria.ToArray();
        }
        catch (IOException error)
        {
            Apartar(ruta, Bandeja.EnUso, error.Message, null);
            return;
        }

        InfoPdf info = LectorPdf.Leer(new MemoryStream(contenido, writable: false));
        Reconocimiento reconocimiento = Reconocedor.Reconocer(
            info,
            Path.GetFileName(ruta),
            new Identificaciones(conexion).Listar(),
            hoy()
        );

        switch (reconocimiento.Resultado)
        {
            case ResultadoReconocimiento.SinTexto:
                Apartar(ruta, Bandeja.SinTexto, reconocimiento.Detalle, null);
                return;

            case ResultadoReconocimiento.PorReconocer:
                Apartar(ruta, Bandeja.PorReconocer, reconocimiento.Detalle, null);
                return;
        }

        ConfiguracionArchivo configuracion = ConfiguracionArchivo.DeJson(
            reconocimiento.Identificacion!.Datos
        );
        string destino = DestinoArchivo.Ruta(configuracion.Destino, reconocimiento.Datos!);
        Traslado traslado = MovedorSeguro.Mover(ruta, destino);
        Registrar(conexion, ruta, destino, traslado, "guardar");
    }

    private void Registrar(
        SqliteConnection conexion,
        string ruta,
        string destino,
        Traslado traslado,
        string accion
    )
    {
        switch (traslado.Resultado)
        {
            case ResultadoTraslado.Movido:
                pendientes.TryRemove(ruta, out _);
                new Auditoria(conexion).Registrar(
                    App,
                    accion,
                    ruta,
                    destino,
                    traslado.Huella,
                    traslado.Detalle ?? "ok"
                );
                ActualizarRecientes(conexion);
                break;

            case ResultadoTraslado.YaEstabaIgual:
                Apartar(ruta, Bandeja.YaGuardado, null, destino);
                break;

            case ResultadoTraslado.DestinoConOtroContenido:
                Apartar(ruta, Bandeja.MismoNombre, "Mismo nombre, distinto contenido", destino);
                break;

            case ResultadoTraslado.OrigenEnUso:
                Apartar(ruta, Bandeja.EnUso, traslado.Detalle, destino);
                break;

            default:
                Apartar(ruta, Bandeja.NoSeGuardo, traslado.Detalle, destino);
                new Auditoria(conexion).Registrar(
                    App,
                    accion,
                    ruta,
                    destino,
                    traslado.Huella,
                    "error: " + traslado.Detalle
                );
                break;
        }
    }

    private void Apartar(string ruta, Bandeja bandeja, string? detalle, string? destino) =>
        pendientes[ruta] = new Pendiente(ruta, bandeja, detalle, destino);

    private void ActualizarRecientes(SqliteConnection conexion) =>
        Volatile.Write(ref recientes, new Auditoria(conexion).Recientes(App, 200));
}
