---
bloque: Q-2c
app: Buscadero (asistente nuevo del esquema: correcciones tras prueba en vivo)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Buscadero/**, src/Hormiguero.Buscadero.Core/**, tests/Hormiguero.Buscadero.Core.Tests/**]
archivos_prohibidos: [todo lo demás]
rama: nucleo/q2-enlaces-esquema
---

# Q-2c — Corregir el asistente "Esquema de documentos" (prueba en vivo de Claude, pantalla 1366×768)

Prueba en vivo de `DialogoEsquemaDiccionario`:

1. **La ventana se sale de la pantalla**: mide 900×700 y se abre desplazada (y=156), así que los botones Anterior/Siguiente/Cancelar quedan **fuera de la pantalla** (abajo). Debe abrirse **centrada en la pantalla de trabajo**, alto máximo = alto útil de la pantalla − 40, contenido de cada paso con scroll y **pie fijo** con los botones siempre visibles (mismo patrón que el editor de avisos de C3-d2b).
2. **Paso 1**: el título dice "Documentos del proceso" (debe decir **"Proveedor"**). "Nombre" del esquema es obligatorio y bloquea: debe **llenarse solo** con "Proceso con {proveedor}" (editable). El mensaje de error "Escriba el proveedor y el nombre…" queda pegado en los pasos siguientes: limpiarlo al avanzar. Proveedor con autocompletar de emisores conocidos.
3. **Paso 2 "Documentos del proceso"**: al pulsar "Agregar tipo →" **no se ve ninguna lista de elegidos** (la columna derecha está vacía). Rehacer con el diseño que ya funcionó en C3-b2: **dos listas lado a lado**: "Documentos disponibles" | "Documentos del proceso, en orden", con "Agregar →", "← Quitar", doble clic, y **Subir/Bajar bajo la lista derecha**.
   - La lista disponible muestra **nombres de documento** ("Factura del proveedor", no "N° Factura del proveedor"), agrupados en el orden natural de categorías: **Ventas propias, Compras propias, Del cliente, Del proveedor, Otros**.
   - **Excluir** RUT y nombre/razón social (no son documentos; `DiccionarioDatosEnlazantes.EsParte`).
4. **Revisar los pasos 3 a 7** con el mismo criterio (título correcto por paso, explicación de una línea, efecto visible de cada botón, listas con texto amigable, nada fuera de pantalla). En "Enlaces": "Documento A", "Documento B" (solo los del paso 2), "Por qué dato" (todos los datos del diccionario agrupados por categoría, sugeridos primero con ✓), "Uno a uno / Uno a varios", y la frase de cada enlace en la lista; sugerencias precargadas que se pueden quitar.
5. **Prueba STA** que abre el asistente en 1366×768 y verifica: los botones del pie están dentro de la pantalla; el paso 2 muestra ambas listas y "Agregar" mueve un tipo a la derecha; la lista no contiene "RUT"/"razón social"; los nombres no empiezan con "N°".

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores en tus archivos. Reporte al final con lo corregido en cada paso.

## Reporte del agente

- Paso 1: titulé la pantalla «Proveedor», agregué autocompletado con emisores de identificaciones y proveedores de esquemas guardados, y propuse «Proceso con {proveedor}» como nombre editable. El estado de validación se limpia al avanzar.
- Paso 2: puse las listas de documentos disponibles y documentos del proceso en paralelo. Agregar, quitar, doble clic, Subir y Bajar actualizan la selección visible. Ordené las categorías según lo pedido, quité el prefijo «N°» de los nombres y filtré los datos que `DiccionarioDatosEnlazantes.EsParte` identifica como partes.
- Paso 3: titulé «Inicios de cadena» y expliqué qué marcar; las filas muestran categoría y nombre amigable, con acción para quitar.
- Paso 4: titulé «Enlaces»; etiqueté Documento A, Documento B y Por qué dato; mantuve solo documentos seleccionados, ordené los datos por sugerencia y categoría, mostré la marca ✓ y conservé la frase de cada enlace y la acción para quitarlo.
- Paso 5: titulé «Modos» y aclaré cómo se usan los modos opcionales y el documento que los determina.
- Paso 6: titulé «Avisos», aclaré su propósito y mantuve visibles la configuración, la acción para agregar y la lista de avisos dentro del contenido desplazable.
- Paso 7: titulé «Resumen» y aclaré que permite revisar antes de guardar.
- Ventana: centré el diálogo, limité su alto al área útil menos 40 px, dejé el contenido de los pasos con scroll y fijé el pie con Anterior, Siguiente, Guardar y Cancelar.
- Prueba STA: comprueba los límites del pie, la propuesta automática del nombre, ambas listas, la exclusión de RUT/razón social y prefijos «N°», y que Agregar mueve un tipo a la lista del proceso.
- Verificación: `dotnet build` correcto, 0 advertencias y 0 errores; `dotnet test` correcto, 807 pruebas aprobadas, 0 fallidas y 0 omitidas; `dotnet csharpier check .` correcto, 338 archivos revisados.
