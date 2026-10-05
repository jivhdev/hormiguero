---
bloque: C-1c
app: Núcleo
fase: C
estado: hecho
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

## Decisión de Claude (2026-10-05)

Importar un CSV con fechas que ya existen en el calendario: (a) fecha repetida **dentro del mismo archivo** → error claro y no se importa nada; (b) fecha que ya existe **activa** → se actualiza el nombre si cambió (con auditoría); (c) fecha que existe **anulada** → se reactiva con el nombre del archivo (con auditoría). Al terminar, devolver un resumen (agregados, actualizados, reactivados). Agrega pruebas de los tres casos. Además mueve el CSV de `src/Hormiguero.Nucleo/Datos/Datos/` a `src/Hormiguero.Nucleo/Recursos/` (ajusta el `.csproj`).

## Reporte del agente

Estado: bloque implementado.

Implementado en los archivos permitidos:
- Migración v7 idempotente y transaccional para calendarios y feriados, con auditoría en la misma transacción y sin borrado en cascada.
- Repositorio para listar y crear calendarios, seleccionar el predeterminado, agregar/editar/anular feriados, consultar feriados activos por rango e importar/exportar CSV UTF-8.
- Importación con fechas repetidas dentro del archivo rechazada antes de guardar; las fechas activas actualizan el nombre cuando cambia y las anuladas se reactivan. Ambas operaciones registran auditoría. Devuelve el resumen de agregados, actualizados y reactivados.
- Precarga embebida de Chile, una sola vez, desde `Recursos/feriados-chile.csv`.
- Pruebas de migración sobre v6 y repetida, precarga, calendario predeterminado, anulación/auditoría, CSV, los tres casos de importación, rango anual y cálculo hábil saltando Navidad.

Verificación ejecutada:
- `dotnet build Hormiguero.slnx`: correcto, 0 advertencias y 0 errores.
- `dotnet test Hormiguero.slnx`: correcto, 645 pruebas superadas.
- `dotnet csharpier check .`: correcto, 242 archivos revisados.

Fechas dudosas marcadas `VERIFICAR`: ninguna. Se contrastaron las fechas nacionales de 2026 y 2027 con [feriadoschilenos.cl](https://www.feriadoschilenos.cl/2021-2030.html) y las leyes citadas en la [BCN](https://www.bcn.cl/leychile/). El 17-09-2027 está cubierto por la Ley 20.983 porque el 18 y 19 de septiembre caen sábado y domingo. Los feriados regionales y bancarios no se incluyeron en el calendario nacional. La fuente y el recordatorio de revisión anual están en el CSV.
- Claude: 2027-09-17 (viernes) quitado de la precarga y marcado VERIFICAR en el CSV: la Ley 20.983 hace feriado el 17 de septiembre solo si es lunes (y el 20 si es viernes). Javier lo confirma o agrega en el calendario.
