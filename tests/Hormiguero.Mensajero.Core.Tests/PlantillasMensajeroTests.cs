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
            "Retiro en BODEGA FRAY CAMILO 889\nIdentificarse como: \"Retiro JCV\"\n\nOCC 104523\nOCL 4500012345\n\nDirección: Fray Camilo Henríquez 889, Santiago\nHorario: lunes a viernes, 09:00 a 17:00 hrs",
            PlantillasMensajero.GenerarRetiro(almacen, "COBELCAR", "104523", "4500012345")
        );
        Assert.Equal(
            "Retiro en HOFFENS\nIdentificarse como: \"Retiro JCV\"\n\nOCC 104523\nOCL 4500012345\nNVV _______________\n\nDía: _______________\nBloque: _______________\nDirección: Camino Lonquen 10707, Maipú\nMapa: https://maps.app.goo.gl/smWJQSwCrq2vfCcx7\n\nSi no se retira ese día, queda armado 3 días hábiles más, en el mismo horario.",
            PlantillasMensajero.GenerarRetiro(
                almacen,
                "HOFFENS",
                "104523",
                "4500012345",
                "",
                "Manual"
            )
        );
        Assert.Equal(
            "Retiro en SENSUS (Bodega E1 INVAC)\nIdentificarse como: \"Retiro JCV\"\n\nOCC 104523\nOCL 4500012345\n\nDirección: Camino del Cerro 290, Quilicura\nMapa: https://maps.app.goo.gl/Eg8LzVUWXCSym7Yp7\nHorario: lunes a jueves 08:00-13:00 y 14:00-16:00 / viernes 08:00-13:00\n\nAntes de ir, enviar nombre y teléfono de quien retira y el día (se genera un QR para entrar).",
            PlantillasMensajero.GenerarRetiro(almacen, "SENSUS", "104523", "4500012345")
        );
        Assert.Equal(
            "Retiro en CHILE HDPE\nIdentificarse como: \"Retiro JCV\"\n\nOCC 104523\nOCL 4500012345\n\nDirección: Cacique Colín 11950, Lampa\nMapa: https://maps.app.goo.gl/HwHhbufKS8cBGboG9\nHorario: 08:30 a 17:30",
            PlantillasMensajero.GenerarRetiro(almacen, "CHILE HDPE", "104523", "4500012345")
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
}
