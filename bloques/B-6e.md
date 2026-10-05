---
bloque: B-6e
app: Buscadero (y núcleo)
fase: B-6
estado: hecho
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Buscadero.Core/**, src/Hormiguero.Buscadero/**, src/Hormiguero.Nucleo/Datos/**, tests/Hormiguero.Buscadero.Core.Tests/**, tests/Hormiguero.Nucleo.Tests/**]
archivos_prohibidos: [todo lo demás; no toques Archivero ni Mensajero; no cambies la migración v5 salvo que sea imprescindible (explícalo)]
rama: buscadero/b6e-cadenas-comunes
---

# B-6e — Modelos y cadenas de Buscadero en la base común

Sección 2 de `definicion/DISENO-B6-MARCAS-Y-CADENAS.md` (D-71). Adapta `src/Hormiguero.Buscadero.Core/Lineas/` (`RepositorioLineas.cs`, `ServicioLineas.cs`) para que modelos (`modelos_cadena`, `vagones_modelo`) y cadenas (`cadenas`, `vagones_cadena`, `enlaces_cadena`) se guarden en la base común usando los repositorios del Núcleo de B-6b.

## Reglas

- **El comportamiento visible de Buscadero no cambia**: crear modelo guiado, vagones (orden, múltiple, anexo, ramas, cadena hija), preferencia de nombre, crear cadena desde modelo, vincular documento a cadena, cadena madre/hija, mostrar árboles, deshacer. Todas las pruebas actuales de líneas/cadenas deben seguir pasando (ajústalas solo si cambia un detalle interno, nunca el comportamiento).
- Un documento ubicado en un vagón se guarda como **enlace** (`enlaces_cadena`, origen manual) a la **versión** del documento, registrándolo en la base común con `AsegurarDocumentoYVersionVigente` (B-6d) **solo cuando el usuario lo vincula**. Quitar/deshacer = anular el enlace (historial y auditoría), el vagón queda libre; nunca se borra nada.
- Instantánea de estructura: una cadena conserva su estructura aunque luego se edite el modelo.
- Base común con `DocumentosGuardados.RutaBaseComun` (respeta `HORMIGUERO_DATOS`), **nunca** `BaseComun.RutaPorDefecto`.
- Lecciones de B-6d: abrir/ver no escribe en la base; si una carga en segundo plano termina después de que el usuario cambió de documento o cadena, se descarta.
- D-68: no se migran modelos ni cadenas viejos (la base propia deja de usarse para escribir).
- Nada fijo de JCV (D-70). Sin reglas automáticas todavía (B-6f).

## Pruebas

Modelo con ramas, múltiples, anexos y cadena hija; instantánea tras editar el modelo; orden; preferencia de nombre (genérico y por documento); vincular, mover de vagón y deshacer con historial y auditoría; documento movido (misma huella) sigue en su vagón; contenido cambiado → el enlace queda en la versión vieja como historial y el vagón muestra el aviso que corresponda (define el más simple y anótalo); consulta "en qué cadenas está este documento"; escritura con lector abierto.

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Reporte al final, incluyendo cualquier comportamiento que no pudiste conservar idéntico.

## Reporte del agente

- Adapté modelos, vagones y cadenas de Buscadero para guardar en `DocumentosGuardados.RutaBaseComun`; la pantalla abre Lineas con esa ruta. No se usa `BaseComun.RutaPorDefecto` y no se modificó la migración v5 ni se migraron datos antiguos.
- Los enlaces manuales registran la versión mediante `AsegurarDocumentoYVersionVigente`. Cambiar o quitar un vínculo anula la fila anterior y conserva el historial; las pruebas verifican origen manual, estados e inserción con un lector SQLite abierto.
- La estructura de cada cadena se conserva como instantánea. Al borrar un modelo se desvinculan sus referencias sin alterar la estructura guardada de las cadenas.
- Aviso definido para contenido cambiado: el vagón conserva su vínculo a la versión anterior y muestra «el contenido cambió» si el archivo de la ruta registrada existe y su huella difiere. La lectura calcula la huella y no escribe en la base.
- Las pruebas que enlazaban rutas inexistentes se actualizaron para crear documentos temporales reales, como requiere el registro de versiones.
- Limitación para revisión: B-6b no expone todos los listados, cambios y anulaciones que requiere la interfaz anterior; `RepositorioLineas` usa los repositorios del Núcleo para crear modelos/vagones y gestionar versiones/enlaces, y consulta/actualiza directamente algunas tablas comunes para conservar el contrato. No se cambió la migración v5. Si D-71 exige que cada operación pase por una API del Núcleo, falta ampliar esos repositorios antes de cerrar el bloque.
- Verificación: `dotnet build` correcto, 0 advertencias; `dotnet test` correcto (597 pruebas); `dotnet csharpier check .` correcto (227 archivos).
- Comportamiento no conservado: ninguno identificado en las pruebas del bloque. La preferencia `Personalizado` conserva su comportamiento anterior, equivalente a `Generico`; las cargas asíncronas no aplican a estas operaciones de Lineas.

### Revisión de Claude (2026-10-04)

- Aceptado: comportamiento conservado (597 pruebas), enlaces manuales por versión con historial, instantánea, ruta común correcta.
- Pendiente para B-6f: (1) borrar un modelo o vagón borra físicamente la configuración (como antes) pero sin auditoría y chocará con `reglas_vagon` (FK): anular reglas y auditar; (2) `RepositorioLineas` ejecuta SQL directo contra tablas comunes: pasar esas operaciones a repositorios del Núcleo.
