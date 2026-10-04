---
bloque: B-6d
app: Buscadero (y núcleo)
fase: B-6
estado: hecho
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Buscadero.Core/**, src/Hormiguero.Buscadero/**, src/Hormiguero.Nucleo/Datos/**, tests/Hormiguero.Buscadero.Core.Tests/**, tests/Hormiguero.Nucleo.Tests/**]
archivos_prohibidos: [todo lo demás; no toques Archivero ni Mensajero; no cambies la migración v5 salvo que sea imprescindible (explícalo)]
rama: buscadero/b6d-marcas-comunes
---

# B-6d — Documentos de Buscadero y sus marcas en la base común

Sección 6 (marcas) de `definicion/DISENO-B6-MARCAS-Y-CADENAS.md` y D-71 ("todo Hormiguero está enlazado").

## El problema previo (resuélvelo primero)

Buscadero ve muchos PDF que Archivero nunca guardó (su índice propio está en `buscadero.db`). Para que una marca (y después un vagón de cadena, B-6e) apunte a una **versión de documento** de la base común, Buscadero necesita **registrar el documento en la base común cuando el usuario actúa sobre él** (marcar, y más adelante enlazar), no al indexar miles de PDFs.

1. En el Núcleo, un método único y reutilizable "asegurar documento y versión vigente" para una ruta: si ya existe (por ruta, o por huella si se movió) lo reutiliza; si no, lo crea con su huella (`Huella` del núcleo) y versión vigente; si la huella cambió, crea versión nueva. En transacción, con auditoría. Úsalo también desde lo que ya publicó B-6c si corresponde, sin duplicar lógica (si tocar `DatosDocumento.cs` de B-6c es necesario, hazlo con cuidado y pruebas).
2. Marcas de Buscadero (`src/Hormiguero.Buscadero.Core/Marcas/`) pasan a guardarse en `marcas_version` del núcleo por versión: guardar, editar y quitar = anular (nunca borrar), con auditoría. **El comportamiento visible de las marcas en Buscadero no cambia** (mismas pantallas, mismas operaciones); la base propia de marcas deja de usarse para escribir (D-68: no se migran marcas viejas).
3. Si el PDF cambió de contenido (huella distinta), las marcas de la versión anterior siguen visibles como historial y la versión nueva parte sin marcas (las pruebas lo verifican).
4. Concurrencia: Archivero puede estar escribiendo a la vez (WAL); ninguna operación de Buscadero debe bloquear la interfaz más de lo que ya lo hace hoy.

Pruebas: asegurar documento (nuevo, existente, movido con misma huella, contenido cambiado), marcas guardar/editar/anular con historial y auditoría, regresión de las pruebas actuales de marcas (`SesionMarcasTests` u otras), escritura con lector abierto.

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Reporte al final.

## Reporte del agente

Implementé B-6d dentro del alcance autorizado:

- Núcleo: `AsegurarDocumentoYVersionVigente` calcula la huella y registra/reutiliza el documento y su versión en una transacción con auditoría. `PublicarDocumento` de B-6c comparte la misma lógica. La ruta se prioriza y la huella permite reconocer un documento movido; un cambio de contenido crea una versión nueva.
- Buscadero: las marcas se leen y escriben en `marcas_version` de la base común. Guardar, editar, deshacer y quitar dejan auditoría e historial mediante inserciones y anulaciones en una transacción. La base propia ya no se usa para escribir marcas y no se migraron las antiguas.
- La carga de marcas y el cálculo de huella del documento se ejecutan en segundo plano en los visores principal y de comparación. Se mantuvo intacta la migración v5.
- Añadí pruebas para documento nuevo, existente, movido y modificado; historial y auditoría de marcas; regresiones de `SesionMarcasTests`; y escritura WAL con lector abierto.

Verificación: `dotnet build` correcto, 0 advertencias y 0 errores; `dotnet test` correcto (595 pruebas); `dotnet csharpier check .` correcto (227 archivos).

### Revisión de Claude (2026-10-04)

- Corregido: Buscadero abría la base común con `BaseComun.RutaPorDefecto`, que ignora `HORMIGUERO_DATOS` (en pruebas habría escrito marcas en la base real). Ahora usa `DocumentosGuardados.RutaBaseComun`.
- Corregido: abrir un PDF lo registraba en la base común (y en auditoría). Ahora solo lee (busca la versión vigente por huella); registra recién al guardar una marca. Prueba nueva `AbrirSinMarcar_NoRegistraElDocumentoEnLaBaseComun`.
- Corregido: al guardar se usaba una versión en caché; si el PDF cambió desde que se abrió, la marca iba a la versión vieja. Ahora siempre se confirma la versión vigente.
- Corregido: si se abría otro documento mientras cargaban las marcas, quedaban las del anterior.
