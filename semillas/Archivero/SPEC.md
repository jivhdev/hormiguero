---
tipo: spec
app: Archivero
version: 0.1 (apertura, paso 2)
fecha: 2026-10-04
estado: borrador
---

# SPEC — Archivero, apertura (paso 2)

<!-- Fase 4 (MQD, sección 3). Alcance: apertura según PROTOTIPO.md (elementos marcados paso 2) e IDEA.md. Criterios y medidas propuestos por Claude; los aprueba Javier. Usa el núcleo (semillas/Nucleo/SPEC.md). Datos de prueba: los mismos 52 archivos del Descubrimiento de Buscadero (C:\JV\pruebas), solo metadatos (D-11). -->

## 1. Intención

Que un oficinista deje caer sus PDF en una carpeta de entrada y Archivero los guarde solos, con el nombre y en la carpeta correctos, sin perder nunca un archivo; y que lo que no reconoce se lo enseñe una sola vez, marcando con el mouse.

## 2. Requisitos

### REQ-001 Carpetas vigiladas (A1)

- **Given** Archivero abierto por primera vez · **When** no hay carpetas vigiladas · **Then** pide elegir al menos una carpeta de entrada (local, de red o Drive).
- **Given** una carpeta vigilada · **When** llega un PDF · **Then** Archivero lo revisa sin que el usuario haga nada, usando los avisos de cambio de Windows (sin consultar a cada rato, D-52).
- **Given** Archivero cerrado · **When** llegan PDF y luego se abre · **Then** revisa lo que llegó mientras estaba cerrado.
- **Given** un PDF que se está copiando todavía · **When** llega el aviso · **Then** espera a que termine de copiarse antes de leerlo.
- **Given** los avisos de Windows fallan (pasa en algunas carpetas de red) · **Then** revisa solo los archivos nuevos y lo indica en el estado de la carpeta.
- **Given** cualquier momento · **When** el usuario toca "Revisar ahora" · **Then** revisa todas las carpetas vigiladas.
- **Given** una carpeta vigilada que no responde · **Then** se marca "No disponible" y las demás siguen funcionando.

### REQ-002 Reconocer un documento

- **Given** un PDF con texto y una configuración guardada · **When** el texto de **todas** sus zonas coincide exactamente (tipo y emisor) · **Then** el documento queda reconocido con esa configuración.
- **Given** un PDF que coincide solo en parte · **Then** no se archiva: queda en "Por reconocer" (nada de clasificaciones a medias).
- **Given** un PDF que coincide con dos configuraciones · **Then** no se archiva: queda en "Por reconocer" con el aviso "coincide con dos configuraciones".
- **Given** un PDF sin texto (escaneado) o dañado · **Then** queda visible en "Sin texto o dañados" para guardarlo a mano (A3), nunca escondido.
- **Given** un archivo que no es PDF · **Then** se ignora.

### REQ-003 Asistente de identificación (A2, pasos 1 a 5)

- **Given** un documento en "Por reconocer" · **When** se toca "Identificar" · **Then** se abre el asistente con ese PDF siempre a la vista, un paso por pantalla.
- **Given** el paso 1 (Tipo), 2 (Emisor) o 3 (Número) · **When** el usuario marca un rectángulo sobre el PDF · **Then** se muestra al instante el texto que quedó dentro, para confirmar que es el correcto.
- **Given** el paso 2 · **When** el usuario escribe el emisor · **Then** se autocompletan los emisores ya usados.
- **Given** el paso 4 (Dónde se guarda) · **When** elige carpeta madre y forma (año\añomes, solo año o directo) y si el mes sale de hoy o de una fecha marcada en el PDF · **Then** ve la vista previa de la ruta para el mes anterior, este y el siguiente.
- **Given** el paso 5 (Nombre) · **When** elige dato marcado, nombre original o una combinación · **Then** ve el nombre final del PDF de ejemplo.
- **Given** el asistente terminado · **When** se guarda la configuración · **Then** se revisan solos todos los documentos de "Por reconocer" con la configuración nueva.
- **Given** cualquier paso · **When** el usuario cierra el asistente · **Then** no queda ninguna configuración a medias.

### REQ-004 Guardar sin perder nunca un archivo

- **Given** un documento reconocido · **When** se guarda · **Then** se copia al destino, se verifica que la copia tenga la misma huella (N-007) y recién entonces se borra el original.
- **Given** la copia falla o su huella no coincide · **Then** el original queda donde estaba, se borra la copia mala y se avisa.
- **Given** un número con ceros a la izquierda (`000104523`) · **When** se arma el nombre · **Then** el nombre lleva el número sin ceros (`104523`).
- **Given** el destino ya tiene un archivo con la misma huella · **Then** no se guarda: queda en "Ya guardados antes" (REQ-006). Nunca se cae (arreglo pendiente de la v1).
- **Given** el destino ya tiene un archivo con el mismo nombre y **distinto** contenido · **Then** nunca se sobrescribe: ver tema abierto 1.
- **Given** cada movimiento (guardar, descartar, guardar a mano) · **Then** queda en el registro de auditoría: fecha y hora, origen, destino, huella y resultado.

