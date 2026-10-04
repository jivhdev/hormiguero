---
bloque: B-5b
app: Archivero + Nucleo
fase: B (D-66, D-68)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Nucleo/Pdf/LectorPdf.cs, src/Hormiguero.Nucleo/Pdf/ZonaPdf.cs, tests/Hormiguero.Nucleo.Tests/ZonaPdfTests.cs, src/Hormiguero.Archivero/Servicios/Pdf/LectorPdf.cs, src/Hormiguero.Archivero/Hormiguero.Archivero.csproj, Directory.Packages.props]
archivos_prohibidos: [todo lo demás, en especial las pruebas existentes de Archivero]
rama: fase-b/b5b-archivero-pdf-nucleo
---

# B-5b — Archivero lee y dibuja los PDF con el núcleo

## Objetivo

Que Archivero use la lectura y el dibujo de PDF del núcleo de Hormiguero (PdfPig + PDFtoImage) en vez de su librería propia (Docnet), para que las tres apps lean los PDF igual. **El comportamiento de Archivero no cambia**: sus 280 pruebas deben pasar **sin modificarlas**. Si alguna prueba antigua no puede pasar sin cambiarla, **detente y explica por qué en el reporte** (no la cambies).

## Contexto

- `src/Hormiguero.Archivero/Servicios/Pdf/LectorPdf.cs` es el único lugar que usa Docnet. Expone: `ContarPaginas(ruta)`, `RenderizarPagina(ruta, numeroPagina)` → `PaginaRenderizada(byte[] PixelesBgra, int Ancho, int Alto)`, `TieneTextoExtraible(ruta)`, `ExtraerTexto(ruta, numeroPagina, RectanguloFraccion)`; páginas **desde 0**. `RectanguloFraccion(X, Y, Ancho, Alto)` son fracciones de la página con origen **arriba a la izquierda**. Hoy dibuja en un lienzo de 1240 × 1754 px (A4 a 150 ppp).
- Núcleo: `Hormiguero.Nucleo.Pdf.LectorPdf.Leer(Stream)` → `InfoPdf(EstadoPdf Estado, int Paginas, IReadOnlyList<bool> PaginaTieneTexto, IReadOnlyList<PalabraPdf> Palabras)`; `PalabraPdf(Texto, Pagina /*desde 1*/, X, Y /*abajo izquierda, puntos*/, Ancho, Alto)`; `DibujoPdf.Paginas(byte[])`, `DibujoPdf.Dibujar(byte[] pdf, int pagina /*desde 0*/, double zoom)` → `ImagenPagina(Ancho, Alto, PixelesBgra)` (a 96 × zoom ppp, fondo blanco); `ZonaPdf.Texto(InfoPdf, Zona)`.

## Contenido exacto

1. Núcleo, `LectorPdf.cs`: agrega a `InfoPdf` una propiedad **no posicional** `public IReadOnlyList<(double Ancho, double Alto)> TamanosPagina { get; init; } = [];` (puntos PDF, una por página) y llénala en `Leer` con el tamaño de cada página. No cambies el constructor (lo usan otras pruebas).
2. Núcleo, `ZonaPdf.cs`: agrega `public static string TextoEnFraccion(InfoPdf info, int paginaDesdeCero, double x, double y, double ancho, double alto)` que convierte la fracción (origen arriba a la izquierda) a una `Zona` en puntos usando `TamanosPagina` y devuelve `Texto(info, zona)`. Si la página no existe, devuelve `""`. Pruebas en `ZonaPdfTests.cs`: conversión correcta (texto arriba a la izquierda y abajo a la derecha) y página inexistente.
3. Archivero, `LectorPdf.cs`: misma API pública (nombres, firmas y páginas desde 0), ahora con el núcleo:
   - Lee el archivo completo en memoria con `FileShare.ReadWrite | FileShare.Delete` (nunca lo deja bloqueado).
   - `ContarPaginas` → `DibujoPdf.Paginas`.
   - `RenderizarPagina` → `DibujoPdf.Dibujar(bytes, numeroPagina, 150.0 / 96.0)` (mismo tamaño que antes para A4).
   - `TieneTextoExtraible` → `true` si el PDF tiene **al menos una palabra** (`Palabras.Count > 0`), igual que antes con Docnet (cualquier texto). **No** uses `InfoPdf.TieneTexto` (tiene un umbral de 20 letras por página que cambiaría el comportamiento).
   - `ExtraerTexto` → `ZonaPdf.TextoEnFraccion(...)` y `.Trim()`.
   - Borra `ExtraerTextoDePagina` y todo uso de Docnet. Si alguna prueba antigua usa `ExtraerTextoDePagina`, **detente y repórtalo**.
4. `Hormiguero.Archivero.csproj`: quita el paquete `Docnet.Core` y la propiedad `DocnetRuntime`. `Directory.Packages.props`: quita `Docnet.Core`.

## Criterio de término

- `dotnet build` sin advertencias; `dotnet test` todo pasa (Archivero 280 sin cambios en sus pruebas); `dotnet csharpier check .` sin errores.
- Escribe tu reporte al final de esta nota, incluida cualquier diferencia de comportamiento que hayas notado.

## Reporte del agente

- Implementado el uso del lector y dibujante PDF del núcleo desde Archivero. Se agregó `InfoPdf.TamanosPagina` sin cambiar su constructor, la conversión de fracciones a zonas de texto y sus pruebas. Se quitó Docnet del proyecto y de las versiones centralizadas.
- Archivero lee cada archivo a memoria usando `FileShare.ReadWrite | FileShare.Delete`, renderiza a 150 ppp y determina texto extraíble por cantidad de palabras. Si el núcleo informa un PDF dañado o protegido, el lector lanza `InvalidDataException`; la vigilancia existente conserva así el motivo `ArchivoDanado`.
- No se encontraron pruebas que usen `ExtraerTextoDePagina`. Ninguna prueba existente fue modificada.
- Diferencia observada: la extracción ahora reconstruye texto a partir de palabras PdfPig ordenadas por línea, con espacios entre palabras; antes Docnet concatenaba caracteres. También se conserva el tratamiento de archivos dañados mediante la clasificación ya existente.
- Verificación final: `dotnet build` correcto, 0 advertencias y 0 errores; `dotnet test` correcto, 563 pruebas totales, incluidas las 280 de Archivero App; `dotnet csharpier check .` correcto (220 archivos).
