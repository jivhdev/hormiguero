---
tipo: prototipo
app: Archivero
fecha: 2026-10-03
estado: boceto aprobado
---

# Prototipo — Archivero (versión final)

<!-- Fase 3 (MQD, sección 3; D-51). Boceto v2 aprobado por Javier el 2026-10-03 (D-58). Paso = escalón de la escalera: 2 apertura, 5 medio, 8 final. Atajos: Alt + A/S/D/F/G (D-55). -->

## Pantallas

### A1. Principal

| Zona | Elemento | Paso |
|---|---|---|
| Barra superior | Modo simple / completo; Configuraciones (ver y editar por emisor y tipo); Modelos de cadena; Certificados | 2 · 5 · 8 |
| Vigilancia | Carpetas vigiladas con estado, "Cambiar carpetas", "Revisar ahora" (D-52) | 2 |
| Guardados recientes | Documento → carpeta destino, "Abrir" y "Carpeta"; cedibles descartadas a la vista; historial completo | 2 |
| Guardados recientes | Marca de impreso al archivar | 5 |
| Guardados recientes | Tiempo ahorrado del mes | 8 |
| Aviso | Por reconocer (se revisan solos al crear una configuración nueva) → "Identificar" | 2 |
| Aviso | Sin texto o dañados → "Guardar a mano" (A3) | 2 |
| Aviso | Ya guardados antes → comparar lado a lado con zoom y descartar | 2 |

Invisible pero obligatorio (paso 2): registro de auditoría de cada movimiento; mover = copiar, verificar y borrar; ceros a la izquierda fuera de los nombres.

### A2. Asistente de identificación (un paso por pantalla, PDF de ejemplo siempre a la vista)

| Paso del asistente | Qué se hace | Paso |
|---|---|---|
| 1 Tipo | Marcar la zona que dice el tipo; asociarlo a un tipo con nombre | 2 |
| 2 Emisor | Marcar la zona del emisor (puede ser el RUT); autocompletado de emisores ya usados | 2 |
| 3 Número | Marcar la zona del número | 2 |
| 4 Dónde se guarda | Carpeta madre; subcarpetas año\añomes, solo año o directo; el mes sale de hoy o de la fecha marcada; vista previa del mes anterior, este y el siguiente | 2 |
| 4 Dónde se guarda | Mes forzado a mano | 5 |
| 5 Nombre | Dato marcado, nombre original o combinación | 2 |
| 5 Nombre | Confirmar el nombre cada vez (opción) | 5 |
| 6 Fecha y número enlazante | Solo modo completo: datos que Buscadero usa para enlazar | 5 |
| 7 Cliente, impresión, abrir | Enlazar a un cliente, imprimir al archivar (desactivado por defecto; 1ª página, preguntar o todas), abrir después de guardar | 5 |

### A3. Guardar a mano (escaneados y dañados)

Visor con zoom; elegir una ubicación existente o "Crear ubicación"; editar nombre y fecha; "Guardar" (paso 2). Guardado rápido con atajos configurables, conservando la fecha (paso 5).

### A4. Modelos de cadena (modo completo, paso 5)

Qué documentos forman una cadena y en qué orden (ej.: orden de cliente → orden a proveedor → guía → factura de proveedor → factura a cliente), para que Buscadero enlace.

### A5. Certificados (paso 8)

Cargar certificados de medidores en una carpeta e indexar sus datos.

**Aprobadas por Javier:** sí (boceto)

## Flujo entre pantallas

A1 → "Identificar" → A2 → A1. A1 → "Guardar a mano" → A3 → A1. A1 → Modelos de cadena → A4. A1 → Certificados → A5.

## Cierre

<!-- Regla de cierre (D-28): Javier aprueba cada pantalla. Boceto aprobado (D-58); falta el prototipo WPF con capturas. -->
