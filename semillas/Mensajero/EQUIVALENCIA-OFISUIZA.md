---
tipo: equivalencia
app: Mensajero
origen: AP07 Ofisuiza, "OFISUIZA .EXE FUNCIONAL VERSION EN USO" (Python, 2026-10-02)
fecha: 2026-10-04
---

# Equivalencia — Ofisuiza → Mensajero (fase A, D-65)

Todo lo que hace la Ofisuiza en uso y su estado en Mensajero. Ninguna fila puede quedar peor que hoy (D-64). La "respuesta correcta" de cada función sale de correr el Ofisuiza original sobre OCC sintéticas: `tests/Hormiguero.Mensajero.Core.Tests/Equivalencia/Ofisuiza/generar_esperado.py` → `esperado.json`.

| # | Qué hace hoy | Detalle | Estado |
|---|---|---|---|
| 1 | Extraer OCC, NVV y OCL de la OCC | Zonas fijas en la página 1 (puntos, origen arriba a la izquierda); OCC sin ceros a la izquierda; NVV sin prefijo "NVV" ni guion | ✅ |
| 2 | Asunto del correo | "OCC {occ} NVV {nvv} OCL {ocl}", omitiendo lo vacío; Alt+S con el último PDF | ✅ |
| 3 | Cuerpo del correo (despacho) | Bloque "INFORMACIÓN PARA EL DESPACHO" hasta el final de la página; título + líneas no vacías; Alt+D; copia con una línea en blanco final para la firma | ✅ |
| 4 | Proveedor | Zona "Señores/as:"; sin S.A./LTDA./LIMITADA/LTD; mapeo HOFFENS, COBELCAR (incluye VELIZ BELTRAN), SENSUS, CHILE HDPE (incluye HDPE), FLUCHILE | ✅ |
| 5 | Mensaje de retiro por proveedor | Textos exactos de Cobelcar, Hoffens (día y bloque 09:00-11:00 / 11:00-13:00 / manual), Sensus, Chile HDPE; "Proveedor no reconocido" para el resto; Alt+F; copia con una línea en blanco final para la firma | ✅ |
| 6 | Envío de guías (Hoffens) | Obra y comuna desde el despacho (lista de 20 comunas); día hoy / ayer / otro; texto exacto; Alt+G; copia con una línea en blanco final para la firma | ✅ |
| 7 | Buscar OCC por número | Primer número del nombre del archivo, sin ceros; avisa si no hay o si hay varias | ✅ |
| 8 | Último PDF de la carpeta | El de fecha de modificación más reciente | ✅ |
| 9 | Carpeta de OCC | Se elige una vez y se recuerda ("Cambiar") | ✅ |
| 10 | Modo automático | Vigila la carpeta (sin subcarpetas); al llegar un PDF lo precarga y avisa | ✅ |
| 11 | Clientes NVV (Cobelcar) | Lista "RUT NOMBRE RETIRA CLIENTE"; buscador sin distinguir mayúsculas; Enter copia el primero; doble clic / botón copia la selección; 7 clientes por defecto si no hay lista | ✅ |
| 12 | Editar clientes | Una línea por cliente; aviso de cambios sin guardar; confirmar al salir | ✅ |
| 13 | Copiar al portapapeles con aviso | Cada copia muestra un aviso breve ("toast") | ✅ |
| 14 | Datos que se conservan (D-68) | `clientes.txt` en uso (2026-10-02; mostrar a Javier las 2 líneas que solo están en la copia de `dist`) | ⏳ |

| 15 | Nuevo (pedido de Javier, 2026-10-04) | "Copiar PDF (Alt+X)": copia el ARCHIVO de la última OCC agregada a la carpeta (fecha de creación o modificación, la más reciente) para pegarla como adjunto | ✅ |
| 16 | Cambio pedido por Javier (2026-10-04) | Si no se puede extraer, el aviso nombra el archivo usado ("❌ No se pudo extraer: archivo.pdf"). El "último PDF" sigue siendo el más reciente de la carpeta, sea o no OCC: Javier lo quiere así | ✅ |

Verificado (2026-10-04): extracción idéntica al original en 13 OCC sintéticas y 36/36 campos en 6 OCC reales; pantalla probada en vivo por Claude con datos sintéticos (todos los botones, buscadores, editor y atajos).

Atajos actualizados por pedido de Javier (2026-10-04): Alt+S asunto, Alt+D cuerpo, Alt+F mensaje de retiro, Alt+G mensaje de guía, Alt+A foco en el buscador de clientes y Alt+X copiar PDF. Se quitaron todos los Ctrl+1..5.

## Cambio aprobado (D-72, 2026-10-04)

Las filas 5 y 6 conservan las funciones y respuestas originales probadas en `Equivalencia/`. La pantalla de Mensajero ahora usa las plantillas configurables aprobadas para los mensajes de retiro y guía; las copias agregan una línea en blanco final para la firma. Las plantillas se guardan en `AlmacenMensajero` con claves `plantilla.retiro.*` y `plantilla.guia.hoffens`. No hay pantalla para editarlas todavía.

Marcadores: `{OCC}` y `{OCL}` son los números extraídos; `{DIA}` y `{BLOQUE}` son los datos de retiro Hoffens (vacíos y el bloque «Manual» se muestran como `_______________`). `{FECHA_GUIA}` es `hoy`, `ayer` o `el día X`; si «Otro» está vacío se usa `hoy`. `{OBRA}` es obra y comuna cuando hay ambas; si falta la obra o es «No detectada», se muestra `_______________`. Los marcadores desconocidos permanecen sin cambios.
