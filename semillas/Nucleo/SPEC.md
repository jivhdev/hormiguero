---
tipo: spec
app: Núcleo (nivel 0 y visor, para la apertura de Buscadero)
version: 0.1
fecha: 2026-10-03
estado: aprobada
---

# SPEC — Núcleo compartido (lo que pide la apertura de Buscadero)

<!-- Fase 4 (MQD, sección 3). Solo se especifica lo que la apertura de Buscadero necesita (D-33). Criterios y medidas propuestos por Claude; aprobados por Javier el 2026-10-03 (D-60). Proyectos: Hormiguero.Nucleo (lógica) y Hormiguero.Diseno (estilo, ADR-003). -->

## 1. Intención

Piezas comunes que cualquier app de Hormiguero usa para guardar datos, leer PDF y verse como parte de la misma familia.

## 2. Requisitos

### N-001 Base de datos común

**Descripción:** el núcleo crea y abre la base común en `%LOCALAPPDATA%\Hormiguero\hormiguero.db` (SQLite, modo WAL) y aplica sus migraciones (ADR-001 del ecosistema).

- **Given** un equipo sin la base · **When** una app abre el núcleo · **Then** se crea la carpeta y la base con todas las migraciones y queda en modo WAL.
- **Given** una base creada por una versión anterior · **When** se abre con una versión nueva · **Then** solo se agregan tablas o columnas; no se borra ni renombra nada.
- **Given** dos procesos abiertos a la vez · **When** uno escribe mientras el otro lee · **Then** ninguno falla.

Aprobado por Javier: sí (D-60)

### N-002 Respaldo diario de la base

- **Given** la última copia tiene más de 24 h · **When** se abre la base · **Then** se crea una copia con fecha en `%LOCALAPPDATA%\Hormiguero\respaldos\` y se borran las que excedan 7.

Aprobado por Javier: sí (D-60)

### N-003 Leer información de un PDF

**Descripción:** con PdfPig (ADR-002) el núcleo informa cantidad de páginas, si cada página tiene texto y las palabras con su posición.

- **Given** un PDF con texto · **When** se lee · **Then** devuelve las páginas, `tieneTexto = true` y sus palabras con coordenadas.
- **Given** un PDF escaneado (sin texto) · **When** se lee · **Then** `tieneTexto = false`, sin error.
- **Given** un PDF dañado o protegido con clave · **When** se lee · **Then** devuelve el estado "dañado" o "protegido", sin lanzar excepción ni cerrar la app.

Aprobado por Javier: sí (D-60)

### N-004 Dibujar una página

**Descripción:** con PDFtoImage (ADR-002) el núcleo dibuja una página como imagen a un zoom dado, respetando la rotación del PDF (C9). Las llamadas se hacen de a una (PDFium no admite simultáneas).

- **Given** una página rotada 90° · **When** se dibuja · **Then** la imagen sale derecha, igual que en un lector de PDF común.
- **Given** zoom 200 % · **When** se dibuja · **Then** la imagen mide el doble que a 100 %.
- **Given** dos pedidos de dibujo al mismo tiempo · **When** se ejecutan · **Then** ambos terminan bien (se atienden en orden).

Aprobado por Javier: sí (D-60)

### N-005 Imprimir páginas

- **Given** un PDF de 5 páginas · **When** se pide imprimir "las 2 primeras" · **Then** se envían solo las páginas 1 y 2 a la impresora predeterminada de Windows.
- **Given** un PDF de 1 página · **When** se pide imprimir "las 2 primeras" · **Then** se imprime solo esa página, sin error.

Aprobado por Javier: sí (D-60)

### N-006 Números en el nombre de un archivo

**Descripción:** separa de un nombre de archivo sus números completos con su prefijo y sufijo (C5, C7).

- **Given** `OCC104523.pdf` · **Then** número `104523`, prefijo `OCC`.
- **Given** `FCV0000025001_CEDIBLE.pdf` · **Then** número `25001` (sin ceros a la izquierda), prefijo `FCV`, sufijo `CEDIBLE`.
- **Given** `104523 recibida.pdf` · **Then** número `104523`, sufijo `recibida`.
- **Given** `informe.pdf` · **Then** sin número, sin error.

Aprobado por Javier: sí (D-60)

### N-007 Huella de un archivo

- **Given** dos archivos con el mismo contenido y distinto nombre · **When** se calcula su SHA-256 · **Then** la huella es igual (C1). El archivo se lee por partes, sin cargarlo entero en memoria.

Aprobado por Javier: sí (D-60)

### N-008 Diseño común

**Descripción:** `Hormiguero.Diseno` sobre el tema Fluent de WPF (ADR-003).

- **Given** Windows en modo claro · **When** abre una app · **Then** el color principal es grafito azul `#2E5C8A` con acento verde hoja (D-36).
- **Given** Windows en modo oscuro · **When** abre una app · **Then** el color principal es verde hoja `#6CC08B` con acento grafito azul `#7FB0E0`.
- **Given** el usuario cambia el modo dentro de la app · **When** elige claro u oscuro · **Then** la app cambia sin reiniciarse y lo recuerda.
- Tamaños mínimos: texto normal 14 px, botones de al menos 40 px de alto (pantallas amplias, D-35).

Aprobado por Javier: sí (D-60)

## 3. Requisitos no funcionales

| ID | Atributo | Estímulo | Entorno | Respuesta | Medida numérica |
|---|---|---|---|---|---|
| N-RNF-1 | Rapidez | Dibujar la primera página de un PDF | Equipo de referencia: AMD A12, 15 GB RAM | La imagen aparece | ≤ 1,5 s para PDF de hasta 5 MB |
| N-RNF-2 | Memoria | Huella de un archivo de 50 MB | Mismo equipo | Se calcula | ≤ 50 MB de memoria extra |
| N-RNF-3 | Disponibilidad | Base de datos abierta por 2 apps | Mismo equipo | Ninguna falla por bloqueo | 0 errores en 1.000 escrituras alternadas |

## 4. Lista de listo para construir

- [x] Cada requisito tiene al menos un Given/When/Then, propuesto por la IA y aprobado por Javier (D-09). Cada uno se convierte en prueba automática.
- [x] Cada requisito no funcional tiene una medida numérica (atributo, estímulo, entorno, respuesta y medida).
- [x] La sección "Qué NO construir" no está vacía.
- [x] Una vuelta completa de "¿Qué tal si...?" no encontró nada nuevo.
- [x] Toda decisión con ventajas y desventajas reales tiene su ADR en `decisiones/`.
- [x] Ningún nombre visible para el usuario quedó elegido por la IA.

## 5. Qué NO construir (en esta etapa)

- Reconocimiento por zonas (llega con Archivero, nivel 1).
- OCR.
- Marcas sobre el PDF (llegan en el paso 4).
- Utilidades de fechas, feriados y RUT (llegan cuando una app las pida).
- Sincronización entre equipos.

## 6. Decisiones

- ADR-001 (datos comunes), ADR-002 (lectura de PDF), ADR-003 (estilo visual) en `definicion/decisiones/`.

## 7. Temas abiertos

- Ninguno por ahora.
