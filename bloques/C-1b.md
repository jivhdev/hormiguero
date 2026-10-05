---
bloque: C-1b
app: Núcleo
fase: C
estado: pendiente
agente: OpenCode Go
archivos_permitidos: [src/Hormiguero.Nucleo/Utilidades/CalculoFechas.cs, tests/Hormiguero.Nucleo.Tests/CalculoFechasTests.cs]
archivos_prohibidos: [todo lo demás]
rama: nucleo/c1b-calculo-fechas
---

# C-1b — Cálculo de fechas: días corridos y hábiles (trivial)

Sección 2 y "Decisiones de Claude" punto 1 de `definicion/DISENO-C1-ALERTAS-Y-VENCIMIENTOS.md`.

Crea `src/Hormiguero.Nucleo/Utilidades/CalculoFechas.cs` (namespace `Hormiguero.Nucleo.Utilidades`, como `Huella.cs`):

```csharp
public enum TipoDias { Corridos, Habiles }
public static class CalculoFechas
{
    // Suma (o resta, si cantidad < 0) días a fechaBase. La fecha base no cuenta.
    // Corridos: todos los días. Hábiles: solo lunes a viernes que no estén en feriados.
    // cantidad 0 devuelve fechaBase tal cual.
    public static DateOnly Sumar(DateOnly fechaBase, int cantidad, TipoDias tipo, IReadOnlySet<DateOnly>? feriados = null);
    public static bool EsHabil(DateOnly fecha, IReadOnlySet<DateOnly>? feriados = null);
}
```
Rechaza `cantidad` fuera de -3650..3650 con `ArgumentOutOfRangeException` (mensaje claro en español).

Pruebas (`tests/Hormiguero.Nucleo.Tests/CalculoFechasTests.cs`, xUnit, como las demás del proyecto): corridos +/-; 1 hábil desde viernes = lunes; 1 hábil desde sábado = lunes; -1 hábil desde lunes = viernes anterior; feriado en medio se salta; feriado en sábado no cuenta doble; feriados repetidos; cantidad 0 en un domingo devuelve el domingo; 5 hábiles cruzando fin de año con 2026-12-25 y 2027-01-01 como feriados; fuera de rango lanza error.

`dotnet build` sin advertencias; `dotnet test tests/Hormiguero.Nucleo.Tests` pasa; `dotnet csharpier format` de los 2 archivos. Reporte breve al final.

## Reporte del agente
