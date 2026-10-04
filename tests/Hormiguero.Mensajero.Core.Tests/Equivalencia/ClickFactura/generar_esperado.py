"""Genera entradas y respuestas ejecutando los servicios originales con datos sintéticos."""

import json
import hashlib
import os
import sys
import tempfile
import types
from datetime import datetime
from pathlib import Path

from openpyxl import Workbook

ORIGINAL = Path(r"C:\Users\jihja\Desktop\Entorno Antiguo\AP03-ClickFactura")
if not ORIGINAL.is_dir():
    raise SystemExit(f"No se encontró el original ClickFactura: {ORIGINAL}")
sys.path.insert(0, str(ORIGINAL))
sys.path.insert(0, str(ORIGINAL / "src"))

# Evita cargar la integración del portapapeles de Windows al importar servicios puros.
paquete_servicios = types.ModuleType("services")
paquete_servicios.__path__ = [str(ORIGINAL / "src" / "services")]
sys.modules["services"] = paquete_servicios

from database import clientes_repo, db as base_datos  # noqa: E402
from services import analysis, copier, email_composer, file_finder, xls_reader  # noqa: E402
from utils import rut_utils  # noqa: E402
import config  # noqa: E402


SALIDA = Path(__file__).parent
ENTRADA_XLSX = SALIDA / "entrada.xlsx"


class FechaFija(datetime):
    @classmethod
    def now(cls, tz=None):
        return cls(2026, 10, 4, 12, 30, 45)


def crear_entrada():
    libro = Workbook()
    hoja = libro.active
    hoja.title = "Ventas"
    hoja.append(["Reporte semanal"])
    hoja.append(["TD", "Número", "Entidad", "Descripción"])
    filas = [
        ["FCV", 123, "76.000.000-1", "Factura de prueba"],
        ["NCV", 9876543210, "76000000-1", "Nota grande"],
        ["FCV", "AB12", "77.000.000-K", "Empresa Ñandú"],
        ["FCV", 7, "77.000.000-K", "Segundo documento"],
        ["NCV", "12.5", "78.000.000-0", "Número textual no entero"],
        [None, None, None, None],
        ["NCV", 42, "78.000.000-0", "Tildes: Á É Í Ó Ú"],
    ]
    for fila in filas:
        hoja.append(fila)
    libro.save(ENTRADA_XLSX)


def normalizar_documento(documento):
    return {"td": documento["td"], "numero": documento["numero"], "entidad": documento["entidad"]}


crear_entrada()
documentos = [normalizar_documento(documento) for documento in xls_reader.leer_xls(ENTRADA_XLSX)]

casos_rut = [
    "76.123.456-7",
    "76 123 456-k",
    "76123456k",
    "76000000-1",
    "77000000-K",
    "",
    "RUT inválido",
]

correos = []
for razon_social, descripcion, tipos in [
    ("EMPRESA DE PRUEBA", "1° semana de marzo 2026", ["FCV"]),
    ("Compañía Ñandú", "2° semana de agosto 2026", ["NCV"]),
    ("Áridos Uno", "3° semana de octubre 2026", ["FCV", "NCV", "FCV"]),
    ("Cliente vacío", "", []),
]:
    docs = [{"td": tipo, "numero": str(indice), "entidad": "76000000-1"} for indice, tipo in enumerate(tipos)]
    mensaje = email_composer.generar_mensaje_completo(razon_social, descripcion, docs)
    correos.append({"razon_social": razon_social, "descripcion": descripcion, "documentos": docs, **mensaje})

