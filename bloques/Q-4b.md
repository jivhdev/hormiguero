---
bloque: Q-4b
app: Buscadero (pulido del editor de avisos del esquema + prueba de aceptación)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Buscadero/**, src/Hormiguero.Buscadero.Core/**, tests/Hormiguero.Buscadero.Core.Tests/**]
archivos_prohibidos: [todo lo demás]
rama: buscadero/q4-avisos-esquema
---

# Q-4b — Pulir el editor de avisos (prueba en vivo de Claude tras Q-4)

Q-4 funciona en vivo: modos en lista, tres tarjetas, campos por tipo, "Por línea" se habilita con el enlace Guía ↔ Factura, guardar y resumen. Claude ya corrigió en el Núcleo el borrado de modos quitados y un error al guardar avisos con "Solo si existe" vacío (no toques el Núcleo). Falta:

1. **Tarjeta elegida resaltada**: hoy las tres se ven iguales; la elegida con borde/fondo de acento (tema claro y oscuro) y `AutomationProperties.ItemStatus`/nombre que indique "elegida".
2. **Textos de la frase**: "7 días hábiles" (minúscula y con tilde; corridos → "corridos"). En "Listo para…": "Listo para facturar cuando estén Guía del proveedor + Factura del proveedor" (no "Listo para «Listos para facturar»").
3. **Texto sugerido que sigue a los campos** mientras el usuario no lo haya escrito él: falta dato → "Ingresar {nombre del dato en minúscula}" (cambia al cambiar el dato); plazo → "Falta {Hasta que llegue}"; listo para → "Listos para facturar". Si el usuario editó el texto, no se pisa.
4. **Prueba STA de aceptación** que quedó pendiente en Q-4 (ver `bloques/Q-4.md`, sección Aceptación): arma el caso completo, guarda, reabre con Editar y compara documentos, enlaces, modos, documento que decide y los tres avisos; y verifica que "Guardar aviso" y los botones del asistente quedan dentro de 1366×768.

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores en tus archivos. Español neutro. Reporte al final.

## Reporte del agente

Pulí el editor: la tarjeta activa usa borde y fondo de acento, anuncia «elegida» y distingue las tres categorías con tema claro y oscuro. Corregí «hábiles»/«corridos» y la frase de «Listo para facturar». Los textos sugeridos cambian con el dato o documento mientras no se hayan editado. El caso «fecha de retiro» se ofrece como dato sintético del aviso, ya que el catálogo del núcleo no lo contiene y el formato del aviso permite guardar nombres libres; no se modificó el núcleo. Corregí también la recarga de avisos con campos opcionales nulos.

Añadí la aceptación STA al recorrido de ventanas existente: arma el esquema PROVEEDOR DEMO SPA, guarda y vuelve a abrir mediante Editar para comparar documentos, enlaces, modos, documento que decide y avisos. Comprueba los límites de 1366×768 y la información accesible de las tarjetas en ambos temas.

Verificación: dotnet build correcto, 0 advertencias; dotnet test correcto (811 pruebas); dotnet csharpier check . correcto (339 archivos).
