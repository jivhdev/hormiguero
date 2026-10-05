# Diseño C-1: alertas y vencimientos

**Alcance:** cálculo de fechas en Núcleo/Utilidades y alertas de Seguimiento en Buscadero, sobre la base SQLite común administrada por Núcleo. Es diseño para la fase final; no implementa código ni cambia la apertura actual.

## 1. Qué pide Javier

- Recuperar de ficheri el cálculo desde una fecha que ingresa la persona, sumando o restando días hábiles (sin sábados, domingos ni feriados) o corridos, y enlazar el vencimiento a una alerta. D-41 asigna el cálculo a Núcleo/Utilidades y la alerta a Seguimiento. En la lista de pendientes, Javier explica que necesitaba saber cuándo vencería un documento recibido y que solía sumar siete días mentalmente ([D-41](DECISIONES.md#L47), [PENDIENTES-FUNCIONALES.md](PENDIENTES-FUNCIONALES.md#L40)).
- Tener una aplicación configurable para cualquier oficinista. Países, proveedores, mensajes y feriados son datos configurables; nada específico de Javier queda fijo en código. Los datos de Javier pueden venir precargados como configuración ([D-70](DECISIONES.md#L76)).
- Permitir cadenas configuradas por la persona, con vagones opcionales y reglas de enlace por vagón; cada app muestra y modifica lo que le corresponde. Cuando hay duda, el enlace va a revisión y no se hace en silencio ([D-48](DECISIONES.md#L54), [D-71](DECISIONES.md#L77)).
- Incorporar alertas y vencimientos en el juego final de Buscadero, después de filtros, cadenas, marcas y comparación ([D-50](DECISIONES.md#L56)). La idea confirmada de Buscadero incluye avisar vencimientos; su prototipo deja un contador de alertas en la barra, un panel de alertas y plazos/feriados en Configuración ([IDEA](../semillas/Buscadero/IDEA.md#lo-que-mas-o-menos-si-quiero), [PROTOTIPO](../semillas/Buscadero/PROTOTIPO.md#p2-pantalla-principal)).
- Mostrar en la cadena el eslabón enlazado, dudoso o faltante. El prototipo aprobado menciona “guía esperando factura” y alertas especiales para “esperando nota de crédito”, “esperando fabricación” y “retiro futuro” ([PROTOTIPO](../semillas/Buscadero/PROTOTIPO.md#p2-pantalla-principal)). Son ejemplos configurables, no tipos fijos de negocio.
- Mantener el modelo de cadenas y vagones de B-6. Ese diseño reserva una futura alerta relacionada con cadena o enlace, con fecha base, cantidad, tipo de día, vencimiento y estado; B-6 no la crea ni la calcula ([DISENO-B6-MARCAS-Y-CADENAS.md](DISENO-B6-MARCAS-Y-CADENAS.md#6-integridad-auditoria-y-consulta)).
- No construir alertas en la apertura: la SPEC aprobada las deja para el paso 7 junto con vencimientos, feriados y ventana aparte de la cadena ([SPEC](../semillas/Buscadero/SPEC.md#5-que-no-construir-en-la-apertura)).

El reparto sigue siendo de todo el ecosistema: Mensajero tiene acciones y funciones relacionadas con alertas en sus pasos, pero la cadena y la bandeja se consultan desde donde corresponde (D-56, D-71); sus textos son plantillas configurables (D-72). Si Archivero identifica un documento nuevo, puede publicar el evento usando el modo observador sin alterar el archivo vigilado (D-69). D-68 establece partir de cero para Buscadero: las notas antiguas sirven como antecedente, no como migración ni requisito cerrado. Registran alertas por OCC/línea, una bandeja agrupable, varios avisos por NCC, fabricación/stock y retiro futuro. También advierten un fallo cuando la evaluación dependía de que cada acción “tocara” la OCC. Se rescata la evaluación explícita, sin depender de una escritura incidental; no se copian nombres, umbrales ni datos específicos sin configuración/confirmación.

## 2. Cálculo de fechas en Núcleo/Utilidades

Proponer una utilidad pura que reciba fecha base, cantidad entera con signo, modo (`corridos` o `habiles`) y calendario de feriados. Devuelve una fecha sin hora. El signo permite avanzar o retroceder; la cantidad cero devuelve la fecha base.

- **Corridos:** resultado = fecha base más la cantidad de días de calendario, incluidos fines de semana y feriados.
- **Hábiles:** avanzar o retroceder un día por vez y contar únicamente lunes a viernes que no estén en el calendario. La fecha base no cuenta como uno de los días; por ejemplo, un día hábil desde viernes llega al lunes si no hay feriado. Con cero días se conserva la fecha base, aunque sea inhábil.
- **Feriados:** tabla editable en la base local, sin lista de países ni fechas inmutables en el código. Cada calendario identifica país y, si se necesita, ámbito regional/local; sus fechas y nombres son filas editables. Se entrega un calendario de Chile precargado como datos y se permite crear/importar otros calendarios. Cambiar el calendario no reescribe las fechas ya guardadas en alertas: estas conservan el cálculo y el calendario usados al crearse; la persona puede recalcularlas explícitamente.
- Reglas de borde: fechas base inválidas y cantidades fuera del rango admitido se rechazan con un error claro; los feriados repetidos cuentan una sola vez; sábado/domingo y feriado coincidentes se saltan una sola vez; una fecha que cae en inhábil se permite como fecha base/vencimiento si el conteo corrido o la cantidad cero la produce. Evitar desbordamiento de fecha y bloquear una operación que no pueda terminar dentro del rango admitido.

La calculadora del panel de alertas puede usar la misma utilidad sin crear alerta. El valor por defecto para tipo de día y calendario debe pertenecer a la regla/configuración y poder cambiarse; no inferirlo del documento ni fijar siete días para todos.

## 3. Alertas configurables

Una alerta puede nacer de una regla o ser manual. La regla se configura sobre un modelo de cadena/vagón y puede referirse a otro vagón de la cadena; las reglas son opcionales. Un ejemplo de configuración, no de comportamiento codificado: “cuando se complete Guía, si Factura sigue vacía al pasar 5 días hábiles, crear aviso: ‘Guía esperando factura’”. La persona selecciona vagones, condición, espera, calendario, texto y si se repite. Se muestra una frase completa para revisar antes de guardar, siguiendo el patrón amigable de configuración descrito por B-6.

Las reglas admiten, como mínimo, estas fuentes de fecha: fecha ingresada por la persona (vencimiento manual) o evento observable de la cadena, como enlazar una versión al vagón. La ausencia de documento se evalúa contra el vagón de destino de esa misma cadena. Crear alerta una sola vez por combinación de regla y evento/cadena/vagón; las re-evaluaciones al abrir la app no duplican registros. Si cambia la condición, queda historial y no se borra la alerta previa. Una regla puede crear alertas adicionales ante un nuevo evento si así se configuró.

La alerta manual se puede asociar a un documento/versión, a una cadena o a un vagón. Incluye texto, fecha objetivo y, opcionalmente, cantidad/modo/calendario para calcular esa fecha. Si solo se asocia una fecha, esa fecha es la objetivo. No requiere crear una regla.

Estados persistentes: **pendiente**, **vencida**, **resuelta** y **descartada**. Una alerta pendiente pasa a vencida cuando la fecha objetivo ya pasó según la fecha local del equipo; la evaluación sucede al abrir la app y al entrar/actualizar el panel. Resolver o descartar requiere una acción explícita; descartar pide motivo. Si se completa el vagón que originó un aviso, ofrecer resolverlo, pero no resolverlo automáticamente. Cada creación, cambio de estado, edición de fecha/texto, recalculo, resolución y descarte agrega un evento de historial inmutable con fecha, acción, motivo si existe y valores anteriores/nuevos necesarios para entender el cambio. No se borran alertas ni eventos.

## 4. Dónde se ven y cómo se avisan

- **Buscadero, pantalla principal:** contador de alertas pendientes/vencidas en la barra, como en el prototipo aprobado. Al abrir, Núcleo evalúa fechas y reglas pendientes contra los datos locales y Buscadero presenta un resumen discreto; la persona abre la lista para atenderlas. No abrir una ventana modal por cada alerta.
- **Lista de alertas:** mostrar texto, estado, fecha objetivo, cadena/vagón o documento, y permitir filtrar por estado/fecha y ordenar por vencimiento. Seleccionar una alerta abre el documento o la cadena correspondiente; cuando se trata de una cadena, el estado de alerta también aparece en su panel y en la ventana de cadena del paso 7.
- **Otras apps:** cada app muestra avisos solo cuando puede actuar sobre su parte de la información. Archivero puede aportar el evento de documento recibido/identificado mediante Núcleo; Mensajero puede consultar la cadena al preparar una salida. La bandeja y gestión de estas alertas siguen en Buscadero, según D-71; no replicar bandejas en todas las apps.
- **Ejecución:** sin servicio residente ni consultas periódicas. Evaluar al iniciar Buscadero y cuando cambia una cadena/regla/documento por una acción normal de una app; dejar una acción manual “revisar ahora” disponible en el panel. Sin conexión, la evaluación usa la última información local disponible y muestra si una carpeta enlazada no está disponible; el cálculo de fechas no necesita red.

Los textos visibles del prototipo son referencias aprobadas. Todo texto nuevo y nombre visible de controles queda como propuesta hasta que Javier lo apruebe; no elegir nombres finales en este diseño.

## 5. Modelo de datos: migración v7

Agregar tablas en Núcleo, respetando claves foráneas, transacciones, auditoría y la regla de no borrado físico de B-6. Las tablas actuales llegan hasta v6; v7 agrega solo estas entidades y sus índices:

| Tabla | Campos principales propuestos |
|---|---|
| `calendarios_feriados` | `id`, `nombre`, `pais_codigo`, `region` nullable, `activo`, `origen`, fechas de creación/edición. País y región son configuración, no enumeración cerrada. |
| `feriados` | `id`, `calendario_id`, `fecha` (ISO `YYYY-MM-DD`), `nombre`, `activo`; único por calendario y fecha. Baja lógica al quitar una fecha. |
| `reglas_alerta` | `id`, `nombre`/texto, `activa`, `modelo_cadena_id` nullable, `vagon_origen_modelo_id` nullable, `vagon_destino_modelo_id` nullable, condición/evento, días, modo, `calendario_id` nullable, texto configurable, repetición y marcas de tiempo. Regla nula en alertas manuales. |
| `alertas` | `id`, `regla_id` nullable, `cadena_id` nullable, `vagon_cadena_id` nullable, `version_id` nullable, `clave_evento` nullable y única junto con la regla, `tipo` configurable, texto y motivo/estado actual, `fecha_base`, `cantidad_dias`, modo, `calendario_id` nullable, `fecha_objetivo`, `creada_en`, `actualizada_en`, `resuelta_en`/`descartada_en` nullable. Guardar una instantánea de regla/datos de cálculo para que editar la regla no cambie avisos existentes. |
| `historial_alertas` | `id`, `alerta_id`, acción, estado anterior/nuevo, datos anteriores/nuevos necesarios, motivo nullable, fecha y app de origen. Solo inserciones; no cascada de borrado. |

Índices por estado/fecha objetivo, cadena/estado, vagón/estado, versión/estado, regla/activa y calendario/fecha. Índice único de idempotencia para la identidad del evento que originó una alerta de regla. Las referencias a cadena, vagón o versión son opcionales según el tipo de alerta y no se eliminan en cascada. Antes de cerrar implementación, precisar restricciones e identidad idempotente junto con los contratos de eventos de las apps.

## 6. Plan de bloques pequeños

Cada bloque debe limitar archivos y pruebas en su nota. “Trivial para OpenCode Go” solo cuando las decisiones estén cerradas y el cambio sea mecánico; Claude revisa igualmente.

| Bloque | Entrega | OpenCode Go |
|---|---|---|
| C-1b | Contrato de cálculo de fechas y pruebas unitarias de corridos/hábiles, dirección, cero y bordes; todavía sin feriados configurables. | **Trivial** una vez fijada la convención de conteo. |
| C-1c | Migración v7 para calendarios/feriados y repositorio de Núcleo; precarga editable de Chile y calendario vacío genérico. Pruebas de migración repetida, unicidad y baja lógica. | **Trivial** si el esquema y la fuente de datos de Chile ya están aprobados; en otro caso, Claude. |
| C-1d | Cálculo hábil conectado a calendarios, edición de feriados y casos de borde. | **Trivial** tras C-1c y pruebas definidas. |
| C-1e | Tablas/repositorios de reglas, alertas e historial; estados, motivos, transacciones e idempotencia. | **No trivial**: persistencia e historial. |
| C-1f | Evaluador al abrir/actualizar, eventos de cadena y vencimiento; evitar duplicados y no resolver solo. | **No trivial**: contratos entre Núcleo y apps. |
| C-1g | Lista, contador, panel de cadena, calculadora y ajustes con textos aprobados; integración con Archivero/Mensajero donde corresponda. | **No trivial**: flujos y accesibilidad entre pantallas. |

Pantallas en palabras simples: configuración guiada con una frase de ejemplo; lista con qué falta, para cuándo y dónde; detalle con acciones claras para resolver/descartar; calculadora con fecha inicial, días, tipo de día y resultado. Usar textos cortos y botones grandes siguiendo D-35. Javier aprueba los nombres y textos nuevos antes de fijarlos.

## 7. Preguntas para Javier

1. **¿Cómo se cuentan los días?** Recomendación: excluir la fecha base; el día 1 es el siguiente día contado en la dirección elegida; cero conserva la fecha base. Así, un día hábil desde viernes cae lunes si no hay feriado.
2. **¿Qué debe pasar si desaparece la condición de una alerta automática?** Recomendación: conservarla pendiente/vencida hasta que la persona la resuelva o descarte; mostrar la condición actual para que pueda decidir. Evita cerrar trabajo sin confirmación y conserva el antecedente de la bitácora antigua.
3. **¿Qué fuente se usa para precargar y actualizar feriados de Chile y de otros países?** Recomendación: incluir Chile como datos editables versionados con año/cobertura visibles, permitir edición/importación manual y no descargar actualizaciones automáticamente; definir una fuente verificable antes de cargar años futuros.

## Decisiones de Claude (2026-10-05, Javier pidió avanzar sin consultar; puede cambiarlas)

1. Conteo: se excluye la fecha base; el día 1 es el siguiente día contado en la dirección elegida; 0 conserva la fecha base (1 hábil desde viernes = lunes si no hay feriado).
2. Alertas automáticas: si la condición se cumple por un enlace confirmado (p. ej. llegó la factura al vagón), la alerta se **resuelve sola** y el historial dice por qué ("resuelta automáticamente: llegó Factura"). Si la condición desaparece por otra razón (regla anulada, cadena anulada, documento quitado), queda pendiente para que la persona resuelva o descarte.
3. Feriados: Chile precargado como datos editables dentro de Hormiguero (año y cobertura visibles), sin servicios externos; otros países: calendario vacío editable. Actualizar es editar o importar un archivo simple.
