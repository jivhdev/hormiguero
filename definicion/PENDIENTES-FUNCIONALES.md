# Pendientes funcionales — que no se olviden

Ideas, arreglos y programas futuros que Javier anotó en el entorno antiguo. **No son requisitos todavía:** cuando le toque el turno a cada app, se revisan y Javier decide cuáles entran a su especificación.
Recopilado el 2026-10-03. Las citas son textuales.

## Archivar (hoy Archivero / Facturas JCV)

Fuente: `AP01\Arreglos archivero.txt`, `AP01\ARCHIVERO VERSION 0.1 B\preguntas\Caso-7.md`, `AP04\config.json`.

1. Guardado rápido con fecha: "Cuando guardo un guardado rapido, igual tengo que ingresar la fecha. Que me de la opcion de guardar la fecha igual en los guardados rapidos".
2. Impresión automática al archivar: "si esta activado como impresion automatica, tambien se imprime solo la primera imagen del documento, o que el usuario decida una por una, o otras opciones, pero que sea facil elegir entre las opciones". Caso-7 ya lo había detallado: desactivada por defecto, 3 modos (solo la primera página / preguntar cada vez / todas) y una impresora global (por defecto, la de Windows). Facturas JCV ya imprimía automáticamente.
3. Duplicados: "cuando cae un duplicado y eso crashea el programa, se cierra si llega a la carpeta un doc ya guardado... Hay que corregir eso".
4. Ventanas siempre maximizadas y la ruta de ubicación visible completa (Caso-7, punto 2).

## Buscar y ver (hoy Buscadero)

Fuente: `AP02\Arreglos buscadero.txt`.

1. Velocidad: "Se esta demorando muchos segundos por busqueda, minutos te diré. Necesito implementar una forma de que cuando no este buscando, como que vaya "adelantando" trabajo siendo claro, un vecino silencioso".
2. Imprimir: "Hay que crear el boton imprimir primera pagina y imprimir primeras 2 o algo asi".

## Comunicar (hoy ClickFactura / Ofisuiza)

Fuente: `AP03\Arreglos click factura.txt`, `AP06\Programas futuros.txt`.

1. "que el preparar envio sea claro a quien prepare el envio".
2. "cuando dejo un cliente pendiente de enviar, luego no puedo volver a revisar solo los pendientes. Debemos crear esa funcionalidad."
3. "debe ser mas claro cuando agrego 2 correos a un cliente, cuando o como se separan."
4. "La carpeta donde se generan los pdf temporales, debe poder ser elegida por el usuario [...] Y que haya un boton para abrir la carpeta elegida".
5. Ofisuiza: "Funcion en ofisuiza que x texto lo pase de minuscula a mayusculas".

## Seguimiento / enlace de documentos (hoy Motores / Legajo)

Fuente: `AP06\Programas futuros.txt`, `AP02\Legajo 1\Legajobsidian\Cambios que integrar en occ...md`.

1. "Para el momento de facturar, necesito algo que me diga solo con la occ a que cliente corresponde la nvv o la ocl o algo asi."
2. "Funcionalidad de algunos de los programas que enlace documentos entre si. Asi, al facturar, al poner la occ de la factura, me muestra de que cliente es, su nvv, y el numero de la guia de despacho, y el de la obra, todo listo para procesar".
3. Enlazar a un cliente antes de guardar (en Archivar), mientras el enlace completo no exista: "Talvez para archivero, crear la opcion de poner o enlazar a algun cliente antes de guardar".
4. Botones según el documento abierto, y una pestaña con todos juntos en pequeño; incluir ahí las herramientas de Ofisuiza que correspondan.

## Programas futuros (todavía sin app asignada)

Fuente: `AP06\Programas futuros.txt`, `AP06\motores 1\bitacora\historial\Motor futuro.md`.

1. Copiar ítems a la planilla de precios: "Programa que copie en el excel de precios de region los items, para no hacerlo manual uno a uno".
2. Comparar dos conjuntos de ítems: "Programa que compare 2 conjuntos de items ordenados por filas y columnas iguales, ver discrepancias o confirmar que todo este correcto."
3. Certificados de medidores: "Enlace entre certificado y numero medidor guia de despacho, cada medidor tiene un certificado asociado [...] los numeros de los certificados son largos, y van en intervalos y años los certificados, y claro el modelo de medidor." Y: "puedo subir todos los certificados en una capreta, y que este motor se encarge de indexar sus datos clasificables [...] Esta funcion sera nativa, porque es un trabajo importante que a dia de hoy me quita mucho tiempo."

## Forma de trabajar (ya cubierto)

- "Planificar con claude, construir con opencode o algo asi gratis. Buscar la forma de que no quede trabajo a "medias". Busacar una ia economica." → resuelto con D-14, D-15 y D-12.
