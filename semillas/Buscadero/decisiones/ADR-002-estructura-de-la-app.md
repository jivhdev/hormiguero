---
tipo: adr
app: Buscadero
numero: 2
fecha: 2026-10-03
estado: aprobada # propuesta | aprobada | reemplazada
---

# ADR-002 (Buscadero): Cómo se organiza la app por dentro

## Contexto

Buscadero necesita lógica que se pueda probar sin abrir ventanas (índice, búsqueda, agrupar resultados) y pantallas WPF simples. AGENTS.md pide el mínimo código y ninguna dependencia evitable. ADR-001 del ecosistema exige que solo el núcleo toque la base común.

## Opciones consideradas

| Opción | A favor | En contra |
|---|---|---|
| **A. Biblioteca de lógica + app WPF delgada (código detrás de cada ventana, sin framework)** | Cero dependencias; la lógica se prueba sola; ventanas simples | Algo de código repetido para avisar cambios a la pantalla |
| B. MVVM con CommunityToolkit.Mvvm (MIT) | Menos código repetido; patrón conocido | Una dependencia y una capa más para pantallas que son simples |
| C. Todo dentro de las ventanas | Lo más rápido de escribir | La lógica no se puede probar sin abrir ventanas |

## Decisión

**Opción A.** Proyectos:
- `Hormiguero.Buscadero.Logica` (biblioteca, sin WPF): carpetas configuradas, indexador, búsqueda, agrupación de resultados. Usa el núcleo para leer PDF y para guardar en la base común.
- `Hormiguero.Buscadero` (app WPF): ventanas P1, P2, P3; solo muestran y llaman a la lógica.
- `Hormiguero.Buscadero.Tests`: pruebas de la lógica.

Las tablas comunes de documentos e índice las crea y administra el núcleo (migraciones del núcleo); Buscadero solo tiene su propia tabla de carpetas configuradas.

Tomada por Claude el 2026-10-03 por delegación ("dale sigue así, avísame cuando pueda probar"); Javier la revisa en el hito de la apertura.

## Consecuencias

- Si más adelante las pantallas crecen (paso 4), se puede reevaluar MVVM con un ADR nuevo.
