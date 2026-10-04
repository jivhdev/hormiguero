---
bloque: B-6d
app: Buscadero (y núcleo)
fase: B-6
estado: pendiente
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
