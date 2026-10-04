using System.IO;
using System.Threading;
using Archivero.Datos;
using Archivero.Servicios.Pdf;

namespace Archivero.Servicios;

public class VigilanciaCarpetaService : IDisposable
{
    private readonly string _carpetaObservada;
    private readonly PendienteRepository _pendientes = new();
    private readonly BorradorRepository _borradores = new();
    private readonly ConfiguracionDocumentoRepository _configuraciones = new();
    private readonly object _candadoProceso = new();
    private FileSystemWatcher? _watcher;

    /// <summary>Un PDF nuevo no coincide con ninguna configuración: pasa al flujo de identificación (REQ-003).</summary>
    public event Action<string>? ArchivoPendienteDetectado;

    /// <summary>Un archivo que estaba en pendientes se sacó de la carpeta observada (a mano, por fuera de Archivero).</summary>
    public event Action<string>? ArchivoPendienteEliminado;

    /// <summary>Un PDF se reconoció y se guardó solo en su ubicación definitiva.</summary>
    public event Action<string, string>? ArchivoGuardadoAutomaticamente;

    /// <summary>Un PDF coincidió con una configuración pero algo impidió guardarlo solo (queda pendiente, hay que avisar).</summary>
    public event Action<string, string>? ArchivoRequiereAtencion;

    /// <summary>La carpeta observada dejó de existir (se borró o se movió) mientras Archivero corría.</summary>
    public event Action? CarpetaObservadaNoDisponible;

    public VigilanciaCarpetaService(string carpetaObservada)
    {
        _carpetaObservada = carpetaObservada;
    }

