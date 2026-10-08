# Diseño C-3 — Esquemas por proveedor, cadenas y alertas

Fuente: D-84 (conversación con Javier, 2026-10-08), antecedentes de MQD 01 / Motores (patrones de alerta A y B, condición de cierre por caso, "todo se configura en el mismo flujo"), D-77, D-78, D-83. Todo es **configurable por el usuario**; nada de un proveedor o empresa concreta va en el código. Cada día Javier parte de cero: no se requiere compatibilidad con cadenas "simples" anteriores (las tablas viejas quedan sin uso).

## 1. Conceptos

- **Esquema de cadena (por proveedor):** la plantilla de un proceso comercial de compra-venta con un proveedor. **Uno por proveedor.** Lo arma el usuario en un asistente guiado.
  - **Lugares**: lista ordenada de tipos de documento (los configurados en Archivero), p. ej. OC del cliente → Nota de venta propia → OC propia → Guía del proveedor → Factura del proveedor → Guía propia → Factura propia.
  - **Tipos de inicio**: uno o varios lugares marcados "inicia la cadena" (p. ej. Nota de venta propia y OC propia).
  - **Líneas**: un lugar puede recibir **varios** documentos. Los lugares pueden declararse **pareja 1 a 1** (p. ej. Guía del proveedor ↔ Factura del proveedor): cada guía tiene una factura y viceversa; las parejas cuelgan del documento de origen que comparten (la OCC).
  - **Modos** (Fase 3): nombres que define el usuario (p. ej. "Retiro", "Despacho"); cada modo tiene sus alertas. Un lugar se marca como "decide el modo".
  - **Alertas** (Fase 3): reglas por lugar y por modo.
- **Cadena**: un proceso real (p. ej. "NVV 4512 · Proveedor X · Cliente Y"). Usa el esquema de su proveedor. Tiene proveedor (obligatorio), cliente (si algún documento lo trae), modo (si el esquema tiene modos) y estado.
- **Proveedor**: entidad (lista de Archivero). En documentos "del proveedor" es su emisor; en los propios de compra (OC propia) es el dato "Nombre/RUT del proveedor" (D-84). **Un documento de un proveedor no puede quedar sin proveedor**: si no se extrae, Archivero lo pide.

## 2. Cómo se llena una cadena (Fase 2)

1. Llega (Archivero u observador) un documento publicado. Se determina su **proveedor**.
2. Si su tipo es **de inicio** en el esquema de ese proveedor y no calza con una cadena existente → **se crea la cadena sola** con ese documento.
3. Si comparte un dato enlazable con documentos de una cadena del mismo proveedor (reglas de D-84: literal → automático; solo limpio → dudoso; varias cadenas → dudoso) → entra **a su lugar** del esquema; si el lugar es pareja 1 a 1, se empareja con el documento de la pareja que comparte dato (p. ej. la factura con la guía que tiene su mismo número de guía).
4. **Sin piso**: pertenece al esquema pero no calza con ninguna cadena y no es de inicio → queda en Archivero, sección **"Decisiones pendientes"**, con tres opciones: *Buscar el documento de origen* (abre Buscadero buscando el número que menciona) · *Crear la cadena igual* · *Solo archivar, sin cadena*.
5. Proveedor sin esquema → el documento se publica normal, sin cadena (aviso informativo "Este proveedor no tiene esquema de cadena").

## 3. Asistente "Esquema del proveedor" (Buscadero → Cadenas → "Esquemas…")

Un paso por pantalla, explicación arriba, Atrás/Siguiente, resumen final con "Cambiar":
1. **Proveedor**: elegir de la lista de entidades (o escribir). Si ya tiene esquema, se edita.
2. **Documentos del proceso**: el programa muestra los tipos configurados en Archivero y **cuáles ya comparten datos del diccionario entre sí** (p. ej. "Factura del proveedor ↔ OC propia comparten *N° OC propia*"), para ayudar. El usuario elige los lugares y los ordena (subir/bajar).
3. **¿Cuáles inician la cadena?** Marcar uno o varios.
4. **Parejas 1 a 1** (opcional): unir dos lugares (Guía ↔ Factura del proveedor) e indicar por qué dato se emparejan (de los que comparten).
5. *(Fase 3)* **Modos**: ¿el proceso tiene modos? Nombrarlos (Retiro, Despacho, …) y elegir qué lugar decide el modo.
6. *(Fase 3)* **Alertas** por lugar y modo (ver §5).
7. **Resumen** y Guardar. Vista previa con un ejemplo dibujado del árbol.

