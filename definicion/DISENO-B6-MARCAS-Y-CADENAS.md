# Diseño B-6: cadenas configurables y datos comunes

**Alcance:** diseño para llevar a la base SQLite común las cadenas de Buscadero y los datos que Archivero publica para enlazarlas. No fija tipos de documento, campos comerciales ni proveedores. La base es local y la administra Nucleo (D-47, ADR-001). Archivero y Buscadero parten sin migrar configuraciones antiguas (D-68).

## 1. Decisiones que guían el diseño

- Se conserva el modelo actual de Buscadero: modelos de cadena con vagones ordenados, anexos, vagones múltiples que abren cadenas hijas, preferencia de nombre; y cadenas reales derivadas del modelo, con vagones, archivos y relaciones madre/hija.
- Cada vagón puede tener una regla de enlace automático opcional, configurada por la persona. Sin regla no hay enlace automático. Un único resultado seguro se enlaza; cualquier ambigüedad queda para revisión (D-48, D-71).
- Los tipos y datos comparables los define Archivero por configuración de documento. Los datos específicos de Javier/Motores son solo una configuración precargada (D-69, D-70).
- Nucleo es la única puerta a las tablas comunes. Las entidades relacionadas se anulan y auditan; no se borran físicamente. No se migran las bases antiguas de Buscadero ni Archivero (D-68).

## 2. Comportamiento actual que se conserva

### Modelos y vagones

- `LineaModelos.cs` define `PlantillaLinea` (nombre, creación, modelo hijo, preferencia de nombre y vagón que aporta el nombre), `PlantillaVagon` (modelo, padre, orden, nombre, múltiple/anexo y modelo hijo), sus nodos de árbol y los modelos de instancia. `PreferenciaNombreCadena` tiene `Generico`, `PorDocumento` y `Personalizado`; hoy `Personalizado` se guarda, pero se comporta como `Generico`.
- `RepositorioLineas.cs` crea y consulta `PlantillasLinea` y `PlantillaVagones`, ordena los vagones por `Orden`, calcula el orden siguiente entre hermanos y guarda la preferencia de nombre. `PadreId` organiza anexos bajo un documento principal. `ModeloCadenaHijaId` conserva el modelo que sigue a un vagón múltiple. La inicialización de sus tablas y columnas está en las líneas 18–80; las operaciones de modelos, vagones y preferencia, en 117–357.
- `ServicioLineas.cs` valida nombres y relaciones (16–68, 99–220), construye el árbol ordenado (222–240), y al crear una cadena guarda una instantánea de la estructura del modelo y materializa sus vagones (244–287, 607–650). Un anexo cuelga de un principal (367–393). Un vagón múltiple permite crear una cadena hija desde el modelo asociado y registra sus dos referencias (320–365). Se puede vincular y desvincular un archivo del vagón (395–415). La cadena muestra el nombre del archivo del vagón elegido como nombre, con retorno al nombre genérico mientras está vacío (75–97).

### Pantallas y operaciones

- `DialogoModeloGuiado.xaml.cs` guía la creación de un modelo con al menos dos documentos raíz; permite agregar anexos y, al marcar un documento como ramificado, crear o elegir un modelo hijo y definirlo antes de regresar al padre (35–50, 57–149, 154–274, 276–320). Al terminar, pregunta cómo nombrar las cadenas derivadas (322–344).
- `DialogoVagon.xaml.cs` permite nombrar/editar un vagón, marcarlo múltiple o anexo y elegir o crear el modelo hijo (22–70, 72–100). La pantalla de modelos de `MainWindow.xaml.cs` muestra el árbol y deja agregar/editar modelos y vagones (1028–1058, 1828–1950).
- `DialogoPreferenciaNombre.xaml.cs` ofrece nombre genérico, el nombre del documento seleccionado o personalizado; obliga a elegir documento si se selecciona “Por documento” (12–70). `ServicioLineas` actualmente hace que “Personalizado” use el nombre genérico (75–97); la v5 conserva ese comportamiento y no inventa una fórmula personalizada.
- `MainWindow.xaml.cs` muestra las acciones para añadir los anexos que aún no se materializaron y crear cadenas hijas en vagones múltiples (1296–1343). `DialogoVincularCadena.xaml.cs` permite escoger un modelo, crear una cadena vacía, buscar un archivo, localizar en qué cadena está y elegir un vagón destino; impide ocupar un destino que ya tiene archivo (24–48, 64–111, 113–236, 238–302). `MainWindow` abre el diálogo y muestra la cadena resultante (1571–1596). La acción explícita de quitar una cadena hija o un anexo puede advertir que se perderá el trabajo relacionado (1684–1731).

