---
bloque: C-1g
app: Buscadero (seguimiento)
fase: C
estado: hecho
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Buscadero/**, src/Hormiguero.Buscadero.Core/**, tests/Hormiguero.Buscadero.Core.Tests/**]
archivos_prohibidos: [todo lo demás; no cambies el Núcleo salvo que sea imprescindible (explícalo)]
rama: buscadero/c1g-pantallas-alertas
---

# C-1g — Pantallas de alertas y vencimientos en Buscadero

Secciones 4 y 6 de `definicion/DISENO-C1-ALERTAS-Y-VENCIMIENTOS.md`; repositorios y evaluador del Núcleo (C-1c, C-1e, C-1f). **Amigable** (D-35, D-70): palabras simples, botones grandes, nada de jerga.

1. **Contador y lista de alertas**: en la ventana principal, junto a "N dudosos", un botón **"N alertas"** (resaltado si hay vencidas). La lista muestra: qué falta o qué hay que hacer, la cadena, **para cuándo** ("vence hoy", "vence en 3 días hábiles", "venció hace 2 días"), y estado. Filtros simples: Pendientes / Vencidas / Resueltas / Todas. Acciones: **"Ver cadena"**, **"Resolver"** y **"Descartar"** (piden un motivo corto), **"Reabrir"**.
2. **Configurar una alerta de cadena**: en el editor de modelos (junto a la regla de vagón), "**Avisarme si falta un documento**": elegir vagón de inicio ("cuando llegue…"), vagón esperado ("…y falte…"), cantidad de días, hábiles o corridos, calendario (predeterminado Chile) y el texto del aviso con sugerencia ("Guía esperando factura"). Mostrar la frase completa antes de guardar: "Si llega *Guía* y en *5 días hábiles* no llega *Factura*, avisar: *Guía esperando factura*".
3. **Alerta manual**: desde un documento o una cadena, "Recordarme…" con fecha (o "en N días hábiles/corridos") y texto.
4. **Calculadora de fechas**: pequeña ventana con fecha inicial, cantidad, hábiles/corridos y calendario → resultado (usa `CalculoFechas`), sin crear alerta.
5. **Feriados**: ventana simple para ver/agregar/anular feriados del calendario elegido e importar/exportar CSV (repositorio de C-1c).
6. Lecciones obligatorias: eventos en XAML durante `InitializeComponent`; tema claro y oscuro (`DynamicResource Hormiguero.*`); 1366×768; Alt con `e.SystemKey`; español neutro; base `DocumentosGuardados.RutaBaseComun`; cargas en segundo plano descartadas si el usuario cambió de vista; errores visibles.
7. Prueba de humo: abre Buscadero con `HORMIGUERO_DATOS` en carpeta temporal y abre cada ventana nueva sin caerse (si no puedes automatizar ventanas, una prueba STA que las construya, como `VentanasArchiveroSmokeTests`).

Pruebas en Core para lo que no sea solo pantalla (textos "vence hoy/en N/venció hace N", frase de la regla, filtros). Si algo no está definido, **decide lo más simple, anótalo y sigue**. `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Reporte al final.

## Reporte del agente

Completado en Buscadero, sin cambios en Núcleo.

- Se agregó el contador y la lista de alertas con filtros y acciones para abrir una cadena, resolver, descartar con motivo y reabrir. Las cadenas muestran sus avisos pendientes y vencidos.
- Se agregó configuración de aviso por modelo con selección de documentos, espera, calendario y frase de revisión; recordatorio manual asociado a una cadena; calculadora con `CalculoFechas`; y consulta, alta, anulación e importación/exportación CSV de feriados.
- Se usa la base común de `DocumentosGuardados.RutaBaseComun`; las ventanas usan recursos dinámicos del tema. La ventana principal abre a 1366×768. La prueba STA construye la ventana principal y las nuevas ventanas en temas claro y oscuro con `HORMIGUERO_DATOS` temporal.
- Decisión de presentación: al mostrar «para cuándo» se cuentan días hábiles, porque el contrato que expone `Alerta` no devuelve el modo ni el calendario guardados. Los cálculos y fechas objetivo se siguen haciendo mediante Núcleo; mostrar el modo histórico exacto requeriría exponer esos datos desde Núcleo, fuera del alcance permitido.

Verificación:

- `dotnet build`: correcto, cero advertencias y cero errores.
- `dotnet test -m:1`: correcto; pasaron 668 pruebas en los cinco proyectos de pruebas.
- `dotnet csharpier check .`: correcto.

- Pendiente menor: "para cuándo" cuenta siempre días hábiles en pantalla (el contrato de `Alerta` no expone modo/calendario); exponerlo desde el Núcleo en un bloque futuro.
