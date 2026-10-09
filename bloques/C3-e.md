---
bloque: C3-e
app: Núcleo + Archivero (prueba de punta a punta del proceso)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [tests/Hormiguero.Archivero.App.Tests/**, tests/Hormiguero.Nucleo.Tests/**, src/Hormiguero.Nucleo/Datos/**, src/Hormiguero.Archivero/Servicios/**]
archivos_prohibidos: [todo lo demás; migraciones existentes intactas; no cambies pantallas]
rama: pruebas/c3e-punta-a-punta
---

# C3-e — Prueba de punta a punta: de Archivero a la cadena y sus avisos

Objetivo: una (o pocas) pruebas de integración que recorran **el flujo real** con las piezas ya integradas (C3-a, C3-c1, C3-c2, C3-d1, P5-1), usando el camino de publicación de Archivero (`PublicadorDatosDocumentoService` con configuraciones y datos del diccionario como los crea el asistente, `RepositorioDatosEnlazantes.GuardarDatoTipo`, campos con `dato_diccionario_id`) y no atajos de base de datos. Si una prueba revela un error de integración, **corrígelo** en el código permitido y explícalo en el reporte.

Escenario (datos sintéticos; reloj inyectable donde haga falta):
1. Tipos (identificaciones) con sus datos enlazables: Nota de venta propia (N° NVV, N° OC del cliente, nombre_cliente), OC propia (N° OC propia, N° NVV, nombre_proveedor), Guía del proveedor (N° Guía del proveedor, N° OC propia), Factura del proveedor (N° Factura del proveedor, N° Guía del proveedor, N° OC propia), Factura propia (N° Factura propia, N° NVV).
2. Esquema del proveedor "PROVEEDOR UNO": lugares en ese orden; inicio = Nota de venta propia; pareja Guía ↔ Factura del proveedor por N° Guía del proveedor; modos "Retiro" y "Despacho", decide la OC propia; reglas: (Retiro) falta dato "Fecha de retiro"; (Retiro) plazo desde dato "Fecha de retiro" hasta Guía del proveedor 0 días; (todos) plazo desde Factura del proveedor 7 días corridos hasta Factura propia por línea; listo_para cuando Guía del proveedor y exista Factura del proveedor hasta Factura propia ("Listos para facturar al cliente").
3. Flujo: publicar NVV (nace la cadena, cliente tomado) → OC propia con proveedor (entra a su lugar; decisión de modo pendiente) → fijar modo Retiro (aparece "falta fecha de retiro") → guardar fecha (se cierra; aparece plazo) → 2 guías del proveedor con distintos números y la misma OC → 2 facturas del proveedor, cada una con su N° de guía (se emparejan 1 a 1 en dos líneas) → avisos por línea y "Listo para facturar" por línea → Factura propia (cierra lo que corresponde). Además: una guía con N° OC "limpio" distinto al literal (va a dudosos); un documento de compra sin proveedor (no entra; queda sin cadena); una Factura del proveedor sin origen (decisión "sin piso"), y luego llega su origen (entra sola).
4. Verificar en cada paso: cadenas, lugares, líneas, decisiones, avisos abiertos/cerrados con su urgencia, listas "Listo para…".

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores en tus archivos. Reporte al final: qué errores de integración encontraste y cómo los corregiste.

## Reporte del agente
