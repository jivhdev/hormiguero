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
- **Siguiente paso**: fase A de Mensajero. Los programas de salida (ClickFactura, ExtractorCobelcar, Motores, Ofisuiza) están en Python: traducción fiel a .NET; traslado de los datos de ClickFactura (base en el PC del trabajo) y Ofisuiza (`clientes.txt`, mensajes de retiro).

## Traspasos anteriores

- 2026-10-04 — Fase A: Archivero 0.1 B y Buscadero caso 15 traídos al monorepo (D-66).
- 2026-10-04 — Apertura nueva de Archivero (B-029 a B-042) reemplazada por la migración del código antiguo (D-64, D-65).
- 2026-10-03 — Apertura nueva de Buscadero (B-007 a B-028); reemplazada por la migración (D-65).
- 2026-10-03 — B-002 a B-006: esqueleto del monorepo terminado (paso 4).
