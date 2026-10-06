# Genera los íconos de las apps de Hormiguero: una letra blanca en un círculo de color,
# el mismo estilo que el ícono de Archivero. Uso: python herramientas/generar_iconos.py
import os
from PIL import Image, ImageDraw, ImageFont

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
APPS = {  # app: (letra, color)
    "Buscadero": ("B", (46, 139, 87)),   # verde
    "Mensajero": ("M", (230, 126, 34)),  # naranjo
}
TAMANOS = [16, 32, 48, 64, 128, 256]

def fuente(tam):
    for nombre in ("segoeuib.ttf", "arialbd.ttf"):
        try:
            return ImageFont.truetype(nombre, tam)
        except OSError:
            pass
    return ImageFont.load_default()

for app, (letra, color) in APPS.items():
    im = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse((4, 4, 252, 252), fill=color)
    f = fuente(150)
    caja = d.textbbox((0, 0), letra, font=f)
    x = (256 - (caja[2] - caja[0])) / 2 - caja[0]
    y = (256 - (caja[3] - caja[1])) / 2 - caja[1]
    d.text((x, y), letra, font=f, fill="white")
    destino = os.path.join(RAIZ, "src", f"Hormiguero.{app}", "Recursos", "icono.ico")
    im.save(destino, sizes=[(t, t) for t in TAMANOS])
    print(destino)
