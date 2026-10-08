---
bloque: C3-a
app: Núcleo (esquemas por proveedor y cadenas)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Nucleo/**, tests/Hormiguero.Nucleo.Tests/**, src/Hormiguero.Archivero/Servicios/PublicadorDatosDocumentoService.cs]
archivos_prohibidos: [todo lo demás (las pantallas son otros bloques); migraciones existentes intactas: la nueva va con el número siguiente al más alto de main]
rama: nucleo/c3a-esquemas
---

# C3-a — Núcleo: esquema de cadena por proveedor y motor que crea y ubica cadenas

Lee **completo** `definicion/DISENO-C3-ESQUEMAS-Y-ALERTAS.md` (§1, §2) y D-84 en `definicion/DECISIONES.md`. Este bloque es **solo el Núcleo** (datos + motor + pruebas); las pantallas vienen después (C3-b, C3-c) y las alertas en C3-d (deja los modelos preparados para modos y alertas, sin implementarlos).

Javier parte cada día de cero: no migres datos de "cadenas simples"; las tablas viejas quedan sin uso (no las borres). El motor actual (`MotorCadenasSimples`) se **reemplaza** por el nuevo en el punto de publicación (`PublicadorDatosDocumentoService.RevisarEnlaces` y el observador lo usan a través de él). Conserva las reglas de enlace de D-84 ya implementadas: coincidencia **literal** → automática; **solo al limpiar** → dudoso con motivo; varias cadenas → dudoso.

1. **Datos (migración nueva)**:
   - `esquemas_cadena` (id, proveedor = entidad/nombre normalizado, nombre, activo, fechas). **Uno por proveedor** (único activo).
   - `lugares_esquema` (id, esquema_id, orden, tipo de documento = identificación tipo·emisor o tipo del diccionario —decide lo más robusto y explícalo—, inicia_cadena bool, nombre visible).
   - `parejas_esquema` (lugar_a, lugar_b, dato_diccionario por el que se emparejan).
   - Preparado para C3-d: `modos_esquema` (id, esquema_id, nombre), `lugar_decide_modo` (columna en esquema), `reglas_alerta_esquema` (estructura mínima: lugar, modo, tipo, parámetros JSON) — solo tablas y repositorio, sin lógica.
   - Cadena: proveedor, cliente (nullable), modo (nullable), estado ('activa' por ahora), esquema_id. Documento en cadena: lugar_id y, si el lugar es pareja, `linea_id`/pareja con su documento par.
   - Repositorio `RepositorioEsquemas` (crear/editar/leer por proveedor, lugares ordenados, parejas) y extensiones de `RepositorioCadenas` (cadenas por proveedor/cliente, árbol por lugares y líneas para la vista).
2. **Proveedor de un documento**: función del Núcleo que lo determina al publicar: si el tipo es "del proveedor" → su emisor; si es propio de compra (OC propia) → dato `nombre_proveedor`/`rut_proveedor` (D-84, P5-1); sin proveedor → `null` (Archivero lo exigirá en C3-c).
3. **Motor nuevo** (§2 del diseño), al publicar una versión:
   - Sin proveedor o sin esquema para su proveedor → sin cadena (resultado "sin esquema", para avisar).
   - Si su tipo **inicia** en el esquema y no calza con una cadena existente del mismo proveedor → **crear cadena** y ubicarlo en su lugar.
   - Si comparte un dato enlazable con documentos de una cadena del mismo proveedor (literal → automático; solo limpio → dudoso; varias → dudoso; un calce solo-limpio hacia la **misma** cadena que ya calza exacto por otro dato NO genera duda, con prueba) → ubicarlo en **su lugar**; si el lugar es pareja, enlazarlo con su par por el dato de la pareja (1 a 1).
   - Pertenece al esquema pero no calza y no inicia → resultado **"sin piso"** (registro para "Decisiones pendientes" de Archivero en C3-c: guarda versión, proveedor, números que menciona).
   - Cliente de la cadena: tomar `nombre_cliente`/`rut_cliente` del primer documento que lo traiga.
   - Todo con auditoría como el motor actual.
4. **Pruebas** (Núcleo, base temporal): esquema con lugares, inicio y pareja; NVV de inicio crea cadena; OCC que comparte dato entra a su lugar; 2 guías y 2 facturas del proveedor se emparejan 1 a 1 por el número de guía; solo-limpio → dudoso; documento sin piso → registro; proveedor sin esquema → sin cadena; cliente tomado del documento; uno por proveedor.

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores en tus archivos. Si algo no está definido, elige lo más simple y robusto, anótalo y sigue. Reporte al final con el modelo de datos final.

## Reporte del agente
