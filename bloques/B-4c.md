---
bloque: B-4c
app: Buscadero
fase: B (D-66, definicion/PLAN-MIGRACION.md)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Buscadero.Core/Hormiguero.Buscadero.Core.csproj, src/Hormiguero.Buscadero.Core/Indexado/RepositorioIndice.cs, src/Hormiguero.Buscadero.Core/Indexado/ImportadorDeGuardados.cs, src/Hormiguero.Buscadero.Core/Busqueda/ServicioBusqueda.cs, src/Hormiguero.Buscadero/MainWindow.xaml.cs, tests/Hormiguero.Buscadero.Core.Tests/ImportadorDeGuardadosTests.cs]
archivos_prohibidos: [todo lo demás]
rama: fase-b/b4c-buscadero-lee
---

# B-4c — Buscadero encuentra al instante lo que guarda Archivero

## Objetivo

Antes de cada búsqueda, Buscadero lee del registro común de Hormiguero los documentos guardados desde la última vez y los agrega a su índice, para encontrarlos **sin esperar** a que su índice en segundo plano llegue a esa carpeta. **La búsqueda y todo lo demás de Buscadero siguen igual** (sus 116 pruebas deben seguir pasando).

Este bloque toca 6 archivos: es más de lo habitual (3), pero son cambios chicos y conectados; por eso va a Codex.

## API existente que usas (no la cambies)

- `Hormiguero.Nucleo.Datos.BaseComun.Abrir(string ruta)` → `SqliteConnection` (crea la base y aplica migraciones).
- `Hormiguero.Nucleo.Datos.DocumentosGuardados(SqliteConnection)`: `IReadOnlyList<DocumentoGuardado> Despues(long ultimoVisto, int maximo = 1000)`; `record DocumentoGuardado(long Id, string Ruta, string App, DateTime GuardadoEn)`; `static string RutaBaseComun`.
- En Buscadero: `RepositorioIndice` (tabla `ArchivosIndexados`: `Ruta`, `RutaClave` única, `Nombre`, `CarpetaContenedora`, `CarpetaClave`, `FechaModificacion` = `LastWriteTimeUtc.Ticks`; `static ClaveRuta(string)`), `ServicioBusqueda`, `MainWindow` (crea `ServicioBusqueda` en su constructor). Léelos antes de empezar.

## Contenido exacto

1. `Hormiguero.Buscadero.Core.csproj`: `ProjectReference` a `..\Hormiguero.Nucleo\Hormiguero.Nucleo.csproj`.
2. `RepositorioIndice.cs`:
   - En `Inicializar()`, crea `CREATE TABLE IF NOT EXISTS ImportacionGuardados (Id INTEGER PRIMARY KEY CHECK (Id = 1), UltimoId INTEGER NOT NULL);`
   - `public long LeerUltimoGuardadoImportado()` (0 si no hay fila) y `public void GuardarUltimoGuardadoImportado(long id)` (upsert).
   - `public void AgregarArchivo(string ruta)`: inserta o actualiza (por `RutaClave`) una fila de `ArchivosIndexados` con los mismos valores que pondría el indexador (`Nombre` = nombre del archivo, `CarpetaContenedora` = carpeta, `CarpetaClave` = `ClaveRuta(carpeta)`, `FechaModificacion` = `File.GetLastWriteTimeUtc(ruta).Ticks`). **No toques `CarpetasIndexadas`**: así la próxima pasada del índice igual revisa esa carpeta.
3. `ImportadorDeGuardados.cs` (nuevo, `namespace Buscadero.Core.Indexado`):
   - Constructor `ImportadorDeGuardados(RepositorioIndice repositorio, Func<IReadOnlyList<string>> carpetasMadre, string rutaBaseComun)`.
   - `public int Importar()`: si no existe el archivo `rutaBaseComun`, devuelve 0 sin crearlo. Si existe, lee `Despues(LeerUltimoGuardadoImportado())`; por cada uno, lo agrega con `AgregarArchivo` **solo si** termina en `.pdf` (sin importar mayúsculas), el archivo existe y está **dentro** de alguna carpeta madre (comparación por segmentos completos con `ClaveRuta`: `C:\Docs\FooBar` NO está dentro de `C:\Docs\Foo`). Al final guarda como último el mayor `Id` leído (aunque se haya ignorado). Devuelve cuántos agregó.
   - Cualquier `IOException`, `UnauthorizedAccessException` o `SqliteException` se atrapa y devuelve 0: **nunca** debe romper una búsqueda.
4. `ServicioBusqueda.cs`: parámetro opcional nuevo al final del constructor, `ImportadorDeGuardados? importador = null`, y como primera línea de `Buscar(...)`: `_importador?.Importar();`.
5. `MainWindow.xaml.cs`: crea el importador con `repositorioIndice`, las carpetas madre (`_servicioCarpetas.ObtenerTodas().Select(c => c.Ruta).ToList()`) y `Hormiguero.Nucleo.Datos.DocumentosGuardados.RutaBaseComun`, y pásalo al `ServicioBusqueda`.
6. `ImportadorDeGuardadosTests.cs` (usa `EntornoDePrueba` y crea una base común temporal propia con `BaseComun.Abrir`; registra con `new DocumentosGuardados(conexion).Registrar(ruta, "Archivero")`):
   - `Lo_guardado_por_Archivero_se_encuentra_sin_reindexar`: carpeta madre configurada e indexada; se crea un PDF nuevo en una subcarpeta y se registra; buscar su número lo encuentra **sin** que el indexador haga pausas (usa un `Indexador` con contador de pausas, como `BusquedaRapidaTests`).
   - `Fuera_de_las_carpetas_se_ignora`, `Archivo_que_ya_no_existe_se_ignora`, `No_importa_dos_veces_lo_mismo` (el marcador avanza), `Sin_base_comun_no_hace_nada`.

## Criterio de término

- `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Sin identificadores con tilde ni ñ.
- Escribe tu reporte al final de esta nota.

## Reporte del agente

- Se añadió la referencia de Buscadero Core a Hormiguero.Nucleo, el marcador de importación en el índice y `ImportadorDeGuardados`, que incorpora únicamente PDFs existentes dentro de las carpetas madre.
- La búsqueda importa los guardados antes de consultar el índice. MainWindow configura el importador con las carpetas actuales y la ruta común de datos.
- Se añadieron las cinco pruebas solicitadas, incluida la búsqueda del archivo guardado sin pausas del indexador.
- `dotnet build --no-restore -m:1`: correcto, 0 advertencias y 0 errores.
- `dotnet test --no-build --no-restore -m:1`: correcto, 558 pruebas aprobadas en todos los proyectos.
- `dotnet csharpier check .`: correcto, 220 archivos revisados.
