---
bloque: B-6f
app: Núcleo y Buscadero
fase: B-6
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Nucleo/Datos/**, src/Hormiguero.Buscadero.Core/**, src/Hormiguero.Archivero/Servicios/PublicadorDatosDocumentoService.cs, tests/Hormiguero.Nucleo.Tests/**, tests/Hormiguero.Buscadero.Core.Tests/**, tests/Hormiguero.Archivero.App.Tests/**]
archivos_prohibidos: [todo lo demás; sin pantallas (son B-6g); no cambies la migración v5 salvo que sea imprescindible (explícalo)]
rama: nucleo/b6f-reglas-dudosos
---

# B-6f — Motor de enlace automático y lista de dudosos

Sección 4 de `definicion/DISENO-B6-MARCAS-Y-CADENAS.md` (D-48, D-71). Sin pantallas.

1. **Motor en el Núcleo**: dada una regla de vagón ("completar este vagón con documentos de [configuración] cuando [dato] sea igual a [dato] del vagón [Y]"), compara solo por **igualdad** del valor clave tras las normalizaciones de la regla (espacios sí por defecto; guiones y ceros iniciales no por defecto; largo mínimo 6 por defecto, editable). Valida al guardar la regla que el vagón de comparación pertenezca al mismo modelo (o a su cadena madre/hija, según el diseño).
2. **Cuándo corre**: cuando se publica un documento con valores (B-6c: Archivero y el futuro observador) y cuando se crea una cadena o se completa el vagón de comparación. Que corra donde corresponde (D-71) sin bloquear la interfaz; si falla, el documento igual queda guardado y el error queda registrado y visible.
3. **Resultados**: un único candidato seguro y vagón libre → enlace automático (origen automático, regla, auditoría). Cualquier duda (valor corto, varios candidatos, vagón ocupado, versión cambiada, dato faltante, lectura no confiable) → **dudoso** con el motivo en palabras simples (para mostrar después: "Hay 2 guías con el mismo N° OC", etc.). En vagón múltiple, cada documento queda propuesto individualmente. Nunca se fuerza ni se reemplaza nada; las reglas nuevas no tocan enlaces confirmados.
4. **Acciones sobre dudosos** (API, sin pantalla): enlazar, "no son el mismo documento" (no se vuelve a proponer ese par), ver datos; todo con historial y auditoría.
5. **Pendientes de B-6e** (ver `bloques/B-6e.md`): borrar un modelo o vagón debe anular sus reglas y quedar auditado (sin romper FK); pasa a repositorios del Núcleo las operaciones que `RepositorioLineas` hace con SQL directo contra tablas comunes, sin cambiar comportamiento.
6. Nada fijo de JCV (D-70). Base con `DocumentosGuardados.RutaBaseComun`.

Pruebas: coincidencia única → enlace; empate → dudoso; valor corto → dudoso; opciones de espacios/guiones/ceros; vagón ocupado; versión cambiada; dato faltante; "no son el mismo" no reaparece; regla nueva no altera confirmados; borrar modelo con reglas; publicación desde Archivero dispara el motor; regresión total.

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Reporte al final.

## Decisión de Claude (respuesta a la pregunta de Codex, 2026-10-04)

Confianza de lectura: un valor **sin confianza informada** (texto extraído del PDF, que hoy es exacto) o **corregido a mano** es confiable. Un valor **con confianza informada** (OCR futuro) es confiable solo si es **≥ 0,90**; si no, el caso va a dudosos con el motivo "la lectura no es segura". El 0,90 es un valor configurable (guardado como configuración, con 0,90 por defecto), no fijo en el código. Sigue con el bloque completo.

## Reporte del agente
