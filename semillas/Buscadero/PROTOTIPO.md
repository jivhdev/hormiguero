---
tipo: prototipo
app: Buscadero
fecha: 2026-10-03
estado: boceto aprobado
---

# Prototipo — Buscadero (versión final)

<!-- Fase 3 (MQD, sección 3; D-51). Boceto de la versión final aprobado por Javier el 2026-10-03 ("dale, sigue con Archivero y Mensajero"). El prototipo WPF con capturas viene después. Paso = escalón de la escalera de desarrollo (1 apertura, 4 medio, 7 final). -->

## Pantallas

### P1. Primera configuración (paso 1)

**Para qué sirve:** elegir las carpetas donde Buscadero busca. Aparece solo la primera vez; después se llega desde Configuración.

**Qué se ve:** título "¿Dónde están tus documentos?", texto que explica que pueden ser carpetas del equipo, de red o de Drive, y la lista de carpetas elegidas con "Quitar".

**Qué se puede hacer:** "Agregar carpeta", "Quitar", "Listo, empezar" (no se puede empezar sin al menos una carpeta).

**Aprobada por Javier:** sí (boceto)

### P2. Pantalla principal

| Zona | Elemento | Paso |
|---|---|---|
| Barra superior | Nombre de la app; Alertas (con cantidad); Dudosos (con cantidad); Configuración | 7, 4, 1 |
| Buscador | Campo de número grande + "Buscar" | 1 |
| Filtros | Proveedor, Tipo, Año, Mes, Carpeta | 4 |
| Columna izquierda | Resultados: cada documento una vez, "En N carpetas", etiquetas de versión (original, cedible, escaneado); estado del índice | 1 |
| Centro | Visor del PDF con zoom y rotación | 1 |
| Centro, arriba | Marcas (visto, X, raya, círculo, texto), "Mostrar/ocultar marcas", "Comparar" (lado a lado) | 4 |
| Centro, abajo | "Imprimir 1ª página", "Imprimir 2 primeras", "Ver en carpeta" | 1 |
| Panel derecho, pestaña Relacionados | Cadena del documento: verde enlazado, ámbar dudoso, gris falta | 4 |
| Panel derecho, pestaña Dudosos | Lista; al tocar uno se abre en el visor para decidir mirándolo: "Enlazar" o "No corresponde" | 4 |
| Panel derecho, pestaña Alertas | Vencimientos y calculadora: fecha + días hábiles o corridos | 7 |

**Aprobada por Javier:** sí (boceto)

### P3. Configuración

Carpetas (paso 1); feriados y plazos de alerta por tipo de documento o proveedor (paso 7).

**Aprobada por Javier:** sí (boceto)

## Flujo entre pantallas

P1 (solo la primera vez) → P2. Desde P2, "Configuración" abre P3 y vuelve a P2.

## Cierre

<!-- Regla de cierre (D-28): esta fase termina cuando Javier aprueba cada pantalla. Boceto aprobado; falta el prototipo WPF con capturas. -->
