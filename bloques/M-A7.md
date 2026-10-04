---
bloque: M-A7
app: Mensajero
fase: análisis previo a rediseño (D-68)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [semillas/Mensajero/ANALISIS-FACTURAS-JCV.md]
archivos_prohibidos: [todo lo demás: este bloque NO escribe código]
rama: mensajero/a7-analisis-facturas-jcv
---

# M-A7 — Análisis de "Facturas JCV" (ExtractorCobelcar) para rediseñarlo

## Contexto

Javier tiene un programa llamado **Facturas JCV** que "no funciona como quiere": no se traduce igual (como Ofisuiza y ClickFactura), sino que se **reestructurará como una herramienta nueva integrada a Mensajero** (D-68). Antes de diseñar hace falta entender qué hace hoy.

Originales (SOLO LECTURA, no ejecutes nada que escriba en ellos):
- `C:\Users\jihja\Desktop\Entorno Antiguo\AP04-Facturas JCV\`
- `C:\Users\jihja\Desktop\Entorno Antiguo\AP05-JCV_ExtractorCobelcar\` (su `.spec` se llama "Facturas JCV")

## Reglas de datos (D-10, D-11)

- No abras ni copies el contenido de PDFs, planillas, bases ni archivos de estado reales (`data\`, `bin\`, carpetas con documentos). Solo puedes listar **nombres y tipos** de archivos y leer **código fuente** y archivos de configuración sin datos de clientes (si `config.json` u otro tiene rutas, anota solo la estructura de claves).
- No ejecutes los programas originales.

## Qué entregar: `semillas/Mensajero/ANALISIS-FACTURAS-JCV.md`

1. **Qué versión es cuál**: relación entre AP04 y AP05, cuál es la más reciente por fecha y cuál parece la que se usa.
2. **Qué hace hoy**, paso a paso, en lenguaje simple (para Javier, que no es programador experto): qué carpeta vigila o qué archivos lee, qué extrae de cada documento (campos, zonas/coordenadas), qué hace con eso (renombra, imprime, copia, mueve, genera mensajes...), qué guarda y dónde (esquema de estado/datos, sin contenidos).
3. **Pantalla actual**: secciones, botones, textos, atajos.
4. **Problemas probables**: errores de código, casos no cubiertos, cosas frágiles (coordenadas fijas, rutas fijas, procesos que se cortan, duplicados...), con archivo y línea.
5. **Qué se puede reutilizar de Hormiguero**: compara con lo que ya existe (`src/Hormiguero.Mensajero.Core/LogicaOfisuiza.cs` extracción por zonas idéntica a PyMuPDF, `VigilanteCarpeta`, `ClickFactura/`, `src/Hormiguero.Nucleo` con `MovedorSeguro`, `LectorPdf`, `Huella`, Archivero que identifica y archiva PDFs).
6. **Preguntas para Javier** (máximo 10, concretas y cortas) para decidir cómo debe funcionar la herramienta nueva.

Escribe en español neutro, claro y breve. Reporte al final de esta nota.

## Reporte del agente
