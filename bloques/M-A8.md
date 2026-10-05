---
bloque: M-A8
app: Mensajero
fase: C (D-72)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Mensajero.Core/**, src/Hormiguero.Mensajero/**, tests/Hormiguero.Mensajero.Core.Tests/** (salvo Equivalencia/**), semillas/Mensajero/EQUIVALENCIA-OFISUIZA.md, semillas/Mensajero/EQUIVALENCIA-CLICKFACTURA.md]
archivos_prohibidos: [todo lo demás; NO toques tests/**/Equivalencia/** (respuestas correctas de los originales); no cambies AlmacenMensajero salvo para guardar plantillas con LeerValor/GuardarValor]
rama: mensajero/a8-textos-cortos
---

# M-A8 — Textos cortos aprobados como plantillas configurables

Lee D-70 y D-72 en `definicion/DECISIONES.md`.

## Qué hacer

1. **Plantillas**: los mensajes de retiro (por proveedor), guía y cuerpo de correo de facturas pasan a generarse desde **plantillas de texto con marcadores** (por ejemplo `{OCC}`, `{OCL}`, `{DIA}`, `{BLOQUE}`, `{FECHA_GUIA}`, `{OBRA}`, `{TIPO_DOCUMENTOS}`, `{SEMANA}`, `{RAZON_SOCIAL}`; define los que hagan falta y documéntalos). Las plantillas se guardan como configuración (`AlmacenMensajero.LeerValor/GuardarValor`, claves `plantilla.*`); si no hay una guardada, se usa la **predeterminada** = texto nuevo de abajo. Sin pantalla de edición todavía (anótalo como pendiente).
2. **Originales intactos**: las funciones actuales que reproducen exactamente a Ofisuiza y ClickFactura se conservan (las pruebas de `Equivalencia/` deben seguir pasando sin tocarlas). Si las renombras, ajusta solo las pruebas que no son de equivalencia.
3. **La pantalla usa las plantillas nuevas** (retiro, guía y cuerpo de facturas). El asunto de facturas y el texto "Proveedor no reconocido" no cambian.
4. **Espacio final**: todo texto que se copia como cuerpo de correo termina con una línea en blanco (ya existe `PrepararCuerpoCorreo`; verifica que se aplica a todos los mensajes, incluidos los nuevos).
5. Pruebas nuevas (fuera de `Equivalencia/`): cada plantilla predeterminada produce **exactamente** el texto de abajo con datos de ejemplo (`OCC 104523`, `OCL 4500012345`, día vacío → `_______________`, bloque "Manual" → `_______________`); plantilla guardada reemplaza a la predeterminada; marcador desconocido queda tal cual.
6. Actualiza las listas de equivalencia marcando el cambio aprobado (D-72).

## Textos predeterminados (exactos; `[...]` = dato)

COBELCAR:
```
Retiro en BODEGA FRAY CAMILO 889
Identificarse como: "Retiro JCV"

OCC [OCC]
OCL [OCL]

Dirección: Fray Camilo Henríquez 889, Santiago
Horario: lunes a viernes, 09:00 a 17:00 hrs
```
HOFFENS:
```
Retiro en HOFFENS
Identificarse como: "Retiro JCV"

OCC [OCC]
OCL [OCL]
NVV _______________

Día: [DIA]
Bloque: [BLOQUE]
Dirección: Camino Lonquen 10707, Maipú
Mapa: https://maps.app.goo.gl/smWJQSwCrq2vfCcx7

Si no se retira ese día, queda armado 3 días hábiles más, en el mismo horario.
```
SENSUS:
```
Retiro en SENSUS (Bodega E1 INVAC)
Identificarse como: "Retiro JCV"

OCC [OCC]
OCL [OCL]

Dirección: Camino del Cerro 290, Quilicura
Mapa: https://maps.app.goo.gl/Eg8LzVUWXCSym7Yp7
Horario: lunes a jueves 08:00-13:00 y 14:00-16:00 / viernes 08:00-13:00

Antes de ir, enviar nombre y teléfono de quien retira y el día (se genera un QR para entrar).
```
CHILE HDPE:
```
Retiro en CHILE HDPE
Identificarse como: "Retiro JCV"

OCC [OCC]
OCL [OCL]

Dirección: Cacique Colín 11950, Lampa
Mapa: https://maps.app.goo.gl/HwHhbufKS8cBGboG9
Horario: 08:30 a 17:30
```
Guía (Hoffens) — `[FECHA_GUIA]` = "hoy" / "ayer" / "el día X" (si "Otro" está vacío, "hoy"); `[OBRA]` = "Obra, Comuna" (o solo obra; si falta obra o es "No detectada", `_______________`):
```
Buenos días:

Envío la guía emitida [FECHA_GUIA] para la obra [OBRA].
Si llegan más guías hoy, se las envío. Hoffens lo contactará para coordinar la entrega.

Saludos.
```
Cuerpo de facturas — `[TIPO_DOCUMENTOS]` = "facturas", "notas de crédito" o "facturas y notas de crédito" (misma regla que hoy):
```
Estimados:

Adjunto las [TIPO_DOCUMENTOS] de la [SEMANA] de [RAZON_SOCIAL].

Saludos cordiales.
```

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Reporte al final.

## Reporte del agente
