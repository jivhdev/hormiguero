# Genera tinta-base14.txt: la caja de tinta (en milésimas de em) que MuPDF/PyMuPDF usa para
# cada letra de las 14 fuentes estándar de PDF cuando el PDF no las trae incrustadas (MuPDF
# las reemplaza por URW Nimbus; PdfPig usa las medidas de Adobe, que difieren en milésimas).
# Ofisuiza decide qué letras entran en una zona con esa caja, así que Mensajero la necesita.
# Uso: python generar_tinta_base14.py  (requiere PyMuPDF; escribe junto a este archivo)
import os

import pymupdf

FUENTES = {
    "Helvetica": "helv", "Helvetica-Oblique": "heit", "Helvetica-Bold": "hebo",
    "Helvetica-BoldOblique": "hebi", "Courier": "cour", "Courier-Oblique": "coit",
    "Courier-Bold": "cobo", "Courier-BoldOblique": "cobi", "Times-Roman": "tiro",
    "Times-Italic": "tiit", "Times-Bold": "tibo", "Times-BoldItalic": "tibi",
}
# WinAnsi: ASCII, Latin-1 y los extras de 0x80-0x9F.
LETRAS = [*range(0x20, 0x7F), *range(0xA0, 0x100),
          *map(ord, "€‚ƒ„…†‡ˆ‰Š‹ŒŽ‘’“”•–—˜™š›œžŸ")]

lineas = [f"# PyMuPDF {pymupdf.VersionBind}; letra x0 y0 x1 y1 (milésimas de em, y hacia arriba)"]
for nombre, corto in FUENTES.items():
    fuente = pymupdf.Font(corto)
    lineas.append(f"[{nombre}]")
    for u in LETRAS:
        if not fuente.has_glyph(u):
            continue
        caja = fuente.glyph_bbox(u)
        x0, y0, x1, y1 = (round(v * 1000) for v in (caja.x0, caja.y0, caja.x1, caja.y1))
        lineas.append(f"{u:X} {x0} {y0} {x1} {y1}")
destino = os.path.join(os.path.dirname(os.path.abspath(__file__)), "tinta-base14.txt")
with open(destino, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(lineas) + "\n")
print(destino, len(lineas))
