# Diseño C-2: cadenas asistidas desde Archivero

**Estado:** propuesta para conversar con Javier; todavía no es una especificación aprobada.

**Propósito:** que la persona configure en Archivero qué datos pueden servir para relacionar documentos y luego cree sus cadenas con ayuda de esas coincidencias. La persona decide qué documentos forman cada cadena. Hormiguero propone relaciones y deja las dudosas para revisión.

## 1. Recorrido de la persona

Los nombres de controles de estos bocetos son de trabajo. Los ejemplos de documentos y valores ilustran la propuesta; no son tipos fijos del programa. El diccionario debe servir a cualquier oficina (D-70), y la configuración de Javier puede entregarse precargada y editable.

### Primera vez: revisar el diccionario

En modo completo, Archivero ofrece el paso de datos enlazantes. Presenta un diccionario de ejemplo que la persona puede revisar antes de usarlo. Cada entrada representa un dato reconocible en distintos documentos; no es un documento ni una cadena. Se sugiere partir con:

- N° OC del cliente.
- OC propia.
- NVV.
- N° de guía.
- N° de factura.
- N° de nota de crédito.

La persona puede cambiar el nombre, agregar sinónimos que aparezcan en sus documentos, crear un dato, cambiar el orden o quitar una entrada. Antes de quitarla, Archivero informa qué tipos de documento y cadenas la usan, y permite revisar el efecto. Si Javier quiere empezar de cero, D-73 lo permite; debe quedar claro qué configuración se reiniciará. No se deben borrar por ese motivo documentos, versiones ni el historial crítico protegido por D-68.

```text
┌ Archivero · Datos que pueden relacionar documentos ──────────┐
│ Estos datos pueden aparecer en más de un tipo de documento. │
│                                                             │
│ N° OC del cliente       [Editar] [↑] [↓] [Quitar]          │
│ OC propia               [Editar] [↑] [↓] [Quitar]          │
│ NVV                     [Editar] [↑] [↓] [Quitar]          │
│ N° de guía              [Editar] [↑] [↓] [Quitar]          │
│ N° de factura           [Editar] [↑] [↓] [Quitar]          │
│ N° de nota de crédito   [Editar] [↑] [↓] [Quitar]          │
│                                                             │
│ [Agregar dato]             [Restaurar ejemplos] [Continuar] │
└─────────────────────────────────────────────────────────────┘
```

“Restaurar ejemplos” es una acción propuesta, no una decisión cerrada: se recomienda restaurar solo las entradas de ejemplo faltantes, sin sobrescribir cambios de la persona.

### Configurar un tipo de documento en Archivero

Al crear o editar una configuración de reconocimiento, la persona indica si el documento es **Emitido** o **Recibido**. El nombre se propone con el formato “Tipo · Emisor/Cliente”; por ejemplo, “Factura · Cobelcar” o “Guía · Hoffens”. El texto entre paréntesis es sugerido a partir del emisor o cliente y se puede editar. La pregunta sobre qué entidad debe ir en ese lugar queda abierta (véase pregunta 1).

En la sección nueva “Datos que aparecen en este documento”, la persona elige entradas del diccionario y marca cada zona en el PDF, igual que en el asistente actual. Tras marcar una zona, Archivero muestra el texto extraído y una vista del rectángulo. Se puede indicar que un dato no aparece en ese diseño de documento. Un mismo tipo puede tener más de un diseño o patrón, como ya permite Archivero.

```text
┌ Configuración de documento ──────────── PDF de ejemplo ──────┐
│ Grupo: (•) Emitido  ( ) Recibido      ┌───────────────────┐ │
│ Nombre sugerido: Factura · Cobelcar   │                   │ │
│                                       │  N° OC cliente:   │ │
│ Datos enlazantes que trae:            │  [4500012345]     │ │
│ [✓] N° OC del cliente  [Marcar zona]  │  ┌────────────┐   │ │
│ [ ] OC propia          [Marcar zona]  │  └────────────┘   │ │
│ [✓] N° factura         [Marcar zona]  │                   │ │
│                                       └───────────────────┘ │
│ Texto leído: 4500012345       Página 1 · [Probar otra vez]  │
│ [Guardar configuración]                         [Cancelar]  │
└─────────────────────────────────────────────────────────────┘
```

