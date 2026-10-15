---
bloque: R-1b
app: Núcleo + Archivero (pruebas que faltaron en R-1)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Nucleo/**, src/Hormiguero.Archivero/**, tests/**]
archivos_prohibidos: [migraciones 1-20 intactas; Mensajero; Buscadero]
rama: nucleo/r1-cadenas-jcv
---

# R-1b — Pruebas pendientes de R-1 (y arreglar lo que muestren)

R-1 dejó solo 2 pruebas (normalizar e importar). Faltan las que pedía `bloques/R-1.md` → "Pruebas". Escríbelas sobre la base real (migraciones + `configuracion/cadenas-jcv.json` importado) y, si alguna falla, **arregla el código** (no la prueba):

1. Reconocimiento: OCC con `rut_proveedor` "96.548.020-K", GRC con emisor "HOFFENS" (identificación "Del proveedor") y FCC con `nombre_proveedor` "HOFFENS S.A." → **la misma cadena** del esquema HOFFENS. Un documento con RUT sin calce sigue el comportamiento anterior.
2. GRCF sin texto: registrada como lo hace `IdentificarSinTextoWindow` con tipo `guia_firmada_proveedor` + proveedor HOFFENS y archivo `0001190036.pdf` (número = 1190036) → queda en la **línea de la GRC 1190036** de su cadena. Prueba también el orden inverso (GRCF antes que la GRC: queda sin piso y al llegar la GRC... documenta qué pasa hoy; R-2 lo cambiará).
3. Punta a punta Hoffens (fechas fijas): OCC 20417 (RUT Hoffens, NVV 20092, OCL 73106) → nace la cadena y pide modo; modo Despacho RM → a los 6 días hábiles sin GRC hay aviso "Proveedor no ha despachado"; GRC 1190036 con N° OCC 20417 → entra y cierra ese aviso; FCC 2419619 con N° guía 1190036 → entra en la línea; 8 días después sin GRCF → aviso "Falta guía firmada"; GRCF → cierra el aviso y la cadena aparece en "Listos para facturar"; FCV con N° guía 1190036 → sale de la lista.
4. Lo mismo para Cobelcar en versión corta: OCC 20527 (RUT 78.615.580-0) → GRC 313091 (emisor "VELIZ BELTRAN LIMITADA") → FCC con guía 313091 → "Listos para facturar" sin GRCF.
5. Modo Retiro con dato "Fecha de retiro" = día D: sin GRC al día D+1 → "Cliente no retiró".

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores en tus archivos. Reporte al final con lo que tuviste que arreglar.

## Reporte del agente
