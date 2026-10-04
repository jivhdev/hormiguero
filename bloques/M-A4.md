---
bloque: M-A4
app: Mensajero
fase: A (D-65, D-68)
estado: hecho
agente: Codex
modelo: codex
archivos_permitidos: [Directory.Packages.props, src/Hormiguero.Mensajero.Core/Hormiguero.Mensajero.Core.csproj, tests/Hormiguero.Mensajero.Core.Tests/Hormiguero.Mensajero.Core.Tests.csproj, semillas/Mensajero/EQUIVALENCIA-CLICKFACTURA.md, src/Hormiguero.Mensajero.Core/ClickFactura/**, tests/Hormiguero.Mensajero.Core.Tests/ClickFactura/**, tests/Hormiguero.Mensajero.Core.Tests/Equivalencia/ClickFactura/**]
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
2. Traduce a C# toda la **lógica sin interfaz** (lo que no es tkinter/ventanas): cálculos, formatos de texto, armado de mensajes/correos, lectura de archivos (PDF/Excel) y reglas de negocio, en `src/Hormiguero.Mensajero.Core/ClickFactura/` (namespace `Hormiguero.Mensajero.Core.ClickFactura`). Para PDF usa PdfPig y, si hace falta la misma extracción por zona que PyMuPDF, reutiliza la de Ofisuiza (puedes llamar a `ExtractorOcc` si sirve, pero no lo modifiques; si necesitas algo nuevo, explícalo en el reporte). Para Excel: el original usa openpyxl (`.xlsx`); usa **ClosedXML** (licencia MIT): agrégalo a `Directory.Packages.props` (versión estable más reciente) y referéncialo en `Hormiguero.Mensajero.Core.csproj` (autorizado). Para crear `.xlsx` sintéticos en `generar_esperado.py` usa openpyxl. Lo que dependa de Outlook/pywin32 o del portapapeles de Windows no se traduce aquí: descríbelo en la lista de equivalencia y en el reporte.
3. Respuesta correcta: `tests/Hormiguero.Mensajero.Core.Tests/Equivalencia/ClickFactura/generar_esperado.py` crea entradas sintéticas y corre el **código original de ClickFactura sin modificarlo** (importándolo, como hace el de Ofisuiza) para escribir `esperado.json`. Nunca escribas a mano los valores esperados. Incluye casos de borde (vacíos, tildes y ñ, montos grandes, RUT con K, varios ítems, etc.).
4. Pruebas en `tests/Hormiguero.Mensajero.Core.Tests/ClickFactura/` que comparen carácter por carácter contra `esperado.json`.
5. Sin interfaz WPF en este bloque (será M-A5).

## Criterio de término

- `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores.
- Si algo no se puede reproducir exactamente, **no ajustes la respuesta correcta**: explícalo en el reporte.
- Escribe tu reporte al final de esta nota: versión usada, qué quedó traducido, qué falta, dudas para Javier.

## Reporte del agente

- Copia del original usada: `C:\Users\jihja\Desktop\Entorno Antiguo\AP03-ClickFactura\`, con `main.py` en la raíz y módulos de negocio bajo `src/`. La seleccioné porque es la única copia localizada con los servicios Python completos (`analysis.py`, `xls_reader.py`, `file_finder.py`, `email_composer.py`, `rut_utils.py`, `copier.py` y `database/clientes_repo.py`); las otras rutas revisadas tenían pruebas, archivos de compilación o copias parciales. Leí `Arreglos click factura.txt` (28-09-2026). No abrí bases ni planillas reales.
- ClosedXML `0.105.1`, última versión estable publicada al realizar el bloque y con licencia MIT, agregada a `Directory.Packages.props` y al proyecto Core.
- Traducido en `src/Hormiguero.Mensajero.Core/ClickFactura/`: lectura de XLSX, normalización/validación de RUT (incluido el multiplicador 7→9 del original), composición de asunto/cuerpo, rutas y búsqueda de PDF por período o ampliada, agrupación/análisis de clientes y copiado/limpieza de archivos temporales. ClickFactura no extrae texto ni datos de los PDF, por lo que no era necesario PdfPig ni `ExtractorOcc`.
- `EQUIVALENCIA-CLICKFACTURA.md` documenta pantallas, textos, avisos, atajos, formatos, rutas, reglas y esquema de SQLite. Las operaciones de UI/Qt y portapapeles `CF_HDROP` con `pywin32` quedan pendientes de M-A5; el original no tiene automatización de Outlook. No se tradujo acceso a la base desde la app: las pruebas usan únicamente una SQLite temporal creada desde el esquema original y datos falsos.
- `generar_esperado.py` crea `entrada.xlsx` con openpyxl y ejecuta servicios del original para escribir `esperado.json`; las pruebas comparan textos y campos carácter por carácter. Incluye RUT con K, nombres con tildes/ñ, documentos con números grandes, filas vacías, varias facturas/notas y archivos cedibles sintéticos.
- Pendiente/dudas para Javier: el original usa una carpeta temporal fija en la pantalla, aunque `config.py` define otra; la nota de arreglos pide que la persona elija y pueda abrir su carpeta. El campo actual de correo es único: falta decidir cómo representar y separar varios destinatarios. También queda precisar el filtro/flujo de revisión de pendientes en M-A5. La tabla de sesiones e historial está en el esquema aunque el flujo de análisis consultado no la usa.
- Verificación final: `dotnet build Hormiguero.slnx -m:1` (0 advertencias, 0 errores); `dotnet test Hormiguero.slnx --no-build -m:1` (579 pruebas aprobadas); `dotnet csharpier check .` (217 archivos, sin errores).

### Revisión de Claude (2026-10-04)

- La respuesta correcta sale del original: regenerada con `generar_esperado.py` da un `esperado.json` idéntico. Solo datos inventados en una carpeta temporal; no hay base real de ClickFactura en este PC.
- Corregido: el caso "RUT inválido" tenía el error escrito a mano; ahora también lo decide el original (mismo resultado).
- `validar_rut` del original tiene un error (tras el multiplicador 7 salta a 9 en vez de volver a 2), pero el programa nunca la llama. Se tradujo igual (fase A); si en fase B/C se usa, hay que corregirla con Javier.