El nombre sugerido no sustituye el tipo y emisor que Archivero necesita para reconocer el PDF. Emitido/Recibido y la forma visible definitiva de nombrar los tipos requieren aprobación. La configuración nueva no cambia el destino, el renombrado ni las demás reglas de guardado existentes.

### Crear una cadena con ayuda

Desde Archivero, la persona inicia una cadena y elige los documentos que la forman. Puede buscar por tipo o por los datos extraídos, elegir documentos existentes y asignarles un orden. Mientras configura, Hormiguero detecta valores compartidos y presenta una propuesta concreta, con los documentos y el dato a la vista. Ejemplo: “Estos 3 documentos comparten la OC 4500012345: OCC, NVV y guía. ¿Agregar a la misma cadena?”. La persona acepta o descarta la propuesta y puede quitar o agregar documentos antes de guardar.

La coincidencia sugiere una relación; por sí sola no crea la cadena ni decide su composición. Si hay dos documentos con el mismo dato, un dato incompleto, una lectura incierta o conflicto entre valores, la propuesta explica qué revisar. Nunca se reemplaza silenciosamente un documento ya asociado. Las reglas de comparación detalladas del diseño B-6 (espacios, guiones y ceros iniciales) requieren ajustarse al diccionario y validarse con Javier; no se presentan como una elección técnica obligatoria.

```text
┌ Nueva cadena ────────────────────────────────────────────────┐
│ Documentos seleccionados (orden de la cadena)               │
│ 1. OCC · Cliente A              OC cliente: 4500012345       │
│ 2. NVV · Hoffens                 OC cliente: 4500012345       │
│ 3. Guía · Cobelcar               OC cliente: 4500012345       │
│                                                             │
│ Sugerencia: estos 3 documentos comparten N° OC del cliente   │
│ 4500012345. ¿Agregar a la misma cadena?                      │
│ [Agregar los 3] [Revisar documentos] [No agregar]            │
│                                                             │
│ Nombre de cadena: [OCC104523____________________]            │
│ [Guardar cadena]                                  [Cancelar] │
└─────────────────────────────────────────────────────────────┘
```

Al guardar, Buscadero muestra y permite consultar la cadena, los documentos y los valores relacionados. La ubicación exacta de la acción “crear cadena” entre Archivero y Buscadero es un punto de diseño a aprobar: D-75 dice que el flujo empieza en Archivero; D-71 asigna a Buscadero la consulta y administración de cadenas. La propuesta es que Archivero inicie el flujo y que ambas aplicaciones usen la misma información.

### Día a día: llega un documento nuevo

Archivero reconoce y archiva el documento con su flujo habitual, extrae los datos configurados y muestra una sugerencia si encuentra una cadena compatible: “Este documento tiene la OC 4500012345, igual que la cadena OCC104523. ¿Agregarlo?”. La persona puede abrir la cadena, aceptar o dejarlo pendiente. Si aparecen varias cadenas posibles, se muestran juntas con los valores que coinciden y no se elige una automáticamente. En modo observador futuro, Archivero puede sugerir una relación a partir del documento observado sin moverlo ni renombrarlo (D-69).

```text
┌ Documento recibido: Factura · Cobelcar ──────────────────────┐
│ Se archivó en: ...\2026\10                                  │
│                                                             │
│ N° OC del cliente: 4500012345                               │
│ N° factura:  FCV25001                                       │
│                                                             │
│ Posible cadena: OCC104523                                   │
│ Coincide N° OC del cliente: 4500012345                      │
│ [Ver cadena] [Agregar a esta cadena] [Dejar pendiente]       │
└─────────────────────────────────────────────────────────────┘
```

## 2. Qué se mantiene, cambia y se descarta

