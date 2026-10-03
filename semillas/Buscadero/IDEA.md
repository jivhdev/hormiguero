---
tipo: idea
app: Buscadero
fecha: 2026-10-03
estado: cerrada
---

# Idea — Buscadero

<!-- Fase 1 (MQD, sección 3). Borrador armado con la idea de Buscadero de MQD v1 (2026-09-13) y las decisiones D-22 y D-41. Javier confirmó los puntos de la v1 el 2026-10-03; el de enlaces se reemplazó por D-48. -->

Rol en Obrera: **consulta y control** — lo que se tiene (D-22). Absorbe Buscadero, el visor con marcas, la comparación y el seguimiento (cadenas, dudosos, alertas y vencimientos de ficheri, D-41).

## Lo que NO quiero

- Una interfaz llena de opciones: debe ser simple, para un usuario básico.
- Lento.
- Que el PDF no sea lo principal de la pantalla.
- Que no muestre claramente los enlaces entre documentos.
- Que vigile o indexe todas las carpetas del equipo: solo las que el usuario elige.
- Que su configuración inicial dependa del entorno de un usuario en particular: debe poder configurarse desde cero en cualquier oficina.
- Rígido en cómo se ordenan y enlazan los documentos: la lógica de negocio cambia según la empresa.
- Que enlace documentos en silencio cuando hay duda (D-48).
- Que modifique el PDF original al marcarlo.
- Que ralentice o sobrecargue carpetas de red o servidores ("vecino silencioso, pero confiable").
- Que comprometa la seguridad de los documentos de la empresa.

## Lo que más o menos SÍ quiero

- Buscar documentos por número exacto, solo dentro de las carpetas que el usuario elige.
- Afinar la búsqueda por proveedor, tipo de documento, carpeta madre, año y mes (en los distintos formatos en que el mes aparece en las carpetas).
- Un visor de PDF como pantalla principal.
- Una pestaña aparte, no encima del PDF, con los documentos relacionados.
- Relacionar documentos en un orden (ej.: orden de cliente → orden a proveedor → guía de despacho → factura de proveedor → factura a cliente).
- Marcas sobre el PDF (visto, X, raya, círculo, texto libre movible) sin modificar el original, con botón para mostrar u ocultar todas.
- Comparar dos documentos lado a lado, sobre todo de una misma cadena.
- Gratis, rápido y apto para equipos de gama baja.
- Fácil de configurar (agregar carpetas), pero sin funcionar hasta que esa configuración mínima esté hecha.
- Carpetas locales, de red (por ejemplo, las del ERP) o de Drive.
- Que la búsqueda no tarde minutos: adelantar trabajo mientras no se está buscando, sin sobrecargar la red.
- Botones para imprimir la primera página o las dos primeras.
- Enlazar documentos solo cuando el dato calza con certeza; los dudosos quedan en una lista para que el usuario decida (D-48).
- Calcular vencimientos (fecha + días hábiles o corridos, sin feriados) y avisar con una alerta.

## Quién la usa

Un oficinista en su propio equipo: primero Javier, después cualquier oficinista que lo instale. Cada uno trabaja con sus propias carpetas (locales, de red o Drive). No hay usuarios ni contraseñas dentro de la app.

## ¿Toca algo fuera del equipo? (red, carpetas compartidas, internet)

Sí: puede observar carpetas de red o servidor compartido (por ejemplo, las del ERP) y de Drive. Si indexa de forma agresiva, puede afectar a otros usuarios de esa red. Aplica la regla del "vecino silencioso" (MQD, sección 9).

## Cierre

<!-- Regla de cierre (D-28): al menos un "no quiero" y un "sí quiero", confirmados por Javier. -->

Fase 1 cerrada el 2026-10-03: Javier confirmó la lista completa (D-48) y quién la usa.
