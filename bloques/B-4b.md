---
bloque: B-4b
app: Archivero
fase: B (D-66, definicion/PLAN-MIGRACION.md)
estado: pendiente
agente: OpenCode
modelo: opencode-go/glm-5.3-flash
archivos_permitidos: [src/Hormiguero.Archivero/Servicios/ClasificadorService.cs, src/Hormiguero.Archivero/App.xaml.cs, tests/Hormiguero.Archivero.App.Tests/ClasificadorServiceTests.cs]
archivos_prohibidos: [todo lo demás]
rama: fase-b/b4b-archivero-avisa
---

# B-4b — Archivero avisa cada documento que guarda

## Objetivo

Cada vez que Archivero guarda un documento, lo anota en el registro común de Hormiguero (`Hormiguero.Nucleo.Datos.DocumentosGuardados`, ya existe), para que Buscadero lo encuentre al instante. **No cambia nada más del comportamiento de Archivero.**

## Contenido exacto

1. `ClasificadorService.cs`: agrega
   ```csharp
   /// <summary>Se dispara con la ruta final cada vez que un documento quedó guardado (fase B-4).</summary>
   public static event Action<string>? DocumentoGuardado;
   ```
   y dispáralo al final de `CopiarVerificarBorrar(origen, destino)`, **solo si todo salió bien** (después de borrar el original), con `destino`. Un error de quien escucha el evento **nunca** debe romper el guardado: envuelve la invocación en `try { ... } catch (Exception) { }`.
2. `App.xaml.cs`: en `OnStartup`, justo después de `UsarCarpetaComunDeHormiguero();` (dentro del mismo `if (string.IsNullOrWhiteSpace(datosDePrueba))`, para que el modo de prueba `ARCHIVERO_DATOS` no escriba en la base común real), suscribe:
   ```csharp
   ClasificadorService.DocumentoGuardado += AvisarAHormiguero;
   ```
   con este método en la misma clase:
   ```csharp
   // Fase B-4 (D-66): avisa a las otras apps (Buscadero lo encuentra al instante).
   // Si falla, el documento igual quedó guardado: solo se anota en la auditoría.
   private static void AvisarAHormiguero(string rutaFinal)
   {
       try
       {
           using var conexion = Hormiguero.Nucleo.Datos.BaseComun.Abrir(
               Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun
           );
           new Hormiguero.Nucleo.Datos.DocumentosGuardados(conexion).Registrar(rutaFinal, "Archivero");
       }
       catch (Exception error)
       {
           AuditoriaService.Registrar("AVISO_HORMIGUERO_FALLIDO", $"{rutaFinal}: {error.Message}");
       }
   }
   ```
3. `ClasificadorServiceTests.cs`: agrega dos pruebas (mira cómo arman archivos las pruebas existentes de ese mismo archivo):
   - `Al_guardar_avisa_la_ruta_final`: suscribe al evento, clasifica un archivo y verifica que llegó exactamente la ruta final. Desuscribe al terminar (`try/finally`).
   - `Si_la_copia_falla_no_avisa`: provoca que no se pueda guardar (por ejemplo, destino ya existente si así falla `Clasificar`, o el caso de error que las pruebas existentes ya usen) y verifica que el evento **no** se disparó.

## Criterio de término

- `dotnet build` sin advertencias; `dotnet test` todo pasa (Archivero tenía 277); `dotnet csharpier check .` sin errores.
- Escribe tu reporte al final de esta nota.

## Reporte del agente

Estado: **bloque terminado**.

Cambios (solo los tres archivos permitidos):

1. `src/Hormiguero.Archivero/Servicios/ClasificadorService.cs`: evento estático
   `DocumentoGuardado` (con el resumen del bloque) y disparo al final de
   `CopiarVerificarBorrar(origen, destino)`, después de `File.Delete(origen)`
   (o sea, solo si copia+verificación+borrado salieron bien), con `destino`.
   La invocación va envuelta en `try { ... } catch (Exception) { }` para que un
   error de quien escucha nunca rompa el guardado.
2. `src/Hormiguero.Archivero/App.xaml.cs`: en `OnStartup`, dentro del
   `if (string.IsNullOrWhiteSpace(datosDePrueba))`, justo después de
   `UsarCarpetaComunDeHormiguero();`, se suscribe
   `ClasificadorService.DocumentoGuardado += AvisarAHormiguero;`. El método
   `AvisarAHormiguero(string rutaFinal)` abre la base común
   (`BaseComun.Abrir(DocumentosGuardados.RutaBaseComun)`, que respeta
   `HORMIGUERO_DATOS`) y registra con `Registrar(rutaFinal, "Archivero")`;
   si falla, anota `AVISO_HORMIGUERO_FALLIDO` en `AuditoriaService`. En modo
   prueba `ARCHIVERO_DATOS` no se suscribe, así no toca la base común real.
3. `tests/Hormiguero.Archivero.App.Tests/ClasificadorServiceTests.cs`: dos
   pruebas nuevas, con el armado de archivos del propio archivo de pruebas:
   - `Al_guardar_avisa_la_ruta_final`: suscribe, clasifica, verifica que llegó
     exactamente una vez la ruta final esperada; desuscribe en `try/finally`.
   - `Si_la_copia_falla_no_avisa`: duplicado en destino (mismo caso que usan
     las pruebas existentes), verifica `ArchivoDuplicadoException`, que el
     evento no se disparó (0 avisos) y que el original quedó intacto;
     desuscribe en `try/finally`.

Verificación:

- `dotnet build`: 16 proyectos, 0 errores, 0 advertencias.
- `dotnet test`: 555 pruebas en 6 proyectos, ninguna falla. Archivero pasó de
  277 a 279 (las 2 nuevas pasan).
- `dotnet csharpier check .`: 218 archivos revisados, sin errores.

Notas / decisiones:

- Ninguna. El bloque se implementó tal cual; no toqué nada fuera de los tres
  archivos permitidos y no aparecieron decisiones de arquitectura ni de
  nombres (los textos venían dados en la nota del bloque).

