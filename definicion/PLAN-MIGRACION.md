---
tipo: plan
fecha: 2026-10-04
estado: aprobado (D-65)
---

# Plan de migración: partir de lo que ya funciona

Javier, 2026-10-04: "al final todo quedara perfecto tal como ya funciona hoy en dia... al menos partir desde ahi". Las versiones antiguas de Archivero (166 pruebas, Casos 1-10) y Buscadero (85 pruebas, Casos 1-15) usan el mismo lenguaje que Hormiguero (.NET y WPF, PDFtoImage, SQLite). Reconstruirlas desde cero dejó la apertura por debajo de lo que Javier ya usa. Desde ahora, cada app **parte de su versión antigua** y se mejora sin perder nada.

## Fases por app

| Fase | Qué se hace | Cuándo termina |
|---|---|---|
| **A. Traer** | Se copia el código y las pruebas de la versión antigua al monorepo (`src/Hormiguero.<App>`), se actualiza a .NET 10 y a las reglas del repositorio, **sin cambiar su comportamiento**. Sigue usando su propia base de datos, así las configuraciones que Javier ya tiene siguen sirviendo. | Todas sus pruebas antiguas pasan y Claude recorre la lista de verificación usando la app. Javier confirma que funciona igual que hoy. |
| **B. Mejorar por dentro** | Una pieza a la vez, cada una con sus pruebas: tema y diseño de Hormiguero, mover sin perder (núcleo), vigilancia nueva, base común, índice rápido (Buscadero). Las pruebas antiguas siguen pasando en cada paso. | Cada pieza queda integrada sin que nada funcione peor (lista de equivalencia). |
| **C. Crecer** | Se retoman los pasos del plan (medio y final) desde una base que ya funciona. | Lo de siempre: un mes de uso real sin cambios (D-16/17). |

## Orden

1. Archivero A → B.
2. Buscadero A → B (lo primero de B es su punto débil: la lentitud, con el índice nuevo).
3. Mensajero: mismo método, partiendo de ClickFactura, Ofisuiza, Motores y Facturas JCV.

## Qué pasa con lo construido en la apertura nueva

- **Núcleo** (base común, PDF, mover sin perder, huella, zonas) se queda: es la base de la fase B.
- **Lógica nueva que ya es mejor** (índice silencioso de Buscadero, vigilante y archivador de Archivero, preferencia de impresión) se usa en la fase B para reemplazar la pieza antigua equivalente, con sus pruebas.
- **Pantallas nuevas** que queden por debajo de las antiguas se retiran.

## Lista de equivalencia

`semillas/<App>/EQUIVALENCIA.md`: una fila por cosa que la versión antigua hace (sacada de sus Casos y su código, más lo que Javier agregue), con su estado en Hormiguero. Ninguna app se da por cerrada si alguna fila funciona peor que en la versión antigua (D-64).

## Antes de cada hito

Claude prueba cada flujo usando la app como Javier (incluido el mouse dentro de su ventana, con datos sintéticos) siguiendo la lista de verificación, y recién entonces avisa (D-64).
