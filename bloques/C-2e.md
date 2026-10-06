---
bloque: C-2e
app: Buscadero (y Núcleo para alertas)
fase: C-2 (D-73, D-75, D-77, D-78)
estado: hecho
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Buscadero/**, src/Hormiguero.Buscadero.Core/**, src/Hormiguero.Nucleo/Datos/Alertas.cs, src/Hormiguero.Nucleo/Datos/EvaluadorAlertas.cs, src/Hormiguero.Nucleo/Datos/Migraciones.cs, tests/Hormiguero.Buscadero.Core.Tests/**, tests/Hormiguero.Nucleo.Tests/**]
archivos_prohibidos: [todo lo demás; NO toques Archivero (otro bloque trabaja ahí); migraciones existentes intactas (v10 nueva solo si hace falta)]
rama: buscadero/c2e-pantallas-cadenas
---

# C-2e — Buscadero: pantallas de cadenas simples asistidas y alertas sobre ellas

`definicion/DISENO-C2-CADENAS-ASISTIDAS.md` (bocetos "Nueva cadena" y "Buscadero · Cadena"), D-73/D-75/D-77/D-78; motor y servicio de C-2d (`MotorCadenasSimples.cs`, `ServicioCadenasSimples.cs`).

1. **Reemplazar** en la pestaña "Cadenas documentales" lo de modelos/vagones/reglas de vagón por el flujo nuevo (D-77 punto 8): al abrir por primera vez después de este cambio, un aviso simple ("Las cadenas ahora se crean de otra forma. Los modelos anteriores dejan de usarse; los documentos no se tocan.") con Aceptar. Las APIs viejas pueden quedar sin pantalla (no borres tablas).
2. **Nueva cadena** (boceto): buscar documentos (por número o dato del diccionario), agregarlos, ver la **sugerencia** "Estos N documentos comparten *N° OC del cliente 4500012345*: … ¿Agregarlos?" con [Agregar todos] [Revisar] [No agregar], ordenar (↑↓), quitar, nombre (sugerido por el primer documento), Guardar/Cancelar.
3. **Ver cadena**: lista en orden con cada documento (tipo · emisor, número, fecha), botón para abrir el PDF, menú del documento "Quitar de la cadena" (anula con historial), renombrar cadena, agregar documentos con la misma asistencia. Buscar una cadena por cualquier número.
4. **Dudosos** (los de C-2d): la lista existente debe mostrar los nuevos ("Coincide con 2 cadenas por N° OC del cliente 4500012345") con acciones Enlazar (eligiendo la cadena) / No corresponde / Ver archivo.
5. **Alertas sobre cadenas simples** (pendiente que dejó C-2d): las reglas de alerta pasan a decir "si la cadena tiene un documento de *[tipo/dato]* y en N días no tiene uno de *[tipo/dato]*, avisar…" (adapta `reglas_alerta`/evaluador a documentos de la cadena por tipo de documento o dato del diccionario; v10 si hace falta). Alertas manuales sobre cadena siguen igual. Ajusta la pantalla "Avisarme si falta un documento" al nuevo lenguaje.
6. Lecciones: eventos de XAML durante `InitializeComponent`; tema claro y oscuro; 1366×768; español neutro; errores visibles (nunca cerrar la app); prueba STA de las ventanas tocadas; base `DocumentosGuardados.RutaBaseComun`.

Pruebas de la lógica nueva (crear con sugerencia, quitar, reordenar, buscar por número, dudoso a elegir cadena, regla de alerta nueva crea/resuelve). `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Si algo no está definido, decide lo más simple y seguro, anótalo y sigue. Reporte al final.

## Reporte del agente

- **Resultado:** implementadas las pantallas de cadenas simples asistidas, búsqueda por número o dato extraído, sugerencias con revisión, orden y retiro con historial, consulta/renombrado y apertura de PDF. La pestaña informa una sola vez que los modelos anteriores dejan de usarse y mantiene accesibles alertas y dudosos.
- **Dudosos:** el diálogo existente combina las propuestas heredadas y las de cadenas simples; las nuevas permiten elegir la cadena, marcar «No corresponde» y abrir el archivo.
- **Alertas:** añadida la migración v10 con reglas para cadenas simples por tipo de documento o dato del diccionario; la evaluación crea alertas al faltar el documento esperado y las resuelve cuando llega. Los recordatorios manuales siguen disponibles en una cadena simple. Las reglas antiguas y migraciones anteriores se conservaron.
- **Decisiones:** las reglas nuevas se guardan en una tabla separada para no alterar las reglas existentes; la columna nueva en `alertas` mantiene la idempotencia por cadena. La búsqueda considera números indexados, nombre de archivo y valores extraídos vigentes. Todo usa `DocumentosGuardados.RutaBaseComun`.
- **Pruebas añadidas:** flujo de sugerencia/búsqueda/orden/retiro, elección de cadena para un dudoso, creación y resolución de alerta; ventanas tocadas verificadas en STA con temas claro y oscuro.
- **Archivos modificados:** únicamente los permitidos por el bloque, más esta sección del reporte.
- **Verificación:** `dotnet build Hormiguero.slnx` correcto, 0 errores y 0 advertencias; `dotnet test Hormiguero.slnx` correcto, 709 pruebas superadas y 0 omitidas; `dotnet csharpier check .` correcto (275 archivos).