### Correspondencia en la base común

La v5 representa las entidades actuales en tablas comunes. Nombres SQL en minúsculas; las claves son locales a la base. Se agregan tablas e índices, no se renombra ni elimina lo existente.

| Tabla v5 | Campos y comportamiento que representa |
|---|---|
| `modelos_cadena` | Equivale a `PlantillaLinea`: nombre, fecha, `es_modelo_hijo`, preferencia de nombre y `vagon_nombre_id` opcional. La preferencia por documento apunta a un vagón de ese modelo. |
| `vagones_modelo` | Equivale a `PlantillaVagon`: modelo, `padre_id`, orden entre hermanos, nombre, `es_multiple`, `es_anexo` y `modelo_cadena_hija_id`. Restricciones comprueban que el padre/anexo pertenezca al mismo modelo y que el modelo hijo exista. |
| `cadenas` | Equivale a `InstanciaLinea`: modelo de origen opcional, nombre del modelo conservado como referencia, nombre/fecha, y `cadena_madre_id` más `vagon_padre_id` para la relación madre/hija. Conserva una instantánea de estructura para que cambios posteriores al modelo no reescriban cadenas existentes. |
| `vagones_cadena` | Equivale a `InstanciaVagon`: cadena, padre/anexo, vagón de modelo nullable, orden, nombre, indicadores múltiple/anexo y referencia a la versión de documento, nullable hasta enlazar. Conserva el comportamiento de un archivo por vagón y anexos separados. |
| `reglas_vagon` | Cero o una regla activa por vagón de modelo: configuración/tipo de origen, campo origen, vagón y campo de comparación, operación, opciones de normalización y largo mínimo. Desactivada o ausente equivale al comportamiento manual actual. |
| `enlaces_cadena` | Registro de enlace entre vagón y versión de documento, origen (manual/automático), regla si aplica, estado y fechas. El estado permite activo, dudoso, rechazado o anulado; la fila se conserva al deshacer. Índices por versión/estado y vagón/estado. |

Los modelos, sus vagones y las cadenas se identifican por ID y orden; índices por modelo, padre y cadena evitan recorrer toda la base. Claves foráneas se usan sin borrado en cascada. El `EstructuraJson` actual se conserva como instantánea al convertir a la nueva representación; el repositorio del núcleo ofrece árboles equivalentes a `ObtenerArbolPlantilla` y `ObtenerArbolInstancia`. La relación con documentos pasa de una ruta copiada en `InstanciaVagon` a un ID de versión; ruta y nombre se consultan en el índice común. Una cadena hija sigue apuntando a la cadena madre y al vagón múltiple que la abrió; los anexos y nombres de respaldo siguen siendo visibles igual.

## 3. Configuración y valores de documentos

Archivero hoy reconoce `Emisor`, `Tipo`, `Fecha` y `NombreArchivo` (`src/Hormiguero.Archivero/Datos/Modelos.cs`, líneas 23–29). Sus marcas indican zonas a leer; la configuración y patrones viven hoy en su base propia (`src/Hormiguero.Archivero/Datos/BaseDeDatos.cs`, líneas 40–68; guardado de patrones/marcas en `ConfiguracionDocumentoRepository.cs`, 117–140 y 236–280). La tabla común `identificaciones` ya guarda configuración general en JSON (`Migraciones.cs`, líneas 37–55; `Identificaciones.cs`, líneas 5–7 y 33–70), pero todavía no define campos con identidad consultable.

La v5 añade:

- `campos_documento`: campo definido por configuración/tipo (`identificacion_id`), nombre visible y estable, tipo de dato (texto o fecha), activo y origen de lectura. Los campos base son Emisor, Tipo, Fecha y NombreArchivo; la persona puede agregar, por ejemplo, “N° OC” o “N° guía”. El nombre visible puede cambiar sin cambiar el ID del campo.
- `valores_documento`: versión de documento, campo, valor leído original, valor clave para consulta, origen (marca/configuración, observador o corrección manual), confianza si el lector la entrega, estado y fecha. Índices por `(campo_id, valor_clave, estado)` y `(version_id, campo_id)`.
- `versiones_documento`: versión, documento del índice, huella, ruta observada, fecha de registro y estado vigente/anulado. Índice por huella y unicidad parcial de una versión vigente por documento. Huella nueva crea versión; cambio de ruta con la misma huella actualiza la ruta observada conservando identidad, sujeto a revisión si no se puede demostrar que es el mismo archivo.

