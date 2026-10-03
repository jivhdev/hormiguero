---
tipo: estado
---

# ESTADO

## Último traspaso

- **Fecha y hora**: 2026-10-03
- **Agente y modelo**: Claude Code (revisión) + OpenCode glm-5.3-flash (B-002 a B-006)
- **Bloque y estado**: B-002 a B-006 aprobados e integrados — esqueleto del monorepo terminado (paso 4)
- **Qué se hizo**: configuración común de .NET 10, solución `Hormiguero.slnx`, proyecto vacío `Hormiguero.Nucleo`, proyecto de pruebas (1 prueba), herramientas locales (CSharpier, nuget-license), licencias permitidas, `.editorconfig`, CI en GitHub Actions, hook de gitleaks, tablero de Obsidian.
- **Pruebas**: `dotnet test` 1/1. CSharpier y licencias sin observaciones.
- **Commit**: ver `git log` (rama `main`).
- **Pendiente**: activar el hook en cada equipo nuevo con `git config core.hooksPath .githooks`.
- **Decisión requerida de Javier**: ninguna.
- **Siguiente paso**: paso 6 — especificar con MQD el nivel 0 del núcleo y la apertura de Buscadero, empezando por la Idea de Buscadero (`semillas/Buscadero/IDEA.md`).

## Traspasos anteriores
