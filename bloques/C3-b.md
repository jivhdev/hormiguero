---
bloque: C3-b
app: Buscadero (asistente de esquema y vista de cadena)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Buscadero/**, src/Hormiguero.Buscadero.Core/**, tests/Hormiguero.Buscadero.Core.Tests/**]
archivos_prohibidos: [todo lo demás; el Núcleo (RepositorioEsquemas, RepositorioCadenas, MotorCadenas) ya está hecho en C3-a y no se toca: si falta una consulta, pídela en el reporte y resuélvelo en Buscadero.Core]
rama: buscadero/c3b-esquemas
---

# C3-b — Buscadero: asistente "Esquema del proveedor" y vista de la cadena en árbol

Lee **completo** `definicion/DISENO-C3-ESQUEMAS-Y-ALERTAS.md` (§3 pasos 1-4 y 7, §4) y el reporte de `bloques/C3-a.md` (modelo de datos y API: `RepositorioEsquemas.Guardar/ObtenerPorProveedor/Listar`, `DefinicionLugarEsquema`, `DefinicionParejaEsquema`, `RepositorioCadenas.ListarPorProveedor/ListarPorCliente/ArbolPorLugares`). Todo configurable por el usuario; nada de un proveedor concreto en el código.

1. **Asistente "Esquema del proveedor"** (desde la ventana de cadenas: botón "Esquemas…" → lista de esquemas por proveedor con "Nuevo", "Editar", "Desactivar"). Un paso por pantalla, "Paso N de 5", Atrás/Siguiente, ayuda arriba:
   1. **Proveedor**: autocompletar con los emisores conocidos (identificaciones del Núcleo) o escribir. Si ya tiene esquema, se edita ("Uno por proveedor").
   2. **Documentos del proceso**: lista de tipos de documento configurados (identificaciones tipo · emisor, de todos los emisores: incluye los propios como Nota de venta propia y los del proveedor). Arriba, un recuadro **"Estos documentos ya comparten datos"**: pares de tipos que tienen algún dato del diccionario enlazable en común (p. ej. "Factura del proveedor ↔ OC propia: N° OC propia"), calculado de `tipos_documento_datos`. El usuario agrega los tipos al esquema y los ordena (Subir/Bajar).
   3. **¿Cuáles inician la cadena?**: casillas en la lista ordenada; al menos uno.
   4. **Parejas 1 a 1** (opcional): elegir dos lugares y el dato que comparten (solo datos que ambos tienen) → "Guía del proveedor ↔ Factura del proveedor por N° Guía del proveedor". Explicación: "Cada documento de un lugar tiene uno solo del otro (p. ej. cada guía su factura)."
   5. **Resumen**: frases simples + un **dibujo de ejemplo del árbol** (lugares en orden, parejas lado a lado), "Cambiar" por paso, "Guardar".
   (Los pasos de modos y alertas se agregan en C3-d: deja el asistente preparado para insertar pasos.)
2. **Vista de cadenas** (reemplaza la de "Administrar cadenas"): lista a la izquierda con filtros **Proveedor, Cliente, Estado** y agrupación por proveedor o por cliente; a la derecha el **árbol visual** de la cadena elegida (`ArbolPorLugares`): cada lugar como una fila con su nombre; sus documentos como tarjetas (tipo, número como se lee, fecha); los lugares de **pareja** muestran filas por **línea**: "Guía 313091 ↔ Factura 25378", colgando del documento de origen; lugares vacíos en gris "Falta". Clic en una tarjeta abre el documento en el visor de Buscadero; doble clic lo abre en el visor predeterminado. Acciones: "Quitar de la cadena" (con confirmación) y la lista de **dudosos** de esa cadena (aceptar / rechazar), reutilizando lo existente si sirve.
3. **Buscador maestro**: agregar filtros Proveedor y Cliente; al elegir un documento que está en una cadena, "Ver cadena" abre la vista del punto 2 en esa cadena.
4. Eliminar de la interfaz lo de "cadenas simples" que quede (creación manual de cadenas ya no existe: las cadenas nacen solas con un documento de inicio, D-84). Si un proveedor no tiene esquema, la vista de cadenas lo dice con un botón "Crear esquema".

Reglas: eventos XAML en `InitializeComponent`; tema claro/oscuro; 1366×768 (scroll dentro, botones visibles); español neutro; errores visibles sin cerrar la app. Pruebas: lógica en Buscadero.Core (pares de tipos que comparten datos; validaciones del asistente: proveedor, al menos un lugar, al menos un inicio, pareja con dato común; armado del modelo de árbol con líneas para la vista); prueba STA que abre el asistente y la vista con datos sintéticos. `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores en tus archivos. Si algo no está definido, elige lo más simple para un oficinista, anótalo y sigue. Reporte al final con la lista de pantallas.

## Reporte del agente

Implementé el asistente de esquema y la vista de cadenas dentro de Buscadero. El asistente tiene cinco pantallas: proveedor; documentos del proceso y datos enlazables compartidos; tipos que inician; parejas 1 a 1; y resumen con ejemplo de árbol y acciones para cambiar cada paso. Permite crear y editar el esquema por proveedor. La lista de esquemas ofrece Nuevo, Editar y Desactivar.

La vista de cadenas filtra por proveedor, cliente y estado, y agrupa por proveedor o cliente. Muestra el árbol por lugar, documentos con tipo, número y fecha disponible, lugares vacíos como “Falta” y la línea de las parejas. Al seleccionar una tarjeta se abre en el visor de Buscadero; con doble clic se abre con la aplicación predeterminada. Quitar de la cadena pide confirmación. Dudosos muestra las propuestas de la cadena seleccionada y permite aceptar o rechazar cada una. El buscador maestro conserva los filtros de proveedor y cliente y “Ver cadena” lleva a la cadena seleccionada.

Añadí a Buscadero.Core las consultas necesarias para mostrar tipos que comparten datos enlazables y para filtrar cadenas por cliente, más validaciones del asistente y armado del modelo de árbol. Añadí pruebas de validación, lugares vacíos y parejas, y extendí la prueba STA para abrir la vista, el asistente y la lista de esquemas con datos temporales.

Pantallas incluidas: cadenas y árbol; lista de esquemas; asistente de cinco pasos; buscador maestro con acceso a la cadena.

Verificación: `dotnet test` pasó todas las suites, incluida Buscadero Core (120/120). `dotnet csharpier check .` pasó (322 archivos). `dotnet build` de la solución pasó con 0 advertencias y 0 errores; el proyecto Buscadero también pasó su compilación dirigida. No se modificaron archivos fuera del alcance.
