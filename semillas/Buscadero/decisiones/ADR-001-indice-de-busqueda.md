---
tipo: adr
app: Buscadero
numero: 1
fecha: 2026-10-03
estado: aprobada # propuesta | aprobada | reemplazada
---

# ADR-001 (Buscadero): Índice de búsqueda y "vecino silencioso"

## Contexto

En la v1 una búsqueda en carpetas de red tardaba minutos. La apertura debe buscar por número (C1 a C9) en carpetas locales, de red y de Drive sin sobrecargar la red (MQD, sección 9; D-52). Los datos viven en la base común (ADR-001 del ecosistema).

## Opciones consideradas

| Opción | A favor | En contra |
|---|---|---|
| A. Recorrer las carpetas en cada búsqueda | Simple | Lento en red (el problema de la v1) |
| **B. Índice local en la base común, que se adelanta en segundo plano con un límite de velocidad** | Búsqueda casi instantánea; la red se toca poco y de a poco | Más código; el índice puede estar algo atrasado |
| C. Índice de Windows (Windows Search) | Ya existe | No indexa bien carpetas de red ni Drive; no se controla |

## Decisión

**Opción B** (aprobada por Javier, D-59).

- Por cada archivo se guarda: ruta, nombre, números del nombre (con prefijo y sufijo separados, para C5 y C7), tamaño, fecha de modificación, hash (C1), si tiene texto (C2) y los números de su texto (C6).
- El índice se arma la primera vez y después se actualiza solo con lo nuevo o modificado (por fecha de modificación); nunca se relee lo que no cambió.
- En segundo plano avanza con un límite (por ejemplo, unas pocas carpetas por segundo) y se pausa mientras el usuario busca.
- Si la búsqueda no encuentra algo en el índice, revisa en vivo solo la carpeta más probable y suma lo que encuentre.
- "Cancelar" corta la búsqueda en curso.

## Consecuencias

- La primera indexación de una carpeta grande de red tarda (se avisa con "Índice: armándose, 40 %").
- Buscadero muestra el estado del índice ("Índice al día").
