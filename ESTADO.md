---
tipo: estado
---

# ESTADO

## Último traspaso

- **Fecha y hora**: 2026-10-04, 12:50
- **Agente y modelo**: Claude Code (Opus 5.5) + Codex y OpenCode Go para bloques puntuales
- **Etapa**: migración (D-65, `definicion/PLAN-MIGRACION.md`). Fase A aprobada (D-66): Archivero 0.1 B y Buscadero caso 15 viven en Hormiguero. Fase B en curso.
- **Hecho en la fase B**:
  - B-1 Diseño común: hoja `Hormiguero.Diseno/Temas/Controles.xaml` (de los estilos de Buscadero), tema claro/oscuro según Windows en las dos apps, barra de título oscura.
  - B-2a Búsqueda rápida: Buscadero busca primero en el índice y lo mantiene al día en segundo plano (22,6 s → ~170 ms con 20.000 PDF).
  - B-3 Carpeta común: `%LocalAppData%\Hormiguero\` con `hormiguero.db`, `archivero.db`, `buscadero.db` y respaldos diarios; la primera vez cada app copia sola sus datos anteriores (quedan intactos).
- **Pruebas**: núcleo 62, diseño 9, Archivero 277 (antiguas) + 42 (lógica nueva), Buscadero 116 (111 antiguas + 5) + 44 (lógica nueva). CI en verde.
- **Para probar**: accesos directos del escritorio → `E:\Probar\Archivero` y `E:\Probar\Buscadero`.
- **Variables de prueba** (datos sintéticos, nunca los reales): `HORMIGUERO_DATOS` (carpeta de datos), `HORMIGUERO_TEMA` (claro/oscuro), `ARCHIVERO_DATOS` (solo Archivero).
- **Pendientes conocidos**: ventanas secundarias de ambas apps sin revisar a fondo en modo oscuro; Archivero no abre maximizado (igual que la 0.1 B, revisar contra el caso 7); `Caso-Sin-Cadenas.md` de la copia vieja de Buscadero por revisar.
- **Siguiente paso**: B-4, Archivero registra cada documento guardado en la base común y Buscadero lo encuentra al instante.

## Traspasos anteriores

- 2026-10-04 — Apertura nueva de Archivero (B-029 a B-042) reemplazada por la migración del código antiguo (D-64, D-65).
- 2026-10-03 — Apertura nueva de Buscadero (B-007 a B-028); reemplazada por la migración (D-65).
- 2026-10-03 — B-002 a B-006: esqueleto del monorepo terminado (paso 4).
