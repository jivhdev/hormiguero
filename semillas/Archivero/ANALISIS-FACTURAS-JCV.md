# Análisis de Facturas JCV (ExtractorCobelcar)

Revisión para preparar su rediseño como herramienta integrada en Mensajero (D-68). Se revisaron nombres, tipos y fechas de los archivos, código fuente AP05 y estructura de claves de configuración. No se abrieron PDFs, planillas, bases de datos, registros ni otros archivos con datos; tampoco se ejecutaron los programas originales.

## 1. Versiones AP04 y AP05

- **AP05-JCV_ExtractorCobelcar** es el proyecto fuente Python: contiene `main.py`, módulos `core/`, `ui/`, `utils/` y `Facturas JCV.spec` de PyInstaller. Los archivos centrales `core/monitor.py` y `core/orchestrator.py` datan del 3 de agosto de 2026, 08:47 y 08:48; el `.spec`, de las 08:49.
- **AP04-Facturas JCV** contiene `Facturas JCV.exe` (3 de agosto, 08:51) y `config.json`, sin código fuente. El `.spec` de AP05 pide generar un ejecutable llamado «Facturas JCV». Por nombre y fechas, AP04 parece ser el paquete generado desde AP05; el ejecutable se compiló después de las últimas modificaciones visibles de AP05. No se puede confirmar que sean idénticos sin inspeccionar o comparar binarios, lo cual no es necesario para este análisis.
- La configuración de AP04 tiene fecha posterior (3 de agosto, 11:39), pero eso no demuestra una versión de programa distinta. Ambas configuraciones tienen las mismas claves: `carpeta_facturas`, `impresora`, `intervalo_polling_segundos`, `mes_manual`.
- **Parece que se usa AP04**: allí existe el ejecutable junto a bases SQLite y un registro con fechas hasta el 2 de septiembre; AP05 conserva el entorno de desarrollo y también datos de estado. Esta es una inferencia por archivos y fechas, no por lectura de esos datos.

## 2. Qué hace hoy

1. Al iniciar, lee `config.json` desde el directorio de trabajo. Si falta una carpeta configurada, ofrece elegir una y un mes `YYYYMM`; guarda ambas opciones en la configuración. Usa `mes_manual` o intenta obtener seis dígitos de la ruta para elegir la base mensual (`main.py:42-48, 68-99, 178-205, 228-238`).
2. Vigila solo la carpeta elegida, sin subcarpetas, buscando archivos `.pdf`. Combina eventos de creación/modificación con un sondeo periódico; el sondeo inicial ocurre a los 10 segundos. Espera hasta 30 segundos a que el tamaño del archivo se estabilice (`core/monitor.py:14-27, 30-47, 51-91`).
3. Antes de leer el PDF, evita volver a procesar una ruta ya registrada. Si el nombre contiene un sufijo como `_CEDIBLE` y encuentra en la misma carpeta un PDF hermano con el mismo nombre base sin ese sufijo, descarta el cedible (`core/deduplicator.py:12-23, 38-67, 94-120`).
4. Lee **solo la primera página** y extrae tres rectángulos fijos, en puntos PDF: cliente `(102,188)-(280,202)`, número `(484,98)-(540,112)` y marca cedible `(496,760)-(540,774)`, con tolerancia de 3 puntos (`core/pdf_processor.py:11-17, 59-65, 81-93`). Busca «COMERCIAL JCV SPA», toma dígitos del número y los normaliza como `FCV` más 10 dígitos; considera cedible si el texto de la zona contiene «CEDIBLE» (`core/pdf_processor.py:99-140, 157-177`).
5. Si el PDF no coincide con JCV, lo registra como descartado. Si es cedible y su número ya existe, lo descarta; si no, lo deja como dudoso. Si es JCV y no cedible, lo agrega a pendientes. Ante fallas de lectura o archivo inestable, reintenta y luego lo envía a dudosos (`core/orchestrator.py:70-160, 164-190`). No renombra, copia ni mueve los PDF.
6. Guarda estado local en `./data/invoices_<mes>.db`, con tabla `invoices`: número, ruta, nombre, fecha de recepción, estado (`pending`, `printed`, `uncertain`, `discarded`), fecha de impresión y notas. Activa WAL e índices por estado y número (`core/state_manager.py:18-66`). Los registros se agrupan por mes; los descartes también se guardan como filas. Escribe el registro de actividad en `data/logs/app.log` (`main.py:19-26`). Al exportar, genera un CSV de facturas marcadas como impresas.
7. Para imprimir, la pantalla abre los PDF seleccionados con el visor predeterminado para que la persona los imprima manualmente. La función auxiliar `utils/printer.py:48-70` puede enviar un PDF a SumatraPDF, pero no está conectada a la interfaz actual.

