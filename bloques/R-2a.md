---
bloque: R-2a
app: Núcleo + Archivero (+ Buscadero solo lectura de dudosos)
estado: pendiente
agente: Codex
modelo: codex
archivos_permitidos: [src/Hormiguero.Nucleo/**, src/Hormiguero.Archivero/**, src/Hormiguero.Buscadero/**, src/Hormiguero.Buscadero.Core/**, tests/**]
archivos_prohibidos: [migraciones 1-20 intactas (nueva 21 aditiva si hace falta); Mensajero; configuracion/cadenas-jcv.json]
rama: nucleo/r1-cadenas-jcv
---

# R-2a — "Ningún documento sin cadena" (D-86 punto 3)

Lee **D-86** (`definicion/DECISIONES.md`), `bloques/R-1.md`, `bloques/R-1b.md` y `configuracion/cadenas-jcv.json`. Javier parte **cada día con los datos en blanco** (Reiniciar), pero sus PDF de días anteriores siguen en sus carpetas: por eso "buscar en la carpeta" es clave.

Términos: en un esquema, los lugares con `orden` **menor** que el de inicio (OCC) son **previos** (COVP, COV, OCL, NVV); el inicio y los de orden mayor son **posteriores** (OCC, GRC, GRCF, FCC, NCC, FCV, NCV).

## 1. Previos: esperan en silencio
- Un documento de tipo previo que no calza con ninguna cadena **no** genera decisión, dudoso ni aviso: queda archivado "esperando su OCC". Los documentos propios previos (NVV, OCL, COV) no tienen proveedor: para ellos se consideran **todos los esquemas activos** que tienen ese tipo como lugar.
- Un previo **puede estar en varias cadenas** (una NVV con OCC a Hoffens y a Cobelcar): si calza exacto con varias, se ubica en todas (no es dudoso). Los posteriores siguen pudiendo estar en una sola cadena (si calzan con varias → dudoso como hoy).

## 2. Completar al ubicar
Cada vez que un documento se ubica en una cadena (incluida su creación), para cada enlace declarado que toca su lugar se buscan los documentos del **otro lado** que calzan por el dato del enlace (con la equivalencia `enlaza_como`) y que aún no están en esa cadena, y se ubican respetando la cardinalidad (uno a uno ocupado → dudoso). Es recursivo: lo que entra también completa. Se buscan:
1. primero en lo ya registrado en el Núcleo (incluidos los previos "esperando" y los posteriores sin cadena), y
2. si no está, **en la carpeta** (punto 3).

Ejemplo: llega la OCC 20527 (trae NVV 20220 y OCL 1806) → nace la cadena → entran la NVV 20220 y la OCL 1806 archivadas antes; si la GRC 313091 (trae OCC 20527) se archivó antes y quedó sin cadena, también entra, y con ella su FCC y su FCV.

## 3. Buscar en la carpeta (sin leer todo el disco)
Para un tipo de documento + número: tomar las **configuraciones de Archivero** que guardan ese tipo (por el dato que define el tipo) y, si el tipo es del proveedor, cuyo emisor calce con los reconocimientos del esquema (R-1); revisar **solo** su carpeta destino (con sus subcarpetas de período) buscando PDF cuyo **nombre** contenga ese número (dígitos sin ceros a la izquierda, ej. `OCC0000020527.pdf`, `1190036.pdf`). Si aparece: leer sus datos con **esa** configuración (sus zonas), registrarlo en el Núcleo como archivado **sin moverlo, sin imprimir y sin abrirlo**, y pasarlo al motor. Si hay varios candidatos con el mismo número, tomar el que calce la configuración; si siguen varios → dudoso. Las configuraciones viven en Archivero: implementa la búsqueda en Archivero (servicio) y entrégasela al motor del Núcleo como dependencia (interfaz), de modo que el Núcleo siga sin conocer Archivero. Las guías firmadas (sin texto) se reconocen por su carpeta + nombre (configuración/guardado rápido de R-1).

## 4. Posteriores sin cadena: subir hasta la OCC
Si un posterior no calza con ninguna cadena: siguiendo los enlaces hacia el inicio (ej. FCV → GRC por N° guía → OCC por N° OCC), buscar el documento "padre" con su número, primero en el Núcleo y luego en la carpeta (punto 3); si se llega a una OCC, se procesa (nace la cadena) y el punto 2 ubica todo. Si no se encuentra:
- Queda como **Dudoso** (decisión `sin_inicio`, reemplaza a `sin_piso` para posteriores) con motivo concreto: "Falta la OCC 20527: no está en la carpeta." y tres opciones: **"Buscar otra vez"**, **"Crear cadena igual"**, **"Solo archivar"**.
- Se resuelve **solo** si después llega el documento que faltaba (al crear/completar una cadena, reintentar los `sin_inicio` pendientes cuyo número calce).
- Se ven en Archivero → "Decisiones pendientes" (con los 3 botones; título de la fila "Dudoso: falta …") y en Buscadero → "Dudosos" (lista de solo lectura con el motivo y "Se resuelve en Archivero").
- "Solo archivar": el documento queda archivado fuera de cadenas y no vuelve a preguntar. "Crear cadena igual": usa `CrearCadenaManual`.

## 5. Aplica a todo
El mismo motor corre para lo que llega por el asistente, por guardado automático, por guardado rápido, por "Pendientes de distribuir" (sin texto) y por las **carpetas observadas** (ej. FCV de Cobelcar que nunca pasa por el asistente).

## Pruebas (sintéticas, con la precarga JCV)
1. NVV 20220 y OCL 1806 archivadas solas → sin decisiones ni avisos; luego OCC 20527 (trae ambas) → nace la cadena con las tres.
2. NVV 20220 referida por OCC a Hoffens y OCC a Cobelcar → está en ambas cadenas, sin dudoso.
3. GRC 313091 (trae OCC 20527) llega primero, la OCC está solo en una carpeta configurada (PDF sintético con nombre `OCC0000020527.pdf` que calza la configuración de OCC) → se registra la OCC desde la carpeta, nace la cadena y la GRC queda en ella; el PDF no se movió.
4. FCV con guía 313091 sin GRC ni OCC en ningún lado → Dudoso `sin_inicio` "Falta la GRC 313091…"; al llegar después la GRC + OCC → se resuelve solo y la FCV entra en su línea.
5. "Solo archivar" y "Crear cadena igual" funcionan y no vuelven a preguntar.
6. Carpeta observada (FCV de Cobelcar) pasa por el mismo motor.
7. GRCF que llega **antes** que su GRC (hoy queda `sin_piso` y no se reubica, ver reporte de R-1b): espera y, al llegar la GRC, entra sola en su línea.
8. Ninguna búsqueda en carpeta abre PDF que no tengan el número en el nombre (cuenta las lecturas).

`dotnet build` sin advertencias; `dotnet test` todo pasa; `dotnet csharpier check .` sin errores en tus archivos. Español neutro. Reporte al final.

## Reporte del agente
