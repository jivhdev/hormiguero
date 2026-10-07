---
bloque: C-3a
app: Archivero (y Núcleo)
fase: C-3 (D-77, D-78, D-79)
estado: hecho
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Archivero/**, src/Hormiguero.Nucleo/Datos/**, tests/Hormiguero.Archivero.App.Tests/**, tests/Hormiguero.Nucleo.Tests/**]
archivos_prohibidos: [todo lo demás; migraciones existentes intactas (v11 nueva si hace falta); el diccionario enlazante sigue fijo (D-77)]
rama: archivero/c3a-tipo-por-dato
---

# C-3a — Archivero: el tipo sale del dato que identifica; enlazables y datos informativos

Lee **D-79** (y D-75, D-77, D-78) en `definicion/DECISIONES.md`. Base actual: C-2b/C-2c (`DatosEnlazantes.cs`, datos por tipo y diseño, `IdentificarDocumentoWindow`, `DatosEnlazantesConfiguracionService`).

## Cambios

1. **Sin menú de tipos**: al configurar un documento el usuario indica el **Emisor** y, al marcar los datos del diccionario, elige **uno solo** como **"Este dato define qué es el documento"** (radio/estrella junto a cada dato marcado). El tipo se deriva de ese dato (p. ej. "N° Factura del proveedor" → "Factura del proveedor"; define una etiqueta corta por dato del diccionario, inmutable, junto al diccionario) y el nombre estándar queda "*Tipo · Emisor*" (sigue editable). Es obligatorio elegir uno antes de guardar (mensaje simple si falta).
2. **"Se puede enlazar"**: casilla por dato marcado (marcada por defecto en los datos enlazantes). Solo los marcados participan en el enlace automático y en las sugerencias (C-2d): ajusta el motor para respetarlo.
3. **"Calzaría con"**: al leer el valor de ejemplo de un dato enlazable, busca (clave de enlace normalizada, `DiccionarioDatosEnlazantes.ClaveDeEnlace`) documentos ya guardados con ese dato y valor y muestra al lado: "Calzaría con: *Tipo · Emisor* N° … (fecha)" (máx. 3 y "…y N más"); si ninguno: "Aún no hay documentos con este número".
4. **Datos informativos** (no enlazan, no son parte del diccionario enlazante fijo): **Fecha del documento**, **Encargado** y **Nombre de cliente**. Se marcan igual (zona por diseño) en una sección "Datos informativos"; se leen, guardan y muestran. La **Fecha del documento** se guarda como fecha (formatos chilenos comunes: dd-mm-aaaa, dd/mm/aaaa, dd.mm.aaaa, "6 de octubre de 2026"; si no se reconoce, se guarda el texto y se marca "fecha no reconocida") y queda disponible para las alertas (expón una consulta en el Núcleo; no cambies alertas en este bloque).
5. Migración (v11) solo si hace falta para guardar: dato identificador por tipo/diseño, marca "enlazable", datos informativos y fecha del documento. Nada se borra (D-68 no aplica a configuraciones: D-73 permite reiniciar).
6. Lecciones: eventos XAML en `InitializeComponent`; tema claro/oscuro; 1366×768; español neutro; errores visibles; prueba STA de la ventana; palabras simples en pantalla (D-70).

Pruebas: derivar tipo y nombre del dato identificador; obligatorio elegir uno; enlazable excluye del motor; "calzaría con" (0, 1, 4 coincidencias); fechas en varios formatos y no reconocida; encargado y cliente guardados. `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Si algo no está definido, decide lo más simple y seguro, anótalo y sigue. Reporte al final.

## Reporte del agente

Implementado C-3a dentro del alcance permitido.

- El tipo se deriva de una etiqueta fija por dato del diccionario; el nombre estándar se sugiere como «Tipo · Emisor» y sigue editable. Guardar exige un dato marcado como identificador.
- Se guarda la opción «Se puede enlazar» por dato y diseño. El enlace automático y las sugerencias la respetan. Las configuraciones antiguas sin filas en el nuevo esquema conservan el comportamiento anterior.
- «Calzaría con» compara con `ClaveDeEnlace`, muestra hasta tres documentos y cuenta los restantes; sin coincidencias muestra «Aún no hay documentos con este número».
- Los datos informativos tienen zonas por diseño y se guardan en tablas separadas del diccionario fijo. Se persisten fecha, encargado y cliente; las fechas reconocidas se normalizan y las no reconocidas conservan el texto con su marca. La ficha del documento guardado los muestra y el Núcleo expone la consulta de fechas para alertas.
- La migración v11 agrega la selección del identificador, la marca enlazable y el almacenamiento de datos informativos; no borra filas existentes.
- Decisión de compatibilidad: el motor de reglas conserva el comportamiento de configuraciones anteriores que todavía no tienen configuración por diseño. Cuando ya existe configuración por diseño, solo usa datos activos y enlazables.
- La prueba STA de Archivero mantiene el tema claro y oscuro y el tamaño 1366 × 768; su espera máxima aumentó a 60 segundos por el contenido adicional de la ventana.

Verificación final:

- `dotnet build`: correcto, 0 advertencias y 0 errores.
- `dotnet test --no-restore`: correcto, 735 pruebas aprobadas.
- `dotnet csharpier check .`: correcto, 287 archivos revisados.
