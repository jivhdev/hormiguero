---
bloque: S-2b
app: Archivero — pulir el modo cadena del asistente (prueba en vivo de Claude) + pruebas STA pendientes
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Archivero/**, tests/Hormiguero.Archivero.App.Tests/**]
archivos_prohibidos: [todo lo demás]
rama: archivero/s2b-pulido
---

# S-2b — Que el modo cadena se lea como obligatorio (y las STA que faltaron en S-2)

Prueba en vivo de Claude (OCC sintética, "Sí, de todas las cadenas"): la pregunta aparece bien en el paso 1, el paso 4 muestra solo los 8 datos y "Siguiente" queda bloqueado. Falta:

1. **Paso 4 con cadena**: título **"Paso 4 de 6 — Datos de la cadena (obligatorios)"**, ayuda "Marca la zona de cada dato. No puedes seguir hasta marcarlos todos." y línea viva **"Faltan: N° Nota de venta propia, Fecha del documento…"** (o "Todo marcado ✓"). Ocultar "Filtrar por categoría" y el expansor "Otros datos que aparecen en este documento" (mostrar la lista directo). Cada dato requerido aparece **marcado y bloqueado** (no se puede desmarcar; hoy "N° Nota de venta propia" aparecía sin marcar); "Se puede enlazar" no editable. Sin cadena: todo como antes.
2. **Paso 3 con cadena**: ocultar "Este documento no trae número" (el número es obligatorio).
3. **Resumen (paso 6)**: línea "Cadena: todas las que usan OC propia" o "Cadena: Hoffens".
4. **Pruebas STA** que S-2 dejó pendientes (botones reales en ventana real con la precarga JCV): OCC → pregunta visible; "todas" → solo 8 datos, título obligatorio, Siguiente deshabilitado hasta marcar los 8, "no trae número" oculto; GRC + "solo Hoffens" → 4 datos; "No" → diccionario completo; tipo sin esquema → sin pregunta; "Completar" en Administrar clasificaciones abre el paso 4.

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores en tus archivos. Español neutro. Reporte al final; si algo queda pendiente, dilo explícitamente.

## Reporte del agente

Implementé el modo cadena como obligatorio en el asistente:

- El paso 4 muestra el título y la ayuda de datos obligatorios, presenta la lista directamente y oculta el filtro y el encabezado expansible. Los datos requeridos quedan incluidos y sus controles de inclusión y enlace bloqueados. El estado indica los faltantes o «Todo marcado ✓» y controla el botón «Siguiente».
- En el paso 3 se oculta «Este documento no trae número» y se bloquea «Se puede enlazar» para el número obligatorio.
- El resumen muestra el alcance de la cadena.
- Agregué cobertura STA con la precarga JCV para OCC, GRC de Hoffens, el diccionario sin cadena y un tipo sin esquema.

Verificación:

- `dotnet build`: correcto, 0 advertencias y 0 errores.
- `dotnet test`: correcto, 859 pruebas en total.
- `dotnet csharpier check .`: correcto, 354 archivos revisados.

Pendiente: no automatizé el clic en «Completar» de Administrar clasificaciones, porque abre un diálogo nativo para elegir el PDF. El flujo existente pasa `comenzarEnDatos: true` al asistente; no quedó cubierto por la prueba STA.