| Tema actual | Propuesta | Motivo |
|---|---|---|
| Modelos y vagones ordenados | Se mantienen como una posible forma de describir una cadena repetible, pero se crean y editan en lenguaje de documentos: elegir tipos y orden; los vagones no deben ser el concepto que primero ve la persona. | Los modelos representan una secuencia reutilizable útil (B-6 y D-71), pero Javier no entendió la configuración tal como estaba. |
| Cadena real derivada de un modelo | Se mantiene que la persona crea cada cadena. La asistencia ofrece documentos que coinciden; la persona confirma cuáles entran. | D-75 rechaza que la cadena se arme sola y pide creación asistida. |
| Regla de vagón configurada manualmente, campo por campo | Se reemplaza como flujo visible principal por el diccionario y la indicación de qué dato trae cada tipo en Archivero. Las reglas de comparación podrían conservarse como configuración avanzada solo si Javier confirma que aún las necesita. | D-75 mueve el punto de partida a Archivero y estandariza los datos. |
| Enlace exacto automático y lista de dudosos (D-48) | Se conserva el principio de no forzar coincidencias inciertas. D-48 permite enlace automático seguro; D-75 requiere asistencia y decisión humana al crear la cadena. Falta confirmar si un documento nuevo puede incorporarse automáticamente a una cadena existente con una regla que la persona haya activado. | Se evita interpretar “asistida” como autorización para armar cadenas completas en automático. |
| Dudosos de enlace | Se mantienen como propuestas que requieren decisión, mostrando el documento, la cadena, el valor y por qué hay duda. Debe distinguirse un dato no leído de dos cadenas que coinciden. | Compatible con D-48 y con la revisión clara en Buscadero. |
| Datos propios de Archivero | Sus marcas y extracción por coordenadas se aprovechan; los nombres libres actuales se vinculan a una entrada común del diccionario. Se mantiene la posibilidad de varios patrones para distintos diseños de PDF. | El usuario sigue indicando dónde está el dato, pero el mismo dato debe tener un nombre estable entre documentos (D-75). |
| Nombre de cadena y nombres de modelos | Se conservan editables. La preferencia actual por nombre de documento puede servir de sugerencia; no se da por cerrado el nombre automático ni la fórmula “personalizada”. | Evita perder una función útil y deja los nombres visibles bajo decisión de Javier (D-73). |
| Anexos, múltiples y cadenas hijas | No forman parte del recorrido inicial propuesto. Mantenerlos solo si Javier confirma casos concretos que el orden simple no cubra; diseñarlos como etapa posterior y con ejemplos suyos. | Son capacidades avanzadas que aumentan la dificultad de configuración. D-73 permite partir de una configuración más simple. |

D-73 permite reemplazar la configuración actual de modelos, vagones y reglas y empezar de cero. Se propone no migrar automáticamente esos ajustes al flujo nuevo: ofrecer una transición/reinicio explícito, conservar los PDF y datos críticos, y explicar qué configuración quedará sin efecto. La decisión final sobre conservar modelos antiguos como referencia es de Javier.

## 3. Edición, eliminación y orden

D-73 exige que todo lo que configure la persona se pueda corregir después. En la interfaz se propone una acción de edición y de baja para cada elemento, y controles de orden donde el orden tenga significado.

- **Diccionario:** editar nombre y sinónimos, agregar, quitar y reordenar. Al quitar un dato usado, mostrar los tipos y cadenas que lo utilizan y pedir que se resuelvan esas referencias; preservar valores históricos necesarios para entender documentos ya procesados. Permitir redefinir el diccionario y reiniciar la configuración, con una vista previa de lo afectado.
- **Datos de un tipo de documento:** agregar/quitar qué datos trae; cambiar la zona, página y patrón; volver a marcar y probar en PDF; cambiar Emitido/Recibido y nombre sugerido. El cambio vale para documentos futuros. Para corregir un valor ya leído, mostrar una corrección explícita y su efecto en sugerencias; no alterar el PDF original.
- **Cadenas:** cambiar nombre y orden de documentos; agregar o retirar un documento; aceptar o descartar sugerencias; deshacer una asociación con registro del cambio. Antes de retirar un documento que tenga alertas o relaciones dependientes, informar qué quedará afectado.
- **Modelos:** cambiar nombre, secuencia y datos requeridos; duplicar para crear una variante; desactivar o eliminar una configuración no usada. Si se usa en cadenas existentes, editar el modelo no debe reescribirlas. Eliminar o reiniciar modelos debe advertir el efecto; no borrar el historial.
- **Emitidos y Recibidos:** son grupos de presentación, no datos enlazantes. Si se cambia un tipo de grupo, conservar el historial de documentos con su clasificación anterior y aplicar el cambio a los siguientes, salvo que Javier defina otro comportamiento.

