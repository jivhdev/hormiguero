---
tipo: adr
app: Hormiguero (todas)
numero: 1
fecha: 2026-10-03
estado: aprobada # propuesta | aprobada | reemplazada
---

# ADR-001: Cómo comparten datos las apps de Hormiguero

## Contexto

D-18 exige que cada app funcione sola, pero que nazca lista para compartir datos con las demás (documentos reconocidos, clientes y proveedores, plantillas). Las apps corren en Windows de gama baja, sin servidor y sin internet obligatorio. Varias pueden estar abiertas a la vez en el mismo equipo (Archivero archivando mientras Buscadero busca).

Hechos verificados en la documentación oficial de SQLite:
- En modo WAL, varios procesos del **mismo equipo** pueden leer mientras otro escribe; escribe uno a la vez ([sqlite.org/wal.html](https://sqlite.org/wal.html)).
- WAL no funciona en carpetas de red, y en general una base SQLite en una carpeta compartida puede corromperse ([sqlite.org/useovernet.html](https://sqlite.org/useovernet.html)).

## Opciones consideradas

| Opción | A favor | En contra |
|---|---|---|
| **A. Una base SQLite común, local en cada equipo** (`%LOCALAPPDATA%\Hormiguero\hormiguero.db`, modo WAL), administrada por el núcleo | La integración es automática: todas leen lo mismo. Sin servidor ni procesos extra. Rápida en gama baja. SQLite ya está validado en Archivero y Buscadero | Escribe una app a la vez (suficiente para un usuario). Las migraciones deben ser compatibles entre versiones de distintas apps |
| B. Una base por app + intercambio por archivos | Cada app aislada | Hay que sincronizar y duplicar datos; más código y más formas de que se desordene |
| C. Un servicio local dueño de la base; las apps le piden datos | Un solo dueño de los datos | Un proceso más corriendo en un equipo modesto; instalación y fallas más complejas |
| D. Servidor de base de datos (PostgreSQL) | Varios equipos en red | Requiere servidor y administración; contradice "sin servidor" y gama baja |

## Decisión

**Opción A** (aprobada por Javier, D-47).

- El núcleo (`Datos`) es el único código que abre la base. Ninguna app escribe SQL contra tablas que no son suyas.
- Tablas **comunes** (las administra el núcleo): documentos, emisores (clientes y proveedores), plantillas de reconocimiento, configuración general, feriados.
- Tablas **propias** de cada app, con su prefijo (por ejemplo `archivero_`, `buscadero_`).
- Si una app se instala sola, crea la base con las tablas que necesita. Cuando llega otra app, encuentra la base y suma las suyas.
- **Migraciones solo hacia adelante y solo agregando**: nunca se borra ni se renombra una columna que otra app pueda estar leyendo.
- La base vive siempre en el disco local; los documentos pueden estar en red o en Drive.
- Respaldo: copia diaria automática de la base en la misma carpeta, guardando las últimas 7.

## Consecuencias

- Integración desde el día uno sin esfuerzo extra: Buscadero ve lo que Archivero reconoció.
- Cada equipo tiene su propia base. **Queda abierto:** compartir datos entre equipos (tus dos PC, o compañeros de oficina). No entra en las aperturas; se decide con un Caso cuando haga falta.
- Las pruebas automáticas del núcleo deben cubrir dos apps abiertas a la vez escribiendo en la base.
