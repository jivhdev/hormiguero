---
bloque: M-A6
app: Mensajero
fase: C (pedido directo de Javier, 2026-10-04)
estado: hecho
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Mensajero/VentanaPrincipal.xaml, src/Hormiguero.Mensajero/VentanaPrincipal.xaml.cs, src/Hormiguero.Mensajero/VentanaFacturas.xaml, src/Hormiguero.Mensajero/VentanaFacturas.xaml.cs, semillas/Mensajero/EQUIVALENCIA-OFISUIZA.md, semillas/Mensajero/EQUIVALENCIA-CLICKFACTURA.md]
archivos_prohibidos: [todo lo demás; no cambies textos de mensajes ni lógica de Core]
rama: mensajero/a6-atajos-alt
---

# M-A6 — Atajos con Alt, cercanos entre sí, y espacio antes de la firma

Javier: los Ctrl+número quedan obsoletos; quiere atajos con **Alt + letras vecinas de la mano izquierda**.

## 1. Atajos (quita TODOS los Ctrl+1..5)

Ventana de Ofisuiza (`VentanaPrincipal`), los Alt existentes se mantienen y se suma uno:
- Alt+S copiar asunto (último PDF) · Alt+D copiar cuerpo (último PDF) · Alt+F copiar mensaje de retiro · Alt+G copiar mensaje de guía · Alt+A ir al buscador de clientes · **Alt+X copiar PDF** (antes Ctrl+5).

Ventana de Facturas (`VentanaFacturas`, en la etapa de envíos):
- **Alt+A copiar correo · Alt+S copiar asunto · Alt+D copiar cuerpo · Alt+F preparar envío** (antes Ctrl+1/2/3; Preparar Envío no tenía atajo).

Reglas: con Alt, WPF entrega la tecla en `e.SystemKey` (`e.Key == Key.System`). Un atajo actúa aunque el foco esté en un cuadro de texto. Si la acción no corresponde (p. ej. Facturas no está en la etapa de envíos), no hace nada. Cambia los rótulos de botones y textos que digan "(Ctrl+N)" para que digan el Alt nuevo, p. ej. "📋 Asunto (Alt+S)", "Asunto: (Alt+S)", "📄 Copiar PDF (Alt+X)".

## 2. Línea en blanco antes de la firma

Todo texto de **cuerpo de correo** que se copia al portapapeles termina con una línea en blanco (`texto.TrimEnd() + "\r\n\r\n"`), para que la firma de Outlook no quede pegada: cuerpo de la OCC (Alt+D y botón "Cuerpo" del encontrado), mensaje de retiro, mensaje de guía y cuerpo de Facturas. Asunto, correo, RUT, clientes NVV y archivos NO cambian. Haz un único método auxiliar por ventana, sin tocar la lógica de Core.

## 3. Documentación

Actualiza las filas de atajos en `EQUIVALENCIA-OFISUIZA.md` y `EQUIVALENCIA-CLICKFACTURA.md` (marca que es cambio pedido por Javier el 2026-10-04).

## Criterio de término

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Reporte al final.

## Reporte del agente

- Implementé los atajos Alt solicitados en Ofisuiza y Facturas; eliminé el manejo Ctrl+1..5 y actualicé las etiquetas visibles. En Facturas los atajos solo actúan durante la etapa de envíos.
- Los cuerpos de correo copiados desde ambas ventanas terminan con `\r\n\r\n` para dejar una línea en blanco antes de la firma. Asunto, correo, RUT, clientes NVV y archivos no se modificaron.
- Actualicé las equivalencias de Ofisuiza y ClickFactura con el pedido de Javier del 2026-10-04.
- Verificación: `dotnet build` correcto, 0 advertencias y 0 errores; `dotnet test` correcto, 583 pruebas superadas y 0 fallidas; `dotnet csharpier check .` correcto (221 archivos). `git diff --check` sin errores.
