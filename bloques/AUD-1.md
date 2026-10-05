---
bloque: AUD-1
app: todas
fase: verificación (pedido de Javier, 2026-10-04)
estado: hecho
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

- Entregué `definicion/AUDITORIA-FUNCIONES.md` con comparación por app, versión elegida, referencias a fuente, estado, pendientes priorizados e impacto para Javier.
- Versiones: Archivero `ARCHIVERO VERSION 0.1 B` (26-09-2026); Buscadero `Buscadero - copia` (03-10-2026 08:27:54, respaldo más nuevo que incluye Caso-15); Ofisuiza, directorio funcional indicado; ClickFactura, copia más reciente en raíz (04-10-2026). Inspección de código/configuración solamente; no se ejecutaron aplicaciones antiguas ni se abrieron PDFs, planillas o bases.
- Hallazgos prioritarios: la importación NVV de Ofisuiza existe en el núcleo nuevo, pero no está conectada desde la UI; ClickFactura importa clientes en ciertos casos, pero no se acreditó migración de sesiones/envíos/caché; preferencias/log y filtro de pendientes quedan señalados como pendientes. No encontré una omisión inequívoca en las funciones principales confirmadas por D-66 y las equivalencias de Mensajero.
- Verificación: `dotnet build` correcto, 0 advertencias y 0 errores. `dotnet test` correcto: 613 pruebas superadas en los proyectos reportados (Diseño 9, Archivero 284, Núcleo 82, Mensajero.Core 108, Buscadero.Core 130); 0 fallidas y 0 omitidas.
- Sin código ni pruebas nuevas. Archivos escritos: solo el entregable autorizado y esta sección de reporte.
