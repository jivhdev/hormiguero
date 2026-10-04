---
bloque: M-A2
app: Mensajero
fase: A (D-65, D-68)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Mensajero/**, src/Hormiguero.Mensajero.Core/VigilanteCarpeta.cs, tests/Hormiguero.Mensajero.Core.Tests/VigilanteCarpetaTests.cs, Hormiguero.slnx]
archivos_prohibidos: [todo lo demás; no cambies la lógica de M-A1 ni AlmacenMensajero]
rama: mensajero/a2-pantalla-ofisuiza
---

# M-A2 — Pantalla de Ofisuiza en Mensajero (WPF)

## Objetivo

Crear la app `Hormiguero.Mensajero` (WPF, .NET 10) con la pantalla "Pedidos y retiros", **equivalente a la ventana de la Ofisuiza en uso**: mismas secciones, botones, textos de aviso, atajos y comportamiento. Lee `semillas/Mensajero/EQUIVALENCIA-OFISUIZA.md` y el original (solo lectura): `C:\Users\jihja\Desktop\Entorno Antiguo\AP07-Ofisuiza Versiones Antiguas\OFISUIZA .EXE FUNCIONAL VERSION EN USO\ui\` (`app.py`, `extractor.py`, `clientes.py`, `retiro.py`, `guias.py`, `toast.py`) y `memoria.py`, `vigilancia.py`.

## Reglas

- Usa la lógica ya traducida en `Hormiguero.Mensajero.Core` (`ExtractorOcc`, `MensajesRetiro`, `MensajeGuia`, `CarpetaOcc`, `ClientesNvv`) y `AlmacenMensajero` (carpeta de OCC y clientes NVV: `AlmacenMensajero.AbrirComun()`). No la cambies; si falta algo, explícalo en el reporte.
- Diseño de Hormiguero: en `App.OnStartup`, `Hormiguero.Diseno.Tema.Aplicar(this, ModoTema.Sistema)`; colores solo con `DynamicResource Hormiguero.*` (mira `src/Hormiguero.Diseno/Temas/Claro.xaml` y `Controles.xaml`). Nada de colores fijos salvo la vista previa del texto si hace falta.
- Sin frameworks MVVM: manejadores de eventos en el code-behind, como `src/Hormiguero.Buscadero/MainWindow.xaml.cs`.
- Sin identificadores con tilde ni ñ.

## Contenido exacto

1. `src/Hormiguero.Mensajero/Hormiguero.Mensajero.csproj` (WinExe, `net10.0-windows`, `UseWPF`, `AssemblyName` = `Mensajero`), referencias a `Hormiguero.Mensajero.Core` y `Hormiguero.Diseno`. Agrégalo a `Hormiguero.slnx`.
2. `VentanaPrincipal`: título "Mensajero", 1050 × 850 centrada.
   - Barra superior: "Mensajero", nombre de la carpeta de OCC y botón "Cambiar" (`Microsoft.Win32.OpenFolderDialog`; se guarda con `GuardarCarpetaOcc`).
   - Dos columnas, como Ofisuiza: izquierda **Extractor OCC** y **Clientes NVV (COBELCAR)**; derecha **Generar mensaje de retiro** y **Envío de guías (Hoffens)**. Mismos botones y etiquetas que el original (los emojis pueden quedar).
   - Extractor OCC: "Asunto (Ctrl+1)" y "Cuerpo (Ctrl+2)" con el último PDF; buscar por número (Enter) con botones "Asunto" y "Cuerpo" para el encontrado; nombre del archivo encontrado; modo automático "Activar" / "Detener" con estado "Auto: Activo" / "Auto: Inactivo".
   - Clientes NVV: buscador (filtra al escribir), lista, Enter en el buscador copia el primer resultado, doble clic o botón "Copiar selección" copia el seleccionado, "Editar" abre el editor.
   - Editor de clientes (ventana modal): un cliente por línea ("Formato: RUT NOMBRE RETIRA CLIENTE"), aviso "Hay cambios sin guardar", "Guardar" y "Cancelar" con confirmación si hay cambios (también al cerrar con la X). Guarda con `GuardarClientesNvv`.
   - Retiro: "Último PDF", buscar por número, "Proveedor: X", para HOFFENS los campos Día (texto) y Bloque (lista `BloquesHoffens`; "Manual" = sin bloque), vista previa de solo lectura, "Actualizar vista" y "Copiar mensaje (Ctrl+3)".
   - Guías: buscar OCC (rellena Obra y Comuna desde el despacho con `ExtraerObraYComuna`; "No detectada" si no hay obra), día de emisión Hoy / Ayer / Otro (con su campo), vista previa, "Generar mensaje" y "Copiar mensaje (Ctrl+4)".
   - Avisos breves ("toast") abajo, con los **mismos textos** que el original (ej.: "✅ Asunto copiado: OCC 1 NVV 2 OCL 3", "❌ Sin carpeta configurada", "❌ No hay PDFs en la carpeta", "⚠️ Múltiples archivos con N°{n}", "🟢 Nuevo PDF detectado"), que desaparecen solos.
   - Atajos: **Ctrl+1 a Ctrl+4 como en Ofisuiza** y además los del boceto aprobado: Alt+S asunto, Alt+D cuerpo, Alt+F copiar retiro, Alt+G copiar guía, Alt+A ir al buscador de clientes.
   - Portapapeles: `Clipboard.SetText` con reintento breve si está ocupado (el original esperaba 50 ms).
   - Clientes: al abrir, si `LeerClientesNvv()` está vacío, guarda y usa `ClientesNvv.PorDefecto` (igual que Ofisuiza cuando no había archivo).
3. `src/Hormiguero.Mensajero.Core/VigilanteCarpeta.cs` (de `vigilancia.py`): `FileSystemWatcher` sin subcarpetas, solo `.pdf`, avisa `Created` y `Changed` tras 0,3 s si el archivo existe y pesa más de 0; ignora el mismo archivo repetido durante 2 s; `Iniciar(carpeta)` (devuelve false y avisa error si no existe), `Detener()`, `EstaActivo`, eventos `NuevoPdf(string ruta)` y `Error(string mensaje)` (llegan desde otro hilo: la pantalla los pasa al suyo). Pruebas en `VigilanteCarpetaTests.cs` con carpeta temporal.

## Criterio de término

- `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores.
- Escribe tu reporte al final de esta nota, con lo que no pudiste dejar igual al original.

## Reporte del agente

**Estado: completado.**

- Se creó `Hormiguero.Mensajero` como aplicación WPF .NET 10, se agregó a la solución y se implementó la ventana «Mensajero» con sus cuatro secciones, atajos, avisos temporales, edición modal de clientes, selección de carpeta, extracción, retiro, guías y portapapeles con reintento de 50 ms. Se usaron el tema de Hormiguero y `AlmacenMensajero.AbrirComun()`; la lógica de M-A1 y `AlmacenMensajero` no se modificaron.
- Se agregó `VigilanteCarpeta` con observación no recursiva de PDF y pruebas para detección, deduplicación, carpeta inexistente y detención.
- Diferencias pendientes respecto del original: los colores y controles siguen el tema de Hormiguero, por lo que la apariencia no es píxel a píxel igual a Tkinter. El núcleo no ofrece un equivalente de `MemoriaTemporal`; la aplicación hace una precarga no bloqueante con los extractores existentes y las acciones vuelven a extraer el PDF.
- Verificación: `dotnet build` correcto, 0 advertencias y 0 errores; `dotnet test` correcto (530 pruebas); `dotnet csharpier check .` correcto.