---
bloque: B-5c
app: Buscadero
fase: B (D-66, D-68)
estado: pendiente
agente: OpenCode
modelo: opencode-go/glm-5.3-flash
archivos_permitidos: [src/Hormiguero.Buscadero.Core/Pdf/RenderizadorPdf.cs]
archivos_prohibidos: [todo lo demás]
rama: fase-b/b5c-buscadero-dibujo-nucleo
---

# B-5c — Buscadero dibuja los PDF con el núcleo

## Objetivo

Que `RenderizadorPdf` de Buscadero use el dibujo de PDF del núcleo de Hormiguero en vez de llamar directo a la librería, para que todas las apps dibujen igual. **Su API pública no cambia** (`DpiBase`, `ObtenerTotalPaginas`, `RenderizarPagina` con el mismo `PaginaRenderizada` que entrega PNG). Las pruebas de Buscadero (121) deben pasar sin cambiarlas.

## Contenido exacto

En `src/Hormiguero.Buscadero.Core/Pdf/RenderizadorPdf.cs`:

1. Lee el archivo completo en memoria (`File.Open(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)` y copiar a un `MemoryStream`; nunca lo dejes bloqueado).
2. `ObtenerTotalPaginas(ruta)` → `Hormiguero.Nucleo.Pdf.DibujoPdf.Paginas(bytes)`.
3. `RenderizarPagina(ruta, indicePagina)` → `Hormiguero.Nucleo.Pdf.DibujoPdf.Dibujar(bytes, indicePagina, DpiBase / 96.0)` devuelve `ImagenPagina(int Ancho, int Alto, byte[] PixelesBgra)`. Conviértelo a PNG con SkiaSharp (ya está disponible): `SKBitmap` de `Ancho × Alto` con `SKColorType.Bgra8888` y `SKAlphaType.Premul`, copia los píxeles (por ejemplo con `Marshal.Copy` a `GetPixels()`), y codifica a PNG calidad 90 como hoy. `Ancho` y `Alto` del resultado = los de la imagen.
4. No uses más `PDFtoImage.Conversion` en este archivo.

## Criterio de término

- `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores.
- Escribe tu reporte al final de esta nota.

## Reporte del agente

Hecho. `RenderizadorPdf` de Buscadero ya no llama directo a `PDFtoImage`; ahora usa `Hormiguero.Nucleo.Pdf.DibujoPdf`.

Cambios en `src/Hormiguero.Buscadero.Core/Pdf/RenderizadorPdf.cs` (único archivo tocado):

- `ObtenerTotalPaginas(ruta)` lee el archivo completo a un `byte[]` (con `FileShare.ReadWrite | FileShare.Delete` y copia a `MemoryStream`) y llama a `DibujoPdf.Paginas(bytes)`.
- `RenderizarPagina(ruta, indicePagina)` llama a `DibujoPdf.Dibujar(bytes, indicePagina, DpiBase / 96.0)` y convierte la `ImagenPagina` (BGRA premultiplicado) a PNG con SkiaSharp: `SKBitmap` `Bgra8888`/`Premul`, `Marshal.Copy` a `GetPixels()` y encode PNG calidad 90.
- La API pública no cambió (`DpiBase`, `ObtenerTotalPaginas`, `RenderizarPagina`, `PaginaRenderizada`), y las pruebas de Buscadero pasaron sin modificarlas.

Verificación:

- `dotnet build`: 16 proyectos, 0 errores, 0 advertencias.
- `dotnet test`: 561 pruebas pasando en 6 proyectos (incluye las 121 de Buscadero).
- `dotnet csharpier check .`: 220 archivos, sin errores de formato.