    public void Iniciar()
    {
        ReconciliarPendientesConDisco();
        RevisarArchivosExistentes();

        if (!Directory.Exists(_carpetaObservada))
        {
            // La carpeta observada ya no existe (se borró o se movió) desde ANTES de que
            // Archivero arrancara -- no solo mientras corría (ese caso ya lo cubre el evento
            // Error del FileSystemWatcher, más abajo). Sin este aviso, Archivero se queda
            // observando nada en silencio total: ningún archivo nuevo se nota nunca, sin
            // ninguna pista visible de por qué (bug real reportado por Javier).
            CarpetaObservadaNoDisponible?.Invoke();
            return;
        }

        _watcher = new FileSystemWatcher(_carpetaObservada, "*.pdf")
        {
            NotifyFilter = NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        // FileSystemWatcher entrega los eventos Created de a uno, en orden, en un unico hilo de
        // fondo: como este handler es sincronico (sin async/Task.Run), los archivos se procesan
        // de a uno y nunca en paralelo, tal como pide SPEC.md.
        _watcher.Created += (_, e) => ProcesarArchivo(e.FullPath);

        // Si el usuario saca un archivo de la carpeta observada por fuera de Archivero
        // (lo borra, lo mueve a mano) mientras estaba en pendientes, hay que reflejarlo:
        // si no, la lista de pendientes queda mostrando un archivo que ya no existe.
        _watcher.Deleted += (_, e) => ManejarArchivoEliminado(e.FullPath);
        _watcher.Renamed += (_, e) =>
        {
            ManejarArchivoEliminado(e.OldFullPath);
            ProcesarArchivo(e.FullPath);
        };

        // Si la carpeta observada se borra o se mueve mientras Archivero esta corriendo, el
        // FileSystemWatcher dispara Error en vez de quedarse callado.
        _watcher.Error += (_, _) => CarpetaObservadaNoDisponible?.Invoke();
    }

    /// <summary>
    /// Caso-11, punto 3: vuelve a intentar la coincidencia automática contra todos los
    /// documentos que están hoy en "pendientes por reconocer" -- no solo contra los que lleguen
    /// de ahora en adelante. Se llama apenas se crea o edita una configuración, y a mano desde
    /// el botón "Reprocesar pendientes". Los "pendientes de distribuir" (PDF sin texto o archivos
    /// dañados) no se reprocesan: nunca se clasifican solos, tengan la configuración que tengan.
    /// </summary>
    public void ReprocesarPendientes()
    {
        ReconciliarPendientesConDisco();

        foreach (var pendiente in _pendientes.ObtenerTodos().Where(p => !p.Motivo.EsPendienteDeDistribuir()))
        {
            ProcesarArchivo(pendiente.RutaArchivo, esReproceso: true);
        }
    }

    private void ManejarArchivoEliminado(string rutaArchivo)
    {
        _borradores.Eliminar(rutaArchivo);
        if (_pendientes.Quitar(rutaArchivo))
        {
            ArchivoPendienteEliminado?.Invoke(rutaArchivo);
        }
    }

    /// <summary>
    /// Si un archivo se sacó de la carpeta observada mientras Archivero no estaba corriendo,
    /// el watcher en vivo nunca se enteró: al arrancar, hay que limpiar del listado de
    /// pendientes cualquier archivo que ya no exista en el disco.
    /// </summary>
    private void ReconciliarPendientesConDisco()
    {
        foreach (var pendiente in _pendientes.ObtenerTodos())
        {
            if (!File.Exists(pendiente.RutaArchivo))
            {
                ManejarArchivoEliminado(pendiente.RutaArchivo);
            }
        }
    }

    private void RevisarArchivosExistentes()
    {
        if (!Directory.Exists(_carpetaObservada))
        {
            return;
        }

        foreach (var archivo in Directory.GetFiles(_carpetaObservada, "*.pdf"))
        {
            ProcesarArchivo(archivo);
        }
    }

    private void ProcesarArchivo(string rutaArchivo, bool esReproceso = false)
    {
        // Los archivos se procesan de a uno y nunca en paralelo (SPEC.md): el reproceso de
        // pendientes corre fuera del hilo del FileSystemWatcher, así que hace falta serializarlo.
        lock (_candadoProceso)
        {
            ProcesarArchivoSinBloquear(rutaArchivo, esReproceso);
        }
    }

    private void ProcesarArchivoSinBloquear(string rutaArchivo, bool esReproceso)
    {
        try
        {
            if (!File.Exists(rutaArchivo))
            {
                return;
            }

            AuditoriaService.Registrar(esReproceso ? "DOCUMENTO_REPROCESADO" : "DOCUMENTO_DETECTADO", rutaArchivo);

            // Caso-6, punto 1: el evento Created del FileSystemWatcher dispara apenas Windows
            // crea el archivo destino, no cuando termina de copiarse -- con archivos grandes
            // (ej. una guia escaneada en imagen, mucho más pesada que un PDF con texto) es
            // comun que todavia este bloqueado por el proceso que esta copiando. Sin esta
            // espera, LectorPdf tira una excepcion que el catch de abajo se traga en silencio,
            // y el archivo nunca llega a pendientes hasta el proximo reinicio (que si funciona,
            // porque para entonces la copia ya termino).
            if (!EsperarHastaQueElArchivoEsteListo(rutaArchivo))
            {
                return;
            }

            bool tieneTexto;
            try
            {
                tieneTexto = LectorPdf.TieneTextoExtraible(rutaArchivo);
            }
            catch (Exception)
            {
                // Caso-11, punto 5: PDFium no puede abrirlo (dañado, vacío o truncado). Antes
                // RNF-2 lo descartaba en silencio y quedaba invisible; ahora va a "Pendientes de
                // distribuir" para que el usuario decida dónde guardarlo a mano. Nunca se toca.
                AgregarAPendientes(rutaArchivo, MotivoPendiente.ArchivoDanado);
                return;
            }

            if (!tieneTexto)
            {
                // No es un PDF con texto plano extraible (ej. una imagen escaneada): no hay
                // coordenadas que comparar ni Emisor/Tipo que identificar, asi que nunca se
                // auto-clasifica (Caso-4 reemplaza por completo el intento de Caso-1 de
                // reconocerlos solos) -- siempre pasa a "Pendientes de distribuir" para que el
                // usuario elija la ubicacion a mano (RNF-3: nunca se toca ni se mueve en silencio).
                AgregarAPendientes(rutaArchivo, MotivoPendiente.SinTextoExtraible);
                return;
            }

            var configuraciones = _configuraciones.ObtenerTodasConPatrones();
            var coincidencia = CoincidenciaAutomaticaService.BuscarConfiguracionQueCoincide(rutaArchivo, configuraciones);

            if (coincidencia is null)
            {
                AgregarAPendientes(rutaArchivo, MotivoPendiente.NuevoDocumento);
                return;
            }

            ManejarResultadoGuardado(rutaArchivo, GuardadoAutomaticoService.Procesar(rutaArchivo, coincidencia));
        }
        catch
        {
            // RNF-2: si el archivo esta corrupto o no se puede leer, Archivero deja de intentar
            // con ese archivo puntual (sin tocarlo ni moverlo) y sigue observando con normalidad.
        }
    }

    /// <summary>
    /// Reintenta abrir el archivo de solo lectura (sin bloquear a quien lo esta copiando) hasta
    /// 3 segundos: mientras el proceso que lo copia todavia lo tiene abierto para escritura,
    /// abrir falla con IOException (violacion de uso compartido) en vez de tirar una excepcion
    /// rara mas adelante al leerlo con PDFium. Si nunca se libera, se trata como el caso ya
    /// existente de "archivo corrupto o no legible" (RNF-2): se lo deja intacto y se sigue.
    /// </summary>
    private static bool EsperarHastaQueElArchivoEsteListo(string rutaArchivo)
    {
        const int intentos = 10;
        const int esperaEntreIntentosMs = 300;

        for (var intento = 0; intento < intentos; intento++)
        {
            try
            {
                using var stream = File.Open(rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.Read);
                return true;
            }
            catch (IOException)
            {
                Thread.Sleep(esperaEntreIntentosMs);
            }
        }

        return false;
    }

    private void ManejarResultadoGuardado(string rutaArchivo, ResultadoProcesamiento resultado)
    {
        switch (resultado.Resultado)
        {
            case ResultadoGuardadoAutomatico.Guardado:
                _pendientes.Quitar(rutaArchivo);
                ArchivoGuardadoAutomaticamente?.Invoke(rutaArchivo, resultado.RutaFinal!);
                break;

            case ResultadoGuardadoAutomatico.ValorInvalido:
                AgregarAPendientes(rutaArchivo, MotivoPendiente.ValorInvalido);
                break;

            case ResultadoGuardadoAutomatico.Duplicado:
                if (AgregarAPendientes(rutaArchivo, MotivoPendiente.Duplicado))
                {
                    ArchivoRequiereAtencion?.Invoke(rutaArchivo, resultado.Detalle ?? resultado.Resultado.ToString());
                }
                break;

            case ResultadoGuardadoAutomatico.CarpetaNoDisponible:
                if (AgregarAPendientes(rutaArchivo, MotivoPendiente.CarpetaNoDisponible))
                {
                    ArchivoRequiereAtencion?.Invoke(rutaArchivo, resultado.Detalle ?? resultado.Resultado.ToString());
                }
                break;

            case ResultadoGuardadoAutomatico.PeriodoNuevo:
                if (AgregarAPendientes(rutaArchivo, MotivoPendiente.PeriodoNuevo))
                {
                    ArchivoRequiereAtencion?.Invoke(rutaArchivo, resultado.Detalle ?? resultado.Resultado.ToString());
                }
                break;

            case ResultadoGuardadoAutomatico.NombrePorConfirmar:
                // Caso-11, punto 1: sin ventana emergente (REQ-005); queda en pendientes y la bandeja avisa.
                if (AgregarAPendientes(rutaArchivo, MotivoPendiente.NombrePorConfirmar))
                {
                    ArchivoRequiereAtencion?.Invoke(rutaArchivo, resultado.Resultado.ToString());
                }
                break;

            case ResultadoGuardadoAutomatico.ValidacionFallida:
                // Caso-9, mejora 1(g): nunca un guardado silencioso -- motivo específico y legible.
                if (AgregarAPendientes(rutaArchivo, resultado.MotivoValidacion!.Value))
                {
                    ArchivoRequiereAtencion?.Invoke(rutaArchivo, resultado.Detalle ?? resultado.Resultado.ToString());
                }
                break;
        }
    }

    private bool AgregarAPendientes(string rutaArchivo, MotivoPendiente motivo)
    {
        var esNuevo = _pendientes.Agregar(rutaArchivo, motivo);
        if (esNuevo)
        {
            // Único punto donde un archivo pasa a pendientes, para cualquier motivo -- cubre a
            // la vez "documento a pendientes con motivo" y "rechazo por validación" (Caso-9,
            // mejora 2), sin duplicar el registro en cada lugar que llama a este método.
            AuditoriaService.Registrar("DOCUMENTO_PENDIENTE", $"Motivo={motivo}; Ruta={rutaArchivo}");
            ArchivoPendienteDetectado?.Invoke(rutaArchivo);
        }

        return esNuevo;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
