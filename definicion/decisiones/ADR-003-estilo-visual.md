---
tipo: adr
app: Hormiguero (diseño)
numero: 3
fecha: 2026-10-03
estado: propuesta # propuesta | aprobada | reemplazada
---

# ADR-003: Cómo se construye el estilo visual

## Contexto

D-35 pide un estilo minimalista propio, modo claro u oscuro según Windows (con opción de cambiarlo), pantallas amplias y simples. D-36 fija los colores: grafito azul en claro, verde hoja en oscuro.

Desde .NET 9, WPF trae el tema Fluent de Windows 11 incluido, con `ThemeMode` que sigue el modo claro u oscuro del sistema y deja cambiarlo en la app. Microsoft advierte que su soporte "todavía está en progreso".

## Opciones consideradas

| Opción | A favor | En contra |
|---|---|---|
| **A. Tema Fluent incluido en WPF + diccionario propio de Hormiguero** (colores, tamaños, tipografía) encima | Sin dependencias extra; claro/oscuro automático; cada pantalla se ve nativa de Windows | Fluent en WPF aún madura: algún control puede requerir estilo propio |
| B. Librería WPF-UI (MIT) | Muchos controles listos | Dependencia más; su estética es la de Windows 11, menos "propia" |
| C. Todos los estilos escritos a mano | Control total | Mucho trabajo y más errores; poco sentido para pantallas simples |

## Decisión

**Propuesta: opción A.** Un proyecto `Hormiguero.Diseno` con el diccionario de recursos común (colores por modo según D-36, tamaños grandes, espaciados, tipografía) que todas las apps usan.

## Consecuencias

- Cambiar un color o un tamaño se hace en un solo lugar y afecta a toda la familia.
- Si un control de Fluent falla, se le hace un estilo propio en `Hormiguero.Diseno`, nunca en cada app.
