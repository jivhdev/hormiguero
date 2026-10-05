---
bloque: AUD-1
app: todas
fase: verificación (pedido de Javier, 2026-10-04)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [definicion/AUDITORIA-FUNCIONES.md]
archivos_prohibidos: [todo lo demás: este bloque NO escribe código]
rama: docs/aud1-auditoria
---

# AUD-1 — Auditoría: ¿alguna función de las apps viejas quedó afuera?

Javier pidió revisar de nuevo las aplicaciones viejas contra las nuevas para asegurar que **ninguna función** haya quedado afuera. Desde mañana las usa en su trabajo.

Viejas (SOLO LECTURA; no ejecutes nada que escriba en ellas; no abras PDFs, planillas ni bases con datos reales, solo código y configuración):
- Archivero: `C:\Users\jihja\Desktop\Entorno Antiguo\AP01-Archivero Versiones Antiguas\` (la base de la nueva fue la versión 0.1 B; ver `definicion/PLAN-MIGRACION.md` y `definicion/DECISIONES.md` D-65/D-66).
- Buscadero: `...\AP02-Buscadero Versiones Antiguas\` (base: el "caso 15").
- Ofisuiza: `...\AP07-Ofisuiza Versiones Antiguas\OFISUIZA .EXE FUNCIONAL VERSION EN USO\`.
- ClickFactura: `...\AP03-ClickFactura\` (raíz).
Elige siempre la versión más reciente por fecha que corresponda a la que se usa; anota cuál.

Nuevas: `src/Hormiguero.Archivero`, `src/Hormiguero.Buscadero` (+ `.Core`), `src/Hormiguero.Mensajero` (+ `.Core`). Usa también `semillas/Mensajero/EQUIVALENCIA-*.md`.

## Qué entregar: `definicion/AUDITORIA-FUNCIONES.md`

Por app, una tabla: **función vieja** (qué hace, archivo y línea del original) | **dónde está en la nueva** (archivo y línea) | **estado**: ✅ igual, 🔁 cambiada a propósito (cita la decisión D-xx), ⚠️ distinta sin decisión, ❌ falta. Revisa a fondo: botones, menús, atajos, textos de avisos, diálogos, validaciones, configuraciones, archivos que lee/escribe, comportamiento ante errores, vigilancia de carpetas, impresión, copiar al portapapeles, arrastrar y soltar, preferencias recordadas.

Al final: **lista priorizada** de lo que falta o quedó distinto sin decisión (❌ y ⚠️), con una frase de impacto para Javier cada una y si es trivial (OpenCode Go) o no.

Español neutro, claro. Reporte al final de esta nota.

## Reporte del agente
