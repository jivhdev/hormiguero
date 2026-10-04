---
bloque: M-A1b
app: Mensajero
fase: A (D-65)
estado: hecho
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Mensajero.Core/LogicaOfisuiza.cs, tests/Hormiguero.Mensajero.Core.Tests/EquivalenciaOfisuizaTests.cs]
archivos_prohibidos: [todo lo demás; en especial tests/Hormiguero.Mensajero.Core.Tests/Equivalencia/** (respuesta correcta)]
rama: mensajero/a1b-casos-borde
---

# M-A1b — Extraer texto de una zona exactamente como PyMuPDF (casos de borde)

## Problema

Con OCC **reales**, la extracción de M-A1 da NVV y OCL distintos al Ofisuiza original: el texto nuevo **contiene** al original y además **letras vecinas** (en un caso, NVV de 5 dígitos en el original y 13 caracteres en el nuevo). OCC, despacho y proveedor sí coinciden. Las zonas de NVV y OCL están pegadas y se traslapan un poco.

## La respuesta correcta, ampliada

`tests/Hormiguero.Mensajero.Core.Tests/Equivalencia/Ofisuiza/esperado.json` tiene ahora **13 OCC**: las 5 de antes y `BORDE1.pdf` a `BORDE8.pdf`, con etiquetas que cruzan el borde izquierdo de las zonas y renglones vecinos a pocos puntos, en varios tamaños de letra. Se generó corriendo el Ofisuiza original (PyMuPDF `page.get_text("text", clip=rect)`; ver `generar_esperado.py`). Fíjate en lo que incluye PyMuPDF en esos bordes: entran letras que solo tocan la zona (por ejemplo `":NVV-12345"`, `"a:NVV-12345"`, y en `BORDE3` pedazos de la línea vecina). Ese es el comportamiento que hay que reproducir; **no lo "corrijas"**.

## Qué hacer

1. Reemplaza el criterio actual (letra **completa** dentro de la zona con la caja ajustada de PdfPig) por el que use MuPDF/PyMuPDF para `clip`. Pistas: MuPDF mide cada carácter con su caja "de fuente" (avance horizontal completo y alto según ascendente/descendente de la fuente, no el contorno del dibujo) y decide la inclusión por su relación con el rectángulo (estudia los 13 casos para fijar la regla exacta: centro, porcentaje de traslape, etc.). Arma las líneas y el orden como lo hace PyMuPDF en modo `"text"`.
2. Las pruebas de `EquivalenciaOfisuizaTests.cs` deben cubrir los 13 casos (extracción y guías), comparando carácter por carácter.
3. No cambies la API pública de `ExtractorOcc`.

## Criterio de término

- `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores.
- Si algún caso no se puede reproducir exactamente, **detente y explica** en el reporte (no toques la respuesta correcta).
- Escribe tu reporte al final de esta nota.

## Reporte del agente

Detenido: no logré reproducir exactamente los 13 recortes con PdfPig dentro del alcance del bloque. Probé incluir caracteres con traslape positivo y conservar el orden de lectura de `Page.Letters`; seis casos aún diferían por caracteres faltantes/extra y orden o espaciado de renglones. Revertí esa regla para no dejar una extracción incorrecta ni modificar la respuesta esperada.

La prueba existente ya toma automáticamente los 13 elementos de `extraccion` y los 52 casos de `guias` (cuatro por PDF), comparando cadenas completas. No hizo falta editar `EquivalenciaOfisuizaTests.cs`.

Verificaciones:
- `dotnet build`: correcto, 0 advertencias y 0 errores.
- `dotnet test`: 7 fallos en `ExtraccionCoincideCaracterPorCaracter` para BORDE2, BORDE3, BORDE4, BORDE5, BORDE6, BORDE7 y BORDE8. Las otras suites pasan. Los fallos muestran que el criterio vigente tampoco reproduce los nuevos casos de borde.
- `dotnet csharpier check src/Hormiguero.Mensajero.Core/LogicaOfisuiza.cs`: correcto.
- `dotnet csharpier check .`: falla solo por finales de línea distintos en `Hormiguero.Mensajero.Core.csproj` y `Hormiguero.Mensajero.Core.Tests.csproj`, fuera de los archivos permitidos.

No cambié la API pública ni los datos esperados. Para continuar hace falta una implementación de caja de carácter/orden de texto que replique MuPDF y que se pueda mantener dentro del alcance autorizado.

### Cierre (Claude, 2026-10-04)

Codex no logró la regla; la terminé yo con experimentos sobre PyMuPDF 1.28.2:

- MuPDF deja una letra si su **caja de tinta** toca la zona (con borde incluido); un espacio cuenta como el punto donde empieza.
- Recorre las letras en el orden del PDF; entre cada letra que queda y la anterior: avance < 0,15 em o retroceso corto = nada; 0,15 a 0,8 em hacia adelante = un espacio; desvío o distancia >= 0,8 em = renglón nuevo.
- Con fuentes estándar sin incrustar, MuPDF mide la tinta con URW Nimbus y PdfPig con Adobe (difieren en milésimas, decisivo en el borde: BORDE7 y 2 de las 6 OCC reales usan Courier sin incrustar). `Recursos/tinta-base14.txt` (generado por `generar_tinta_base14.py`) trae la tabla de MuPDF.

Resultado: 89/89 pruebas; OCC reales: campos iguales 36/36.
