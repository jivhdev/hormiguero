---
bloque: M-A9
app: Mensajero
fase: C (D-72: ajustes aprobados)
estado: hecho
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Mensajero/**, src/Hormiguero.Mensajero.Core/**, tests/Hormiguero.Mensajero.Core.Tests/** (salvo Equivalencia/**), semillas/Mensajero/EQUIVALENCIA-CLICKFACTURA.md]
archivos_prohibidos: [todo lo demás; NO toques tests/**/Equivalencia/**; no cambies el esquema de AlmacenMensajero salvo agregar tablas/valores nuevos sin tocar las existentes]
rama: mensajero/a9-arreglos-clickfactura
---

# M-A9 — Arreglos de ClickFactura pedidos por Javier y registro de errores

Lista de Javier (`C:\Users\jihja\Desktop\Entorno Antiguo\AP03-ClickFactura\Arreglos click factura.txt`, aprobada en D-72) y `definicion/AUDITORIA-FUNCIONES.md` (revisión de Claude, puntos 3 y 5). La carpeta temporal elegible ya está hecha (P-1).

1. **"Preparar Envío" claro**: que quien lo use entienda qué hace y qué sigue. Debajo del botón, una línea simple, por ejemplo: "Copia los PDF de este cliente a una carpeta y los deja listos para pegar en el correo (Ctrl+V)". Al terminar, un aviso con pasos numerados: "1. Pega el correo (Alt+A) · 2. Asunto (Alt+S) · 3. Cuerpo (Alt+D) · 4. Pega los PDF en el correo (Ctrl+V) · 5. Marca como enviado", y botón **"Abrir carpeta"**. Si faltan PDF, decirlo claro antes de preparar ("Faltan 2 PDF: FCV 123, NCV 42").
2. **Revisar solo pendientes**: hoy, al dejar un cliente pendiente no se puede volver a ver solo los pendientes. Agrega en la etapa de envíos un selector **"Todos / Solo pendientes / Solo enviados"** (o equivalente simple) y un contador ("3 pendientes"). Los estados (pendiente/enviado) de la semana analizada deben **recordarse** aunque se cierre Mensajero (guárdalos en `mensajero.db` en una tabla nueva por período + RUT; nunca borrar).
3. **Varios correos por cliente**: deja claro cómo se separan. En la gestión de clientes: texto de ayuda "Si son varios correos, sepáralos con punto y coma ( ; )", y al guardar normaliza separadores comunes (`,` `;` espacios, saltos de línea) a `; ` y valida que cada parte parezca un correo (aviso claro si no). Al copiar el correo (Alt+A) se copian todos separados por `; ` (formato que acepta Outlook). Muestra en la etapa de envíos cada correo en su propia línea.
4. **Registro de errores** (equivalente al log de ClickFactura): Mensajero escribe en `%LOCALAPPDATA%\Hormiguero\mensajero.log` (carpeta común; respeta `HORMIGUERO_DATOS`) los errores y avisos de error que hoy solo se muestran en pantalla, con fecha y detalle; rota el archivo al superar ~1 MB conservando 3 anteriores (mira `src/Hormiguero.Archivero/Servicios/AuditoriaService.cs`). Nunca escribir datos de clientes completos, solo lo necesario para diagnosticar.
5. Lecciones: XAML durante `InitializeComponent`; tema claro y oscuro; 1366×768; español neutro; Alt con `e.SystemKey`; nada fijo de JCV (D-70).

Pruebas: normalización y validación de correos, estados pendientes recordados y filtrados, rotación del registro. `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Abre Mensajero y Facturas una vez con `HORMIGUERO_DATOS` en carpeta temporal. Reporte al final.

## Reporte del agente

- Implementé el aviso de preparación con pasos y acción «Abrir carpeta», el detalle previo de PDF faltantes y la explicación bajo los controles.
- Agregué el filtro «Todos / Solo pendientes / Solo enviados», el contador y la persistencia de estados por período + RUT en la tabla nueva `estados_envio_factura`; no se eliminan filas.
- Normalicé y validé los correos al guardar; en envíos se muestran en líneas separadas y Alt+A copia todos con `; `.
- Agregué `%LOCALAPPDATA%\Hormiguero\mensajero.log`, configurable con `HORMIGUERO_DATOS`, con fecha, detalle diagnóstico sin datos completos de clientes y rotación al superar 1 MiB, conservando tres archivos. La rotación verifica la copia antes de borrar el original.
- Verificación: `dotnet build` correcto, 0 advertencias; `dotnet test` correcto, 620 pruebas aprobadas; `dotnet csharpier check .` correcto (242 archivos).
- Abrí Mensajero con `HORMIGUERO_DATOS` en `C:\Users\jihja\AppData\Local\Temp\Hormiguero-M-A9` y confirmé que creó `mensajero.db`. No pude abrir la ventana Facturas: la automatización de ventanas nativas está deshabilitada en esta sesión (`cua.getState()` no expone aplicaciones y no hay API de control nativo). Finalicé el proceso Mensajero después para liberar el ejecutable y completar la compilación final.

- Claude: corregido riesgo real: al mostrar y copiar correos se validaban y un correo importado escrito distinto (con nombre, sin punto, etc.) habría hecho caer la etapa de envíos. Ahora mostrar/copiar usa `CorreoFactura.Separar` (no valida, nunca falla); validar queda solo al guardar. Prueba nueva.
