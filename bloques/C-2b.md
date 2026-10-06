---
bloque: C-2b
app: Núcleo
fase: C-2 (D-75, D-77, D-78)
estado: pendiente
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
