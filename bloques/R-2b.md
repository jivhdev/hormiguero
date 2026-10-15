---
bloque: R-2b
app: Núcleo + Buscadero (estados de la cadena y COV sugerida por cliente)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Nucleo/**, src/Hormiguero.Buscadero/**, src/Hormiguero.Buscadero.Core/**, tests/**]
archivos_prohibidos: [migraciones existentes intactas (nueva aditiva si hace falta); Archivero; Mensajero; configuracion/cadenas-jcv.json]
rama: nucleo/r1-cadenas-jcv
---

# R-2b — Estado de la cadena y COV sugerida (D-86 puntos 6 y nota de COV)

Lee D-86, `bloques/R-2a.md`, `R-2a2.md`, `R-2a3.md` y sus reportes (las ubicaciones se leen de la vista unificada de la migración 21).

## 1. Estado
`cadenas_diccionario.estado` tiene un CHECK ('activa','anulada') que no se puede cambiar: agrega una columna aditiva `situacion` ('activa','completa','cerrada', por defecto 'activa') + `situacion_desde` (fecha).
- **Completa**: la cadena tiene al menos una GRC y **cada línea de GRC tiene su FCV**, y toda NCC tiene su NCV.
- **Reabierta**: si llega una NCC o NCV (o cualquier documento) a una cadena completa o cerrada → vuelve a 'activa' (historial: "Reabierta: llegó NCC 123"). Al llegar la NCV que faltaba → vuelve a completa.
- **Cerrada**: completa hace **90 días** sin documentos nuevos (se revisa al evaluar avisos, una vez al día). Las cerradas no generan avisos nuevos.
- Recalcular al ubicar/quitar un documento y al evaluar. Mostrar el estado en Buscadero (lista de cadenas: filtro Activas / Completas / Cerradas / Todas; por defecto Activas + Completas) y en el detalle.

## 2. COV sugerida por cliente
La COV no trae números que la unan, pero sí el cliente (`rut_cliente`/`nombre_cliente`). En el detalle de una cadena, si la cadena tiene cliente (RUT del cliente de su NVV, OCL o FCV), mostrar "Cotizaciones del mismo cliente" con las COV archivadas de ese RUT (últimos 90 días, que no estén en otra cadena) y botón **"Agregar a esta cadena"** (y "Quitar" para las agregadas a mano). Lo mismo para COVP con "Agregar a mano…" (lista de COVP del proveedor del esquema, sin filtro de cliente).

## Pruebas
Completa con 2 GRC solo cuando ambas tienen FCV; NCC reabre y NCV vuelve a completa; 90 días → cerrada y sin avisos nuevos; documento nuevo reabre una cerrada; COV del mismo RUT aparece sugerida y se agrega/quita; COV de otro cliente no aparece.

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores en tus archivos. Español neutro. Reporte al final.

## Reporte del agente

Implementé la migración aditiva 22 con `situacion`, `situacion_desde`, el RUT del cliente, el historial de cambios y las cotizaciones agregadas a mano. El Núcleo recalcula la situación al ubicar o quitar documentos y al evaluar avisos: exige GRC, FCV para cada línea de GRC y NCV para cada NCC; reabre cadenas completas o cerradas al llegar otro documento, y cierra las completas tras 90 días. Las cerradas dejan de generar avisos.

Buscadero muestra la situación en la lista y el detalle. El filtro inicial incluye activas y completas, y permite filtrar cada situación por separado. En el detalle se sugieren COV sin otra cadena del mismo RUT, registradas en los últimos 90 días, y COVP del proveedor del esquema; ambas se pueden agregar y quitar a mano.

Agregué pruebas integradas para dos GRC, NCC/NCV, cierre a los 90 días, reapertura por documento nuevo, sugerencias COV por RUT, exclusión de otro cliente y agregado/retiro de COV y COVP.

Verificación: `dotnet build Hormiguero.slnx` terminó con 0 advertencias y 0 errores; `dotnet test Hormiguero.slnx` pasó las 827 pruebas; `dotnet csharpier check .` revisó 346 archivos sin errores.
