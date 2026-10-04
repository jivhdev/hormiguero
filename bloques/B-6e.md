---
bloque: B-6e
app: Buscadero (y núcleo)
fase: B-6
estado: pendiente
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