Archivero publica cada valor leído/corregido y la configuración de campos mediante repositorios de Nucleo. La escritura de versión, valores y registro de publicación va en una transacción. Buscadero consulta por ID e índices. En el futuro modo observador (D-69), Archivero registra ruta y datos leídos en la misma base sin mover ni renombrar el archivo observado; la procedencia distingue la carpeta observada. Puede publicar solo NombreArchivo o los campos opcionales extraídos, como OC y guía. El observador usa los mismos IDs de campo y flujo de confianza que el archivo guardado normalmente.

## 4. Regla de enlace opcional y segura

Una regla se expresa en términos configurables: **“Completar este vagón con documentos de [configuración/tipo] cuando [dato] sea igual a [dato] del vagón [documento relacionado]”**. El campo de comparación puede ser NombreArchivo. La primera versión admite igualdad únicamente; “contiene”, sufijos y coincidencias parciales no son seguras para decidir por sí solas y no se habilitan. Se puede comparar con un dato de otro vagón, incluso de la cadena madre/hija, cuando esa relación existe.

Cada regla incluye:

- Normalizar espacios: sí por defecto (recortar extremos y reducir secuencias internas a un espacio).
- Ignorar guiones: no por defecto. Si se activa, elimina solo el guion normal `-`; no elimina otros signos.
- Ignorar ceros iniciales: no por defecto. “00123” y “123” siguen distintos salvo que la persona lo active.
- Largo mínimo: sugerencia inicial de 6 caracteres significativos, editable por regla. Se cuenta después de las normalizaciones elegidas; por debajo del mínimo no se enlaza automáticamente y queda dudoso. El valor es una protección editable, no una condición fija por tipo.

El motor solo considera valores presentes, válidos y con lectura suficientemente confiable o confirmada por la persona. Con una coincidencia exacta y un único candidato se crea el enlace y un evento de auditoría. Sin coincidencia, con valor corto/incierto, con varios candidatos, con un destino ya ocupado o si cambia una versión, no se reemplaza ni se fuerza nada: se registra como dudoso para revisión. En un vagón marcado múltiple, varios resultados tampoco se aceptan en bloque sin revisión; cada documento queda propuesto individualmente. Aceptar, rechazar o deshacer un enlace deja su historial. Las reglas nuevas no alteran enlaces ya confirmados.

## 5. Flujo sencillo en pantalla

El asistente usa nombres y ejemplos de la configuración elegida, nunca términos como “clave”, “normalización” o “candidato”:

1. **“¿Qué documentos forman esta cadena?”** La persona elige o crea el modelo y ordena sus documentos; puede marcar un anexo o indicar que un documento abre una cadena hija.
2. **“¿Cuándo se completa este documento automáticamente?”** Se puede elegir “No completar automáticamente” o escoger una configuración/tipo, el dato que llega y con qué documento/dato de la cadena compararlo. Se muestra una frase completa antes de guardar.
3. **“¿Cómo deben coincidir los datos?”** Casillas simples para espacios, guiones y ceros iniciales, con una explicación breve y el largo mínimo sugerido. Un ejemplo enseña qué valores se consideran iguales.
4. **“¿Cómo se llamarán las cadenas?”** Nombre genérico o nombre de un documento. Se conserva la opción personalizada actual; hasta que se defina su fórmula, seguirá usando el nombre genérico.

La lista de dudosos muestra los dos documentos, los datos que coinciden o faltan, y por qué no se enlazó solo. Botones: **“Enlazar”**, **“No son el mismo documento”** y **“Ver archivo”**. La cadena muestra **“Deshacer vínculo”** en el menú del documento. Deshacer lo marca como anulado, conserva auditoría y deja libre el vagón; no borra ni mueve el PDF.

## 6. Integridad, auditoría y consulta

