---
bloque: M-A4
app: Mensajero
fase: A (D-65, D-68)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [semillas/Mensajero/EQUIVALENCIA-CLICKFACTURA.md, src/Hormiguero.Mensajero.Core/ClickFactura/**, tests/Hormiguero.Mensajero.Core.Tests/ClickFactura/**, tests/Hormiguero.Mensajero.Core.Tests/Equivalencia/ClickFactura/**]
archivos_prohibidos: [todo lo demás; no toques Ofisuiza (LogicaOfisuiza.cs, TintaBase14.cs, Equivalencia/Ofisuiza/**), AlmacenMensajero ni la app WPF]
rama: mensajero/a4-clickfactura-logica
---

# M-A4 — Lógica de ClickFactura traducida a .NET, idéntica al original

## Contexto

Fase A de Mensajero: traducción **fiel** de los programas antiguos de Javier (D-65). Ofisuiza ya está hecho (mira cómo: `src/Hormiguero.Mensajero.Core/LogicaOfisuiza.cs`, `tests/Hormiguero.Mensajero.Core.Tests/EquivalenciaOfisuizaTests.cs` y `Equivalencia/Ofisuiza/generar_esperado.py`). Ahora ClickFactura, que está en Python.

Original (SOLO LECTURA, no modifiques nada ahí): `C:\Users\jihja\Desktop\Entorno Antiguo\AP03-ClickFactura\`. Hay varias copias (raíz, `Proyectos\ClickFactura`, `build`, `dir src`...): la versión válida es **la de cambios más recientes por fecha**; anota en el reporte cuál usaste y por qué. Lee también `Arreglos click factura.txt` si existe.

## Reglas de datos (críticas, D-10, D-11, D-68)

- **No leas, copies ni abras ninguna base real** de ClickFactura (`%LOCALAPPDATA%\ClickFactura\`, `*.db` fuera del repo, planillas reales en `Xls\`). Solo datos **sintéticos** inventados por ti (RUT, nombres y correos falsos, p. ej. `76000000-1 EMPRESA DE PRUEBA`, `prueba@ejemplo.cl`).
- No subas ni pegues documentos reales en ningún lado.
- La migración de los datos reales la hace Claude después con Javier; tú solo documentas el **esquema** (tablas y columnas, leyendo el código, no los datos).

## Qué hacer

1. `semillas/Mensajero/EQUIVALENCIA-CLICKFACTURA.md`: lista numerada de TODO lo que hace ClickFactura (como `EQUIVALENCIA-OFISUIZA.md`): pantallas, botones, textos exactos de mensajes/correos/avisos, atajos, formatos, reglas, archivos que lee y escribe, y el esquema de su base de datos. Marca cada fila ⏳ o ✅ según quede traducida en este bloque.
2. Traduce a C# toda la **lógica sin interfaz** (lo que no es tkinter/ventanas): cálculos, formatos de texto, armado de mensajes/correos, lectura de archivos (PDF/Excel) y reglas de negocio, en `src/Hormiguero.Mensajero.Core/ClickFactura/` (namespace `Hormiguero.Mensajero.Core.ClickFactura`). Para PDF usa PdfPig y, si hace falta la misma extracción por zona que PyMuPDF, reutiliza la de Ofisuiza (puedes llamar a `ExtractorOcc` si sirve, pero no lo modifiques; si necesitas algo nuevo, explícalo en el reporte). Para Excel usa un paquete NuGet mantenido y con licencia libre (por ejemplo ClosedXML) y agrégalo a `Directory.Packages.props` solo si es imprescindible (anótalo en el reporte).
3. Respuesta correcta: `tests/Hormiguero.Mensajero.Core.Tests/Equivalencia/ClickFactura/generar_esperado.py` crea entradas sintéticas y corre el **código original de ClickFactura sin modificarlo** (importándolo, como hace el de Ofisuiza) para escribir `esperado.json`. Nunca escribas a mano los valores esperados. Incluye casos de borde (vacíos, tildes y ñ, montos grandes, RUT con K, varios ítems, etc.).
4. Pruebas en `tests/Hormiguero.Mensajero.Core.Tests/ClickFactura/` que comparen carácter por carácter contra `esperado.json`.
5. Sin interfaz WPF en este bloque (será M-A5).

## Criterio de término

- `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores.
- Si algo no se puede reproducir exactamente, **no ajustes la respuesta correcta**: explícalo en el reporte.
- Escribe tu reporte al final de esta nota: versión usada, qué quedó traducido, qué falta, dudas para Javier.

## Reporte del agente
