---
tipo: idea
app: Mensajero
fecha: 2026-10-03
estado: borrador
---

# Idea — Mensajero

<!-- Fase 1 (MQD, sección 3). Borrador armado con lo que hacían ClickFactura y Ofisuiza, sus arreglos pendientes y los programas futuros. Los puntos marcados ⚠️ esperan decisión de Javier. -->

Rol en Obrera: **salida** — lo que se envía o se entrega (D-22). Absorbe ClickFactura, Ofisuiza, la ayuda para pasar datos al ERP, las herramientas de planillas y el pedido de certificados.

## Lo que NO quiero

- Que no quede claro a quién se le está preparando un envío (arreglo pendiente de ClickFactura).
- Que se mezclen los correos cuando un cliente tiene dos (arreglo pendiente).
- Que el programa decida dónde dejar los PDF temporales: lo decide el usuario (arreglo pendiente).
- Lógica de negocio precargada: proveedores, bodegas, plantillas de mensaje y clientes son configurables.
- ⚠️ Que envíe correos por su cuenta: en ClickFactura el envío final lo hacías tú a mano.

## Lo que más o menos SÍ quiero

- **Correos de facturas** (ClickFactura): leer la planilla que exporta el ERP, cruzar cada RUT con el registro de clientes (razón social y correos), encontrar los PDF de facturas y notas de crédito (sin las cedibles) y armar asunto y cuerpo según el tipo de documento y la semana.
- Dejar un cliente como pendiente de envío y poder volver a ver solo los pendientes (arreglo pendiente).
- Elegir la carpeta de los PDF temporales y abrirla con un botón (arreglo pendiente).
- **Mensajes de retiro y despacho** (Ofisuiza): a partir de una OCC, sacar sus datos (OCC, NVV, OCL, tipo de despacho, proveedor) y armar el mensaje para cada proveedor, con la dirección, el horario y el enlace al mapa de su bodega.
- Mensaje de envío de guías por proveedor (lo que Ofisuiza hacía para Hoffens).
- Lista de clientes frecuentes para retiro (clientes NVV) que se guarda bien entre sesiones.
- Copiar datos al portapapeles con un clic, para pegarlos en el ERP, el correo o WhatsApp.
- Pasar un texto a mayúsculas (arreglo pendiente de Ofisuiza).
- Herramientas de planilla: copiar ítems a la planilla de precios y comparar dos conjuntos de ítems para ver diferencias (programas futuros).
- Pedir certificados de medidores al proveedor con un mensaje preparado (certificados).
- Usar el registro de clientes y proveedores y los datos que reconoce Archivero desde la base común (ADR-001).

## Quién la usa

Un oficinista en su propio equipo: primero Javier, después cualquiera que lo instale.

## ¿Toca algo fuera del equipo? (red, carpetas compartidas, internet)

Lee PDF de carpetas de Drive o de red. ⚠️ Enviar correos o WhatsApp directamente sí tocaría internet; hoy solo se preparan y se copian.

## Cierre

<!-- Regla de cierre (D-28): al menos un "no quiero" y un "sí quiero", confirmados por Javier. -->
