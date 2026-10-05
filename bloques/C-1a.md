---
bloque: C-1a
app: Núcleo y Buscadero (Seguimiento)
fase: C
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [definicion/DISENO-C1-ALERTAS-Y-VENCIMIENTOS.md]
archivos_prohibidos: [todo lo demás: este bloque NO escribe código]
rama: docs/c1-diseno-alertas
---

# C-1a — Diseño: alertas y vencimientos

Lee `AGENTS.md` y en `definicion/`: `DECISIONES.md` (D-41 ficheri, D-48, D-50, D-56, D-68 a D-72), `PLAN-ECOSISTEMA.md`, `DISENO-B6-MARCAS-Y-CADENAS.md` (dejó espacio para alertas), `PENDIENTES-FUNCIONALES.md`, `semillas/Buscadero/{IDEA,SPEC,PROTOTIPO}.md` (panel de cadena: eslabón que falta, "guía esperando factura", alertas especiales: esperando nota de crédito, esperando fabricación, retiro futuro). Antecedentes de Javier (solo lectura): `C:\Users\jihja\Desktop\Entorno Antiguo\AP06-Motores Versiones Antiguas\motores\bitacora\` (alertas/seguimiento). Código actual: `src/Hormiguero.Nucleo/Datos/` (v5/v6), `src/Hormiguero.Buscadero.Core/Lineas/`.

## Qué entregar: `definicion/DISENO-C1-ALERTAS-Y-VENCIMIENTOS.md`

1. **Qué pide Javier**, con citas de las decisiones y semillas.
2. **Cálculo de fechas (Núcleo, Utilidades)**: fecha + N días **hábiles** (sin sábados, domingos ni feriados) o **corridos**, hacia adelante o atrás. **Feriados configurables** en un archivo/tabla editable, con Chile precargado (D-70: cualquier país; nada fijo en código). Casos borde.
3. **Alertas configurables por el usuario** (D-70, D-71), ligadas a cadenas y vagones: por ejemplo "si el vagón *Factura* sigue vacío 5 días hábiles después de llenarse *Guía*, avisar 'Guía esperando factura'"; alertas manuales sobre un documento o cadena con fecha; estados (pendiente, vencida, resuelta, descartada) con historial; nada se borra.
4. **Dónde se ven** (D-71: cada app desde donde corresponde): Buscadero (seguimiento: lista de alertas con contador, en la cadena), y cómo se avisan al abrir las apps; sin servicios en segundo plano complicados si no hacen falta.
5. **Modelo de datos** (migración v7) y **plan de bloques** chicos (marca triviales para OpenCode Go), y pantallas amigables en palabras simples.
6. **Preguntas para Javier**: máximo 3, cada una con recomendación.

Español neutro, claro, breve. Reporte al final de esta nota.

## Reporte del agente
