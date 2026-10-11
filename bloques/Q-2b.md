---
bloque: Q-2b
app: Núcleo + Buscadero + Archivero (terminar Q-2)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Nucleo/Datos/**, src/Hormiguero.Buscadero/**, src/Hormiguero.Buscadero.Core/**, src/Hormiguero.Archivero/MainWindow.xaml, src/Hormiguero.Archivero/MainWindow.xaml.cs, tests/**]
archivos_prohibidos: [migraciones 1-18 intactas (si hace falta, nueva 19); el resto de Archivero]
rama: nucleo/q2-enlaces-esquema
---

# Q-2b — Terminar Q-2: modos, avisos, decisiones y dudosos sobre las tablas nuevas

Lee `bloques/Q-2.md` (pedido, decisión de tablas paralelas y tu reporte: quedó pendiente todo lo de avisos). La migración de Q-2 ahora es la **18** (la 17 es de Q-1). Objetivo: que **todo** el flujo de cadenas use solo el modelo nuevo (`esquemas_diccionario`, `lugares_esquema_diccionario`, `enlaces_esquema_diccionario`, `cadenas_diccionario`, `ubicaciones_cadena_diccionario`, `modos_/reglas_alerta_/alertas_esquema_diccionario`, decisiones) y que el modelo anterior (C3: `esquemas_cadena`, `lugares_esquema`, `parejas_esquema`…) **no se use más** ni aparezca en la interfaz.

1. **Asistente del esquema nuevo** (`DialogoEsquemaDiccionario`): pasos completos y en orden: Proveedor → Documentos (tipos del diccionario) → Inicios → **Enlaces** → **Modos** (opcional; qué documento decide el modo) → **Avisos** (el mismo editor guiado de 3 tipos de C3-d2/C3-d2b, adaptado a lugares/enlaces nuevos; "por línea" disponible si hay un enlace uno a uno o uno a varios entre los lugares) → Resumen. Quita del menú y del código el asistente anterior de C3 (lista "Esquemas…" abre solo el nuevo).
2. **Núcleo**: `EvaluadorAlertasEsquema` (o uno nuevo) evalúa reglas sobre cadenas/ubicaciones nuevas (falta dato, plazo con fecha de emisión o dato ingresado y por línea/padre, listo para…), con urgencias, idempotente, cierre al llegar el destino; modo de la cadena con historial; datos de la cadena; decisiones "sin piso" y "modo" sobre el modelo nuevo; dudosos (solo limpio, varias cadenas) aceptar/rechazar.
3. **Buscadero**: vista "Avisos" y "Listos para…", marca "⚠ n" en cadenas, detalle de la cadena con modo y datos, y dudosos: todo leyendo el modelo nuevo. Vista de cadenas: árbol padre → hijos por línea (OC → [Guía ↔ Factura]).
4. **Archivero**: "Decisiones pendientes" (sin piso y modo) leyendo/escribiendo el modelo nuevo.
5. Pruebas: reescribe las pruebas de alertas/modos/decisiones/punta a punta para el modelo nuevo (las del modelo C3 que ya no aplica pueden eliminarse si el código que probaban se retira); STA que abre el asistente completo (7 pasos) y la vista de avisos.

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores en tus archivos. Si algo no está definido, elige lo más simple, anótalo y sigue. Reporte al final con lo retirado y la lista de pantallas.

## Reporte del agente