- `BaseComun.Abrir` activa WAL, `busy_timeout=5000` y claves foráneas (`src/Hormiguero.Nucleo/Datos/BaseComun.cs`, 14–46). Se mantienen: varias lecturas junto a un escritor, una transacción por operación relacionada y la base fuera de carpetas de red.
- `auditoria` ya registra fecha, app, acción, origen/destino, huella y resultado (migración 3 de `Migraciones.cs`, 37–55). Se registra creación/cambio de valor o regla, enlace, aceptación/rechazo, deshacer, cambio de versión y baja, con IDs y huella disponibles. El historial no se elimina.
- `Documentos.Guardar` hoy actualiza el registro por ruta y reemplaza sus números; `Documentos.Quitar` borra físicamente la fila (`src/Hormiguero.Nucleo/Datos/Documentos.cs`, 26–120 y 132–143). En v5 se agrega estado/fecha de baja a `documentos`; `Quitar` pasa a baja lógica y registra auditoría. No se borra una fila referida por valores, versión o cadena. Las consultas normales excluyen bajas; vistas de auditoría pueden consultarlas.
- Las marcas de Buscadero se llevan a Nucleo con marca por versión, tipo, página, rectángulo, texto, creación y anulación. No se borra al editar o quitar una marca.
- Consultas por documento, dato, cadena o vagón se apoyan en índices de huella, ruta, campo/valor y claves foráneas. Las alertas futuras pueden referir una cadena o enlace y mantener fecha base, cantidad, tipo de día, vencimiento y estado (D-41); B-6 no crea ni calcula alertas.

## 7. Bloques siguientes

Cada bloque mantiene `dotnet build` y `dotnet test` sin errores ni advertencias nuevas. “Trivial para OpenCode Go” indica trabajo mecánico con decisiones resueltas; Claude revisa igual.

| Bloque | Alcance y archivos previstos | Pruebas | OpenCode Go |
|---|---|---|---|
| B-6b | Migración v5 y repositorios de modelos, vagones, cadenas, valores, reglas y enlaces en `src/Hormiguero.Nucleo/Datos/`; ampliar `Migraciones.cs`, `Documentos.cs` y `Auditoria.cs`. Sin tocar pantallas ni adaptar aplicaciones. | `tests/Hormiguero.Nucleo.Tests/`: migración nueva/repetida, claves foráneas, relaciones de rama/anexo, orden, valores/índices, baja lógica, transacciones y WAL. | No trivial: esquema y persistencia relacionados. |
| B-6c | Publicación de campos/valores de Archivero hacia Nucleo: `src/Hormiguero.Archivero/` y pruebas de Archivero/Nucleo. Primero campos base y propios; dejar contrato para modo observador. | Dato original/clave, corrección, campos opcionales, versión y baja. | No trivial: integra dos aplicaciones. |
| B-6d | Repositorio de marcas de Buscadero en Nucleo y adaptación de `src/Hormiguero.Buscadero.Core/Marcas/`. | Guardar, editar, anular, asociar a versión nueva y consultar por documento. | Trivial una vez cerrado B-6b. |
| B-6e | Adaptar `src/Hormiguero.Buscadero.Core/Lineas/RepositorioLineas.cs` y `ServicioLineas.cs` al contrato común; conservar reglas actuales de árboles y cadenas. | Modelos, instantánea, orden, anexos, múltiples, madre/hija, preferencia de nombre, enlace y deshacer. | No trivial: regresión de comportamiento. |
| B-6f | Reglas configurables, comparación segura y lista de dudosos en Nucleo/Core; sin valores fijos por empresa/proveedor. | Coincidencia única, empate, falta de dato, largo mínimo, opciones de espacios/guiones/ceros, destino ocupado y versión cambiada. | No trivial: evita falsos enlaces. |
| B-6g | Pantallas amigables de configuración y revisión en Archivero/Buscadero; configuración precargada de Javier/Motores en datos de ejemplo. | Flujos de configuración, aceptar/rechazar/deshacer, accesibilidad y regresión visual. | No trivial: interacción entre pantallas y datos. |
| B-6h | Modo observador de Archivero publica datos extraídos sin mover/renombrar; integración de carpeta, estados y procedencia (D-69). | Archivo observado intacto, datos opcionales, cedible dudosa y original localizado después. | No trivial: interacción con carpetas externas. |

El primer bloque es B-6b: migración v5 y repositorios en Nucleo, con pruebas y ninguna pantalla.

## 8. Preguntas para Javier

Ninguna decisión de D-71 impide definir este esquema. Se recomienda dejar los vagones ocupados y las coincidencias múltiples como dudosos, y no reemplazar ni enlazar nada automáticamente en esos casos. El largo mínimo sugerido de 6 se puede cambiar en cada regla.
