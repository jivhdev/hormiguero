---
bloque: B-6c
app: Archivero (y núcleo)
fase: B-6
estado: hecho
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Archivero/**, src/Hormiguero.Nucleo/Datos/DatosDocumento.cs, tests/Hormiguero.Archivero.App.Tests/**, tests/Hormiguero.Nucleo.Tests/**]
archivos_prohibidos: [todo lo demás; no toques Buscadero ni Mensajero; no cambies la migración v5 salvo que sea imprescindible (explícalo)]
rama: archivero/b6c-publicar-datos
---

# B-6c — Archivero publica los datos de cada documento en la base común

Sigue la sección 3 de `definicion/DISENO-B6-MARCAS-Y-CADENAS.md` (D-71) y usa los repositorios de B-6b (`src/Hormiguero.Nucleo/Datos/DatosDocumento.cs` y afines).

1. **Campos por configuración**: cada configuración de Archivero publica sus campos en `campos_documento` (los base `Emisor`, `Tipo`, `Fecha`, `NombreArchivo` y los que el usuario agregue con nombre libre). Si hoy Archivero no permite agregar campos propios, agrega lo mínimo para que una configuración pueda tener datos con nombre libre leídos de una zona (como las marcas actuales), sin cambiar cómo se usa lo existente; si eso implica pantallas nuevas, deja solo la lógica y anota en el reporte qué pantalla falta (será B-6g).
2. **Valores al guardar**: cuando Archivero identifica y guarda un documento, en **una transacción**: versión del documento (huella, ruta), valores leídos (original + clave) y auditoría. Si el usuario corrige un dato, se anula el valor anterior y queda el nuevo (origen manual).
3. **Relación con lo existente**: Archivero ya avisa a Hormiguero al guardar (`AvisarAHormiguero`, `documentos_guardados`): engancha ahí, sin duplicar avisos ni romper la importación de Buscadero.
4. **Contrato para el modo observador (D-69)**: deja un método que reciba ruta + configuración + valores leídos y los publique **sin mover ni renombrar** el archivo (procedencia "observador"). Sin pantallas.
5. Nada fijo de JCV (D-70). Si algo falla al publicar, el documento igual se guarda y el error queda registrado y visible (no silencioso).

Pruebas: campos base y propios, valores con tildes/ñ y ceros a la izquierda conservados en el original, corrección manual (anula y reemplaza), mismo archivo movido (misma huella → misma identidad), contenido nuevo (huella distinta → versión nueva), publicación del observador sin tocar el archivo (verifica huella y fecha del archivo antes/después), y que las pruebas existentes de Archivero sigan pasando.

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Reporte al final.

## Reporte del agente

- Archivero publica campos base y valores de configuración en Núcleo. Se agregó almacenamiento lógico de campos propios con nombre y zona de lectura; falta la pantalla para administrarlos, prevista para B-6g.
- La publicación registra la identificación, la fila de documento, la versión por huella, los campos, los valores originales/claves y la auditoría en una transacción. Una huella igual conserva identidad y actualiza la ruta; contenido distinto genera otra versión. El contrato del observador publica con procedencia `observador` sin mover el archivo. La corrección manual anula el valor activo y crea uno con origen `manual`.
- Se conserva un solo aviso `documentos_guardados` por el evento existente. Si falla la publicación automática, el documento queda guardado, se registra en la auditoría local y el detalle se entrega para mostrar atención.
- Verificación: `dotnet build Hormiguero.slnx --no-restore` sin advertencias; `dotnet test Hormiguero.slnx` pasó (591 pruebas); `dotnet csharpier check .` pasó.
