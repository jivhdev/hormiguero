---
bloque: C-2b
app: Núcleo
fase: C-2 (D-75, D-77, D-78)
estado: hecho
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Nucleo/**, tests/Hormiguero.Nucleo.Tests/**]
archivos_prohibidos: [todo lo demás; sin pantallas; no cambies migraciones existentes (v9 nueva)]
rama: nucleo/c2b-diccionario
---

# C-2b — Diccionario fijo de datos enlazantes y datos por tipo de documento (Núcleo)

Primer bloque de implementación del rediseño de cadenas. Lee `definicion/DISENO-C2-CADENAS-ASISTIDAS.md` y **D-75, D-77 y D-78** en `definicion/DECISIONES.md` (D-78 trae la lista exacta del diccionario). Código actual: `src/Hormiguero.Nucleo/Datos/` (v1–v8: `campos_documento`, `valores_documento`, `versiones_documento`, cadenas y reglas de B-6, alertas de C-1).

1. **Diccionario FIJO** (D-77: lo único no configurable): los 17 datos de D-78 como datos del Núcleo, con **identificador estable** (texto corto en inglés o español sin tildes, p. ej. `oc_cliente`, `oc_propia`, `nvv_propia`, `nvv_proveedor`, `guia_propia`, `guia_proveedor`, `factura_propia`, `factura_proveedor`, …), **nombre visible** en español exacto de D-78, **grupo** (Ventas propias / Del cliente / Compras propias / Del proveedor / Otros), **orden** y **código de Javier** de referencia si lo tiene (COV, NVV, GDV, FCV, NCV, OCL, OCC, GRC, FCC, NCC). Defínelo en código (lista inmutable) y en una tabla de solo lectura creada por la migración v9 para que las consultas SQL puedan unirse a ella. No hay API para crear, editar ni borrar entradas.
2. **Tipo de documento**: en la base común, para cada configuración/identificación de Archivero (`identificaciones`): **grupo Emitido/Recibido** (D-75, editable por el usuario) y **nombre estándar** "Tipo · Emisor" (D-77, sugerido y editable).
3. **Qué datos trae cada tipo**: relación tipo de documento ↔ dato del diccionario, con **zona por diseño de PDF** (D-77: un mismo tipo puede tener varios diseños/patrones, cada uno con su zona para ese dato). Reutiliza lo que sirva de `campos_documento` (datos propios de B-6g2): migra o vincula para que cada campo propio pueda apuntar a un dato del diccionario; lo que no sirva, anótalo (D-73 permite reemplazar).
4. **Valores**: los valores leídos de un documento quedan asociados al **dato del diccionario** (además del campo), para consultar en milisegundos "todos los documentos con OC del cliente = X" (índice).
5. Repositorios en el Núcleo para: leer el diccionario; marcar Emitido/Recibido y nombre estándar; agregar/quitar/editar qué datos trae un tipo y su zona por diseño (todo editable, D-73; quitar = anular, nunca borrar historial); buscar documentos por dato + valor.
6. Nada fijo de JCV salvo el diccionario (D-70/D-77).

Pruebas: diccionario completo e inmutable (17 entradas, identificadores únicos, orden, grupos); migración nueva/repetida/sobre v8 con datos; Emitido/Recibido y nombre estándar; datos por tipo con dos diseños distintos; buscar por dato y valor; anular sin borrar. `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Si algo no está definido, decide lo más simple y seguro, anótalo y sigue. Reporte al final.

## Reporte del agente

- **Resultado:** implementado C-2b en Núcleo. Diccionario fijo de 17 entradas en código y tabla v9 protegida contra inserción, edición y borrado; identificaciones con grupo Emitido/Recibido y nombre estándar sugerido `Tipo · Emisor`; configuración de datos por diseño y zona, campos enlazables y valores indexados por dato; repositorios para lectura, edición, baja lógica y búsqueda.
- **Migración:** v9 crea las tablas, columnas e índices nuevos, conserva campos/valores previos y completa el nombre estándar de identificaciones existentes. La migración se verificó repetida y aplicada sobre v8 con identificación, campo y valor existentes.
- **Decisiones:** se conservaron campos y valores históricos sin asignarles un dato fijo cuando su nombre libre no permite inferirlo sin ambigüedad. Desde ahora el campo se puede vincular explícitamente al diccionario; los valores guardados heredan ese identificador. La baja de una relación tipo/dato solo la desactiva.
- **Archivos modificados:** `src/Hormiguero.Nucleo/Datos/DatosDocumento.cs`, `Identificaciones.cs`, `Migraciones.cs`, nuevo `DatosEnlazantes.cs`; pruebas de Núcleo en `IdentificacionesYAuditoriaTests.cs` y nuevo `DatosEnlazantesTests.cs`.
- **Verificación:** `dotnet build --no-restore -m:1` correcto, 0 errores y 0 advertencias. `dotnet test --no-build -m:1` correcto, 694 pruebas superadas. `dotnet csharpier check .` revisó 270 archivos y solo reportó finales de línea distintos en `tests/Hormiguero.Mensajero.Core.Tests/MensajeroLogTests.cs` y `tests/Hormiguero.Mensajero.Core.Tests/ClickFactura/CausasX1Tests.cs`, ambos fuera de los archivos permitidos; `dotnet csharpier check` sobre los seis archivos C# modificados pasó.
