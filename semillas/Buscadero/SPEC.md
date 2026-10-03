---
tipo: spec
app: Buscadero
version: 0.1 (apertura, paso 1)
fecha: 2026-10-03
estado: aprobada
---

# SPEC — Buscadero, apertura (paso 1)

<!-- Fase 4 (MQD, sección 3). Alcance: apertura según D-50 y PROTOTIPO.md (elementos marcados paso 1). Criterios y medidas propuestos por Claude; aprobados por Javier el 2026-10-03 (D-60). Usa el núcleo (semillas/Nucleo/SPEC.md). -->

## 1. Intención

Que un oficinista encuentre y vea en segundos cualquier documento PDF de sus carpetas escribiendo su número, sin cargar la red de su oficina.

## 2. Requisitos

### REQ-001 Primera configuración (P1)

- **Given** Buscadero abierto por primera vez · **When** no hay carpetas · **Then** muestra P1 y "Listo, empezar" está desactivado.
- **Given** P1 · **When** el usuario agrega una carpeta (local, de red o de Drive) · **Then** aparece en la lista y "Listo, empezar" se activa.
- **Given** una carpeta en la lista · **When** el usuario toca "Quitar" · **Then** sale de la lista y sus documentos dejan de aparecer en las búsquedas.
- **Given** una carpeta de red que no responde · **When** se agrega o al abrir la app · **Then** se marca "No disponible" y la app sigue funcionando con las demás.

Aprobado por Javier: sí (D-60)

### REQ-002 Índice (ADR-001 de Buscadero)

- **Given** una carpeta recién agregada · **When** empieza el índice · **Then** se registra cada PDF: ruta, nombre, números del nombre (N-006), tamaño, fecha de modificación, huella (N-007), si tiene texto (N-003) y los números de su texto.
- **Given** un archivo que no es PDF (C8) · **When** se indexa · **Then** se ignora.
- **Given** un PDF dañado o protegido · **When** se indexa · **Then** queda registrado como tal y el índice sigue.
- **Given** un índice ya armado · **When** se vuelve a revisar · **Then** solo se leen los archivos nuevos o con otra fecha de modificación.
- **Given** el índice avanzando · **When** el usuario busca · **Then** el índice se pausa hasta que la búsqueda termina.
- **Given** un archivo indexado que se borró o se movió · **When** se revisa su carpeta · **Then** sale del índice.
- **Given** se corta la red a mitad del índice · **When** vuelve · **Then** el índice retoma donde quedó, sin empezar de cero.
- **Given** un archivo de Drive "solo en línea" (no descargado en el equipo) · **When** se indexa · **Then** se registra solo su nombre; su texto se lee recién cuando el usuario lo abre (no se fuerza la descarga).
- **Given** el índice avanzando · **Then** muestra su estado ("Índice: armándose, 40 %" o "Índice al día · N documentos").

Aprobado por Javier: sí (D-60)

### REQ-003 Buscar por número

- **Given** un documento `OCC104523.pdf` · **When** se busca `104523` · **Then** aparece (C5).
- **Given** un documento `41234.pdf` · **When** se busca `123` · **Then** no aparece (C7: solo el número completo).
- **Given** un PDF con texto cuyo nombre no tiene número y que contiene `104523` en su texto · **When** se busca `104523` · **Then** aparece (C6).
- **Given** un escaneado `5521.pdf` · **When** se busca `5521` · **Then** aparece, encontrado por el nombre (C2).
- **Given** el alcance "Primera coincidencia" · **When** hay coincidencias en varias carpetas · **Then** muestra la primera que encuentra y ofrece "Buscar en todas".
- **Given** el alcance "Carpeta específica" · **When** se busca · **Then** solo se busca en la carpeta elegida.
- **Given** una búsqueda en curso · **When** el usuario toca "Cancelar" · **Then** se detiene en menos de 1 s y quedan los resultados encontrados hasta ese momento.
- **Given** un número que no está en el índice · **When** se busca · **Then** revisa en vivo solo la carpeta más probable y, si no está, muestra "No encontré el documento N en tus carpetas".

Aprobado por Javier: sí (D-60)

### REQ-004 Resultados