Para baja o reinicio se recomienda evitar borrado físico de elementos ya referenciados: dejar de ofrecerlos en configuraciones nuevas y conservar la información histórica y auditoría conforme a D-68. Esto concreta la editabilidad de D-73 sin confundir “eliminar una configuración” con borrar un documento.

## 4. Encaje de las alertas de C-1

Las alertas siguen siendo parte de Seguimiento en Buscadero, según C-1 y D-71. La cadena creada con asistencia pasa a ser el lugar donde se ve qué documento está enlazado, cuál falta y qué alerta corresponde. Archivero aporta los datos del nuevo documento y puede avisar que existe una cadena compatible; no administra la bandeja de alertas ni calcula vencimientos.

Una alerta puede asociarse a una cadena o a un documento/vagón, ser manual o venir de una regla opcional. Ejemplo configurable: “Guía recibida, falta factura después de 5 días hábiles”. Fecha, calendario, días, texto y repetición siguen el diseño C-1; el ejemplo no impone un plazo por tipo de documento. Si llega el documento que faltaba, Buscadero puede ofrecer resolver la alerta según las reglas de C-1. Una cadena no crea alertas por sí sola: se requiere una alerta manual o una regla configurada.

```text
┌ Buscadero · Cadena OCC104523 ────────────────────────────────┐
│ OCC · Cliente A               Enlazado                       │
│ NVV · Hoffens                 Enlazado                       │
│ Guía · Cobelcar               Enlazado                       │
│ Factura                       Falta                           │
│                                                             │
│ Alerta: Guía esperando factura · vence 16 oct.              │
│ [Ver alerta] [Revisar documentos]                           │
└─────────────────────────────────────────────────────────────┘
```

## 5. Preguntas para Javier

Son decisiones abiertas que afectan a los nombres o al comportamiento. Recomendaciones para discutir, no valores ya aprobados.

1. **¿El segundo elemento del nombre estándar debe ser el emisor, el cliente o quien entrega/recibe?** Recomendación: mostrar “Tipo · Emisor/Cliente” y permitir corregirlo por configuración. Ejemplo: “Factura · Cobelcar” para FCV y “Nota de crédito · Hoffens” para NCV, si esos emisores son los que corresponden.
2. **¿“Emitido” y “Recibido” se determinan según quién creó el documento o según desde qué lado de la operación se mira?** Recomendación: que la persona lo elija por tipo de documento y pueda cambiarlo. Ejemplo: una NVV emitida por Hoffens podría ser recibida por tu oficina; revisar también una OCC.
3. **¿Qué entradas exactas debe traer el diccionario inicial y qué significan OC del cliente y OC propia en tus casos?** Recomendación: empezar con los seis ejemplos de D-75 y ajustar etiquetas con tus palabras. Ejemplo: distinguir la OCC tuya de la OC que Cobelcar imprime en una factura o guía.
4. **¿La marca de cada dato se hace una vez por tipo o puede variar por emisor/diseño del PDF?** Recomendación: permitir varios diseños de PDF para el mismo tipo, con sus propias zonas. Ejemplo: comparar guías Cobelcar con guías Hoffens y revisar dónde aparece la OC.
5. **Cuando coincida un documento nuevo con una sola cadena, ¿quieres que se incorpore tras mostrar una sugerencia o que se enlace solo bajo una regla activada?** Recomendación: sugerir y pedir confirmación inicialmente; evaluar automatismo seguro después. Ejemplo: FCV y NCV con la misma OCC, frente a una NVV de Hoffens que trae la misma OC.
6. **¿Una cadena básica es solo una secuencia de documentos o necesitas anexos, varios documentos del mismo tipo y cadenas hijas desde el primer uso?** Recomendación: iniciar con una secuencia simple y sumar esas opciones cuando un caso real las necesite. Ejemplo: una guía Cobelcar y su guía firmada podrían probar anexos; varias NCV para la misma OCC podrían probar múltiples.
7. **Si se modifica o elimina un dato del diccionario que ya usa una cadena, ¿qué resultado prefieres para lo configurado y lo histórico?** Recomendación: no borrar registros históricos; advertir qué configuración queda afectada y permitir desactivar o reemplazar el dato. Ejemplo: renombrar “OC cliente” cuando ya aparece en una OCC, una guía Hoffens y una factura FCV.
8. **Al aprobar el rediseño, ¿se reemplazan los modelos actuales o quieres conservarlos solo como consulta antes de reiniciar?** Recomendación: mostrar una transición con conteo de modelos/reglas que dejarán de usarse, y reiniciar solo con una acción explícita; nunca eliminar documentos ni historial. Ejemplo: revisar modelos con vagones OCC, NVV y factura antes de comenzar el flujo nuevo.