## 4. Vista de la cadena (Buscadero y buscador maestro)

- Lista de cadenas con filtros **proveedor, cliente, estado, modo**; agrupables por cliente o por proveedor.
- Detalle **visual en árbol por lugares y líneas**: arriba los documentos de inicio/origen; debajo, por cada OCC, sus parejas guía ↔ factura en filas; lugares vacíos en gris ("falta"). Clic en un documento lo abre en el visor. Marca de avisos con color.
- Acciones: mover un documento a otro lugar, quitarlo (con historial), resolver dudosos.

## 5. Alertas (Fase 3)

Se configuran en el paso 6 del asistente, por lugar y por modo. Tres tipos:
1. **Falta ingresar un dato** (p. ej. modo Retiro: "fecha de retiro"): mientras la cadena en ese modo no tenga el dato → aviso "Ingresar fecha de retiro". El dato se ingresa en la cadena (o desde "Decisiones pendientes").
2. **Plazo entre documentos** (patrón B): desde el lugar X, si en N días (hábiles/corridos, calendario de feriados) no llega Y → aviso con texto del usuario. La fecha base es la **fecha de emisión** del documento X (dato informativo "Fecha del documento"), o, si se elige, **un dato ingresado** (p. ej. fecha de retiro). Condición opcional "solo si existe [lugar Z]". Por línea si el lugar es pareja (cada guía su factura). Ejemplos de Javier: Retiro → "si pasa la fecha de retiro y no llegó la guía de despacho → Cliente no retiró" (retirado = llegó el documento que lo respalda); "Factura del proveedor → 7 días corridos sin guía firmada → avisar", solo en el esquema del proveedor que lo requiere.
3. **Listo para…**: cuando llega X y se cumple la condición (p. ej. guía firmada + su factura existe) → la cadena aparece en la lista "Listos para facturar al cliente" hasta que llega Y (factura propia). Sin plazo.
Cierre: automático al llegar el documento esperado o al ingresar el dato; manual "descartar" con motivo.

**Dónde se ven:** Buscadero → **"Avisos"**: una lista ordenada por urgencia (rojo = vencido, naranja = vence hoy o mañana, normal), filtros proveedor/cliente/cadena/tipo, y secciones **"Listos para…"**. En cada cadena (lista y buscador maestro) una marca "⚠ n" con el color más urgente. **Sin notificaciones de Windows** por ahora.

**Modo de la cadena:** cuando llega el documento del lugar que "decide el modo", Archivero agrega en **"Decisiones pendientes"**: "NVV 4512 · Proveedor X · ¿Retiro o Despacho?" (botones con los modos del esquema). Cambiable después en la cadena. (Más adelante: decidirlo leyendo una zona.)

## 6. Fuera de alcance por ahora
Varios esquemas por proveedor; cadenas madre/hija; estados finos de cierre (reclamos, ajustes) — se verán después de alertas; decidir modo automáticamente por zona; notificaciones de Windows.

## 7. Bloques sugeridos
- **C3-a (Núcleo)**: tablas de esquema (lugares, inicio, parejas, modos, reglas), cadena con proveedor/cliente/modo/estado, documento↔lugar↔línea; motor que crea/ubica según §2; migración nueva.
- **C3-b (Buscadero)**: asistente de esquema (§3 pasos 1-4 y 7) y vista de cadena en árbol (§4).
- **C3-c (Archivero)**: proveedor obligatorio en documentos de proveedor; sección "Decisiones pendientes" (sin piso).
- **C3-d (Núcleo + Buscadero + Archivero)**: modos y alertas (§3 pasos 5-6, §5), vista "Avisos", decisión de modo.
