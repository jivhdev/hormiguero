# Análisis: requisitos que aparecen al usar la app

**Fecha:** 2026-10-03. **Estado:** propuesta de la sección 3 aprobada completa (D-28).

Origen del problema, en palabras de Javier: "el coso sirvio pero al usar la ap muchos requisitos salian a la luz :c eso parece que es inevitable, pero talvez podemos mejorar nuestro sistema, buscando recursos que nos sirvan".

## 1. Evidencia: los 26 Casos de Archivero y Buscadero

Clasificación aproximada de los ~55 requisitos que aparecieron después de empezar a construir (Archivero Casos 1-11, Buscadero Casos 1-15):

| Tipo | Cantidad | % | Ejemplos |
|---|---|---|---|
| Cómo se ve y se usa la pantalla | ~17 | 31 % | Rediseño del paso de carpetas, vista previa de tres tiempos, asistente de cadenas, ventanas maximizadas, zoom, modo visión |
| Casos borde con documentos reales o comportamiento sin definir | ~11 | 20 % | PDF sin texto o dañados, duplicados, ceros a la izquierda, qué significa exactamente "buscar", nombres inválidos |
| Errores y regresiones | ~10 | 18 % | Vigilancia que no reaccionaba, búsqueda "todo" que dejó de encontrar, espera de 5 s perdida |
| Funciones nuevas descubiertas al usar | ~9 | 16 % | Imprimir al archivar, tiempo ahorrado, "Ver en carpeta", nombrar cadenas |
| Modelo del negocio poco claro | ~4 | 7 % | Cadenas, ramificaciones y modelos hijo |
| Cambiar de opinión después de configurar | ~3 | 5 % | Cambiar la carpeta observada, reclasificar documentos antiguos |

## 2. Qué proponen los libros para cada problema

| Problema | Técnica | Fuente (biblioteca local) |
|---|---|---|
| Pantalla (31 %) | Prototipos antes del código: de baja fidelidad para explorar flujo y pasos, de alta fidelidad para el aspecto final. Un prototipo evolutivo se convierte en el producto; uno desechable solo sirve para aprender | Wiegers, *Software Requirements* 3E, cap. 15 "Risk reduction through prototyping" |
| Casos borde (20 %) | "Deliberate Discovery": buscar activamente lo que todavía no se sabe, lo antes posible, con ejemplos concretos | Smart, *BDD in Action*, sección 4.4 |
| Inevitables (16 %) | Partir el desarrollo en apertura (recorrido completo de punta a punta, aunque sea pobre), medio juego (reglas de negocio) y final (refinar con datos reales). Construir para aprender (MVP como experimento) | Patton, *User Story Mapping*, "Opening-, Mid-, and Endgame Strategy" y "Build to Learn" |
| Inevitables (16 %) | "Tracer bullets": un camino delgado que funciona de punta a punta desde temprano, para recoger requisitos mientras se construye | Thomas y Hunt, *The Pragmatic Programmer*, "Tracer Bullets" y "The Requirements Pit" |
| Inevitables (16 %) | Contar con el cambio desde el diseño, no tratarlo como un fracaso | Brooks, *The Mythical Man-Month*, cap. 11 "Plan to Throw One Away" |
| Errores (18 %) | Especificaciones ejecutables: cada Given/When/Then se vuelve una prueba automática que se corre siempre | Smart, *BDD in Action*, cap. 5 |

## 3. Cambios adoptados en MQD v2 (D-28)

1. **Mantener lo que funcionó:** idea vaga (qué sí / qué no), Given/When/Then, "¿Qué tal si...?", sección "qué NO construir" y el ciclo de Casos (capturó ordenadamente lo emergente).
2. **Agregar prototipo de pantallas antes de programar:** Javier ve y aprueba las pantallas (con datos de ejemplo, sin lógica) antes de construir. Ataca el 31 %.
3. **Agregar descubrimiento con documentos reales:** antes de especificar, se pasan los documentos de prueba por un catálogo de casos difíciles (sin texto, dañados, duplicados, formatos raros). Ataca el 20 %.
4. **Construir por juegos:** apertura = recorrido mínimo de punta a punta que Javier pueda usar pronto; medio = reglas de negocio; final = pulir con uso real. Así lo inevitable aparece temprano y barato. Ataca el 16 %.
5. **Criterios ejecutables:** todo Given/When/Then se convierte en prueba automática; Claude revisa cada bloque de OpenCode contra ellas. Ataca el 18 %.
6. **Agregar a "¿Qué tal si...?":** "¿y si el usuario quiere cambiar esto después de configurarlo?". Ataca el 5 %.