## 6. Plan propuesto después de la aprobación

Los bloques no se deben ejecutar hasta que Javier responda las preguntas de diseño que cambian comportamiento y apruebe los nombres visibles. “Trivial para OpenCode Go” indica trabajo mecánico con decisiones cerradas; Claude revisa igual, de acuerdo con el proceso del repositorio.

| Bloque | Alcance propuesto | OpenCode Go |
|---|---|---|
| C-2b | Definir el contrato y los ejemplos aprobados del diccionario; migración/repositorio común en Núcleo, edición, orden, baja e historial. Datos de muestra opcionales y editables. Pruebas de configuración nueva, reinicio, referencias e historial. | No trivial: define el contrato común y las referencias. |
| C-2c | Cargar como datos de ejemplo el diccionario aprobado, de forma editable e idempotente; incluir casos ficticios para revisión. | **Trivial** después de aprobar el contrato y los seis nombres del diccionario. |
| C-2d | Adaptar Archivero: grupo Emitido/Recibido, nombre estándar aprobado, selección de entradas del diccionario y marcas de zona/patrón; publicar valores identificados sin cambiar el PDF. | No trivial: integra asistente, lectura de zonas y datos comunes. |
| C-2e | Pantallas simples para crear y editar modelos/secuencias en Archivero; definir compatibilidad con modelos actuales o inicio desde cero según decisión aprobada. | No trivial: interacción con cadenas existentes y una configuración avanzada. |
| C-2f | Flujo de creación manual de cadena y panel de sugerencias por datos compartidos; aceptar, descartar, revisar empates y deshacer. Pruebas con OCC, NVV, guías Cobelcar/Hoffens, FCV y NCV ficticias. | No trivial: decisión humana, estados dudosos y seguridad de enlaces. |
| C-2g | Sugerencia al archivar/llegar un documento nuevo, incluyendo varias cadenas posibles y modo observador cuando se construya. | No trivial: integración entre recepción, carpetas y cadenas. |
| C-2h | Integrar cadena y datos enlazantes con alertas C-1 en Buscadero; crear/consultar alertas según reglas aprobadas y mostrar documentos faltantes. | No trivial: cruza dos diseños y estados persistentes. |

No se marcan bloques como triviales mientras falten decisiones de Javier sobre modelo básico, automatismo, nombres o transición desde la configuración actual.

## Reporte del agente

- **Resultado:** propuesta de diseño escrita para revisión de Javier, con recorrido y cuatro bocetos, comparación con B-6, reglas de edición, relación con C-1, ocho preguntas recomendadas y plan de bloques.
- **Archivos modificados:** `definicion/DISENO-C2-CADENAS-ASISTIDAS.md` únicamente.
- **Verificación:** `dotnet build` correcto (0 errores, 0 advertencias). `dotnet test` correcto (684 pruebas superadas, 0 errores, 0 omitidas).
- **Decisiones pendientes:** las preguntas de la sección 5 y la aprobación de los textos/nombres visibles. El documento no implementa ni elige esas decisiones por Javier.
