---
bloque: M-A5
app: Mensajero
fase: A (D-65, D-68)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Mensajero/VentanaFacturas.xaml, src/Hormiguero.Mensajero/VentanaFacturas.xaml.cs, src/Hormiguero.Mensajero/VentanaClientesFactura.xaml, src/Hormiguero.Mensajero/VentanaClientesFactura.xaml.cs, src/Hormiguero.Mensajero/VentanaPrincipal.xaml, src/Hormiguero.Mensajero/VentanaPrincipal.xaml.cs, src/Hormiguero.Mensajero.Core/ClickFactura/**, tests/Hormiguero.Mensajero.Core.Tests/ClickFactura/**, semillas/Mensajero/EQUIVALENCIA-CLICKFACTURA.md]
archivos_prohibidos: [todo lo demás; en especial AlmacenMensajero.cs, LogicaOfisuiza.cs, TintaBase14.cs, tests/**/Equivalencia/** (respuestas correctas)]
rama: mensajero/a5-pantalla-clickfactura
---

# M-A5 — Pantalla de ClickFactura en Mensajero (WPF)

## Objetivo

Agregar a Mensajero la ventana de **ClickFactura, equivalente a la que Javier usa**: mismas secciones, botones, textos, avisos, atajos, orden de pasos y comportamiento. Original (SOLO LECTURA): `C:\Users\jihja\Desktop\Entorno Antiguo\AP03-ClickFactura\` (`main.py`, `src\ui\`, `src\services\`, `config.py`). La lista de lo que hace está en `semillas/Mensajero/EQUIVALENCIA-CLICKFACTURA.md` (M-A4); al terminar, marca ✅ lo que quede igual.

## Reglas

- **No cambies nada que Javier ya use**: la ventana de Ofisuiza (`VentanaPrincipal`) queda idéntica, salvo un botón **"🧾 Facturas"** en su barra superior, junto a "Cambiar", que abre `VentanaFacturas` (no modal; si ya está abierta, la trae al frente).
- Las mejoras de `Arreglos click factura.txt` **NO** van en este bloque (fase A = igual al original). Lístalas en el reporte.
- Lógica: usa la de M-A4 (`Hormiguero.Mensajero.Core.ClickFactura`). Si la pantalla necesita una variante (p. ej. analizar con los clientes ya leídos en vez de una ruta de base), agrégala ahí **sin cambiar el comportamiento** y con pruebas; las pruebas de equivalencia existentes deben seguir pasando sin tocar `Equivalencia/`.
- Datos: clientes con `AlmacenMensajero` (`LeerClientesFactura`, `BuscarClienteFactura`, `GuardarClienteFactura(rut, razon, correo, DateTime.Now)`); carpetas y otras preferencias con `LeerValor`/`GuardarValor` usando claves que empiecen con `factura.` (p. ej. `factura.carpeta_documentos`, `factura.carpeta_temporal`). **No llames a `ImportarClientesFactura`** (la importación real la hace Claude con Javier). Abre el almacén con `AlmacenMensajero.AbrirComun()` (o reutiliza el de la ventana principal) y libéralo al cerrar.
- Portapapeles de archivos (el original usa CF_HDROP con pywin32): `Clipboard.SetFileDropList`, con el mismo reintento breve que usa `VentanaPrincipal.Copiar`. Texto: `Clipboard.SetText`.
- Diseño de Hormiguero: solo `DynamicResource Hormiguero.*` (mira `VentanaPrincipal.xaml`), sin colores fijos. Sin MVVM: manejadores en el code-behind. Sin identificadores con tilde ni ñ.
- Lecciones de M-A2 (obligatorias):
  - Un `IsChecked`, `SelectedIndex`, `Text` o similar puesto en XAML dispara su evento DURANTE `InitializeComponent`, antes de que existan los demás controles: o se asigna en el constructor después de `InitializeComponent`, o el manejador comprueba que los controles no sean null. (Mensajero se caía al abrir por esto.)
  - Con Alt, WPF entrega la tecla en `e.SystemKey` (`e.Key == Key.System`).
  - Los avisos deben leerse en tema claro y oscuro (texto `Hormiguero.Texto`/`Exito`/`Aviso`/`Error` sobre `Hormiguero.Superficie`).
  - La ventana debe caber en pantallas de 1366 × 768 (limita al área útil como hace `VentanaPrincipal`).
- Los textos de avisos y diálogos se copian **exactos** del original.

## Criterio de término

- `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores.
- Abre la app compilada una vez (`dotnet run --project src/Hormiguero.Mensajero` con `HORMIGUERO_DATOS` apuntando a una carpeta temporal tuya) y confirma que la ventana principal y la de Facturas abren sin caerse; ciérralas.
- Reporte al final de esta nota: qué quedó igual, qué no se pudo dejar igual y por qué, mejoras pendientes de `Arreglos click factura.txt`.

## Reporte del agente
