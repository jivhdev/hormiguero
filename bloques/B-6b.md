---
bloque: B-6b
app: Núcleo
fase: B-6
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Nucleo/Datos/**, tests/Hormiguero.Nucleo.Tests/**]
archivos_prohibidos: [todo lo demás; no adaptes Archivero ni Buscadero ni pantallas]
rama: nucleo/b6b-migracion-v5
---

# B-6b — Migración v5 y repositorios de cadenas y datos en el Núcleo

Implementa **exactamente** las secciones 2 ("Correspondencia en la base común"), 3 (`campos_documento`, `valores_documento`, `versiones_documento`) y 6 de `definicion/DISENO-B6-MARCAS-Y-CADENAS.md` (D-71), y la tabla de marcas por versión de la sección 6. Sin pantallas y sin tocar Archivero ni Buscadero (eso es B-6c/d/e).

## Reglas

- Migración **v5** en `Migraciones.cs`, siguiendo el estilo de las v1–v4: idempotente, en transacción, sin renombrar ni borrar nada existente. Claves foráneas sin cascada. Índices de la sección 2/3.
- `Documentos.Quitar` pasa a **baja lógica** (estado/fecha de baja) y registra en `auditoria`; las consultas normales excluyen las bajas. Revisa todos los usos de `Documentos` en el núcleo y ajusta lo necesario para que nada existente cambie de comportamiento visible (las pruebas actuales de Archivero, Buscadero y Núcleo deben seguir pasando).
- Repositorios nuevos bajo `src/Hormiguero.Nucleo/Datos/` (nombres en español, como los existentes): modelos de cadena y sus vagones (árbol equivalente a `ObtenerArbolPlantilla`), cadenas y sus vagones (árbol equivalente a `ObtenerArbolInstancia`, con instantánea de estructura), versiones de documento, campos y valores de documento, reglas de vagón (solo guardar/leer; **no** el motor de enlace, que es B-6f), enlaces de cadena (crear, cambiar estado, deshacer = anular), marcas por versión.
- Nada se borra físicamente: anular con estado y fecha. Cada cambio de enlace, regla, valor, versión o baja registra en `auditoria` en la **misma transacción**.
- Nada específico de JCV/Javier en código (D-70).
- Pruebas en `tests/Hormiguero.Nucleo.Tests/`: migración nueva y repetida (y sobre una base v4 con datos), claves foráneas, árbol de modelo con ramas/anexos/múltiples/cadena hija, orden, instantánea, cadena madre/hija, valores e índices (consulta por campo y valor), versiones por huella y cambio de ruta, baja lógica auditada, enlace/deshacer con historial, escritura con un lector abierto (WAL).

## Criterio de término

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Reporte al final con las tablas creadas y cualquier punto del diseño que no se pudo seguir exacto (y por qué).

## Reporte del agente
