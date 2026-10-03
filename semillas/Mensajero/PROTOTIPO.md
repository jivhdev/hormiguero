---
tipo: prototipo
app: Mensajero
fecha: 2026-10-03
estado: boceto aprobado
---

# Prototipo — Mensajero (versión final)

<!-- Fase 3 (MQD, sección 3; D-51). Boceto v3 aprobado por Javier el 2026-10-03 (D-58). Paso = escalón de la escalera: 3 apertura, 6 medio, 9 final. Mensajero solo prepara; el usuario envía (D-54). Atajos: Alt + A/S/D/F/G (D-55). Cada copia muestra "● Copiado: …". -->

## Pantallas

### M1. Inicio (14 funciones, D-56)

| Grupo | Función | Paso |
|---|---|---|
| Pedidos y retiros | 1 Pedido al proveedor (OCC nueva) · 2 Buscar OCC por número · 3 Mensaje de retiro · 4 Clientes que retiran | 3 |
| Pedidos y retiros | 7 Marcar enviada al proveedor | 6 |
| Despachos y guías | 6 Envío de guías · 8 Avisar despacho al cliente y WhatsApp al encargado de obra · 9 Pedir guía firmada (una o en lote) | 6 |
| Facturas y reportes | 5 Correos de facturas | 3 |
| Facturas y reportes | 12 Reporte semanal en Excel (último día hábil) · 14 Pedir certificados | 9 |
| Datos y herramientas | 10 Datos para el ERP (NVV, RUT, obra, N° guía) · 11 Pasar a MAYÚSCULAS | 6 |
| Datos y herramientas | 13 Planillas: copiar ítems a la de precios y comparar dos listas | 9 |

### M4. Pedidos y retiros (paso 3; envío de guías paso 6)

| Zona | Elemento |
|---|---|
| Barra | Carpeta de OCC ("Cambiar"); modo automático "Activar / Detener" (vigila como D-52) |
| Origen | Buscador de OCC (Enter), "Último PDF"; archivo encontrado; datos extraídos: OCC, NVV, OCL, obra, comuna, proveedor; "Ver PDF" |
| Correo al proveedor | Asunto (OCC · NVV · OCL) Alt+S; cuerpo con el despacho Alt+D; "Marcar enviada" (paso 6) |
| Mensaje de retiro | Día y bloque; datos extra que pide cada proveedor (ej.: nombre y teléfono de quien retira); vista previa; Alt+F |
| Envío de guías | Obra y comuna; día de emisión hoy / ayer / otro; Alt+G (paso 6) |
| Clientes que retiran | Buscador; Enter copia el primero; doble clic copia; "Editar lista"; Alt+A |

### M2. Correos de facturas (paso 3; pendientes y abrir carpeta paso 6)

Semana, mes y año; planilla del ERP; lista de clientes con estado (preparando, enviado, pendiente, RUT sin registrar → "Registrar cliente"); "Preparando para: [cliente]"; correos como etiquetas separadas; asunto y cuerpo copiables con atajos; adjuntos sin cedibles; "Preparar envío" (deja los PDF listos para pegar), "Marcar como enviado", "Dejar pendiente", "Abrir carpeta"; ver solo pendientes.

### M3. Configuración

Clientes (RUT, razón social, correos); columnas de la planilla del ERP; dónde están los PDF (carpeta raíz y forma de subcarpetas, compartido con Archivero); tipos y marca de cedible; texto del correo con datos automáticos ({cliente}, {semana}, {documentos}); carpeta de adjuntos (paso 6); proveedores y bodegas (dirección, horario, mapa, datos que pide cada uno).

**Aprobadas por Javier:** sí (boceto)

## Flujo entre pantallas

M1 → cada función → M1. Una OCC nueva abre M4 sola cuando el modo automático está activo. Configuración (M3) desde la barra.

## Cierre

<!-- Regla de cierre (D-28): Javier aprueba cada pantalla. Boceto aprobado (D-58); falta el prototipo WPF con capturas. -->