## 3. Pantalla actual

Ventana «Facturas JCV» de 1000 × 680, con carpeta y mes activos, botones **Cambiar mes**, contadores de pendientes e impresas y tres pestañas: **Pendientes**, **Impresas** y **Dudosos**. Las tablas muestran número, fechas, nombre de archivo y, en dudosos, motivo. En la parte inferior están **Abrir para imprimir**, **Exportar CSV** y **Refrescar**. En Dudosos aparecen **Es JCV (mover a pendientes)**, **No es JCV (descartar)** y **Abrir PDF**. Doble clic en una fila abre el PDF. La barra inferior resume conteos. Referencia: `ui/main_window.py:20-98, 101-150, 152-216`.

Los diálogos piden confirmar impresión, cambio de mes y descarte; al confirmar un dudoso como JCV se solicita el número. No hay atajos de teclado registrados en la ventana. El mensaje posterior a abrir los archivos indica «Usa Ctrl+P para imprimir», pero ese atajo corresponde al visor PDF, no a la aplicación (`ui/main_window.py:224-275, 354-390`).

## 4. Problemas probables y fragilidades

- **Coordenadas y formato rígidos:** tres rectángulos absolutos y primera página únicamente. Un cambio de plantilla, tamaño, escala, texto escaneado o ubicación puede dar falsos resultados o no extraer el número (`core/pdf_processor.py:11-17, 59-65`).
- **Carpeta y configuración dependientes del directorio de trabajo:** `config.json`, `data/` y el registro usan rutas relativas; iniciar el `.exe` desde otra carpeta puede buscar o crear configuración y estado en otro lugar (`main.py:19-25, 42-48, 199`; `core/state_manager.py:18-20`).
- **Detección incompleta tras desconexión:** el sondeo de arranque procesa todos los PDF pendientes, pero los siguientes solo consideran archivos modificados en los últimos 10 minutos. Un PDF antiguo incorporado mientras la aplicación está cerrada o la carpeta inaccesible podría quedar fuera (`core/monitor.py:94-136`). El vigilante no recorre subcarpetas (`core/monitor.py:80`).
- **Eventos repetidos:** creación y modificación encolan el mismo archivo, y el sondeo también puede encolarlo; la deduplicación persistente sucede más tarde, al procesar. Puede haber trabajo repetido y casos de carrera (`core/monitor.py:14-27, 94-136`; `core/orchestrator.py:32-68`).
- **Se marca impresa al abrir, no al imprimir:** al abrir correctamente el PDF, la aplicación cambia inmediatamente su estado a impreso. Si la persona cancela o el visor falla después, el registro puede indicar algo que no ocurrió (`ui/main_window.py:224-253`). Además, el aviso de error dice que los archivos no abiertos se marcaron impresos, aunque esa rama no los marca (`ui/main_window.py:255-267`).
- **Impresión configurada pero sin uso visible:** la configuración guarda `impresora`; la función de impresión directa existe en `utils/printer.py`, pero la interfaz abre el visor y no la invoca. El comportamiento real es impresión manual.
- **Colisiones de números:** la tabla permite una sola fila por combinación de número y estado (`core/state_manager.py:49-60`). Un segundo PDF con el mismo número pendiente no reemplaza la ruta guardada (`core/state_manager.py:77-96`); podría ocultar el archivo duplicado en vez de ofrecer revisión.
- **Ciclo de vida de hilos:** los hilos del monitor son daemon y el sondeo duerme el intervalo completo; `stop()` no conserva ni espera el hilo de sondeo (`core/monitor.py:70-91`). Un cierre o cambio de mes puede dejar el sondeo anterior activo brevemente y provocar trabajo sobre módulos ya cerrados.
- **Exportación limitada:** el CSV incluye solo los registros en estado impreso; no ofrece pendientes, dudosos ni descartados (`core/state_manager.py:205-227`; `ui/main_window.py:277-302`).

