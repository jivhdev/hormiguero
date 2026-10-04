# Genera la "respuesta correcta" de Ofisuiza (versión en uso) para las pruebas de
# equivalencia de Mensajero (D-65, D-68). Solo datos SINTÉTICOS: ningún documento real.
# Uso: python equivalencia_ofisuiza.py <carpeta_salida>
import json
import os
import sys

import fitz  # PyMuPDF, la misma librería que usa Ofisuiza

OFISUIZA = r"C:\Users\jihja\Desktop\Entorno Antiguo\AP07-Ofisuiza Versiones Antiguas\OFISUIZA .EXE FUNCIONAL VERSION EN USO"
sys.path.insert(0, OFISUIZA)
import config  # noqa: E402
import extractor  # noqa: E402
import gestor_carpeta  # noqa: E402
import clientes_nvv  # noqa: E402
import mensajes_retiro  # noqa: E402

salida = sys.argv[1]
os.makedirs(salida, exist_ok=True)


def escribir(pagina, rect, texto, tam=9):
    """Escribe texto dentro de una zona de Ofisuiza (origen arriba a la izquierda)."""
    x0, y0, x1, y1 = rect
    pagina.insert_text((x0 + 1, y1 - 3), texto, fontsize=tam, fontname="helv")


def occ_sintetica(nombre, occ, nvv, ocl, proveedor, despacho):
    doc = fitz.open()
    pag = doc.new_page(width=595.28, height=841.89)  # A4
    pag.insert_text((40, 60), "ORDEN DE COMPRA - DOCUMENTO SINTETICO DE PRUEBA", fontsize=12, fontname="helv")
    escribir(pag, config.COORD_OCC, occ)
    escribir(pag, config.COORD_NVV, nvv)
    escribir(pag, config.COORD_OCL, ocl)
    escribir(pag, config.COORD_PROVEEDOR, proveedor)
    y = config.COORD_DESPACHO[1] + 14
    for linea in despacho:
        pag.insert_text((36, y), linea, fontsize=9, fontname="helv")
        y += 13
    ruta = os.path.join(salida, nombre)
    doc.save(ruta)
    doc.close()
    return ruta


casos = [
    ("OCC000104523.pdf", "000104523", "NVV-55123", "4500012345", "Señores/as: HOFFENS S.A.",
     ["INFORMACIÓN PARA EL DESPACHO", "Obra: Edificio Las Lilas", "Dirección: Av. Siempre Viva 123", "Comuna: Maipú", "Contacto: Juan Perez"]),
    ("OCC104524.pdf", "104524", "NVV 55124", "4500012346", "Señores/as: VELIZ BELTRAN LIMITADA",
     ["INFORMACIÓN PARA EL DESPACHO", "RETIRA CLIENTE EN BODEGA", "Santiago"]),
    ("OCC104525.pdf", "104525", "55125", "OC-778", "Señores: SENSUS LTDA.",
     ["INFORMACIÓN PARA EL DESPACHO", "Obra: Planta Norte", "Comuna: Quilicura"]),
    ("OCC104526.pdf", "104526", "", "4500099", "Señores/as: CHILE HDPE SpA",
     ["INFORMACIÓN PARA EL DESPACHO", "Condominio El Roble", "Avenida Principal 45, Lampa"]),
    ("OCC104527.pdf", "104527", "NVV-1", "X-1", "Señores/as: FERRETERIA PEREZ S.A.",
     ["INFORMACIÓN PARA EL DESPACHO"]),
]

# Casos de borde (2026-10-04): con OCC reales la traducción capturaba letras vecinas que
# tocan apenas el borde de las zonas de NVV y OCL (zonas pegadas y algo traslapadas).
# Etiquetas que cruzan el borde izquierdo y renglones vecinos a pocos puntos, para que la
# respuesta correcta del Ofisuiza original fije cómo se cuenta cada letra en el borde.
def occ_borde(nombre, cruce, separacion, tam):
    doc = fitz.open()
    pag = doc.new_page(width=595.28, height=841.89)
    pag.insert_text((40, 60), "OCC SINTETICA DE BORDE", fontsize=12, fontname="helv")
    for zona, etiqueta, valor in [(config.COORD_OCC, "N° Orden:", "0000020999"),
                                  (config.COORD_NVV, "Nota de Venta:", "NVV-12345"),
                                  (config.COORD_OCL, "O.C. Cliente:", "4500077777")]:
        x0, y0, x1, y1 = zona
        base = y1 - 3
        ancho = fitz.get_text_length(etiqueta, fontname="helv", fontsize=tam)
        pag.insert_text((x0 + cruce - ancho, base), etiqueta, fontsize=tam, fontname="helv")
        pag.insert_text((x0 + 2, base), valor, fontsize=tam, fontname="helv")
    # renglón extra justo debajo del OCL y justo encima del NVV
    pag.insert_text((config.COORD_OCL[0] + 2, config.COORD_OCL[3] - 3 + separacion), "Obra 9", fontsize=tam, fontname="helv")
    pag.insert_text((config.COORD_NVV[0] + 2, config.COORD_NVV[3] - 3 - separacion), "Fecha 01-10-2026", fontsize=tam, fontname="helv")
    escribir(pag, config.COORD_PROVEEDOR, "Señores/as: HOFFENS S.A.")
    y = config.COORD_DESPACHO[1] + 14
    for linea in ["INFORMACIÓN PARA EL DESPACHO", "Obra: Borde", "Comuna: Renca"]:
        pag.insert_text((36, y), linea, fontsize=9, fontname="helv")
        y += 13
    ruta = os.path.join(salida, nombre)
    doc.save(ruta)
    doc.close()

