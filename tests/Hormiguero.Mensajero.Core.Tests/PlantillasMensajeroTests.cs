using Hormiguero.Mensajero.Core;
using Hormiguero.Mensajero.Core.ClickFactura;

namespace Hormiguero.Mensajero.Core.Tests;

public sealed class PlantillasMensajeroTests : IDisposable
{
    private readonly string raiz = Path.Combine(
        Path.GetTempPath(),
        "HormigueroPlantillasTests",
        Guid.NewGuid().ToString("N")
    );

    private AlmacenMensajero almacen = null!;

    public PlantillasMensajeroTests()
    {
        Directory.CreateDirectory(raiz);
        almacen = new AlmacenMensajero(Path.Combine(raiz, "mensajero.db"));
    }

    public void Dispose()
    {
        almacen.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(raiz, recursive: true);
    }

    [Fact]
    public void Plantillas_predeterminadas_de_retiro_generan_el_texto_aprobado()
    {
        Assert.Equal(
            "RETIRO COBELCAR\nRetirar con: OC JCV 104523\nDecir: \"Retiro JCV\"\n\nFray Camilo Henríquez 889, Santiago\nLunes a viernes, 09:00 a 17:00\n\nRef.: OC cliente 4500012345",
            PlantillasMensajero.GenerarRetiro(almacen, "COBELCAR", "104523", "4500012345")
        );
        Assert.Equal(
            "RETIRO HOFFENS\nRetirar con: NVV Hoffens _______________ · OC JCV 104523\nDecir: \"Retiro JCV\"\n\nDía: _______________ · Bloque: _______________\nCamino Lonquen 10707, Maipú\nhttps://maps.app.goo.gl/smWJQSwCrq2vfCcx7\n\nRef.: OC cliente 4500012345\nSi no se retira ese día, queda 3 días hábiles más, mismo horario.",
            PlantillasMensajero.GenerarRetiro(
                almacen,
                "HOFFENS",
                "104523",
                "4500012345",
                "",
                "Manual",
                ""
            )
        );
        Assert.Equal(
            "RETIRO SENSUS (Bodega E1 INVAC)\nRetirar con: OC JCV 104523\nDecir: \"Retiro JCV\"\n\nAntes de ir, envíenos nombre, teléfono y patente de quien retira, y el día (Sensus genera un QR para entrar).\n\nCamino del Cerro 290, Quilicura\nhttps://maps.app.goo.gl/Eg8LzVUWXCSym7Yp7\nLun a jue 08:00-13:00 y 14:00-16:00 · Vie 08:00-13:00\n\nRef.: OC cliente 4500012345",
            PlantillasMensajero.GenerarRetiro(almacen, "SENSUS", "104523", "4500012345")
        );
        Assert.Equal(
            "RETIRO CHILE HDPE\nRetirar con: OC JCV 104523\nDecir: \"Retiro JCV\"\n\nCacique Colín 11950, Lampa\nhttps://maps.app.goo.gl/HwHhbufKS8cBGboG9\nLunes a viernes, 08:30 a 17:30\n\nRef.: OC cliente 4500012345",
            PlantillasMensajero.GenerarRetiro(almacen, "CHILE HDPE", "104523", "4500012345")
        );
    }

    [Fact]
    public void Retiro_hoffens_incluye_nvv_cuando_se_ingresa()
    {
        Assert.Contains(
            "Retirar con: NVV Hoffens 1066086 · OC JCV 104523",
            PlantillasMensajero.GenerarRetiro(
                almacen,
                "HOFFENS",
                "104523",
                "4500012345",
                "martes 15",
                "09:00 a 12:00",
                "1066086"
            )
        );
        Assert.Contains(
            "Día: martes 15 · Bloque: 09:00 a 12:00",
            PlantillasMensajero.GenerarRetiro(
                almacen,
                "HOFFENS",
                "104523",
                "4500012345",
                "martes 15",
                "09:00 a 12:00",
                "1066086"
            )
        );
    }

    [Fact]
    public void Plantilla_predeterminada_de_guia_genera_el_texto_aprobado()
    {
        Assert.Equal(
            "Buenos días:\n\nEnvío la guía emitida hoy para la obra Obra Ejemplo, Maipú.\nSi llegan más guías hoy, se las envío. Hoffens lo contactará para coordinar la entrega.\n\nSaludos.",
            PlantillasMensajero.GenerarGuia(almacen, "Obra Ejemplo", "Maipú", OpcionDia.Otro, "")
        );
        Assert.Equal(
            "Buenos días:\n\nEnvío la guía emitida ayer para la obra _______________.\nSi llegan más guías hoy, se las envío. Hoffens lo contactará para coordinar la entrega.\n\nSaludos.",
            PlantillasMensajero.GenerarGuia(almacen, "No detectada", "", OpcionDia.Ayer, "")
        );
        Assert.Contains(
            "emitida el día 4 para la obra Obra Ejemplo.",
            PlantillasMensajero.GenerarGuia(almacen, "Obra Ejemplo", "", OpcionDia.Otro, "4")
        );
    }

    [Fact]
    public void Plantilla_predeterminada_de_facturas_genera_el_texto_aprobado()
    {
        DocumentoFactura[] documentos =
        [
            new("FCV", "1", "76000000-1"),
            new("NCV", "2", "76000000-1"),
        ];

        Assert.Equal(
            "Estimados:\n\nAdjunto las facturas y notas de crédito de la 2° semana de Empresa Ejemplo.\n\nSaludos cordiales.",
            PlantillasMensajero.GenerarCuerpoFactura(
                almacen,
                "Empresa Ejemplo",
                "2° semana",
                documentos
            )
        );
        Assert.Equal(
            "notas de crédito",
            PlantillasMensajero
                .GenerarCuerpoFactura(
                    almacen,
                    "Empresa Ejemplo",
                    "2° semana",
                    [new("NCV", "2", "76000000-1")]
                )
                .Split("Adjunto las ")[1]
                .Split(" de la")[0]
        );
    }

    [Fact]
    public void Plantilla_guardada_reemplaza_la_predeterminada_y_marcador_desconocido_se_conserva()
    {
        almacen.GuardarValor(PlantillasMensajero.ClaveCobelcar, "OCC={OCC}; dato={NUEVO}");
        Assert.Equal(
            "OCC=104523; dato={NUEVO}",
            PlantillasMensajero.GenerarRetiro(almacen, "COBELCAR", "104523", "4500012345")
        );
    }

    [Fact]
    public void Vista_previa_reemplaza_marcadores_con_datos_de_ejemplo()
    {
        Assert.Equal(
            "OCC 104523, OCL 4500012345",
            PlantillasMensajero.CrearVistaPrevia(
                PlantillasMensajero.ClaveCobelcar,
                "OCC {OCC}, OCL {OCL}"
            )
        );
        Assert.Contains(
            "Obra Ejemplo, Maipú",
            PlantillasMensajero.CrearVistaPrevia(PlantillasMensajero.ClaveGuiaHoffens)
        );
    }

    [Fact]
    public void Volver_al_texto_predeterminado_reemplaza_el_texto_guardado()
    {
        almacen.GuardarValor(PlantillasMensajero.ClaveCobelcar, "Texto personalizado");

        PlantillasMensajero.VolverAlTextoPredeterminado(almacen, PlantillasMensajero.ClaveCobelcar);

        Assert.Equal(
            PlantillasMensajero.RetiroCobelcar,
            almacen.LeerValor(PlantillasMensajero.ClaveCobelcar)
        );
    }
}
