---
bloque: C-1e
app: Núcleo
fase: C
estado: hecho
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Nucleo/**, tests/Hormiguero.Nucleo.Tests/**]
archivos_prohibidos: [todo lo demás; sin pantallas; no cambies migraciones existentes (v8 nueva)]
rama: nucleo/c1e-alertas
---

# C-1e — Reglas de alerta, alertas e historial (migración v8)

Secciones 3 y 5 y "Decisiones de Claude" (puntos 1 y 2) de `definicion/DISENO-C1-ALERTAS-Y-VENCIMIENTOS.md`. Usa `CalculoFechas` (C-1b) y los calendarios (C-1c, `CalendariosFeriados.cs`).

1. **Migración v8** (estilo v1–v7, idempotente, transacción, sin cascada): reglas de alerta configurables por el usuario y ligadas a modelos de cadena/vagones (p. ej. "si el vagón *Factura* sigue vacío **N días hábiles/corridos** después de llenarse el vagón *Guía*, avisar '*Guía esperando factura*'"), con calendario, texto del aviso, activa/anulada; **alertas** (de regla o manuales sobre un documento/versión o una cadena, con fecha de vencimiento, texto, estado pendiente/vencida/resuelta/descartada, motivo) e **historial** de cambios de estado (nunca borrar; auditoría en la misma transacción).
2. **Repositorios en el Núcleo**: crear/editar/anular reglas (validar que los vagones sean del mismo modelo); crear alerta manual; cambiar estado (resolver/descartar con motivo, reabrir); listar por estado/cadena/documento con contador; idempotencia (la misma regla no crea dos alertas para la misma cadena y vagón).
3. **Sin evaluador todavía** (es C-1f): solo datos, validaciones y consultas. Deja un método "calcular vencimiento" que use `CalculoFechas.Sumar` con el calendario de la regla.
4. Nada fijo de JCV (D-70); base con `DocumentosGuardados.RutaBaseComun` donde corresponda.

Pruebas: migración nueva/repetida/sobre v7 con datos; regla válida e inválida (vagón de otro modelo); alerta manual; cambio de estados con historial y auditoría; no duplicar; vencimiento hábil saltando un feriado; listar por estado con contador.

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Si algo no está definido en el diseño, **decide lo más simple y seguro, anótalo en el reporte y sigue** (no te detengas). Reporte al final.

## Reporte del agente

Implementé la migración v8 con reglas de alerta, alertas e historial; repositorio para crear, editar y anular reglas, crear alertas manuales o desde regla, calcular vencimientos, cambiar estados, consultar historial y listar por estado, cadena, versión o documento con contador. El historial/auditoría se guarda en la misma transacción que la operación. Triggers impiden borrar alertas o editar/borrar historial.

Decisiones para lo que el diseño dejaba abierto:
- La identidad idempotente de alerta automática es regla + cadena + vagón destino; la clave de evento se conserva como dato. `repetir` queda almacenado para el evaluador C-1f y no permite duplicados en C-1e.
- Una alerta manual se asocia a una cadena (con vagón opcional) o a una versión de documento. Fecha/texto y parámetros calculados quedan como copia en la alerta.
- Resolver y descartar exigen motivo según el alcance del bloque; reabrir vuelve a pendiente. Marcar vencida es una transición explícita; no hay evaluación automática.
- Las alertas conservan su cálculo ante cambios posteriores de la regla o del calendario.

Verificación: `dotnet build` correcto, 0 advertencias; `dotnet test` correcto: 116 Núcleo, 9 Diseño, 284 Archivero, 119 Mensajero y 130 Buscadero; `dotnet csharpier check .` correcto (248 archivos).
