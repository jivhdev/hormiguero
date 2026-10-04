# Diseño B-6: marcas y cadenas en la base común

**Alcance:** diseño para B-6; no implementa cambios. La base objetivo es `%LocalAppData%\Hormiguero\hormiguero.db`, administrada por Nucleo (ADR-001). Buscadero y Archivero parten sin migrar sus datos anteriores (D-68).

## 1. Estado actual

- **Índice común:** `documentos` y `numeros_documento`, migración 2, [Migraciones.cs](../src/Hormiguero.Nucleo/Datos/Migraciones.cs#L13). `Documentos.Guardar` reemplaza los números del documento al reindexar y `Documentos.Quitar` borra la fila; [Documentos.cs](../src/Hormiguero.Nucleo/Datos/Documentos.cs#L26) y [Documentos.cs](../src/Hormiguero.Nucleo/Datos/Documentos.cs#L132). `numeros_documento` no distingue OCC, NVV u otro tipo: conserva número, prefijo, sufijo y origen.
- **Marcas de Buscadero:** base propia indicada al crear `RepositorioMarcas`; tabla `Marcas` con ruta clave, página, tipo, rectángulo y texto, e índice por ruta. [RepositorioMarcas.cs](../src/Hormiguero.Buscadero.Core/Marcas/RepositorioMarcas.cs#L6). Cada edición reemplaza todas las marcas de esa ruta en una transacción; [SesionMarcas.cs](../src/Hormiguero.Buscadero.Core/Marcas/SesionMarcas.cs#L104).
- **Cadenas de Buscadero:** base propia indicada al crear `RepositorioLineas`; modelos (`PlantillasLinea`, `PlantillaVagones`) e instancias/documentos (`InstanciasLinea`, `InstanciaVagones`) con padre, orden, ruta y nombre. [RepositorioLineas.cs](../src/Hormiguero.Buscadero.Core/Lineas/RepositorioLineas.cs#L18). Hay cadenas hijas y ramas (`EsMultiple`, `CadenaMadreId`, `InstanciaVagonPadreId`); [ServicioLineas.cs](../src/Hormiguero.Buscadero.Core/Lineas/ServicioLineas.cs#L135) y [ServicioLineas.cs](../src/Hormiguero.Buscadero.Core/Lineas/ServicioLineas.cs#L244). No se enlazan aún por un número comercial común.
- **Configuración de Archivero:** hoy vive en su base propia (`Configuraciones`, `PatronesReconocimiento`, `Marcas`), [BaseDeDatos.cs](../src/Hormiguero.Archivero/Datos/BaseDeDatos.cs#L23). Las marcas configurables son `Emisor`, `Tipo`, `Fecha` y `NombreArchivo`; [Modelos.cs](../src/Hormiguero.Archivero/Datos/Modelos.cs#L23). No identifica campos `OCC`, `NVV`, `OCL`, guía ni factura. La base común ya tiene `identificaciones(tipo, emisor, datos JSON, actualizada)` y su acceso desde Nucleo, migración 3; [Migraciones.cs](../src/Hormiguero.Nucleo/Datos/Migraciones.cs#L36), [Identificaciones.cs](../src/Hormiguero.Nucleo/Datos/Identificaciones.cs#L7). El JSON no tiene hoy un contrato de campos de enlace.
- **Concurrencia y registro:** `BaseComun.Abrir` activa WAL, `busy_timeout=5000` y claves foráneas; [BaseComun.cs](../src/Hormiguero.Nucleo/Datos/BaseComun.cs#L14). `auditoria` registra fecha, app, acción, origen/destino, huella y resultado (migración 3). `documentos_guardados` es un registro de eventos de solo agregado (migración 4).
- **Referencia histórica opcional:** la antigua especificación de Motores enumera campos variables OCC (`OCC`, `NVV`, `OCL`, `DESPACHO`), GRC (`OCC`, `NVV`) y FCC (`OCC`, `NVV`, `GRC`), además de reglas de emparejamiento FCC/NCC con GRC/GRCF y FCV con OCL/NVV/COV. Es antecedente, no una configuración vigente ni un contrato aprobado para Hormiguero: `motores/bitacora/historial/Tablas maestras - formato de intercambio entre motores.md` y `motores/Fase3-Vinculacion/PLAN.md`.

## 2. Modelo propuesto: migración v5

Todas estas tablas se crean desde `Hormiguero.Nucleo`; ninguna app ejecuta SQL directo contra la base común. Sin `CASCADE`: las referencias deben impedir el borrado accidental. Las entidades que representan hechos llevan estado/fecha de baja en vez de borrarse.

| Tabla | Columnas esenciales, claves e índices |
|---|---|
| `versiones_documento` | `id` PK; `documento_id` FK a `documentos(id)`; `huella`, `ruta_observada`, `registrada_en`, `vigente`. Índice único parcial de una versión vigente por documento e índice por `huella`. Conserva versiones cuando cambia el contenido; mover/renombrar con la misma huella conserva la identidad y actualiza la ruta observada. |
| `marcas_documento` | `id` PK; `version_id` FK; `tipo`, `pagina`, `x`, `y`, `ancho`, `alto`, `texto`, `creada_en`, `anulada_en`. Índice `(version_id, anulada_en, pagina)`. Anular una marca conserva el registro. |
| `valores_documento` | `id` PK; `version_id` FK; `tipo_documento`, `campo`, `valor_original`, `valor_clave`, `origen` (`automatico`/`manual`), `identificacion_id` nullable, `confianza` nullable, `estado`, `creado_en`. Índices `(campo, valor_clave, estado)` y `(version_id, campo)`. Aquí van números y otros campos extraídos; se conserva el original y la clave para búsqueda. |
| `cadenas_documentales` | `id` PK; `tipo_ancla`, `numero_ancla`, `numero_clave`, `estado`, `creada_en`, `actualizada_en`. `UNIQUE(tipo_ancla, numero_clave)` para anclas únicas; índice por la misma pareja para consulta. La OCC es el ancla de la cadena principal; los tipos de ancla permiten otros números comunes si se confirma que hacen falta. |
| `miembros_cadena` | `id` PK; `cadena_id` FK; `version_id` FK; `tipo_documento`, `rol`, `documento_padre_id` nullable FK a esta misma tabla, `origen` (`automatico`/`manual`), `estado`, `creado_en`, `anulado_en`. Índices `(cadena_id, estado)` y `(version_id, estado)`; unicidad parcial de miembro activo por cadena y versión. `documento_padre_id` admite ramas y varias guías/facturas bajo la misma OCC. |
| `reglas_enlace` | `id` PK; `tipo_origen`, `campo_origen`, `tipo_destino`, `campo_destino`, `campo_ancla`, `longitud_minima`, `activa`. Índice por tipo/campo origen y destino. Reglas administrables desde la configuración de Archivero; inicialmente vacía hasta acordar el mapa de campos. |

La consulta de una OCC busca `cadenas_documentales` por `(tipo_ancla, numero_clave)` y obtiene miembros con `miembros_cadena(cadena_id, estado)`, luego los datos del índice por `version_id/documento_id`. No requiere recorrer todos los PDF ni comparar rutas. SQLite no garantiza milisegundos para cualquier volumen, pero las búsquedas puntuales quedan respaldadas por índices selectivos.

Las futuras alertas pueden referir `cadena_id` o `miembro_id` y guardar fecha base, cantidad, tipo de días, fecha de vencimiento y estado. El cómputo de días queda en Utilidades del núcleo (D-41); B-6 no crea ni calcula alertas.

## 3. Enlace automático

Una regla compara el valor de un campo con el del campo objetivo/ancla configurado. La tabla siguiente refleja lo escrito hoy y el antecedente, y distingue lo pendiente de aprobación:

| Documento/campo de referencia | Documento/campo que podría coincidir | Estado |
|---|---|---|
| OCC.`NUMERO_DOCUMENTO` | NVV.`OCC`; OCL.`OCC`; guía.`OCC`; factura.`OCC` | Flujo deseado en el bloque (OCC → NVV → guía → factura), pero los nombres de campo por tipo no existen aún en Archivero. |
| GRC.`OCC` / FCC.`OCC` | OCC.`NUMERO_DOCUMENTO` | Mapa documentado en el antecedente Motores; requiere validar nombres y tipos antes de habilitarlo. |
| FCC o NCC | GRC o GRCF de la misma OCC | Emparejamiento antecedente, no regla confirmada para este esquema. |
| FCV | OCL; si no existe, NVV; si no, COV | Orden antecedente; confirmar si aplica a Buscadero. |

Solo se crea el enlace automático con coincidencia exacta de `valor_clave`, una regla activa y un candidato inequívoco dentro del ancla; varios candidatos, baja confianza, dato inválido o referencia sin resolver quedan como pendientes/dudosos, nunca se fuerzan. El número corto no se enlaza automáticamente: `longitud_minima` se define por regla y queda sin valor aprobado hasta que Javier lo confirme. La clave debe normalizar espacios/separadores acordados, conservar `valor_original` y **no quitar ceros iniciales**: `00123` y `123` no son equivalentes salvo regla explícita. No usar coincidencia parcial ni comparar solo sufijos.

El enlace manual crea/anula un miembro o su relación padre, guarda el origen y registra la acción en `auditoria`. Para reemplazo de contenido se cierra la versión anterior, se crea otra y se dejan sus marcas/enlaces históricos; el contenido nuevo no hereda enlaces automáticamente hasta revalidar sus valores. Para un movimiento con misma huella se conserva la identidad y se actualiza la ruta. Si no hay huella fiable, requiere revisión manual. La política de detección de reemplazo y de preservar relaciones requiere una prueba de caso antes de activar.

## 4. Concurrencia, integridad y auditoría

Archivero escribe datos/configuración y Buscadero consulta usando conexiones del núcleo. WAL ya permite lectores junto a un escritor; `busy_timeout=5000` espera bloqueos breves. Escrituras de versión + valores + enlace deben ser una sola transacción; los lectores consultan por ID/índice. No se guardan bases en red.

Toda anulación, corrección de valores, cambio de regla, enlace/desenlace, movimiento o reemplazo registra evento en `auditoria` con app, acción, identificadores, rutas/huellas pertinentes y resultado. Las operaciones son anulaciones/versiones nuevas; no borrados físicos en marcas, valores, cadenas ni miembros. El índice `documentos` actual sí tiene borrado físico por `Documentos.Quitar`; antes de asociarle datos de negocio B-6b debe cambiarse a baja lógica o proteger la fila referenciada y auditar la baja. Los registros históricos de auditoría no se eliminan desde estas funciones.

## 5. Bloques de implementación sugeridos

Rutas y pruebas son propuestas para los siguientes bloques; cada bloque conserva la condición de compilación/pruebas verdes. “Trivial para OpenCode Go” significa alcance mecánico con decisiones ya fijadas, no exime revisión.

| Bloque | Alcance y archivos probables | Pruebas | Dificultad |
|---|---|---|---|
| B-6b | Cerrar con Javier los nombres de campos/reglas, normalización, largo mínimo, identidad de versión y baja lógica. Actualizar `definicion/DISENO-B6-MARCAS-Y-CADENAS.md` o abrir decisión explícita antes de código. | Casos tabulares acordados como criterios en la nota. | No trivial: decisiones de negocio. |
| B-6c | Migración v5 y acceso desde Nucleo: `src/Hormiguero.Nucleo/Datos/Migraciones.cs`, nuevos repositorios bajo `Datos/`; agregar `tests/Hormiguero.Nucleo.Tests/` para esquema, índices, integridad y WAL. | Migración idempotente, claves foráneas, escritura con lector abierto, auditoría de baja. | No trivial: migración e identidad histórica. |
| B-6d | Valores/reglas de enlace en Nucleo; acceso común de Archivero para publicar configuración/campos. `Identificaciones.cs` y clases nuevas de `Nucleo/Datos`; luego adaptador Archivero. | Normalización sin falsos positivos, ceros iniciales, números cortos, ambigüedad y rechazo de regla no configurada. | No trivial: extracción y datos reales. |
| B-6e | Repositorio de marcas Buscadero en Nucleo; adaptar `src/Hormiguero.Buscadero.Core/Marcas/RepositorioMarcas.cs` y sesión. | `SesionMarcasTests.cs`: guardar, editar, anular, versión reemplazada y documento movido. | Trivial después de B-6c. |
| B-6f | Cadenas/miembros y consulta por número en Nucleo; adaptar `src/Hormiguero.Buscadero.Core/Lineas/RepositorioLineas.cs` y `ServicioLineas.cs`. | `LineasTests.cs`: enlace manual/automático, OCC con ramas múltiples, consulta por OCC y anulación auditada. | No trivial: cardinalidad y comportamiento existente. |
| B-6g | Activar reglas acordadas e integrar la lectura de configuración; dejar contrato para futura referencia de alertas, sin UI ni cómputo de vencimientos. | Casos por tipo/campo y regresión conjunta Archivero/Buscadero. | No trivial: validación funcional de Javier. |

No son triviales para OpenCode Go los bloques B-6b, B-6c, B-6d, B-6f y B-6g. B-6e puede asignarse como bloque sencillo solo después de aprobar el esquema y las pruebas de B-6c.

## 6. Preguntas para Javier

1. ¿Confirmas los mapas del antecedente Motores (incluido FCV → OCL/NVV/COV) o indicas los campos vigentes por tipo?
2. ¿Qué longitud mínima debe exigir cada tipo de número para permitir enlace automático?
3. ¿Se puede enlazar manualmente un documento a más de una cadena OCC activa?
4. Si cambia el contenido del PDF en la misma ruta, ¿se conserva la relación anterior como histórica y el reemplazo queda pendiente de confirmar?

## Reporte del agente

- Entregable: diseño redactado en esta nota; no se modificó código ni otro archivo.
- Verificación: `dotnet build Hormiguero.slnx` correcto, 0 advertencias y 0 errores. `dotnet test Hormiguero.slnx` correcto: 583 pruebas aprobadas, 0 fallidas, 0 omitidas (105 Mensajero, 68 Nucleo, 9 Diseño, 121 Buscadero, 280 Archivero).
- Decisiones que deben resolverse antes de implementar: mapa de campos y reglas por tipo, umbrales de números cortos, cardinalidad manual y política de reemplazo (preguntas anteriores).
