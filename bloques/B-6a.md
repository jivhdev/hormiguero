---
bloque: B-6a
app: Buscadero (y núcleo)
fase: B-6 (PLAN-MIGRACION.md)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [definicion/DISENO-B6-MARCAS-Y-CADENAS.md]
archivos_prohibidos: [todo lo demás: este bloque NO escribe código]
rama: buscadero/b6a-diseno-cadenas
---

# B-6a — Diseño: marcas y cadenas en la base común

## Contexto

B-6 (ver `definicion/PLAN-MIGRACION.md`): "Marcas y cadenas de Buscadero en la base común; enlaces con las configuraciones de Archivero". Es la base para la fase C: **cadenas documentales** (OCC → NVV → guía → factura enlazadas; "con la OCC muéstrame cliente, NVV, guía y obra") y después **alertas y vencimientos** (D-41: fecha + N días hábiles sin feriados o corridos, enlazada a una alerta).

Lee primero: `AGENTS.md`, `definicion/PLAN-ECOSISTEMA.md`, `definicion/PLAN-MIGRACION.md`, `definicion/DECISIONES.md` (D-41, D-50, D-56, D-65 a D-68), `definicion/PENDIENTES-FUNCIONALES.md`, `definicion/ANALISIS-REQUISITOS-EMERGENTES.md`. Código: `src/Hormiguero.Nucleo/Datos/` (BaseComun, Migraciones, Documentos, Identificaciones, DocumentosGuardados), `src/Hormiguero.Buscadero.Core/` (cómo guarda hoy marcas y cadenas, si las tiene), `src/Hormiguero.Archivero/` (configuraciones/tipos de documento y qué campos identifica: OCC, NVV, OCL, guía, factura...). Ideas previas de Javier (solo lectura): `C:\Users\jihja\Desktop\Entorno Antiguo\AP06-Motores Versiones Antiguas\motores\bitacora\` (instrucción maestra: formato de intercambio y vinculación) y `motores\Fase3-Vinculacion\`.

D-68: Buscadero y Archivero parten de cero (no hay que migrar marcas viejas).

## Qué entregar: `definicion/DISENO-B6-MARCAS-Y-CADENAS.md` (español neutro, claro, breve)

1. **Qué existe hoy**: dónde y cómo se guardan marcas/cadenas/identificaciones (archivo y línea), qué campos identifica Archivero por tipo de documento.
2. **Modelo propuesto** en `hormiguero.db` como migración v5 de `Hormiguero.Nucleo`: tablas, columnas, claves, índices. Debe permitir: marcar documentos; enlazar documentos en cadenas por un **número común** (OCC, NVV, OCL...) automáticamente y también a mano; una cadena puede ramificarse (una OCC con varias guías y facturas); consultar en milisegundos "todo lo enlazado a la OCC X"; y dejar lugar para alertas/vencimientos (sin implementarlas).
3. **Reglas de enlace automático**: qué campo de qué tipo de documento enlaza con cuál (tabla), cómo se evitan enlaces falsos (números cortos, ceros a la izquierda), qué pasa si un documento se reemplaza o se mueve.
4. **Concurrencia y seguridad**: Archivero escribe y Buscadero lee a la vez (WAL); nada se borra sin dejar rastro (Auditoria).
5. **Plan de bloques** para implementarlo (B-6b, B-6c...): cada uno chico, con archivos y pruebas, y cuáles son triviales (para OpenCode Go) y cuáles no.
6. **Preguntas para Javier** (máximo 5, cortas), solo si algo no se puede decidir con lo escrito.

Reporte al final de esta nota.

## Reporte del agente
