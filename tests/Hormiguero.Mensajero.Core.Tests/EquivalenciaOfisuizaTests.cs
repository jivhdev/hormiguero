using System.Text.Json;
using Hormiguero.Mensajero.Core;

namespace Hormiguero.Mensajero.Core.Tests;

public class EquivalenciaOfisuizaTests
{
    private static JsonDocument Esperado =>
        JsonDocument.Parse(
            File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Equivalencia", "Ofisuiza", "esperado.json")
            )
        );

    public static IEnumerable<object[]> DatosExtraccion()
    {
        using JsonDocument documento = Esperado;
        foreach (
            JsonElement caso in documento.RootElement.GetProperty("extraccion").EnumerateArray()
        )
            yield return [caso.GetProperty("archivo").GetString()!];
    }

    [Theory]
    [MemberData(nameof(DatosExtraccion))]
    public void ExtraccionCoincideCaracterPorCaracter(string archivo)
    {
        using JsonDocument documento = Esperado;
        JsonElement caso = documento
            .RootElement.GetProperty("extraccion")
            .EnumerateArray()
            .Single(elemento => elemento.GetProperty("archivo").GetString() == archivo);
        string ruta = Path.Combine(AppContext.BaseDirectory, "Equivalencia", "Ofisuiza", archivo);
        var (occ, nvv, ocl) = ExtractorOcc.ExtraerOccNvvOcl(ruta);
        Assert.Equal(caso.GetProperty("occ").GetString(), occ);
        Assert.Equal(caso.GetProperty("nvv").GetString(), nvv);
        Assert.Equal(caso.GetProperty("ocl").GetString(), ocl);
        Assert.Equal(
            caso.GetProperty("asunto").GetString(),
            ExtractorOcc.FormatearAsunto(occ, nvv, ocl)
        );
        Assert.Equal(caso.GetProperty("despacho").GetString(), ExtractorOcc.ExtraerDespacho(ruta));
        Assert.Equal(
            caso.GetProperty("proveedor").GetString(),
            ExtractorOcc.ExtraerProveedor(ruta)
        );
    }

    public static IEnumerable<object[]> DatosMensajes()
    {
        using JsonDocument documento = Esperado;
        foreach (
            JsonElement caso in documento
                .RootElement.GetProperty("mensajes_retiro")
                .EnumerateArray()
        )
            yield return [caso.Clone()];
    }

    [Theory]
    [MemberData(nameof(DatosMensajes))]
    public void MensajesRetiroCoincidenCaracterPorCaracter(JsonElement caso)
    {
        string? dia = caso.TryGetProperty("dia", out JsonElement diaJson)
            ? diaJson.GetString()
            : null;
        string? bloque = caso.TryGetProperty("bloque", out JsonElement bloqueJson)
            ? bloqueJson.GetString()
            : null;
        Assert.Equal(
            caso.GetProperty("mensaje").GetString(),
            MensajesRetiro.Generar(
                caso.GetProperty("proveedor").GetString()!,
                caso.GetProperty("occ").GetString()!,
                caso.GetProperty("ocl").GetString()!,
                dia,
                bloque
            )
        );
    }

    public static IEnumerable<object[]> DatosGuias()
    {
        using JsonDocument documento = Esperado;
        foreach (JsonElement caso in documento.RootElement.GetProperty("guias").EnumerateArray())
            yield return [caso.Clone()];
    }

    [Theory]
    [MemberData(nameof(DatosGuias))]
    public void GuiasCoincidenCaracterPorCaracter(JsonElement caso)
    {
        string opcion = caso.GetProperty("opcion_dia").GetString()!;
        OpcionDia dia = opcion switch
        {
            "hoy" => OpcionDia.Hoy,
            "ayer" => OpcionDia.Ayer,
            _ => OpcionDia.Otro,
        };
        string ruta = Path.Combine(
            AppContext.BaseDirectory,
            "Equivalencia",
            "Ofisuiza",
            caso.GetProperty("archivo").GetString()!
        );
        (string obra, string comuna) = MensajeGuia.ExtraerObraYComuna(
            ExtractorOcc.ExtraerDespacho(ruta)
        );
        Assert.Equal(caso.GetProperty("obra").GetString(), obra);
        Assert.Equal(caso.GetProperty("comuna").GetString(), comuna);
        Assert.Equal(
            caso.GetProperty("mensaje").GetString(),
            MensajeGuia.Generar(obra, comuna, dia, caso.GetProperty("dia_manual").GetString()!)
        );
    }

    public static IEnumerable<object[]> DatosNumeros()
    {
        using JsonDocument documento = Esperado;
        foreach (
            JsonElement caso in documento
                .RootElement.GetProperty("numeros_en_nombre")
                .EnumerateArray()
        )
            yield return
            [
                caso.GetProperty("nombre").GetString()!,
                caso.GetProperty("numero").GetString()!,
            ];
    }

    [Theory]
    [MemberData(nameof(DatosNumeros))]
    public void NumeroEnNombreCoincide(string nombre, string numero) =>
        Assert.Equal(numero, CarpetaOcc.NumeroEnNombre(nombre));

    public static IEnumerable<object[]> DatosBusquedaClientes()
    {
        using JsonDocument documento = Esperado;
        foreach (
            JsonElement caso in documento
                .RootElement.GetProperty("buscar_clientes")
                .EnumerateArray()
        )
        {
            string[] lista = caso.GetProperty("lista")
                .EnumerateArray()
                .Select(elemento => elemento.GetString()!)
                .ToArray();
            string[] resultado = caso.GetProperty("resultado")
                .EnumerateArray()
                .Select(elemento => elemento.GetString()!)
                .ToArray();
            yield return [caso.GetProperty("texto").GetString()!, lista, resultado];
        }
    }

    [Theory]
    [MemberData(nameof(DatosBusquedaClientes))]
    public void BuscarClientesCoincide(string texto, string[] lista, string[] resultado) =>
        Assert.Equal(resultado, ClientesNvv.Buscar(texto, lista));

    public static IEnumerable<object[]> DatosClientesPorDefecto()
    {
        using JsonDocument documento = Esperado;
        string[] clientes = documento
            .RootElement.GetProperty("clientes_por_defecto")
            .EnumerateArray()
            .Select(cliente => cliente.GetString()!)
            .ToArray();
        yield return [clientes];
    }

    [Theory]
    [MemberData(nameof(DatosClientesPorDefecto))]
    public void ClientesPorDefectoCoinciden(string[] clientes) =>
        Assert.Equal(clientes, ClientesNvv.PorDefecto);

    [Fact]
    public void CarpetaOccBuscaSoloPdfDeLaCarpetaYOrdenaPorModificacion()
    {
        string carpeta = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string subcarpeta = Path.Combine(carpeta, "sub");
        Directory.CreateDirectory(subcarpeta);
        try
        {
            string viejo = Path.Combine(carpeta, "OCC00012.PDF");
            string nuevo = Path.Combine(carpeta, "OCC12 copia.pdf");
            File.WriteAllText(viejo, "a");
            File.WriteAllText(nuevo, "b");
            File.WriteAllText(Path.Combine(carpeta, "OCC12.txt"), "c");
            File.WriteAllText(Path.Combine(subcarpeta, "OCC12.pdf"), "d");
            File.SetLastWriteTimeUtc(viejo, DateTime.UtcNow.AddMinutes(-2));
            File.SetLastWriteTimeUtc(nuevo, DateTime.UtcNow.AddMinutes(-1));

            Assert.Equal(nuevo, CarpetaOcc.UltimoPdf(carpeta));
            Assert.Equal(
                new[] { viejo, nuevo },
                CarpetaOcc.BuscarPorNumero(carpeta, "12").OrderBy(ruta => ruta)
            );
            Assert.Null(CarpetaOcc.UltimoPdf(Path.Combine(carpeta, "no-existe")));
        }
        finally
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }

    [Fact]
    public void ClientesNvvLeeYGuardaUtf8SinLineasVacias()
    {
        string carpeta = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        string ruta = Path.Combine(carpeta, "clientes.txt");
        try
        {
            Assert.Equal(ClientesNvv.PorDefecto, ClientesNvv.Leer(ruta));
            Assert.True(ClientesNvv.Guardar(ruta, [" Uno ", "", "Ñandú"]));
            Assert.Equal(new[] { "Uno", "Ñandú" }, ClientesNvv.Leer(ruta));
            Assert.Equal(
                $"Uno{Environment.NewLine}Ñandú{Environment.NewLine}",
                File.ReadAllText(ruta)
            );
            Assert.True(ClientesNvv.Guardar(ruta, [" "]));
            Assert.Equal(ClientesNvv.PorDefecto, ClientesNvv.Leer(ruta));
        }
        finally
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }
}
