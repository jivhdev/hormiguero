---
tipo: estado
---

# ESTADO

## Último traspaso

- **Fecha y hora**: 2026-10-03, 23:30
- **Agente y modelo**: Claude Code (Opus 5.5, revisión e integración) + OpenCode Zen gratis (nemotron-3-ultra-free, D-61)
- **Bloque y estado**: B-007 a B-028 aprobados e integrados. **Apertura de Buscadero (paso 1) lista para el hito de Javier.**
- **Qué se hizo**: núcleo (base común, PDF: leer, dibujar, imprimir; huella; números en el nombre); Buscadero: carpetas configuradas, indexador incremental, control del índice (≤ 5 carpetas/s, pausa al buscar), índice en segundo plano (retoma carpetas caídas sin perder su índice), búsqueda por número, agrupador (copias, original/cedible/escaneado), P1 primera configuración, P2 buscador + visor (zoom, giro, imprimir 1ª / 2 primeras, ver en carpeta), P3 Configuración (carpetas e impresión directa, fija o cuadro de Windows).
- **Pruebas**: `dotnet test` → Núcleo 40/40, Diseño 8/8, Buscadero 44/44. CSharpier sin observaciones. Prueba en vivo con 46 PDF sintéticos (automatización de Windows, sin datos reales).
- **Medidas (equipo de desarrollo, datos sintéticos)**: RNF-1 búsqueda con 20.000 documentos 4–246 ms (≤ 1 s) · RNF-3 primera página 0,3 s (≤ 1,5 s) · RNF-4 arranque 2,1–2,4 s (≤ 3 s; la primera vez después de compilar, 4,6 s) · RNF-5 memoria 122–142 MB (≤ 300 MB). RNF-2 por diseño y prueba automática (5 carpetas/s). Falta medirlas en el AMD A12 de la oficina.
- **Commit**: ver `git log` (rama `main`).
- **Pendientes conocidos**:
  - Al cancelar una búsqueda no quedan los resultados parciales (REQ-003).
  - Botón desactivado muy claro en modo oscuro; visor con doble marco.
  - La primera vez que se arma el índice se muestra "N documentos revisados" y no el porcentaje (para calcularlo habría que recorrer antes todas las carpetas y cargar la red); desde la segunda vez, el porcentaje.
  - Un PDF con 20 letras o menos por página se etiqueta "escaneado" (umbral de N-003).
- **Decisión requerida de Javier**: probar la apertura (hito) y revisar ADR-002 de Buscadero (tomada por delegación).
- **Siguiente paso**: según el resultado del hito, corregir y pasar al paso 2 (apertura de Archivero).

### Cómo probar (hito)

1. Abrir una terminal en `C:\JV\hormiguero` y ejecutar `dotnet run --project src\Hormiguero.Buscadero -c Release`.
2. La primera vez aparece "¿Dónde están tus documentos?": agregar `C:\JV\pruebas` (u otra carpeta) y tocar "Listo, empezar".
3. Esperar "Índice al día · N documentos" arriba a la derecha y buscar números reales (con o sin letras: "OCC104523" o "104523").
4. Revisar: copias exactas una sola vez ("En N carpetas"), original y cedible juntos, escaneados encontrados por nombre, números que solo están en el texto.
5. Probar "Imprimir 1ª página", "Imprimir 2 primeras" y "Ver en carpeta"; en "Configuración", cambiar la forma de imprimir.
6. Atajos: Alt+A campo de búsqueda · Alt+S buscar · Alt+D buscar en todas · Alt+F imprimir 1ª · Alt+G imprimir 2 primeras · Escape cancelar.

## Traspasos anteriores

- 2026-10-03 — B-002 a B-006: esqueleto del monorepo terminado (paso 4).
