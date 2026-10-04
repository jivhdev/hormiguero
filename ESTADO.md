---
tipo: estado
---

# ESTADO

## Último traspaso

- **Fecha y hora**: 2026-10-04, 15:10
- **Agente y modelo**: Claude Code (Opus 5.5) diseña, revisa e integra; Codex y OpenCode Go implementan bloques (reparto: memoria `reparto-agentes`).
- **Etapa**: migración (D-65, `definicion/PLAN-MIGRACION.md`). Fase A aprobada (D-66). **Fase B terminada** salvo B-6, que se hace cuando Mensajero lo necesite (D-68).
- **Hecho en la fase B**:
  - B-1 Diseño común claro/oscuro en las dos apps (`Hormiguero.Diseno/Temas/Controles.xaml`).
  - B-2a Búsqueda rápida en Buscadero (22,6 s → ~170 ms con 20.000 PDF).
  - B-3 Carpeta común `%LocalAppData%\Hormiguero\` (archivero.db, buscadero.db, hormiguero.db, respaldos diarios).
  - B-4 Archivero avisa cada documento guardado; Buscadero lo encuentra al instante (registro `documentos_guardados`).
  - B-5 Archivero mueve con el método seguro del núcleo ("Reemplazar" ya no arriesga lo anterior); Archivero y Buscadero leen y dibujan los PDF con el núcleo (sale Docnet).
  - Limpieza: se retiró la lógica de la apertura nueva que ninguna app usaba.
- **Política de datos (D-68)**: Archivero y Buscadero pueden partir de cero; solo se conservan los datos de Ofisuiza y ClickFactura (ver memoria `datos-criticos`).
- **Pruebas**: núcleo 68, diseño 9, Archivero 280, Buscadero 121. CI en verde.
- **Para probar**: accesos directos del escritorio → `E:\Probar\Archivero` y `E:\Probar\Buscadero`.
- **Variables de prueba** (datos sintéticos): `HORMIGUERO_DATOS`, `HORMIGUERO_TEMA`, `ARCHIVERO_DATOS`.
- **Pendientes conocidos**: ventanas secundarias sin revisar a fondo en modo oscuro; Archivero no abre maximizado (igual que la 0.1 B, revisar contra el caso 7); `Caso-Sin-Cadenas.md` de la copia vieja de Buscadero por revisar; páginas rotadas: el texto se lee sin girar (revisar con documentos reales).
- **Siguiente paso**: Mensajero. Hecho: Ofisuiza (M-A1/A1b/A2, 36/36 en OCC reales) y ClickFactura (M-A4 lógica, M-A5 pantalla), atajos Alt (M-A6), almacén de clientes de ClickFactura con importación segura. En curso: análisis de Facturas JCV (M-A7) para rediseñarlo con Javier. Motores no se traduce (era un prototipo antiguo de Archivero).
- **Pendientes de Mensajero (pedidos por Javier, 2026-10-04)**:
  - Facturas: elegir en pantalla la carpeta donde se buscan las facturas y la carpeta temporal donde se preparan los envíos (y abrirla). Junto con las mejoras de `Arreglos click factura.txt` (aclarar "Preparar Envío", ver solo pendientes, separar varios correos).
  - Textos nuevos más cortos de retiro, guía y facturas: propuestos, esperan visto bueno.
  - Importar `clientes.txt` de Ofisuiza (revisar con Javier las 2 líneas solo de `dist`) y `clickfactura.db` (traerla del PC del trabajo).
  - Posible mejora a consultar: Facturas abre en julio (fijo en el original); abrir en el mes actual.
  - Barra de título blanca en modo oscuro en Windows 10 (las 3 apps).

## Traspasos anteriores

- 2026-10-04 — Fase A: Archivero 0.1 B y Buscadero caso 15 traídos al monorepo (D-66).
- 2026-10-04 — Apertura nueva de Archivero (B-029 a B-042) reemplazada por la migración del código antiguo (D-64, D-65).
- 2026-10-03 — Apertura nueva de Buscadero (B-007 a B-028); reemplazada por la migración (D-65).
- 2026-10-03 — B-002 a B-006: esqueleto del monorepo terminado (paso 4).
