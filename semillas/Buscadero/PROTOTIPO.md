---
tipo: prototipo
app: Buscadero
fecha: 2026-10-03
estado: boceto aprobado
---

# Prototipo — Buscadero (versión final)

<!-- Fase 3 (MQD, sección 3; D-51). Boceto v2 de la versión final aprobado por Javier el 2026-10-03 (D-58). El prototipo WPF con capturas viene después. Paso = escalón de la escalera de desarrollo: 1 apertura, 4 medio, 7 final. Atajos: Alt + A/S/D/F/G (D-55). -->

## Pantallas

### P1. Primera configuración (paso 1)

"¿Dónde están tus documentos?": texto explicativo, lista de carpetas (equipo, red o Drive) con "Quitar", "Agregar carpeta" y "Listo, empezar" (no se puede empezar sin al menos una carpeta). Después se llega desde Configuración.

**Aprobada por Javier:** sí (boceto)

### P2. Pantalla principal

| Zona | Elemento | Paso |
|---|---|---|
| Barra superior | Alertas (cantidad) · Dudosos (cantidad) · Configuración | 7 · 4 · 1 |
| Buscador | Campo de número grande, "Buscar", "Cancelar" | 1 |
| Buscador | Alcance: primera coincidencia / todas las carpetas / carpeta específica | 1 |
| Buscador | Autocompletado desde 2 letras e historial de búsquedas recientes | 4 |
| Filtros | Proveedor, Tipo, Año, Mes, Carpeta; "Solo lectura" (sin marcas ni cambios) | 4 |
| Resultados | Cada documento una vez, "En N carpetas", etiquetas de versión (original, cedible, escaneado, firmada); estado del índice | 1 |
| Resultados | Doble clic: vista rápida sin perder el documento actual | 4 |
| Visor | PDF con zoom y rotación | 1 |
| Visor | Modo "Marcar" (visto, X, raya, círculo, texto) o "Seleccionar texto"; mostrar/ocultar marcas; "Comparar" lado a lado | 4 |
| Visor | "Imprimir 1ª página", "Imprimir 2 primeras", "Ver en carpeta" | 1 |
| Panel Cadena | Datos: tipo de despacho, fecha, cliente, estado ("guía esperando factura"); eslabones verde enlazado, ámbar dudoso, gris falta; guía con su firmada | 4 |
| Panel Cadena | "Quitar" un documento exigiendo motivo (proveedor, interna u otro) | 4 |
| Panel Cadena | Abrir la cadena en ventana aparte; alerta especial (esperando nota de crédito, esperando fabricación, retiro futuro) | 7 |
| Panel Dudosos | Cuatro tipos separados, decididos mirando el documento en el visor: dudoso real (Enlazar / No corresponde), OCC reemitida (Reemplazar / Omitir), duplicado exacto (Marcar revisado; nunca se borra), guía firmada (confirmar su par) | 4 |
| Panel Alertas | Vencimientos y calculadora: fecha + días hábiles o corridos | 7 |

**Aprobada por Javier:** sí (boceto)

### P3. Configuración

Carpetas (paso 1); feriados y plazos de alerta por tipo de documento o proveedor (paso 7).

**Aprobada por Javier:** sí (boceto)

## Flujo entre pantallas

P1 (solo la primera vez) → P2. Desde P2: Configuración → P3 → P2; Cadena en ventana aparte (paso 7).

## Cierre

<!-- Regla de cierre (D-28): Javier aprueba cada pantalla. Boceto aprobado (D-58); falta el prototipo WPF con capturas. -->
