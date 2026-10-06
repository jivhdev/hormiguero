---
bloque: C-2d
app: Núcleo y Buscadero.Core
fase: C-2 (D-75, D-77, D-78)
estado: pendiente
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
