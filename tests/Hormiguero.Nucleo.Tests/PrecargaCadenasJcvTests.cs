using Hormiguero.Nucleo.Datos;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Tests;

public sealed class PrecargaCadenasJcvTests
{
    [Fact]
    public void Normaliza_rut_y_nombres_de_proveedor()
    {
        Assert.Equal("96548020K", ProveedorDeDocumento.NormalizarRut("96.548.020-k"));
        Assert.Equal("HOFFENS", ProveedorDeDocumento.NormalizarNombre("  Hofféns   S.A. "));
        Assert.Equal(
            "VELIZ BELTRAN",
            ProveedorDeDocumento.NormalizarNombre("Véliz Beltrán Limitada")
        );
    }

    [Fact]
    public void Importa_solo_una_vez_y_revierte_un_json_danado()
    {
        using var conexion = new SqliteConnection("Data Source=:memory:");
        conexion.Open();
        Migraciones.Aplicar(conexion, Migraciones.Todas);
        string ruta = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(
                ruta,
                """
                {"version":1,"esquemas":[{"proveedor":"ACME","nombre":"Proceso","reconocimientos":[{"tipo":"nombre","valor":"Acme S.A."}],"lugares":[{"tipo":"oc_propia","nombre":"OCC","orden":0,"inicia":true}],"enlaces":[],"modos":["Retiro"],"proponerModo":[{"contiene":"RETIRA","modo":"Retiro"}],"avisos":[]}]}
                """
            );
            Assert.True(PrecargaCadenasJcv.IntentarImportar(conexion, ruta, "Prueba"));
            Assert.False(PrecargaCadenasJcv.IntentarImportar(conexion, ruta, "Prueba"));
            using var consulta = conexion.CreateCommand();
            consulta.CommandText = "SELECT COUNT(*) FROM esquemas_diccionario;";
            Assert.Equal(1L, (long)consulta.ExecuteScalar()!);
            Assert.Equal(
                "ACME",
                RepositorioEsquemasDiccionario
                    .ObtenerPorReconocimiento(conexion, "nombre", "ACME")
                    ?.Proveedor
            );
            Assert.Equal(
                [new ReglaPropuestaModo(0, "RETIRA", "Retiro")],
                new RepositorioModosEsquemaDiccionario(conexion).ListarReglasPropuesta(1)
            );

            using var vacia = new SqliteConnection("Data Source=:memory:");
            vacia.Open();
            Migraciones.Aplicar(vacia, Migraciones.Todas);
            File.WriteAllText(ruta, "{");
            Assert.False(PrecargaCadenasJcv.IntentarImportar(vacia, ruta, "Prueba"));
            using var cuenta = vacia.CreateCommand();
            cuenta.CommandText = "SELECT COUNT(*) FROM esquemas_diccionario;";
            Assert.Equal(0L, (long)cuenta.ExecuteScalar()!);
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Theory]
    [InlineData("76838441-K EPAC RETIRA CLIENTE", "Retiro")]
    [InlineData("RETIRO EN BODEGA", "Retiro")]
    [InlineData("OBRA EDIFICIO VICTORIA, PUERTO MONTT", "Despacho")]
    [InlineData("OBRA REGIÓN", "Despacho")]
    public void Propone_modo_por_reglas_sin_distinguir_mayusculas_ni_tildes(
        string texto,
        string esperado
    )
    {
        var reglas = new[]
        {
            new ReglaPropuestaModo(0, "RETIRA", "Retiro"),
            new ReglaPropuestaModo(1, "RETIRO", "Retiro"),
        };
        Assert.Equal(esperado, RepositorioModosEsquemaDiccionario.Proponer(texto, reglas));
    }

    [Fact]
    public void Sin_texto_no_propone_modo()
    {
        Assert.Null(RepositorioModosEsquemaDiccionario.Proponer("  ", []));
    }
}
