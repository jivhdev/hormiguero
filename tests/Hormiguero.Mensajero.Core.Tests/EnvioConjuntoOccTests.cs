using Hormiguero.Mensajero.Core;

namespace Hormiguero.Mensajero.Core.Tests;

public sealed class EnvioConjuntoOccTests
{
    [Fact]
    public void Asunto_junta_datos_por_tipo_y_conserva_el_orden()
    {
        OccEnvioConjunto[] ordenes =
        [
            Crear("1", "11", "21"),
            Crear("2", "12", "21"),
            Crear("3", "13", "21"),
        ];

        Assert.Equal("OCC 1 2 3 NVV 11 12 13 OCL 21", EnvioConjuntoOcc.FormatearAsunto(ordenes));
    }

    [Fact]
    public void Asunto_conserva_OCL_distintas_y_omite_datos_faltantes()
    {
        OccEnvioConjunto[] ordenes =
        [
            Crear("1", "", "21"),
            Crear("2", "12", "22"),
            Crear("3", "12", ""),
        ];

        Assert.Equal("OCC 1 2 3 NVV 12 OCL 21 22", EnvioConjuntoOcc.FormatearAsunto(ordenes));
    }

    [Fact]
    public void Cuerpo_agrega_un_detalle_por_orden_en_su_orden()
    {
        OccEnvioConjunto[] ordenes =
        [
            Crear("1", "11", "21"),
            Crear("2", "12", "21"),
            Crear("3", "13", "21"),
        ];

        Assert.Equal(
            "Despacho\r\n\r\nOCC 1 · NVV 11 · OCL 21\r\nOCC 2 · NVV 12 · OCL 21\r\nOCC 3 · NVV 13 · OCL 21",
            EnvioConjuntoOcc.GenerarCuerpo("Despacho\r\n", ordenes)
        );
    }

    [Fact]
    public void Rechaza_agregar_una_OCC_de_otro_proveedor()
    {
        OccEnvioConjunto[] ordenes = [Crear("1", "11", "21", "COBELCAR")];

        Assert.False(EnvioConjuntoOcc.PuedeAgregar(ordenes, Crear("2", "12", "22", "HOFFENS")));
        Assert.True(EnvioConjuntoOcc.PuedeAgregar(ordenes, Crear("2", "12", "22", "COBELCAR")));
    }

    private static OccEnvioConjunto Crear(
        string occ,
        string nvv,
        string ocl,
        string proveedor = "COBELCAR"
    ) => new(occ, nvv, ocl, proveedor, $"{occ}.pdf");
}
