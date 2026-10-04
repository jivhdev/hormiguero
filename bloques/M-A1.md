---
bloque: M-A1
app: Mensajero
fase: A (D-65, D-68)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Mensajero.Core/**, tests/Hormiguero.Mensajero.Core.Tests/*.cs]
archivos_prohibidos: [todo lo demás; en especial tests/Hormiguero.Mensajero.Core.Tests/Equivalencia/** (son la respuesta correcta y no se tocan)]
rama: mensajero/a1-logica-ofisuiza
---

# M-A1 — Traducción fiel de la lógica de Ofisuiza a .NET

## Objetivo

Traducir a C# (`Hormiguero.Mensajero.Core`, espacio de nombres `Hormiguero.Mensajero.Core`) **toda la lógica** (no la pantalla) de la Ofisuiza que Javier usa hoy, de modo que dé **exactamente** los mismos resultados. Lee primero `semillas/Mensajero/EQUIVALENCIA-OFISUIZA.md`.

## Código original (solo lectura, Python)

`C:\Users\jihja\Desktop\Entorno Antiguo\AP07-Ofisuiza Versiones Antiguas\OFISUIZA .EXE FUNCIONAL VERSION EN USO\`: `config.py`, `extractor.py`, `mensajes_retiro.py`, `clientes_nvv.py`, `gestor_carpeta.py`, y en `ui/app.py` las funciones `_extraer_obra` y `generar_mensaje_guia`. Léelos completos. **No ejecutes ni modifiques nada de esa carpeta** (solo lectura).

## La respuesta correcta

`tests/Hormiguero.Mensajero.Core.Tests/Equivalencia/Ofisuiza/esperado.json` se generó corriendo ese Ofisuiza original (PyMuPDF) sobre los 5 PDF sintéticos de la misma carpeta (ver `generar_esperado.py`). **Las pruebas deben comparar contra ese archivo, carácter por carácter.** No lo modifiques.

## Contenido exacto

1. `ZonasOcc` (de `config.py`): las 5 zonas en puntos con origen **arriba a la izquierda** (como PyMuPDF) y el mapeo de proveedores **en el mismo orden** (el original recorre el diccionario en orden de inserción).
2. `ExtractorOcc` (de `extractor.py`): `ExtraerOccNvvOcl(string rutaPdf)`, `ExtraerDespacho`, `ExtraerProveedor`, y las puras `LimpiarOcc`, `LimpiarNvv`, `FormatearAsunto(occ, nvv, ocl)`. Lee el PDF en memoria con `FileShare.ReadWrite | FileShare.Delete`. Para el texto usa PdfPig (ya disponible por el núcleo) y **reproduce el resultado de PyMuPDF `get_text("text", clip=...)`** sobre los 5 PDF: qué letras entran en la zona, cómo se arman las líneas y los saltos de línea. Puedes trabajar con letras (`page.Letters`) o palabras; lo que mande es que las pruebas contra `esperado.json` pasen. Ojo: PyMuPDF usa y hacia abajo; PdfPig, y hacia arriba.
3. `MensajesRetiro` (de `mensajes_retiro.py`): `Generar(string proveedor, string occ, string ocl, string? dia = null, string? bloque = null)` con los **textos exactos** (tildes, saltos de línea, enlaces y espacios al final de línea incluidos). `BloquesHoffens` = `["09:00 - 11:00", "11:00 - 13:00", "Manual"]`.
4. `MensajeGuia` (de `ui/app.py`): `ExtraerObraYComuna(string despacho)` y `Generar(string obra, string comuna, OpcionDia dia, string diaManual)` con `enum OpcionDia { Hoy, Ayer, Otro }`.
5. `CarpetaOcc` (de `gestor_carpeta.py`): `NumeroEnNombre(string nombre)` (primer grupo de dígitos sin ceros a la izquierda; `"0"` si son solo ceros; `""` si no hay), `UltimoPdf(string carpeta)` (por fecha de modificación) y `BuscarPorNumero(string carpeta, string numero)` (solo esa carpeta, sin subcarpetas; extensión `.pdf` sin importar mayúsculas).
6. `ClientesNvv` (de `clientes_nvv.py`): `PorDefecto` (los 7 originales), `Buscar(string texto, IReadOnlyList<string> lista)` (mayúsculas, recorta espacios, "contiene"), `Leer(string ruta)` y `Guardar(string ruta, IEnumerable<string> lineas)` con el mismo formato (UTF-8, una por línea, sin líneas vacías; si el archivo no existe o queda vacío, los de por defecto). **No decidas dónde vive el archivo**: eso lo define otro bloque.
7. Pruebas (`tests/Hormiguero.Mensajero.Core.Tests/*.cs`): una teoría por sección de `esperado.json` (extracción de los 5 PDF, 6 mensajes de retiro, 20 guías, números en nombres, búsqueda de clientes, clientes por defecto) más pruebas propias de `CarpetaOcc` y `ClientesNvv` con carpetas temporales.

## Criterio de término

- `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Sin identificadores con tilde ni ñ.
- Si alguna respuesta de `esperado.json` no se puede reproducir exactamente, **detente y explica la diferencia** en el reporte (no ajustes la respuesta correcta).
- Escribe tu reporte al final de esta nota.

## Reporte del agente

- Implementé en `src/Hormiguero.Mensajero.Core/LogicaOfisuiza.cs` zonas, extracción PDF con PdfPig, limpieza y asunto, mensajes de retiro, guías, búsqueda de archivos OCC y gestión de clientes NVV.
- Agregué `tests/Hormiguero.Mensajero.Core.Tests/EquivalenciaOfisuizaTests.cs`: compara extracción y mensajes carácter por carácter con `esperado.json`; incluye las cinco extracciones, seis mensajes, veinte guías, cinco números, cinco búsquedas y la lista de clientes por defecto, además de pruebas temporales para archivos y clientes.
- Equivalencia: las 5 extracciones, 6 mensajes, 20 guías, 5 números, 5 búsquedas y 7 clientes por defecto coinciden con el archivo esperado. La prueba de guías extrae obra y comuna directamente de los PDF. No modifiqué `Equivalencia/` ni el Ofisuiza original.
- Verificación: `dotnet build` correcto, 0 advertencias y 0 errores; `dotnet test` correcto, 522 pruebas superadas; `dotnet csharpier check .` correcto (195 archivos revisados).