- **Given** el mismo PDF (misma huella) en dos carpetas · **When** aparece en los resultados · **Then** se muestra una sola vez con "En 2 carpetas" y la lista de carpetas (C1).
- **Given** `FCV25001.pdf` y `FCV25001_CEDIBLE.pdf` · **When** aparecen · **Then** se muestran como un solo documento con las versiones "original" y "cedible"; se abre la original (C4).
- **Given** un escaneado y un PDF con texto del mismo número y tipo · **When** aparecen · **Then** se muestran como un solo documento con dos versiones (C3).

Aprobado por Javier: sí (D-60)

### REQ-005 Visor

- **Given** un resultado seleccionado · **When** se abre · **Then** el PDF se ve en el centro, con todas sus páginas, zoom y rotación correcta (C9).
- **Given** cualquier uso del visor · **Then** el archivo PDF nunca se modifica (su huella no cambia).
- **Given** un documento abierto en el visor · **When** otra app (Archivero) lo mueve o renombra · **Then** puede hacerlo: el visor lee el PDF en memoria y nunca deja el archivo bloqueado.

Aprobado por Javier: sí (D-60)

### REQ-006 Imprimir

- **Given** un documento abierto · **When** se toca "Imprimir 1ª página" · **Then** se imprime solo la página 1 (N-005).
- **Given** un documento abierto · **When** se toca "Imprimir 2 primeras" · **Then** se imprimen las páginas 1 y 2.
- Por defecto se imprime directo en la impresora predeterminada de Windows. En Configuración se puede elegir otra impresora fija o activar el cuadro de impresión de Windows (D-60).

Aprobado por Javier: sí (D-60)

### REQ-007 Ver en carpeta

- **Given** un documento abierto · **When** se toca "Ver en carpeta" · **Then** se abre el Explorador de Windows en su carpeta con el archivo seleccionado.

Aprobado por Javier: sí (D-60)

## 3. Requisitos no funcionales

| ID | Atributo | Estímulo | Entorno | Respuesta | Medida numérica |
|---|---|---|---|---|---|
| RNF-1 | Rapidez de búsqueda | Buscar un número ya indexado | AMD A12, 15 GB RAM; 20.000 PDF indexados | Muestra resultados | ≤ 1 s |
| RNF-2 | Vecino silencioso | Índice avanzando en una carpeta de red | Red de oficina | Lee carpetas | ≤ 5 carpetas por segundo |
| RNF-3 | Rapidez del visor | Abrir un resultado | Mismo equipo | Se ve la primera página | ≤ 1,5 s para PDF de hasta 5 MB |
| RNF-4 | Arranque | Abrir Buscadero | Mismo equipo, índice ya armado | Pantalla principal lista | ≤ 3 s |
| RNF-5 | Memoria | Un documento abierto | Mismo equipo | Uso de memoria | ≤ 300 MB |

## 4. Lista de listo para construir

- [x] Cada requisito tiene al menos un Given/When/Then, propuesto por la IA y aprobado por Javier (D-09). Cada uno se convierte en prueba automática.
- [x] Cada requisito no funcional tiene una medida numérica (atributo, estímulo, entorno, respuesta y medida).
- [x] La sección "Qué NO construir" no está vacía.
- [x] Una vuelta completa de "¿Qué tal si...?" no encontró nada nuevo.
- [x] Toda decisión con ventajas y desventajas reales tiene su ADR en `decisiones/`.
- [x] Ningún nombre visible para el usuario quedó elegido por la IA. (Los textos de P1 y P2 vienen del boceto aprobado, D-58.)

## 5. Qué NO construir (en la apertura)

- Filtros, autocompletado, historial, solo lectura, vista rápida (paso 4).
- Marcas, comparar, cadena, enlaces automáticos, dudosos, OCR (paso 4).
- Alertas, vencimientos, feriados, ventana aparte de la cadena (paso 7).
- Buscar fuera de las carpetas elegidas.
- Vigilar carpetas con consultas periódicas.
- Usuarios o contraseñas.

## 6. Decisiones

- ADR-001 (Buscadero): índice de búsqueda.
- ADR-001, ADR-002 y ADR-003 del ecosistema.

## 7. Temas abiertos

- Ninguno. Impresión resuelta en D-60. Segunda vuelta de "¿Qué tal si...?" (2026-10-03) sin huecos nuevos.
