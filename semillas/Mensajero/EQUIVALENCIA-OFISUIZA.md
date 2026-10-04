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
| 2 | Asunto del correo | "OCC {occ} NVV {nvv} OCL {ocl}", omitiendo lo vacío; Ctrl+1 con el último PDF | ✅ |
| 3 | Cuerpo del correo (despacho) | Bloque "INFORMACIÓN PARA EL DESPACHO" hasta el final de la página; título + líneas no vacías; Ctrl+2 | ✅ |
| 4 | Proveedor | Zona "Señores/as:"; sin S.A./LTDA./LIMITADA/LTD; mapeo HOFFENS, COBELCAR (incluye VELIZ BELTRAN), SENSUS, CHILE HDPE (incluye HDPE), FLUCHILE | ✅ |
| 5 | Mensaje de retiro por proveedor | Textos exactos de Cobelcar, Hoffens (día y bloque 09:00-11:00 / 11:00-13:00 / manual), Sensus, Chile HDPE; "Proveedor no reconocido" para el resto; Ctrl+3 | ✅ |
| 6 | Envío de guías (Hoffens) | Obra y comuna desde el despacho (lista de 20 comunas); día hoy / ayer / otro; texto exacto; Ctrl+4 | ✅ |
| 7 | Buscar OCC por número | Primer número del nombre del archivo, sin ceros; avisa si no hay o si hay varias | ✅ |
| 8 | Último PDF de la carpeta | El de fecha de modificación más reciente | ✅ |
| 9 | Carpeta de OCC | Se elige una vez y se recuerda ("Cambiar") | ✅ |
| 10 | Modo automático | Vigila la carpeta (sin subcarpetas); al llegar un PDF lo precarga y avisa | ✅ |
| 11 | Clientes NVV (Cobelcar) | Lista "RUT NOMBRE RETIRA CLIENTE"; buscador sin distinguir mayúsculas; Enter copia el primero; doble clic / botón copia la selección; 7 clientes por defecto si no hay lista | ✅ |
| 12 | Editar clientes | Una línea por cliente; aviso de cambios sin guardar; confirmar al salir | ✅ |
| 13 | Copiar al portapapeles con aviso | Cada copia muestra un aviso breve ("toast") | ✅ |
| 14 | Datos que se conservan (D-68) | `clientes.txt` en uso (2026-10-02; mostrar a Javier las 2 líneas que solo están en la copia de `dist`) | ⏳ |

| 15 | Nuevo (pedido de Javier, 2026-10-04) | "Copiar PDF (Ctrl+5)": copia el ARCHIVO de la última OCC agregada a la carpeta (fecha de creación o modificación, la más reciente) para pegarla como adjunto | ✅ |
| 16 | Cambio pedido por Javier (2026-10-04) | Si no se puede extraer, el aviso nombra el archivo usado ("❌ No se pudo extraer: archivo.pdf"). El "último PDF" sigue siendo el más reciente de la carpeta, sea o no OCC: Javier lo quiere así | ✅ |

Verificado (2026-10-04): extracción idéntica al original en 13 OCC sintéticas y 36/36 campos en 6 OCC reales; pantalla probada en vivo por Claude con datos sintéticos (todos los botones, buscadores, editor y atajos).

Atajos: se mantienen Ctrl+1 a Ctrl+4 (costumbre de Javier) y se suman los Alt del boceto aprobado (M4, D-55).
