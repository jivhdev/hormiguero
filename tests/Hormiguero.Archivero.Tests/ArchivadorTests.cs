using Hormiguero.Archivero.Logica;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Pdf;
using Microsoft.Data.Sqlite;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Hormiguero.Archivero.Tests;

public sealed class ArchivadorTests : IDisposable
{
    private static readonly DateOnly Hoy = new(2026, 10, 4);

    private readonly string raiz = Path.Combine(
        Path.GetTempPath(),
        "HormigueroArchivadorTests",
        Guid.NewGuid().ToString("N")
    );

    private string RutaBase => Path.Combine(raiz, "hormiguero.db");
    private string Entrada => Path.Combine(raiz, "entrada");
    private string Destino => Path.Combine(raiz, "documentos");
    private readonly List<string> papelera = [];

    public ArchivadorTests() => Directory.CreateDirectory(Path.Combine(raiz, "entrada"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(raiz, recursive: true);
    }

    // Una factura de prueba: tipo arriba, emisor al medio, número abajo.
    private static byte[] Factura(
        string numero,
        string emisor = "Proveedor Uno",
        string? extra = null
    )
    {
        using var constructor = new PdfDocumentBuilder();
        var fuente = constructor.AddStandard14Font(Standard14Font.Helvetica);
        var pagina = constructor.AddPage(595, 842);
        pagina.AddText("FACTURA ELECTRONICA", 14, new PdfPoint(50, 780), fuente);
        pagina.AddText(emisor, 12, new PdfPoint(50, 700), fuente);
        pagina.AddText("Numero " + numero, 12, new PdfPoint(50, 620), fuente);
        pagina.AddText(
            extra ?? "Documento sintetico para pruebas de Archivero",
            10,
            new PdfPoint(50, 400),
            fuente
        );
        return constructor.Build();
    }

    private static byte[] Escaneado()
    {
        using var constructor = new PdfDocumentBuilder();
        constructor.AddPage(595, 842);
        return constructor.Build();
    }

    private void Configurar(string emisor = "Proveedor Uno")
    {
        using SqliteConnection conexion = BaseComun.Abrir(RutaBase);
        var configuracion = new ConfiguracionArchivo(
            new Zona(1, 40, 770, 400, 30),
            "FACTURA ELECTRONICA",
            new Zona(1, 40, 690, 400, 30),
            emisor,
            new Zona(1, 40, 610, 400, 30),
            null,
            new ReglaDestino(Destino, FormaCarpeta.AnioYMes, [ParteNombre.Numero], "")
        );
        new Identificaciones(conexion).Guardar(
            new Identificacion(0, "Factura", emisor, configuracion.AJson())
        );
    }

    private Archivador Nuevo() =>
        new(RutaBase, [Entrada], () => Hoy, papelera.Add, TimeSpan.FromMilliseconds(200));

    private string Llega(string nombre, byte[] contenido)
    {
        string ruta = Path.Combine(Entrada, nombre);
        File.WriteAllBytes(ruta, contenido);
        return ruta;
    }

    private static async Task<bool> Esperar(Func<bool> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < limite)
        {
            if (condicion())
            {
                return true;
            }
            await Task.Delay(25);
        }
        return condicion();
    }

    private string Guardado(string nombre) => Path.Combine(Destino, "2026", "202610", nombre);

    [Fact]
    public async Task Reconocido_se_guarda_y_queda_en_la_auditoria()
    {
        Configurar();
        string llegada = Llega("scan_0001.pdf", Factura("000104523"));
        using Archivador archivador = Nuevo();

        archivador.Iniciar();

        Assert.True(await Esperar(() => File.Exists(Guardado("104523.pdf"))));
        Assert.True(await Esperar(() => archivador.Recientes.Count == 1));
        Assert.False(File.Exists(llegada));
        Assert.Equal("guardar", archivador.Recientes[0].Accion);
        Assert.Empty(archivador.Pendientes);
    }

