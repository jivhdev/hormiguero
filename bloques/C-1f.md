---
bloque: C-1f
app: Núcleo, Buscadero y Archivero
fase: C
estado: hecho
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Nucleo/**, src/Hormiguero.Buscadero.Core/**, src/Hormiguero.Buscadero/MainWindow.xaml.cs, src/Hormiguero.Archivero/Servicios/**, tests/Hormiguero.Nucleo.Tests/**, tests/Hormiguero.Buscadero.Core.Tests/**, tests/Hormiguero.Archivero.App.Tests/**]
archivos_prohibidos: [todo lo demás; sin pantallas nuevas (C-1g); no cambies migraciones existentes]
rama: nucleo/c1f-evaluador
---

# C-1f — Evaluador de alertas

Sección 4 y "Decisiones de Claude" punto 2 de `definicion/DISENO-C1-ALERTAS-Y-VENCIMIENTOS.md`; repositorios de C-1e (`src/Hormiguero.Nucleo/Datos/Alertas.cs`).

1. **Evaluador en el Núcleo**: para cada regla activa y cada cadena de su modelo: si el vagón de inicio está lleno (enlace activo) y el vagón esperado vacío → crear (idempotente) la alerta con vencimiento = fecha del enlace de inicio + N días (`CalculoFechas`, calendario de la regla); si la fecha ya pasó → estado vencida. Si el vagón esperado se llena con un enlace confirmado → **resolver sola** con motivo "resuelta automáticamente: llegó *{vagón}*". Si la condición desaparece por otra razón (regla/cadena anulada, enlace de inicio anulado) → dejarla como está (la persona decide). Alertas manuales: pasan a vencida cuando llega la fecha.
2. **Cuándo corre** (D-71, sin servicios en segundo plano complicados): al abrir Buscadero, al crear/completar/deshacer enlaces de cadena (manual o automático, B-6f), y después de publicar en Archivero. Rápido, sin bloquear la interfaz; errores visibles y en auditoría (nunca silencioso).
3. Idempotente: correr dos veces seguidas no cambia nada.

Pruebas: crear al faltar, no duplicar, vencer al pasar la fecha (inyecta "hoy" para probar), resolver sola al llegar, no resolver si se anula la regla, manual vence, error visible. Si algo no está definido, **decide lo más simple y seguro, anótalo y sigue**.

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Reporte al final.

## Reporte del agente

Implementé `EvaluadorAlertas` en Núcleo. Revisa reglas activas y cadenas activas, calcula el vencimiento desde la fecha del enlace de origen con el calendario de la regla, crea alertas idempotentes, marca vencidas las alertas automáticas y manuales al llegar su fecha, y resuelve alertas existentes cuando encuentra un enlace confirmado en el vagón esperado. Si no existe el enlace de origen o la regla/cadena está anulada, conserva la alerta sin resolverla. Los errores se registran en auditoría y se propagan para mostrarlos en Buscadero o Archivero.

Conecté la evaluación a la apertura de Buscadero en segundo plano, a los vínculos manuales y aceptados, a la revisión automática de enlaces y a la publicación de documentos en Archivero. Añadí pruebas de creación, idempotencia, vencimiento, resolución, regla anulada, vencimiento manual y error visible/auditable.

Decisión para la fecha límite: se marca vencida cuando `fecha_objetivo <= hoy`, tanto para alertas automáticas como manuales. Un enlace se considera confirmado si está activo y su versión sigue vigente.

Verificación: `dotnet build` correcto, 0 advertencias y 0 errores. `dotnet test` correcto: 664 pruebas aprobadas. `git diff --check` sin errores. `dotnet csharpier check` en los cinco archivos permitidos pasa. `dotnet csharpier check .` señala únicamente `tests/Hormiguero.Mensajero.Core.Tests/AlmacenMensajeroTests.cs` por finales de línea; no lo modifiqué porque está fuera de los archivos permitidos.

- Claude: una alerta vence el día **siguiente** a su fecha objetivo (el mismo día queda pendiente, "vence hoy"); prueba ajustada.
