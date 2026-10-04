---
tipo: adr
app: Archivero
numero: 1
fecha: 2026-10-04
estado: aprobada # propuesta | aprobada | reemplazada
---

# ADR-001 (Archivero): Dónde viven las configuraciones de identificación y el registro de auditoría

## Contexto

Archivero guarda cómo reconocer cada tipo de documento (zonas marcadas, emisor, destino y nombre) y registra cada movimiento de archivos (SPEC REQ-003 y REQ-004). Buscadero y Mensajero usarán después esas mismas configuraciones para enlazar y preparar documentos (D-18: las apps comparten datos y plantillas). ADR-001 del ecosistema exige que solo el núcleo toque la base común.

## Opciones consideradas

| Opción | A favor | En contra |
|---|---|---|
| **A. Tablas comunes en la base común, creadas y administradas por el núcleo** (`identificaciones`, `auditoria`) | Buscadero y Mensajero las leen sin copiar nada; una sola forma de respaldar; cumple ADR-001 | Cada cambio de forma pasa por una migración del núcleo |
| B. Archivos JSON propios de Archivero | Fáciles de mirar a mano | Las otras apps tendrían que leer archivos de Archivero; sin respaldo común ni bloqueos seguros |
| C. Tabla propia de Archivero en la base común | Archivero la cambia solo | Rompe ADR-001 (solo el núcleo toca la base común) y las otras apps dependerían de Archivero |

## Decisión

**Opción A.** El núcleo agrega la migración 3:

- `identificaciones`: una fila por configuración (tipo, emisor, y el resto —zonas, destino, nombre— como JSON versionado en una columna, para no migrar por cada opción nueva del asistente).
- `auditoria`: una fila por movimiento (fecha y hora, app, acción, origen, destino, huella, resultado). Solo se agregan filas; nunca se cambian ni se borran.

El núcleo expone clases para leerlas y escribirlas (`Identificaciones`, `Auditoria`), igual que `Documentos` y `Configuracion`. Archivero sigue la estructura de ADR-002 de Buscadero: `Hormiguero.Archivero.Logica` (sin WPF), `Hormiguero.Archivero` (WPF delgada) y `Hormiguero.Archivero.Tests`.

Tomada por Claude el 2026-10-04 por delegación ("dale sigamos"); Javier la revisa en el hito de la apertura de Archivero.

## Consecuencias

- Buscadero podrá usar las configuraciones de Archivero para enlazar (paso 4) sin cambios de forma.
- El registro de auditoría crece siempre; si llega a molestar, se archivará por año (no en la apertura).
