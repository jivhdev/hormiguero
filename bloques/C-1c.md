---
bloque: C-1c
app: Núcleo
fase: C
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Nucleo/**, tests/Hormiguero.Nucleo.Tests/**]
archivos_prohibidos: [todo lo demás; sin pantallas]
rama: nucleo/c1c-feriados
---

# C-1c — Calendarios de feriados editables (migración v7)

Sección 2, 5 y "Decisiones de Claude" punto 3 de `definicion/DISENO-C1-ALERTAS-Y-VENCIMIENTOS.md`. Usa `CalculoFechas` (C-1b, `src/Hormiguero.Nucleo/Utilidades/CalculoFechas.cs`).

1. **Migración v7** en `Migraciones.cs` (estilo de v1–v6, idempotente, en transacción, sin cascada): tablas de **calendarios** (nombre, país, ámbito opcional, activo, predeterminado) y **feriados** (calendario, fecha, nombre, estado activo/anulado con fecha; nunca borrar). Solo calendarios y feriados: las alertas son C-1e.
2. **Repositorio en el Núcleo**: listar calendarios, elegir el predeterminado, agregar/editar/anular feriados (con auditoría en la misma transacción), obtener el conjunto de feriados activos de un calendario para un rango de años (para pasarlo a `CalculoFechas.Sumar`), importar y exportar un archivo simple (CSV `fecha;nombre`, UTF-8) para que cualquier persona actualice su país (D-70).
3. **Chile precargado como datos** (no en código de cálculo): calendario "Chile" predeterminado con los feriados nacionales de **2026 y 2027**. Créalo en un recurso de datos (CSV embebido) que se carga solo si el calendario no existe. Investiga con cuidado las fechas (feriados que se mueven a lunes, San Pedro y San Pablo, Encuentro de Dos Mundos, Día de los Pueblos Indígenas según solsticio, Iglesias Evangélicas, etc.) y en el CSV deja una línea de comentario con la fuente y "Revisar cada año; editable por el usuario". Si una fecha es dudosa, márcala con comentario "VERIFICAR" en el CSV y menciónala en el reporte. Además un calendario vacío "Otro país" de ejemplo no hace falta: basta con poder crear calendarios.
4. Base con `DocumentosGuardados.RutaBaseComun` donde corresponda; nada fijo de JCV (D-70).

Pruebas: migración nueva/repetida/sobre v6 con datos, precarga solo una vez, anular sin borrar con auditoría, importar/exportar CSV (tildes, líneas vacías, fecha inválida con error claro), conjunto de feriados por rango, `Sumar` hábil saltando un feriado de Chile precargado.

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Reporte al final con la lista de fechas dudosas.

## Reporte del agente