with tempfile.TemporaryDirectory(prefix="clickfactura-sintetico-") as temporal:
    raiz = Path(temporal)
    base_pdfs = raiz / "documentos"
    directorio = base_pdfs / "FCV" / "2026" / "202603"
    directorio.mkdir(parents=True)
    archivos = ["FCV0000000123.pdf", "FCV0000000123_CEDIBLE.pdf", "FCV123.pdf", "NCV0000000042.pdf"]
    for nombre in archivos:
        carpeta = directorio if nombre.startswith("FCV") else base_pdfs / "NCV" / "2026" / "202603"
        carpeta.mkdir(parents=True, exist_ok=True)
        (carpeta / nombre).write_bytes(b"PDF sintetico de prueba")

    file_finder.get_ruta_documentos = lambda tipo, year, month: base_pdfs / tipo / str(year) / f"{year}{month:02d}"
    file_finder.DRIVE_BASE = base_pdfs
    config.DRIVE_BASE = base_pdfs
    hallazgos = {
        "fcv": Path(file_finder.buscar_pdf("FCV", "0000000123", 2026, 3)).name,
        "ncv": Path(file_finder.buscar_pdf("NCV", "42", 2026, 3)).name,
        "cedible_sola": file_finder.buscar_pdf("FCV", "999", 2026, 3),
        "ampliada": Path(file_finder.buscar_pdf_en_todas_las_carpetas("FCV", "123")).name,
    }

    fuente_temporal = raiz / "pdfs_sinteticos"
    destino_temporal = raiz / "copias_sinteticas"
    fuente_temporal.mkdir()
    contenido_sintetico = b"Adjunto PDF inventado, no corresponde a un documento real."
    (fuente_temporal / "FCV0000000123.pdf").write_bytes(contenido_sintetico)
    (fuente_temporal / "NCV0000000042.pdf").write_bytes("Segunda entrada sint\u00e9tica.".encode("utf-8"))
    copier.datetime = FechaFija
    carpeta_temporal = copier.crear_carpeta_temporal(destino_temporal, "76.000.000-1")
    rutas_copiadas = copier.copiar_pdfs(list(fuente_temporal.iterdir()), carpeta_temporal)
    copia_temporal = {
        "carpeta": Path(carpeta_temporal).name,
        "archivos": [Path(ruta).name for ruta in rutas_copiadas],
        "huellas_sha256": {
            Path(ruta).name: hashlib.sha256(Path(ruta).read_bytes()).hexdigest()
            for ruta in rutas_copiadas
        },
    }

    db_path = raiz / "clientes-sinteticos.sqlite"
    base_datos.init_database(db_path)
    conn = base_datos.get_connection(db_path)
    for rut, nombre, correo in [
        ("76000000-1", "EMPRESA DE PRUEBA", "prueba@ejemplo.cl"),
        ("77000000-K", "Empresa Ñandú", "nandu@ejemplo.cl"),
        ("78000000-0", "Áridos Uno", "aridos@ejemplo.cl"),
    ]:
        clientes_repo.insertar_o_actualizar(conn, rut, nombre, correo)
    conn.close()
    file_finder.DRIVE_BASE = base_pdfs
    analysis.buscar_pdf_en_todas_las_carpetas = lambda tipo, numero, cache=None: file_finder.buscar_pdf_en_todas_las_carpetas(tipo, numero, cache)
    analysis.buscar_lote = lambda docs, periodos: file_finder.buscar_lote(docs, periodos)
    analisis = analysis.ejecutar_analisis(ENTRADA_XLSX, [(2026, 3)], "prueba de equivalencia", db_path)

def limpiar_rut_original(rut):
    """Resultado del original, o el tipo de error que lanza (también lo decide el original)."""
    try:
        return {"limpio": rut_utils.limpiar_rut(rut)}
    except Exception as error:  # noqa: BLE001
        return {"error": type(error).__name__}


salida = {
    "documentos_excel": documentos,
    "ruts": [
        {"entrada": rut, **limpiar_rut_original(rut)}
        for rut in casos_rut
    ],
    "validaciones_rut": [
        {"rut": rut, "valido": rut_utils.validar_rut(rut)}
        for rut in ["76123456-7", "77000000-K", "76000000-1", "123-4", "760000001"]
    ],
    "correos": correos,
    "busquedas_pdf": hallazgos,
    "copia_temporal": copia_temporal,
    "analisis": {
        "ruts_no_registrados": analisis.get("ruts_no_registrados", []),
        "total_documentos": analisis["total_documentos"],
        "total_encontrados": analisis["total_encontrados"],
        "total_faltantes": analisis["total_faltantes"],
        "clientes": [
            {
                "rut": cliente["rut"],
                "razon_social": cliente["razon_social"],
                "correo": cliente["correo"],
                "documentos": [
                    {
                        **documento,
                        "ruta_pdf": Path(documento["ruta_pdf"]).name if documento["ruta_pdf"] else None,
                    }
                    for documento in cliente["documentos"]
                ],
                "mensaje": cliente["mensaje"],
            }
            for cliente in analisis["clientes_procesados"]
        ],
    },
}
with (SALIDA / "esperado.json").open("w", encoding="utf-8") as archivo:
    json.dump(salida, archivo, ensure_ascii=False, indent=2)
print(f"Generado: {SALIDA / 'esperado.json'}")
