---
bloque: AR-1
app: Archivero
fase: C (D-73, D-76)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Archivero/**, tests/Hormiguero.Archivero.App.Tests/**]
archivos_prohibidos: [todo lo demás]
rama: archivero/a1-accesos-rapidos
---

# AR-1 — Accesos rápidos editables y con año en curso automático

Javier (uso real): "Al guardar guías firmadas, necesito que los accesos rápidos se puedan eliminar y configurar, y que una opción sea dejar elegido el año, así, para guardar guías del presente año, no tengo que indicar cada vez que se trata del 2026 (pongo 1/1/26). Que el acceso rápido se configure también por año, si aplica, o si es por mes: documento × proveedor × mes, o documento × proveedor × año". Lista vieja (`C:\Users\jihja\Desktop\Entorno Antiguo\AP01-Archivero Versiones Antiguas\Arreglos archivero.txt`, punto 1): "cuando guardo un guardado rápido, igual tengo que ingresar la fecha; que me dé la opción de guardar la fecha en los guardados rápidos". Decisiones D-73 y D-76.

1. Estudia cómo funcionan hoy los accesos rápidos / guardados rápidos (`GuardarAtajoWindow`, `OrganizacionCarpetaControl`, `IdentificarDocumentoWindow`, servicios y repositorio que los guardan).
2. **Administrar accesos rápidos**: una ventana (o sección) donde se vean todos, con **editar** (nombre, documento/tipo, proveedor, carpeta, período), **eliminar** (con confirmación) y **reordenar**. Accesible desde donde hoy se usan.
3. **Período en el acceso rápido**: al crear o editar un acceso rápido, elegir "**Año en curso**" (automático: en 2027 usa 2027), "**Mes en curso**", "**Año fijo**" o "**Preguntar fecha cada vez**" (comportamiento actual). Si el período está definido, al guardar con ese acceso rápido **no se pide la fecha**: se usa el año/mes en curso para la carpeta y el nombre según el patrón configurado (si el patrón necesita día, usar la fecha de hoy y mostrarla para confirmar). Nombre sugerido del acceso: "*Documento · Proveedor · Año en curso*".
4. Los accesos rápidos existentes siguen funcionando como "Preguntar fecha cada vez" (o se pueden perder si es imprescindible, D-73; dilo en el reporte).
5. Lecciones: eventos de XAML durante `InitializeComponent`; tema claro y oscuro; 1366×768; español neutro; errores visibles; nada fijo de JCV (D-70).

Pruebas: crear/editar/eliminar/reordenar; año en curso cambia con la fecha (inyecta "hoy"); mes en curso; año fijo; preguntar cada vez; guardar con acceso rápido sin pedir fecha; prueba STA de la ventana nueva. `dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores. Si algo no está definido, decide lo más simple, anótalo y sigue. Reporte al final.

## Reporte del agente
