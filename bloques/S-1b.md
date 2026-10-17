---
bloque: S-1b
app: Núcleo + Buscadero — "Buscar…" en el eslabón vacío y prueba de fusión
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Nucleo/**, src/Hormiguero.Buscadero/**, src/Hormiguero.Buscadero.Core/**, src/Hormiguero.Archivero/Servicios/BuscadorDocumentosEnCarpetaService.cs, tests/Hormiguero.Nucleo.Tests/**, tests/Hormiguero.Buscadero.Core.Tests/**]
archivos_prohibidos: [migraciones existentes; ventanas de Archivero (las hace S-2 en paralelo); Mensajero; Reinicio]
rama: buscadero/s1b-buscar-eslabon
---

# S-1b — Terminar lo pendiente de S-1

Lee `bloques/S-1.md` y su reporte. **Decisión de Claude** sobre el límite que te detuvo: el registro "sin clasificación" (tipo + número tomado del nombre del archivo + proveedor del esquema, sin mover ni imprimir) se mueve al **Núcleo** como servicio compartido (p. ej. `RegistroDocumentoEnCadena.RegistrarDesdeArchivo(ruta, lugar, esquema)` que publica con `RepositorioDocumentosDatos.PublicarDocumento` y pasa el documento al motor). El servicio de búsqueda de Archivero lo usa para ese caso (cuando hay clasificación, Archivero sigue leyendo zonas como hoy). Buscadero lo usa directamente.

1. **Vista de cadena (Buscadero)**: cada eslabón vacío muestra **"Buscar…"**: primero busca en la `carpeta_busqueda` del lugar con la misma regla (subcarpetas de lo más nuevo a lo más antiguo, filtro por nombre con el número esperado si se conoce por un enlace, sin abrir PDF que no calcen); si encuentra uno, pregunta "Se encontró {archivo}. ¿Agregarlo a la cadena?"; si no, abre el selector de archivo **en esa carpeta**. El elegido se registra con el servicio del Núcleo (número desde el nombre; si el nombre no trae dígitos, pedir el número en un cuadro) y se ubica en ese lugar de esa cadena (cardinalidad ocupada → aviso y dudoso). Funciona también para eslabones como COVP/COV que no se enlazan solos.
2. **Prueba de integración de la fusión** de varias cadenas "igual" con la cadena de su OCC (pendiente de S-1).

Pruebas: servicio del Núcleo registra y ubica; "Buscar…" encuentra por carpeta y por selector (STA con selector simulado); fusión de 2 cadenas.

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores en tus archivos. Español neutro. Reporte al final; si algo queda pendiente, dilo explícitamente.

## Reporte del agente

- **Implementado:** el núcleo comparte `RegistroDocumentoEnCadena`; registra el tipo, el número del nombre (o el número indicado) y el proveedor del esquema. El servicio de búsqueda de Archivero lo usa cuando no hay clasificación; Buscadero lo usa para incorporar y ubicar el archivo en la cadena seleccionada. Las colisiones de cardinalidad uno a uno quedan dudosas.
- **Implementado:** los eslabones vacíos muestran **Buscar…**. Buscadero busca primero en la carpeta configurada, recorre subcarpetas por período descendente, filtra por el número esperado antes de aceptar y, si no hay coincidencia o se rechaza, abre el selector con esa carpeta inicial. La selección se simula mediante delegado en las pruebas.
- **Pruebas agregadas:** registro y ubicación en cadena, búsqueda por carpeta, selector simulado y fusión de dos cadenas enlazadas al procesar una OCC.
- **Verificación:** `dotnet build` correcto, 0 advertencias y 0 errores; `dotnet test` correcto, 856 pruebas; `dotnet csharpier check .` correcto (351 archivos).
- **Pendiente:** ninguno dentro del alcance del bloque.
