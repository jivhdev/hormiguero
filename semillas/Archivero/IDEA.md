---
tipo: idea
app: Archivero
fecha: 2026-10-03
estado: borrador
---

# Idea — Archivero

<!-- Fase 1 (MQD, sección 3). Borrador armado con la idea de Archivero de MQD v1 (2026-09-12), los arreglos pendientes, el Caso-7 y lo que hacía Facturas JCV. Los puntos marcados ⚠️ esperan decisión de Javier. -->

Rol en Obrera: **entrada** — lo que llega (D-22). Absorbe Archivero, Facturas JCV / ExtractorCobelcar (pasa a ser una configuración), la impresión automática al archivar y la carga de certificados.

## Lo que NO quiero

- Una app compleja de entender para un usuario básico.
- Un menú con muchas opciones: la pantalla principal es simple y las cosas aparecen una a una cuando corresponde.
- Que configurar o identificar un documento sea difícil, o que el usuario tenga que recordar pasos: todo se guía visualmente, paso a paso.
- Un menú desordenado como el del programa anterior: la configuración es un asistente que no deja ningún dato afuera.
- Que se note hecho a la rápida: debe verse profesional.
- Lógica de negocio precargada: todo es configurable, para cualquier oficina (incluso la hermana de Javier).
- Clasificaciones a medias: un documento se archiva solo con coincidencia exacta y completa.
- Perder un archivo, nunca (mover = copiar, verificar y recién borrar).
- Que se caiga cuando llega un documento ya guardado (arreglo pendiente de la v1).

## Lo que más o menos SÍ quiero

- Que vigile una carpeta de entrada: cuando llega un PDF ya identificado, lo renombra y lo guarda solo en su carpeta, respetando año y mes según el formato configurado.
- Que si no reconoce un documento, pida identificarlo (en el momento o después) y desde ahí aprenda: marcando con el mouse, en un PDF de ejemplo, las zonas donde están los datos.
- Que pregunte paso a paso todo lo necesario: qué identifica al documento, con qué nombre se guarda, a qué grupo pertenece, dónde y cómo se guarda.
- Que los documentos sin texto (escaneados) y los dañados queden visibles para guardarlos a mano, nunca escondidos.
- Guardado rápido con opción de conservar la fecha (arreglo pendiente).
- Imprimir al archivar, configurable por tipo de documento: desactivado por defecto; solo la primera página, preguntar cada vez o todas; una impresora para toda la app (Caso-7).
- Enlazar el documento a un cliente antes de guardarlo (programas futuros).
- Cargar los certificados de medidores a una carpeta e indexar sus datos (certificados).
- Que reconozca los datos que Buscadero necesita para enlazar (emisor, tipo, fecha, número enlazante) y los deje en la base común (D-18, ADR-001).
- Carpetas locales, de red o de Drive.
- Gratis, profesional, rápido en equipos de gama baja, con algo de personalización visual.
- ⚠️ Vigilar carpetas de red de un proveedor (lo que hacía Facturas JCV, que revisaba el servidor cada 30 segundos). Choca con la regla del "vecino silencioso" (MQD, sección 9).
- ⚠️ ¿Un "modo simple" para quien solo quiere archivar, sin la configuración extra que alimenta a Buscadero? (Idea de Motores, v1.)

## Quién la usa

Un oficinista en su propio equipo: primero Javier y sus compañeros, después cualquiera que lo instale.

## ¿Toca algo fuera del equipo? (red, carpetas compartidas, internet)

Sí: guarda en carpetas de Drive, de red o del servidor del ERP, y puede vigilar carpetas de un proveedor en red. Aplica la regla del "vecino silencioso".

## Cierre

<!-- Regla de cierre (D-28): al menos un "no quiero" y un "sí quiero", confirmados por Javier. -->
