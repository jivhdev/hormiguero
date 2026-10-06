---
bloque: C-2d
app: Núcleo y Buscadero.Core
fase: C-2 (D-75, D-77, D-78)
estado: hecho
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Nucleo/**, src/Hormiguero.Buscadero.Core/**, src/Hormiguero.Archivero/Servicios/PublicadorDatosDocumentoService.cs, tests/Hormiguero.Nucleo.Tests/**, tests/Hormiguero.Buscadero.Core.Tests/**, tests/Hormiguero.Archivero.App.Tests/**]
archivos_prohibidos: [todo lo demás; sin pantallas (C-2e); no cambies migraciones existentes (v10 nueva si hace falta); NO toques los archivos de pantalla de Archivero (otro bloque trabaja en ellos)]
rama: nucleo/c2d-cadenas-simples
---

# C-2d — Cadenas simples y enlace automático por diccionario (sin pantallas)

Secciones 1 ("Crear una cadena con ayuda" y "Día a día") y 2 de `definicion/DISENO-C2-CADENAS-ASISTIDAS.md`; **D-75, D-77, D-78** (D-78: **enlace automático** cuando el dato calza exacto con **una sola** cadena; si calza con varias o hay duda → dudosos; todo se deshace con historial). Diccionario y valores por dato: C-2b (`DatosEnlazantes.cs`, `DatosDocumento.cs`).

1. **Cadena simple** (D-77 punto 6): nombre editable + **lista ordenada de documentos** (versiones), creada por el usuario. Agregar, quitar (anular), reordenar, renombrar (D-73). Reutiliza las tablas existentes (`cadenas`, `vagones_cadena`, `enlaces_cadena`) si sirven sin forzar; si no, migración v10. Debe quedar fácil complejizar después (anexos, varios del mismo tipo, cadenas hijas).
2. **Asistencia al crear** (API): dado un documento o un dato+valor, devolver los documentos que comparten algún **dato del diccionario** con igual valor, agrupados por dato ("Comparten N° OC del cliente 4500012345: OCC…, NVV…, guía…").
3. **Enlace automático** cuando se publica un documento con valores del diccionario: buscar cadenas activas que contengan otro documento con el **mismo dato y valor**; exactamente **una** cadena → agregar el documento al final, origen automático, auditoría; **más de una** → dudoso con motivo en palabras simples ("Coincide con 2 cadenas por N° OC del cliente 4500012345"); ninguna → nada. Nunca duplicar ni reemplazar. Reutiliza la lista de dudosos existente (B-6f) si encaja.
4. Engánchalo donde corresponde (D-71): después de publicar en Archivero (`PublicadorDatosDocumentoService`), y cuando el usuario crea o cambia una cadena; sin bloquear la interfaz; errores visibles y en auditoría.
5. **Modelos viejos** (D-77 punto 8: se reemplazan): no los borres en este bloque; marca en el reporte qué queda en desuso para retirarlo en C-2e.
6. Las alertas (C-1) deben seguir funcionando sobre cadenas (si asumen vagones de modelo, anota qué ajustar).

Pruebas: crear/editar/reordenar/quitar en cadena simple; asistencia por dato compartido; automático con una cadena; dudoso con dos; ninguna; deshacer con historial; idempotencia; publicación desde Archivero dispara el enlace. `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Si algo no está definido, decide lo más simple y seguro, anótalo y sigue. Reporte al final.

## Reporte del agente

Implementado en los archivos permitidos. Se reutilizaron `cadenas`, `vagones_cadena` y `enlaces_cadena`; no hizo falta migración v10. Las cadenas simples admiten crear, renombrar, agregar documentos por versión, quitar con anulación y auditoría, y reordenar. La asistencia agrupa documentos por dato fijo y valor exacto. La publicación de Archivero ejecuta en segundo plano el enlace por diccionario: una cadena recibe el documento al final; varias generan un dudoso con motivo; ninguna no genera cambios. Los enlaces y dudas conservan historial y no se repiten al reintentar. Los valores publicados toman el dato diccionario desde la configuración del tipo/campo o desde el valor recibido.

Decisión aplicada: las claves se comparan exactamente tal como quedaron guardadas por la lectura existente, sin normalizar guiones ni ceros. Cuando varios datos apuntan a más de una cadena y no hay una sola coincidencia dominante, se deja una propuesta dudosa en una de las cadenas candidatas, con el número y dato coincidente en el motivo.

Validación: `dotnet build Hormiguero.slnx` correcto, 0 advertencias y 0 errores; `dotnet test Hormiguero.slnx --no-build` correcto, 699 pruebas; `dotnet csharpier check .` correcto (273 archivos); `git diff --check` correcto.

Pendiente para C-2e: quedan en desuso `modelos_cadena`, `vagones_modelo`, `reglas_vagon` y sus APIs de plantillas/reglas en `RepositorioCadenas`, `RepositorioReglasYEnlaces` y `ServicioLineas`; no se eliminaron en este bloque. `EvaluadorAlertas` y `reglas_alerta` aún asocian alertas a un modelo y sus vagones: para alertar sobre cadenas simples habrá que adaptar la asociación/reglas a documentos de la cadena y probar que las alertas antiguas sigan funcionando.
- Claude: agregada la **clave de enlace** central (`DiccionarioDatosEnlazantes.ClaveDeEnlace`) para todo valor con dato del diccionario: si hay dígitos, solo dígitos sin ceros a la izquierda ("NVV-12345"="12345", "0000020508"="20508", "1066 086"="1066086"); si no, letras/dígitos en mayúscula. "Exacto" (D-78) se aplica sobre esta clave.
