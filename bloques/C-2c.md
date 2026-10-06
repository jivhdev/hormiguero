---
bloque: C-2c
app: Archivero
fase: C-2 (D-75, D-77, D-78)
estado: hecho
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Archivero/**, tests/Hormiguero.Archivero.App.Tests/**]
archivos_prohibidos: [todo lo demás; NO cambies el Núcleo (usa los repositorios de C-2b; si falta algo, anótalo en el reporte)]
rama: archivero/c2c-datos-por-tipo
---

# C-2c — Archivero: Emitido/Recibido, nombre estándar y datos enlazantes por tipo

Sección 1 ("Configurar un tipo de documento en Archivero", con su boceto) de `definicion/DISENO-C2-CADENAS-ASISTIDAS.md`; D-75, D-77, D-78. Repositorios del Núcleo de C-2b: `src/Hormiguero.Nucleo/Datos/DatosEnlazantes.cs`, `Identificaciones.cs`, `DatosDocumento.cs`.

1. En el asistente de identificación/configuración de un tipo de documento: **Grupo: (•) Emitido ( ) Recibido**, **Nombre estándar** sugerido "Tipo · Emisor" (editable).
2. Sección **"Datos que aparecen en este documento"**: lista del **diccionario fijo** (17 datos, agrupados como en D-78) con casilla; al marcar uno, **"Marcar zona"** en el PDF (igual que las marcas actuales), muestra el **texto leído** y permite "Probar otra vez". Una zona por **diseño de PDF** (cada patrón de la configuración tiene sus zonas). Se puede editar zona, quitar un dato (anular, no borrar historial) y volver a marcar (D-73).
3. La sección de **datos propios con nombre libre** (B-6g2) se reemplaza por esta (D-73/D-77: el diccionario es fijo); si existen datos propios guardados, ofrece vincularlos a un dato del diccionario o dejarlos de lado (sin borrar historial). Anota cómo quedó.
4. Al guardar documentos, los valores se publican asociados al **dato del diccionario** (la publicación de B-6c ya existe: reutilízala con el identificador del dato).
5. En listas donde se elige la configuración/tipo: agrupar **Emitidos** y **Recibidos** y mostrar el nombre estándar.
6. Lecciones: eventos de XAML durante `InitializeComponent`; tema claro y oscuro; 1366×768; español neutro; errores visibles; prueba STA de las ventanas tocadas.

Pruebas de la lógica nueva (marcar/editar/anular datos por diseño, publicación con identificador del diccionario, agrupación). `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Si algo no está definido, decide lo más simple y seguro, anótalo y sigue. Reporte al final.

## Reporte del agente

### Implementado

- El asistente permite elegir Emitido/Recibido, propone y guarda «Tipo · Emisor» como nombre estándar editable, y sincroniza esos metadatos con la identificación del Núcleo.
- Sustituí el ingreso libre por los 17 datos del diccionario, agrupados en el orden definido. Cada diseño PDF mantiene sus propias zonas; la vista muestra el texto leído, permite marcar otra vez y anula los datos desmarcados sin borrar sus registros.
- Los datos propios anteriores se muestran para vincularlos al diccionario o dejarlos sin selección. Se conserva el campo local como inactivo; si hay un campo compartido previo, se asocia mediante `GuardarCampo` y la zona nueva referencia ese campo.
- La publicación prepara campos con el ID del diccionario antes de publicar valores; las listas de configuraciones agrupan Emitidos/Recibidos y muestran el nombre estándar.
- Añadí pruebas de guardar/editar/anular zonas por diseño, publicación con ID de diccionario y metadatos mostrados en listas. Las ventanas existentes siguen cubiertas por la prueba STA en tema claro y oscuro.

### Verificación

- `dotnet build Hormiguero.slnx`: correcto, 0 advertencias y 0 errores.
- `dotnet test Hormiguero.slnx`: correcto, 297 pruebas aprobadas.
- `dotnet csharpier check .`: correcto, 273 archivos revisados.

### Dependencias pendientes del Núcleo (sin cambios fuera del alcance)

- No se puede volver a marcar una zona anulada en el mismo diseño: `tipos_documento_datos` mantiene una restricción única también para filas inactivas y `RepositorioDatosEnlazantes.GuardarDatoTipo` solo actualiza filas activas. Archivero muestra un error legible en ese caso y conserva el historial. Hace falta una operación del Núcleo que reactive una fila o cree otra versión sin borrar historial.
- La asociación de un campo propio anterior actualiza `campos_documento`, pero no los valores históricos: `valores_documento.dato_diccionario_id` se guardó como `NULL` al publicarlos y el Núcleo no expone una operación para completar esa columna. Para asociar también el historial hace falta una operación del Núcleo; no se modificó SQL común desde Archivero.