    [Fact]
    public async Task Desconocido_espera_y_se_guarda_al_crear_su_configuracion()
    {
        Llega("scan_0002.pdf", Factura("555"));
        using Archivador archivador = Nuevo();
        archivador.Iniciar();
        Assert.True(
            await Esperar(() => archivador.Pendientes.Any(p => p.Bandeja == Bandeja.PorReconocer))
        );

        Configurar();
        archivador.ReconocerDeNuevo();

        Assert.True(await Esperar(() => File.Exists(Guardado("555.pdf"))));
        Assert.True(await Esperar(() => archivador.Pendientes.Count == 0));
    }

    [Fact]
    public async Task Escaneado_queda_para_guardar_a_mano()
    {
        Configurar();
        Llega("escaneo.pdf", Escaneado());
        using Archivador archivador = Nuevo();

        archivador.Iniciar();

        Assert.True(
            await Esperar(() => archivador.Pendientes.Any(p => p.Bandeja == Bandeja.SinTexto))
        );
    }

    [Fact]
    public async Task Repetido_queda_aparte_y_descartar_lo_manda_a_la_papelera()
    {
        Configurar();
        byte[] factura = Factura("777");
        Directory.CreateDirectory(Path.GetDirectoryName(Guardado("777.pdf"))!);
        File.WriteAllBytes(Guardado("777.pdf"), factura);
        string llegada = Llega("otra_vez.pdf", factura);
        using Archivador archivador = Nuevo();
        archivador.Iniciar();
        Assert.True(
            await Esperar(() => archivador.Pendientes.Any(p => p.Bandeja == Bandeja.YaGuardado))
        );

        await archivador.DescartarAsync(llegada);

        Assert.Equal([llegada], papelera);
        Assert.Empty(archivador.Pendientes);
        Assert.Equal("descartar", archivador.Recientes[0].Accion);
    }

    [Fact]
    public async Task Mismo_nombre_con_otro_contenido_nunca_se_sobrescribe()
    {
        Configurar();
        Directory.CreateDirectory(Path.GetDirectoryName(Guardado("888.pdf"))!);
        File.WriteAllBytes(Guardado("888.pdf"), Factura("888", extra: "version anterior"));
        string llegada = Llega("nueva.pdf", Factura("888", extra: "version nueva"));
        using Archivador archivador = Nuevo();

        archivador.Iniciar();

        Assert.True(
            await Esperar(() => archivador.Pendientes.Any(p => p.Bandeja == Bandeja.MismoNombre))
        );
        Assert.True(File.Exists(llegada));
    }

    [Fact]
    public async Task Cedible_se_guarda_junto_a_su_original()
    {
        Configurar();
        Llega("F999.pdf", Factura("999"));
        Llega("F999 cedible.pdf", Factura("999", extra: "CEDIBLE"));
        using Archivador archivador = Nuevo();

        archivador.Iniciar();

        Assert.True(await Esperar(() => File.Exists(Guardado("999.pdf"))));
        Assert.True(await Esperar(() => File.Exists(Guardado("999_CEDIBLE.pdf"))));
    }

    [Fact]
    public async Task Guardar_a_mano_mueve_y_registra()
    {
        string llegada = Llega("escaneo.pdf", Escaneado());
        using Archivador archivador = Nuevo();
        archivador.Iniciar();
        Assert.True(
            await Esperar(() => archivador.Pendientes.Any(p => p.Bandeja == Bandeja.SinTexto))
        );

        var traslado = await archivador.GuardarAManoAsync(
            llegada,
            Path.Combine(Destino, "a mano", "GD5521.pdf")
        );

        Assert.Equal(Hormiguero.Nucleo.Utilidades.ResultadoTraslado.Movido, traslado.Resultado);
        Assert.Empty(archivador.Pendientes);
        Assert.Equal("guardar a mano", archivador.Recientes[0].Accion);
    }

    [Fact]
    public async Task Llega_mientras_esta_abierto()
    {
        Configurar();
        using Archivador archivador = Nuevo();
        archivador.Iniciar();
        Assert.True(
            await Esperar(() =>
                archivador.Carpetas[Path.GetFullPath(Entrada)] == EstadoCarpeta.Vigilando
            )
        );

        Llega("recien.pdf", Factura("31337"));

        Assert.True(await Esperar(() => File.Exists(Guardado("31337.pdf"))));
    }
}