## 5. Qué se puede reutilizar de Hormiguero

- **Extracción de PDF:** `src/Hormiguero.Mensajero.Core/LogicaOfisuiza.cs` ya implementa extracción por zonas con una reproducción de `page.get_text("text", clip=rect)` de PyMuPDF. Puede servir de base técnica para adaptar las tres zonas JCV, validando antes las coordenadas y reglas con ejemplos autorizados. Las zonas de Ofisuiza son de otro documento; no se deben reutilizar sus coordenadas como si fueran las de JCV.
- **Vigilancia:** `VigilanteCarpeta` ofrece vigilancia no recursiva de PDF, filtro de extensión sin distinguir mayúsculas, eventos de error y una espera breve para archivos nuevos. Puede ser base del ingreso en Mensajero; el comportamiento para archivos existentes al iniciar o reconexión debe definirse para sustituir el sondeo actual.
- **Núcleo PDF y archivos:** `Hormiguero.Nucleo.Pdf.LectorPdf` informa páginas, dimensiones, texto, PDF dañado y protegido. `Huella` calcula SHA-256 y `MovedorSeguro` copia, verifica igualdad y solo después borra el origen, adecuado si la herramienta nueva necesita trasladar documentos. El programa actual no mueve ni copia sus PDF.
- **ClickFactura:** `src/Hormiguero.Mensajero.Core/ClickFactura/` ya organiza lectura de planilla, análisis, búsqueda de PDF y preparación de correo para otro flujo de facturas; puede aportar convenciones de integración, no la lógica específica de Cobelcar.
- **Archivero:** ya identifica y archiva PDFs. Conviene reutilizar sus servicios y decisiones de clasificación/archivo si el rediseño requiere archivar, en lugar de crear un segundo mecanismo. M-A7 no define todavía que JCV deba archivar documentos.

## 6. Preguntas para Javier

1. ¿Qué carpetas exactas debe observar y debe incluir subcarpetas?
2. ¿Qué tipos de factura debe reconocer además de Cobelcar?
3. ¿Qué campos necesita extraer y cuáles son obligatorios?
4. ¿Debe aceptar facturas escaneadas como imagen?
5. ¿Qué debe hacer con el PDF cedible cuando llega sin su original?
6. ¿Debe abrir el visor o imprimir automáticamente?
7. ¿Cuándo considera una factura realmente impresa?
8. ¿Qué hacer si llegan dos PDF con el mismo número?
9. ¿Qué información necesita consultar o exportar y por cuánto tiempo?
10. ¿Los PDF se quedan en su carpeta o deben archivarse en otra ubicación?

## Reporte del agente

- Se revisaron AP04/AP05 respetando D-10 y D-11: solo nombres, tipos y fechas de archivos; código fuente; y claves de configuración. No se leyeron datos reales ni se ejecutaron aplicaciones originales.
- Se completó el análisis de versiones, flujo, pantalla, fragilidades, reutilización y preguntas para Javier.
- Archivos modificados: `semillas/Mensajero/ANALISIS-FACTURAS-JCV.md`.
- `dotnet build`: correcto, 0 advertencias y 0 errores.
- `dotnet test`: correcto, 583 pruebas superadas en 5 conjuntos; 0 fallidas y 0 omitidas.


