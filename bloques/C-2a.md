---
bloque: C-2a
app: Archivero y Buscadero (cadenas)
fase: C (D-73, D-75)
estado: hecho (propuesta, espera a Javier)
agente: Codex
modelo: codex
archivos_permitidos: [definicion/DISENO-C2-CADENAS-ASISTIDAS.md]
archivos_prohibidos: [todo lo demás: este bloque NO escribe código]
rama: docs/c2-rediseno-cadenas
---

# C-2a — Propuesta de rediseño: cadenas asistidas desde Archivero (para conversar con Javier)

Lee D-48, D-69 a D-76 en `definicion/DECISIONES.md` (sobre todo **D-73 y D-75**), `definicion/DISENO-B6-MARCAS-Y-CADENAS.md` (lo actual), `definicion/DISENO-C1-ALERTAS-Y-VENCIMIENTOS.md`, `semillas/Buscadero/*`, `semillas/Archivero/*` y el código actual de Archivero (configuraciones, datos propios B-6g2) y Buscadero (modelos/cadenas, regla de vagón, dudosos).

## El problema (palabras de Javier, uso real)

- "Hay muchísimas opciones... necesito que para el usuario sea más fácil detectar, para todos los tipos de archivos identificados, cuál o cuáles son los datos que podrían ser citados en otros documentos. Agregar ese paso a Archivero: un paso que pida al usuario los datos enlazantes, estandarizados."
- "Me cuesta mucho entender cómo crear la cadena... y yo sé cómo funciona la cadena. Es ilógico: es mi idea y al enfrentarme al programa ya no supe cómo configurar."
- "Todo debería empezar en Archivero, incluso la creación de cadenas. Estandarizar la forma de llamar a las cosas (hoy tengo 'occ xcliente', 'occ xcliente'...). En vez de una lista interminable, agrupar: Emitidos y Recibidos."
- Decidido (D-75): diccionario de datos enlazantes estándar y editable; en Archivero cada tipo de documento indica qué datos trae; **la cadena la crea el usuario, asistida** (Hormiguero sugiere qué documentos calzan), no se arma sola; Emitidos/Recibidos.

## Qué entregar: `definicion/DISENO-C2-CADENAS-ASISTIDAS.md` (español neutro, para Javier, sin jerga)

1. **El recorrido del usuario, paso a paso, como si fuera un tutorial**: primera vez (configurar el diccionario con una base precargada de ejemplo editable: N° OC del cliente, OC propia, NVV, N° guía, N° factura, N° nota de crédito…), al configurar un tipo de documento en Archivero (Emitido o Recibido, nombre estándar sugerido "Tipo · Emisor/Cliente", qué datos del diccionario trae y dónde están en el PDF), al crear una cadena (cómo Hormiguero asiste: "estos 3 documentos comparten la OC 4500012345, ¿agregarlos?"), y en el día a día (documento nuevo que llega → sugerencia de a qué cadena va). Incluye **bocetos de pantalla en texto** (ASCII) para cada paso.
2. **Qué se mantiene, qué cambia y qué se descarta** del diseño actual (modelos/vagones, regla de vagón, dudosos, datos propios), justificando con D-73 (se puede partir de cero) y D-75.
3. **Todo editable** (D-73): cómo se edita/elimina/reordena cada cosa configurada (diccionario, datos de un tipo, cadenas, modelos).
4. **Cómo encajan las alertas** (C-1) en el nuevo flujo.
5. **Preguntas para Javier**: las justas para cerrar el diseño (máximo 8), cada una con una recomendación y un ejemplo concreto de su trabajo (OCC, NVV, guías Cobelcar/Hoffens, facturas FCV/NCV).
6. **Plan de bloques** chicos para implementar después de que Javier apruebe (marca triviales para OpenCode Go).

Reporte al final de esta nota.

## Reporte del agente
