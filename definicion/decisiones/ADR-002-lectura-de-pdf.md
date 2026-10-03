---
tipo: adr
app: Hormiguero (núcleo)
numero: 2
fecha: 2026-10-03
estado: propuesta # propuesta | aprobada | reemplazada
---

# ADR-002: Librerías para leer y mostrar PDF

## Contexto

El núcleo debe (1) mostrar páginas de PDF con zoom y rotación (visor de Buscadero, Archivero y Mensajero), (2) imprimir páginas, y (3) leer el texto con sus coordenadas (buscar números dentro del texto, C6; extraer zonas marcadas en Archivero). Restricciones: gratis, compatible con GPL v3 (D-27), mantenida, .NET 10, equipos de gama baja.

Datos verificados en GitHub el 2026-10-03:

| Librería | Licencia | Última versión | Uso |
|---|---|---|---|
| PDFtoImage (PDFium + SkiaSharp) | MIT (PDFium: BSD-3) | v5.4.0, 2026-08-16 | Dibujar páginas como imagen |
| PdfPig | Apache 2.0 | v0.1.16, 2026-08-22 | Texto y palabras con coordenadas |
| Docnet.Core (usada en Archivero v1) | MIT | v2.7.0-alpha, 2023; sin cambios desde 2024 | Dibujar y leer texto |
| PyMuPDF | AGPL | — | Descartada: AGPL |

## Opciones consideradas

| Opción | A favor | En contra |
|---|---|---|
| **A. PDFtoImage para dibujar + PdfPig para texto** | Ambas activas en 2026, licencias permisivas; PdfPig es 100 % .NET y da palabras con coordenadas exactas | Dos librerías en vez de una |
| B. Docnet.Core para todo | Ya validada en Archivero v1 | Sin mantenimiento desde 2024: riesgo con .NET 10 |
| C. Solo PDFium (dibujar y texto) | Una sola librería | La extracción de texto con coordenadas de PDFium es más tosca que la de PdfPig |

## Decisión

**Propuesta: opción A.** Ambas se usan solo desde el núcleo (`Hormiguero.Nucleo`), detrás de una interfaz propia, para poder cambiarlas sin tocar las apps.

## Consecuencias

- El visor dibuja cada página como imagen y pinta las marcas en una capa aparte (nunca se toca el PDF original).
- Imprimir = dibujar las páginas pedidas y mandarlas a la impresora de Windows.
- PDFium no admite varias llamadas a la vez: el núcleo serializa el dibujo.
