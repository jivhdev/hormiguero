---
bloque: C3-d2b
app: Buscadero (correcciones tras prueba en vivo de C3-b/C3-d2)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Buscadero/**, src/Hormiguero.Buscadero.Core/**, tests/Hormiguero.Buscadero.Core.Tests/**]
archivos_prohibidos: [todo lo demás]
rama: buscadero/c3d2-avisos
---

# C3-d2b — Corregir el editor de avisos y pulir el asistente (prueba en vivo de Claude)

Sigue en esta rama. Prueba en vivo con datos sintéticos:

**Editor "Configurar aviso" (lo más importante):**
1. Muestra **todos los campos de los tres tipos a la vez** (falta dato + plazo + listo para), aunque se elija uno: confunde. Debe mostrar **solo los campos del tipo elegido**. Mejor aún, el tipo arriba como **tres botones/tarjetas** con una línea de explicación cada uno ("Falta ingresar un dato — ej. fecha de retiro", "Plazo entre documentos — ej. 7 días sin guía firmada", "Listo para… — ej. listos para facturar"), y debajo solo sus campos, en orden de frase.
2. "Este aviso aplica al modo" aparece **vacío**: debe partir en **"Todos los modos"** (y listar los modos del esquema); si el esquema no tiene modos, ocultar el campo.
3. El texto por defecto no corresponde al tipo ("Falta el documento esperado" en un aviso de falta dato): poner un texto sugerido por tipo ("Ingresar fecha de retiro", "Falta {lugar esperado}", nada para "Listo para") que el usuario puede cambiar.
4. **"Guardar aviso" y "Cancelar" quedan fuera de la pantalla** (la ventana pasa de 720 px y hay una franja blanca abajo): botones fijos abajo, contenido con scroll, alto máximo 700 px.
5. La frase de revisión debe reflejar los campos del tipo elegido y actualizarse al instante.
6. "Lugar al que pertenece el aviso" + "Plazo: desde el lugar o dato" + "O desde un dato ingresado" es redundante para Plazo: usar un solo "Contar desde: [lugar (fecha del documento)] o [dato ingresado ▾]".

**Asistente del esquema (pulido):**
7. Paso 4 (parejas): los tres combos no tienen etiqueta → "Primer documento", "Segundo documento", "Dato que comparten"; el tercer combo debe listar **solo los datos que ambos comparten**; el aviso "Pareja agregada a la lista." sale en **rojo**: usar color normal/éxito (rojo solo para errores, en todo el asistente).
8. Paso 2: los botones Subir/Bajar están bajo la lista izquierda: moverlos bajo la lista **derecha** (la que ordenan).
9. Pistas del paso 2: aparece "Factura del proveedor **del proveedor**" (sufijo duplicado): no agregar "del proveedor" si el tipo ya lo dice.

Pruebas: lógica de qué campos corresponden a cada tipo y del texto sugerido; datos comunes para pareja; STA que abre el editor con cada tipo y verifica que los campos de otros tipos están ocultos y que "Guardar aviso" es visible en 1366×768. `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores en tus archivos. Reporte al final, debajo del de C3-d2.

## Reporte del agente