bordes = []
for i, (cruce, separacion, tam) in enumerate([(0.0, 12, 9), (1.0, 12, 9), (2.5, 11, 9), (4.0, 10, 9),
                                               (-1.0, 12, 10), (1.5, 9, 8), (3.0, 12, 10), (0.5, 8, 9)]):
    nombre = f"BORDE{i + 1}.pdf"
    occ_borde(nombre, cruce, separacion, tam)
    bordes.append(nombre)

resultado = {"extraccion": [], "mensajes_retiro": [], "guias": [], "numeros_en_nombre": [], "buscar_clientes": []}

for nombre, occ, nvv, ocl, prov, despacho in casos + [(b, None, None, None, None, None) for b in bordes]:
    ruta = occ_sintetica(nombre, occ, nvv, ocl, prov, despacho) if occ is not None else os.path.join(salida, nombre)
    o, n, c = extractor.extraer_occ_nvv_ocl(ruta)
    dp = extractor.extraer_despacho(ruta)
    p = extractor.extraer_proveedor(ruta)
    resultado["extraccion"].append({
        "archivo": nombre, "occ": o, "nvv": n, "ocl": c,
        "asunto": extractor.formatear_linea_ocl(o, n, c), "despacho": dp, "proveedor": p,
    })

# Mensajes de retiro: los cuatro proveedores, Hoffens con y sin día/bloque, y uno desconocido.
for prov, kw in [("COBELCAR", {}), ("HOFFENS", {}), ("HOFFENS", {"dia": "Martes 7", "bloque": "09:00 - 11:00"}),
                 ("SENSUS", {}), ("CHILE HDPE", {}), ("FERRETERIA PEREZ", {})]:
    resultado["mensajes_retiro"].append({
        "proveedor": prov, "occ": "104523", "ocl": "4500012345", **kw,
        "mensaje": mensajes_retiro.generar_mensaje_retiro(prov, "104523", "4500012345", **kw),
    })

# Mensaje de guía (copiado tal cual de ui/app.py: _extraer_obra y generar_mensaje_guia).
def extraer_obra(texto):
    obra, comuna = "", ""
    ls = texto.split('\n')
    for i, l in enumerate(ls):
        lp = l.strip()
        if 'Obra:' in lp or 'obra:' in lp: obra = lp.split(':', 1)[-1].strip(); break
        elif i == 1 and lp and 'INFORMACIÓN' not in lp.upper(): obra = lp
    for l in ls:
        lp = l.strip()
        if 'Comuna:' in lp or 'comuna:' in lp: comuna = lp.split(':', 1)[-1].strip(); break
        for c in ['Santiago','Maipú','Quilicura','Lampa','Puente Alto','Las Condes','Providencia','Ñuñoa','La Florida','San Bernardo','Renca','Conchalí','Huechuraba','Vitacura','Lo Barnechea','Colina','Pudahuel','Estación Central','Cerrillos','Peñalolén']:
            if c.upper() in lp.upper(): comuna = c; break
        if comuna: break
    return obra, comuna

def mensaje_guia(obra, comuna, opcion_dia, dia_manual):
    if not obra or obra == "No detectada": obra = "_______________"
    ub = f"{obra}, {comuna}" if comuna else obra
    if opcion_dia == "hoy": dia_texto = "el día de hoy"
    elif opcion_dia == "ayer": dia_texto = "el día de ayer"
    else: dia_texto = f"el día {dia_manual.strip()}" if dia_manual.strip() else "el día de hoy"
    return f"Buenos días. Para su conocimiento, envío guía emitida {dia_texto} hacia Obra {ub}. De recibir guías adicionales durante la jornada, se las haré llegar oportunamente. Desde Hoffens se comunicarán con usted para coordinar la entrega. Saludos."

for e in resultado["extraccion"]:
    obra, comuna = extraer_obra(e["despacho"])
    for opcion, manual in [("hoy", ""), ("ayer", ""), ("manual", "lunes 6"), ("manual", "")]:
        resultado["guias"].append({
            "archivo": e["archivo"], "obra": obra, "comuna": comuna, "opcion_dia": opcion, "dia_manual": manual,
            "mensaje": mensaje_guia(obra or "No detectada", comuna, opcion, manual),
        })

for nombre in ["OCC000104523.pdf", "occ-104524 v2.pdf", "Factura 0012 de 2026.pdf", "sin numero.pdf", "00000.pdf"]:
    resultado["numeros_en_nombre"].append({"nombre": nombre, "numero": gestor_carpeta.extraer_numero_desde_nombre(nombre)})

lista = ["76000000-1 EMPRESA UNO RETIRA CLIENTE", "77000000-2 Constructora Dos RETIRA CLIENTE", "78000000-K OTRA EMPRESA RETIRA CLIENTE"]
for texto in ["", "uno", " constructora ", "77000000", "nada"]:
    resultado["buscar_clientes"].append({"lista": lista, "texto": texto, "resultado": clientes_nvv.buscar_clientes(texto, lista)})
resultado["clientes_por_defecto"] = clientes_nvv.CLIENTES_DEFAULT

with open(os.path.join(salida, "esperado.json"), "w", encoding="utf-8") as f:
    json.dump(resultado, f, ensure_ascii=False, indent=2)
print("listo:", len(resultado["extraccion"]), "OCC,", len(resultado["mensajes_retiro"]), "mensajes,", len(resultado["guias"]), "guias")