### REQ-005 Guardar a mano (A3)

- **Given** un documento sin texto o dañado · **When** se toca "Guardar a mano" · **Then** se ve en un visor con zoom.
- **Given** A3 · **When** el usuario elige una ubicación existente (las de las configuraciones) o "Crear ubicación", edita el nombre y la fecha y toca "Guardar" · **Then** se guarda con las mismas reglas de REQ-004.

### REQ-006 Ya guardados antes

- **Given** un documento en "Ya guardados antes" · **When** se abre · **Then** se ven lado a lado el que llegó y el que ya estaba, con zoom.
- **Given** esa comparación · **When** se toca "Descartar" · **Then** el que llegó se descarta según el tema abierto 2, y queda en el registro de auditoría.

### REQ-007 Guardados recientes y configuraciones (A1)

- **Given** documentos guardados · **Then** A1 muestra los recientes (documento → carpeta destino) con "Abrir" y "Carpeta", y un historial completo.
- **Given** "Configuraciones" · **Then** se ven y se editan por emisor y tipo; editar una abre el asistente con sus datos.
- **Given** la barra superior · **Then** se puede cambiar entre modo simple y completo (en la apertura los dos se ven igual; lo del modo completo llega en el paso 5).

## 3. Requisitos no funcionales

| ID | Atributo | Estímulo | Entorno | Respuesta | Medida numérica |
|---|---|---|---|---|---|
| RNF-1 | Rapidez | Llega un PDF reconocible a una carpeta local | AMD A12, 15 GB RAM | Queda guardado en su destino | ≤ 3 s |
| RNF-2 | Vecino silencioso | Carpeta de red vigilada sin cambios | Red de oficina | Lecturas a la red | 0 consultas periódicas mientras funcionen los avisos de Windows |
| RNF-3 | Respuesta al marcar | Marcar una zona en el asistente | Mismo equipo | Muestra el texto de la zona | ≤ 0,5 s |
| RNF-4 | Arranque | Abrir Archivero | Mismo equipo | Pantalla principal lista | ≤ 3 s |
| RNF-5 | Memoria | Vigilando 3 carpetas, un documento abierto | Mismo equipo | Uso de memoria | ≤ 300 MB |
| RNF-6 | Seguridad de los archivos | 1.000 guardados seguidos con cortes forzados | Pruebas automáticas | Archivos perdidos o dañados | 0 |

## 4. Lista de listo para construir

- [ ] Cada requisito tiene al menos un Given/When/Then, propuesto por la IA y aprobado por Javier (D-09).
- [x] Cada requisito no funcional tiene una medida numérica.
- [x] La sección "Qué NO construir" no está vacía.
- [ ] Una vuelta completa de "¿Qué tal si...?" no encontró nada nuevo.
- [ ] Toda decisión con ventajas y desventajas reales tiene su ADR en `decisiones/` (pendiente: dónde se guardan las configuraciones y el registro de auditoría).
- [x] Ningún nombre visible para el usuario quedó elegido por la IA (textos del boceto aprobado, D-58).

## 5. Qué NO construir (en la apertura)

- Guardado rápido con atajos y conservar la fecha, mes forzado a mano, confirmar el nombre cada vez (paso 5).
- Fecha y número enlazante, cliente, imprimir al archivar, abrir después de guardar, marca de impreso (paso 5).
- Modelos de cadena (paso 5), certificados y tiempo ahorrado del mes (paso 8).
- Leer escaneados con OCR.
- Lógica de negocio precargada: ninguna configuración viene de fábrica.

## 6. Decisiones

- ADR-001, ADR-002 y ADR-003 del ecosistema. Pendiente ADR-001 de Archivero (configuraciones y auditoría).

## 7. Temas abiertos (decide Javier)

1. **Mismo nombre, distinto contenido en el destino.** Propuesta: no guardar y dejarlo en un aviso para que el usuario elija (nunca sobrescribir ni inventar un nombre).
2. **"Descartar" un repetido.** Propuesta: enviarlo a la Papelera de reciclaje de Windows (se puede recuperar), no borrarlo para siempre.
3. **"Cedibles descartadas a la vista"** (A1 del boceto): falta precisar qué hace Archivero con una cedible que llega.
