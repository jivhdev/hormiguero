---
tipo: descubrimiento
app: Buscadero
fecha: 2026-10-03
estado: borrador
---

# Descubrimiento — Buscadero

<!-- Fase 2 (MQD, sección 3). Análisis automático de los 52 archivos de C:\JV\pruebas solo con metadatos (páginas, capa de texto, rotación, hash, forma del nombre). Ninguna IA leyó el contenido de los documentos (D-11). -->

## Historia de uso

1. El oficinista abre Buscadero por primera vez y elige las carpetas donde están sus documentos (locales, de red o Drive).
2. El oficinista escribe el número de un documento (por ejemplo, una OCC o una factura).
3. Buscadero muestra el documento encontrado en el visor, como pantalla principal.
4. El oficinista revisa los documentos relacionados en una pestaña aparte y los compara lado a lado.
5. El oficinista marca el documento (visto, X, círculo, texto) sin modificar el original.
6. El oficinista imprime la primera página o las dos primeras.
7. Buscadero avisa cuando un plazo vence (fecha + días hábiles o corridos).

## Datos de las pruebas

| Hallazgo | Cantidad |
|---|---|
| Archivos en `C:\JV\pruebas` | 52 (49 PDF, 3 PNG) |
| Contenidos distintos | 29 — **23 archivos son copias exactas** en otra carpeta |
| PDF con texto | 39 |
| PDF escaneados (sin texto) | 10 (5 distintos); solo 1 tiene un PDF con texto del mismo número |
| Páginas rotadas | 2 (en escaneados) |
| Nombres con solo un número | mayoría |
| Nombres con prefijo de tipo | `OCC`, `FCV`, `GDV`, `NCV`, `F` |
| Nombres con sufijo | `CEDIBLE` (4), `recibida` (3) |
| Nombres sin ningún número | 2 |
| Largo de los números | de 3 a 10 dígitos |
| Archivos que no son PDF | 3 imágenes PNG (`preview`) |

## Catálogo de casos difíciles

| Caso | Qué tiene de raro | Qué debe pasar (propuesta) | Decisión de Javier |
|---|---|---|---|
| C1. Copias exactas | El mismo documento está en dos carpetas | Mostrar el resultado una vez, indicando todas las carpetas donde está | |
| C2. Escaneado sin texto | No se puede buscar dentro de su contenido | Encontrarlo solo por el número del nombre del archivo; sin OCR en la apertura | |
| C3. Escaneado con par de texto | Una guía firmada escaneada y su original con texto comparten número | Mostrar ambos como el mismo documento en dos versiones | |
| C4. Copia cedible | `FCV123` y `FCV123_CEDIBLE` son el mismo documento | Mostrar el original primero y la cedible como versión secundaria | |
| C5. Prefijos y sufijos en el nombre | `OCC123`, `F123`, `123 recibida` | Buscar "123" encuentra todos; el prefijo sirve como filtro de tipo | |
| C6. Nombre sin número | No se encuentra buscando por número en el nombre | Buscar también el número dentro del texto del PDF (solo si tiene texto) | |
| C7. Número dentro de otro | Buscar "123" no debe traer "41234" | Coincidencia exacta del número completo, nunca parcial | |
| C8. Archivos que no son PDF | Imágenes PNG mezcladas en las carpetas | Ignorarlas en la apertura | |
| C9. Página rotada | Escaneados girados 90° | El visor respeta la rotación del PDF | |

## Puntos calientes

| Punto | Por qué preocupa | Cómo se resuelve |
|---|---|---|
| Velocidad en carpetas de red | En la v1 una búsqueda tardaba minutos; buscar dentro del texto lo hace más lento | Índice local en la base común (ADR-001) que se adelanta mientras no se busca, sin sobrecargar la red (vecino silencioso) |
| Escaneados sin OCR | 4 de 5 escaneados solo se encuentran por el nombre | Decidir si la apertura usa OCR o no (C2) |
| Copias en varias carpetas | Resultados repetidos confunden | C1 |

## Cierre

<!-- Regla de cierre (D-28): cada documento de prueba clasificado y cada caso difícil con una decisión de Javier. -->
