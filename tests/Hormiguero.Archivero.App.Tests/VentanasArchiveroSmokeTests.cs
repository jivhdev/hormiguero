using System.Threading;
using System.Windows;
using Archivero.Datos;
using Archivero.Servicios;
using Archivero.Vistas;
using Hormiguero.Diseno;
using Microsoft.Data.Sqlite;

namespace Archivero.Tests;

public sealed class VentanasArchiveroSmokeTests
{
    [Fact]
    public void Diccionario_SeFiltraPorCategoriaYDerivaEmitidoRecibido()
    {
        Assert.Equal("Siguiente", AsistenteClasificacionService.TextoBotonOtrosDatos(true));
        Assert.Equal("Saltar", AsistenteClasificacionService.TextoBotonOtrosDatos(false));
        Assert.Contains("Ventas propias", AsistenteClasificacionService.Categorias);
        Assert.Contains("Del cliente", AsistenteClasificacionService.Categorias);
        Assert.Contains("Compras propias", AsistenteClasificacionService.Categorias);
        Assert.Contains("Del proveedor", AsistenteClasificacionService.Categorias);
        Assert.Contains("Otros", AsistenteClasificacionService.Categorias);
        Assert.Contains(
            AsistenteClasificacionService.DocumentosDeCategoria("Del proveedor"),
            dato => dato.EtiquetaTipo == "Factura del proveedor"
        );
        Assert.DoesNotContain(
            AsistenteClasificacionService.DocumentosDeCategoria("Otros"),
            dato => Hormiguero.Nucleo.Datos.DiccionarioDatosEnlazantes.EsParte(dato.Id)
        );
        Assert.Equal("Emitido", AsistenteClasificacionService.GrupoDocumento("Ventas propias"));
        Assert.Equal("Emitido", AsistenteClasificacionService.GrupoDocumento("Compras propias"));
        Assert.Equal("Recibido", AsistenteClasificacionService.GrupoDocumento("Del cliente"));
        Assert.Equal("Recibido", AsistenteClasificacionService.GrupoDocumento("Del proveedor"));
        Assert.Equal(string.Empty, AsistenteClasificacionService.GrupoDocumento("Otros"));
        Assert.True(AsistenteClasificacionService.EsCompraPropia("oc_propia"));
        Assert.True(AsistenteClasificacionService.EsCompraPropia("OC propia"));
        Assert.False(AsistenteClasificacionService.EsCompraPropia("factura_proveedor"));
        Assert.False(AsistenteClasificacionService.EsCompraPropia("Factura del proveedor"));
        Assert.Contains(
            AsistenteClasificacionService.DatosProveedor,
            dato => dato.Id == "nombre_proveedor"
        );
        Assert.Contains(
            AsistenteClasificacionService.DatosProveedor,
            dato => dato.Id == "rut_proveedor"
        );
        Assert.NotNull(AsistenteClasificacionService.ValidarProveedorCompraPropia(false, false));
        Assert.Null(AsistenteClasificacionService.ValidarProveedorCompraPropia(true, false));
        Assert.Null(AsistenteClasificacionService.ValidarProveedorCompraPropia(false, true));
        Assert.True(
            AsistenteClasificacionService.TieneProveedorLegible([("nombre_proveedor", "Acme")])
        );
        Assert.False(AsistenteClasificacionService.TieneProveedorLegible([("rut_proveedor", " ")]));
    }

    [Fact]
    public void NombreEstandarYValidacionPorPaso_UsanTextosConcretos()
    {
        var id = Hormiguero
            .Nucleo.Datos.DiccionarioDatosEnlazantes.Todos.Single(d =>
                d.EtiquetaTipo == "Factura del proveedor"
            )
            .Id;
        Assert.Equal(
            "Factura del proveedor · Cobelcar",
            AsistenteClasificacionService.NombreEstandar(id, "Cobelcar")
        );
        Assert.Equal("Factura del proveedor", AsistenteClasificacionService.NombreDocumento(id));
        Assert.Equal(string.Empty, AsistenteClasificacionService.NombreEstandar(id, " "));
        Assert.NotNull(AsistenteClasificacionService.ValidarDocumento(" "));
        Assert.Null(AsistenteClasificacionService.ValidarDocumento("Factura del proveedor"));
        Assert.NotNull(AsistenteClasificacionService.ValidarEmisor("Cobelcar", false));
        Assert.Null(AsistenteClasificacionService.ValidarEmisor("Cobelcar", true));
        Assert.NotNull(AsistenteClasificacionService.ValidarNumero("", true, false));
        Assert.Null(AsistenteClasificacionService.ValidarNumero("0000025378", true, false));
        Assert.NotNull(AsistenteClasificacionService.ValidarNumero("", false, true));
        Assert.Null(AsistenteClasificacionService.ValidarNumero("", true, true));
    }

    [Fact]
    public void VentanasDeIdentificacionYConfiguracion_AbrenEnHiloSta()
    {
        var raiz = Path.Combine(Path.GetTempPath(), $"Archivero-ventanas-{Guid.NewGuid():N}");
        Directory.CreateDirectory(raiz);
        var rutaAnterior = BaseDeDatos.RutaArchivo;
        var rutaPdf = CreadorPdfDePrueba.Crear(raiz, "EMISOR", "TIPO");
        var rutaPdfOscuro = CreadorPdfDePrueba.Crear(raiz, "EMISOR", "TIPO OSCURO");
        Exception? error = null;
        BaseDeDatos.RutaArchivo = Path.Combine(raiz, "archivero.db");
        BaseDeDatos.AsegurarEsquema();

        var hilo = new Thread(() =>
        {
            try
            {
                var aplicacion = new Archivero.App();
                aplicacion.InitializeComponent();
                aplicacion.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                Tema.Aplicar(aplicacion, ModoTema.Claro);

                var asistenteCarpeta = new AsistenteCarpetaObservadaWindow();
                asistenteCarpeta.Width = 1366;
                asistenteCarpeta.Height = 768;
                asistenteCarpeta.Show();
                var mostrarPasoCarpeta = typeof(AsistenteCarpetaObservadaWindow).GetMethod(
                    "MostrarPaso",
                    System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic
                )!;
                var tituloCarpeta = (System.Windows.Controls.TextBlock)
                    asistenteCarpeta.FindName("TxtTitulo")!;
                for (var paso = 1; paso <= 6; paso++)
                {
                    mostrarPasoCarpeta.Invoke(asistenteCarpeta, [paso]);
                    asistenteCarpeta.UpdateLayout();
                    if (
                        !tituloCarpeta.Text.StartsWith(
                            $"Paso {paso} de 6",
                            StringComparison.Ordinal
                        )
                    )
                        throw new InvalidOperationException(
                            $"El asistente de carpeta no mostró el paso {paso}."
                        );
                }
                asistenteCarpeta.Close();

                var emisorDiseno = $"Emisor de prueba {Guid.NewGuid():N}";
                var tipoDiseno = $"Tipo de prueba {Guid.NewGuid():N}";
                var configuracionesObservador = new ConfiguracionDocumentoRepository();
                int idDiseno = configuracionesObservador.GuardarNueva(
                    emisorDiseno,
                    tipoDiseno,
                    raiz,
                    Archivero.Datos.FormatoCarpeta.Directo,
                    null,
                    false,
                    []
                );
                configuracionesObservador.MarcarSoloObservador(idDiseno);
                var entidadTexto = $"Empresa de prueba {Guid.NewGuid():N}";
                var entidadId = new EntidadRepository().ObtenerOCrear(
                    CategoriaEntidad.Emisor,
                    entidadTexto
                );
                var huellaAntes = System.Security.Cryptography.SHA256.HashData(
                    File.ReadAllBytes(rutaPdf)
                );
                var asistenteGuardado = new AsistenteCarpetaObservadaWindow();
                ((System.Windows.Controls.TextBox)asistenteGuardado.FindName("TxtNombre")!).Text =
                    "Carpeta de prueba";
                ((System.Windows.Controls.TextBox)asistenteGuardado.FindName("TxtRuta")!).Text =
                    raiz;
                (
                    (System.Windows.Controls.TextBox)
                        asistenteGuardado.FindName("TxtIdentificacion")!
                ).Text = "EMPRESA PRUEBA";
                (
                    (System.Windows.Controls.ComboBox)asistenteGuardado.FindName("ComboEntidad")!
                ).Text = entidadTexto;
                (
                    (System.Windows.Controls.CheckBox)asistenteGuardado.FindName("ChkCedibles")!
                ).IsChecked = true;
                (
                    (System.Windows.Controls.TextBox)
                        asistenteGuardado.FindName("TxtCedibleEsperado")!
                ).Text = "CEDIBLE";
                (
                    (System.Windows.Controls.ComboBox)asistenteGuardado.FindName("ComboAccion")!
                ).SelectedIndex = 4;
                var comboImpresora = (System.Windows.Controls.ComboBox)
                    asistenteGuardado.FindName("ComboImpresora")!;
                var impresoraElegida = $"Impresora de prueba {Guid.NewGuid():N}";
                var opcionImpresora = new System.Windows.Controls.ComboBoxItem
                {
                    Content = impresoraElegida,
                    Tag = impresoraElegida,
                };
                comboImpresora.Items.Add(opcionImpresora);
                comboImpresora.SelectedItem = opcionImpresora;
                typeof(AsistenteCarpetaObservadaWindow)
                    .GetField(
                        "_rutaEjemplo",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )!
                    .SetValue(asistenteGuardado, rutaPdf);
                typeof(AsistenteCarpetaObservadaWindow)
                    .GetField(
                        "_zonaIdentificacion",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )!
                    .SetValue(
                        asistenteGuardado,
                        new ZonaControlCarpeta(1, 0.1, 0.2, 0.3, 0.1, "EMPRESA PRUEBA")
                    );
                typeof(AsistenteCarpetaObservadaWindow)
                    .GetField(
                        "_zonaCedible",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )!
                    .SetValue(
                        asistenteGuardado,
                        new ZonaControlCarpeta(1, 0.4, 0.2, 0.2, 0.1, "CEDIBLE")
                    );
                (
                    (List<int>)
                        typeof(AsistenteCarpetaObservadaWindow)
                            .GetField(
                                "_configuraciones",
                                System.Reflection.BindingFlags.Instance
                                    | System.Reflection.BindingFlags.NonPublic
                            )!
                            .GetValue(asistenteGuardado)!
                ).Add(idDiseno);
                asistenteGuardado.Loaded += (_, _) =>
                    typeof(AsistenteCarpetaObservadaWindow)
                        .GetMethod(
                            "GuardarCarpeta",
                            System.Reflection.BindingFlags.Instance
                                | System.Reflection.BindingFlags.NonPublic
                        )!
                        .Invoke(asistenteGuardado, null);
                if (asistenteGuardado.ShowDialog() != true)
                    throw new InvalidOperationException(
                        "El asistente no completó el guardado de prueba."
                    );
                var carpetaGuardada = asistenteGuardado.CarpetaGuardada!;
                if (
                    carpetaGuardada.ModoReconocimiento != "Configuraciones"
                    || carpetaGuardada.ZonaIdentificacion?.TextoEsperado != "EMPRESA PRUEBA"
                    || carpetaGuardada.EntidadIdentificacionId != entidadId
                    || carpetaGuardada.ZonaCedible?.TextoEsperado != "CEDIBLE"
                    || carpetaGuardada.ConfiguracionesDocumentoIds?.Single() != idDiseno
                    || carpetaGuardada.AccionAlLlegar != "AvisarImprimirPrimeraPagina"
                    || carpetaGuardada.Impresora != impresoraElegida
                    || configuracionesObservador.ObtenerTodas().Any(c => c.Id == idDiseno)
                    || !configuracionesObservador
                        .ObtenerTodasConPatronesParaObservador()
                        .Any(c => c.Id == idDiseno)
                    || !huellaAntes.SequenceEqual(
                        System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(rutaPdf))
                    )
                    || !File.Exists(rutaPdf)
                )
                    throw new InvalidOperationException(
                        "El asistente no guardó todos los datos observados o alteró el PDF de ejemplo."
                    );

                var identificar = new IdentificarDocumentoWindow(rutaPdf);
                identificar.Width = 1366;
                identificar.Height = 768;
                identificar.Show();
                identificar.UpdateLayout();
                var tipoPaso = typeof(IdentificarDocumentoWindow).GetNestedType(
                    "Paso",
                    System.Reflection.BindingFlags.NonPublic
                )!;
                var mostrarPaso = typeof(IdentificarDocumentoWindow).GetMethod(
                    "MostrarPaso",
                    System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic
                )!;
                var identificarObservador = new IdentificarDocumentoWindow(rutaPdf, true, raiz);
                identificarObservador.Show();
                identificarObservador.UpdateLayout();
                mostrarPaso.Invoke(identificarObservador, [Enum.Parse(tipoPaso, "Resumen")]);
                identificarObservador.UpdateLayout();
                if (
                    (
                        (System.Windows.Controls.StackPanel)
                            identificarObservador.FindName("PanelCarpeta")!
                    ).Visibility != Visibility.Collapsed
                    || (
                        (System.Windows.Controls.Button)
                            identificarObservador.FindName("BtnCambiarGuardar")!
                    ).Visibility != Visibility.Collapsed
                )
                    throw new InvalidOperationException(
                        "El modo observador ofreció cambiar dónde se guarda."
                    );
                identificarObservador.Close();
                var titulo = (System.Windows.Controls.TextBlock)
                    identificar.FindName("TxtTituloPaso")!;
                mostrarPaso.Invoke(identificar, [Enum.Parse(tipoPaso, "OtrosDatos")]);
                identificar.UpdateLayout();
                var agregarCampoPropio = (System.Windows.Controls.Button)
                    identificar.FindName("BtnAgregarCampoPropio")!;
                agregarCampoPropio.RaiseEvent(
                    new System.Windows.RoutedEventArgs(
                        System.Windows.Controls.Primitives.ButtonBase.ClickEvent
                    )
                );
                if (
                    (
                        (System.Windows.Controls.ItemsControl)
                            identificar.FindName("ListaCamposPropiosEditor")!
                    )
                        .Items
                        .Count != 1
                )
                    throw new InvalidOperationException(
                        "El paso de otros datos no permitió agregar un campo propio."
                    );
                mostrarPaso.Invoke(identificar, [Enum.Parse(tipoPaso, "QueDocumento")]);
                var listaDocumentos = (System.Windows.Controls.ListBox)
                    identificar.FindName("ListaDocumentos")!;
                var categoria = (System.Windows.Controls.ComboBox)
                    identificar.FindName("CmbCategoriaDocumento")!;
                categoria.SelectedItem = "Ventas propias";
                listaDocumentos.SelectedItem = AsistenteClasificacionService
                    .DocumentosDeCategoria("Ventas propias")
                    .Single(d => d.Id == "factura_propia");
                identificar.UpdateLayout();
                var siguiente = (System.Windows.Controls.Button)
                    identificar.FindName("BtnSiguiente")!;
                siguiente.RaiseEvent(
                    new System.Windows.RoutedEventArgs(
                        System.Windows.Controls.Primitives.ButtonBase.ClickEvent
                    )
                );
                identificar.UpdateLayout();
                if (!titulo.Text.StartsWith("Paso 2 de 6", StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Elegir un documento y avanzar no llegó al paso del emisor."
                    );

                var emisor = (System.Windows.Controls.ComboBox)identificar.FindName("CmbEmisor")!;
                var emisorDePrueba = $"Emisor de prueba {Guid.NewGuid():N}";
                var eventoTexto = (System.Windows.Controls.TextChangedEventHandler)
                    Delegate.CreateDelegate(
                        typeof(System.Windows.Controls.TextChangedEventHandler),
                        identificar,
                        typeof(IdentificarDocumentoWindow).GetMethod(
                            "CmbEmisor_TextChanged",
                            System.Reflection.BindingFlags.Instance
                                | System.Reflection.BindingFlags.NonPublic
                        )!
                    );
                emisor.RemoveHandler(
                    System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                    eventoTexto
                );
                emisor.Text = emisorDePrueba;
                typeof(IdentificarDocumentoWindow)
                    .GetField(
                        "_emisor",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )!
                    .SetValue(identificar, emisorDePrueba);
                var marcas =
                    (Dictionary<CampoMarca, Marca>)
                        typeof(IdentificarDocumentoWindow)
                            .GetField(
                                "_marcas",
                                System.Reflection.BindingFlags.Instance
                                    | System.Reflection.BindingFlags.NonPublic
                            )!
                            .GetValue(identificar)!;
                marcas[CampoMarca.Emisor] = new(CampoMarca.Emisor, 0, 0.1, 0.1, 0.1, 0.05);
                var datos =
                    (System.Collections.ObjectModel.ObservableCollection<DatoEnlazanteEdicion>)
                        typeof(IdentificarDocumentoWindow)
                            .GetField(
                                "_datosEnlazantes",
                                System.Reflection.BindingFlags.Instance
                                    | System.Reflection.BindingFlags.NonPublic
                            )!
                            .GetValue(identificar)!;
                var datoNumero = datos.Single(d => d.DefineTipo);
                datoNumero.Incluido = true;
                datoNumero.Marcado = true;
                datoNumero.ValorLeido = "123";
                datoNumero.Pagina = 0;
                datoNumero.X = 0.1;
                datoNumero.Y = 0.1;
                datoNumero.Ancho = 0.1;
                datoNumero.Alto = 0.05;
                ((System.Windows.Controls.TextBox)identificar.FindName("TxtCarpetaDestino")!).Text =
                    raiz;
                (
                    (System.Windows.Controls.RadioButton)identificar.FindName("RbGuardarDirecto")!
                ).IsChecked = true;
                (
                    (System.Windows.Controls.RadioButton)identificar.FindName("RbMantenerNombre")!
                ).IsChecked = true;
                var pasos = new[] { "Numero", "OtrosDatos", "Guardar", "Resumen" };
                for (var indice = 0; indice < pasos.Length; indice++)
                {
                    mostrarPaso.Invoke(identificar, [Enum.Parse(tipoPaso, pasos[indice])]);
                    identificar.UpdateLayout();
                    if (
                        !titulo.Text.StartsWith($"Paso {indice + 3} de 6", StringComparison.Ordinal)
                    )
                        throw new InvalidOperationException(
                            $"No se mostró correctamente el paso {indice + 1}."
                        );
                }
                identificar.Close();

                var rutaPdfSubcarpetas = CreadorPdfDePrueba.CrearConLineas(
                    raiz,
                    "EMISOR",
                    "TIPO",
                    "06-10-2026"
                );
                var identificarSubcarpetas = new IdentificarDocumentoWindow(rutaPdfSubcarpetas)
                {
                    Width = 1366,
                    Height = 768,
                };
                var categoriaSubcarpetas = (System.Windows.Controls.ComboBox)
                    identificarSubcarpetas.FindName("CmbCategoriaDocumento")!;
                categoriaSubcarpetas.SelectedItem = "Ventas propias";
                var documentoSubcarpetas = (System.Windows.Controls.ListBox)
                    identificarSubcarpetas.FindName("ListaDocumentos")!;
                documentoSubcarpetas.SelectedItem = AsistenteClasificacionService
                    .DocumentosDeCategoria("Ventas propias")
                    .Single(d => d.Id == "factura_propia");
                string emisorSubcarpetas = $"Emisor de prueba {Guid.NewGuid():N}";
                (
                    (System.Windows.Controls.ComboBox)identificarSubcarpetas.FindName("CmbEmisor")!
                ).Text = emisorSubcarpetas;
                typeof(IdentificarDocumentoWindow)
                    .GetField(
                        "_emisor",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )!
                    .SetValue(identificarSubcarpetas, emisorSubcarpetas);
                var marcasSubcarpetas =
                    (Dictionary<CampoMarca, Marca>)
                        typeof(IdentificarDocumentoWindow)
                            .GetField(
                                "_marcas",
                                System.Reflection.BindingFlags.Instance
                                    | System.Reflection.BindingFlags.NonPublic
                            )!
                            .GetValue(identificarSubcarpetas)!;
                marcasSubcarpetas[CampoMarca.Emisor] = new(CampoMarca.Emisor, 0, 0, 0.02, 1, 0.15);
                marcasSubcarpetas[CampoMarca.Tipo] = new(CampoMarca.Tipo, 0, 0, 0.21, 1, 0.15);
                var datosSubcarpetas =
                    (System.Collections.ObjectModel.ObservableCollection<DatoEnlazanteEdicion>)
                        typeof(IdentificarDocumentoWindow)
                            .GetField(
                                "_datosEnlazantes",
                                System.Reflection.BindingFlags.Instance
                                    | System.Reflection.BindingFlags.NonPublic
                            )!
                            .GetValue(identificarSubcarpetas)!;
                var numeroSubcarpetas = datosSubcarpetas.Single(d => d.DefineTipo);
                numeroSubcarpetas.Incluido = true;
                numeroSubcarpetas.Marcado = true;
                numeroSubcarpetas.ValorLeido = "123";
                numeroSubcarpetas.Pagina = 0;
                numeroSubcarpetas.X = 0;
                numeroSubcarpetas.Y = 0.21;
                numeroSubcarpetas.Ancho = 1;
                numeroSubcarpetas.Alto = 0.15;
                marcasSubcarpetas[CampoMarca.Fecha] = new(
                    CampoMarca.Fecha,
                    0,
                    0,
                    0.40,
                    1,
                    0.15,
                    "06-10-2026"
                );
                (
                    (System.Windows.Controls.TextBox)
                        identificarSubcarpetas.FindName("TxtCarpetaDestino")!
                ).Text = raiz;
                (
                    (System.Windows.Controls.RadioButton)
                        identificarSubcarpetas.FindName("RbGuardarSubcarpetas")!
                ).IsChecked = true;
                (
                    (System.Windows.Controls.RadioButton)
                        identificarSubcarpetas.FindName("RbMantenerNombre")!
                ).IsChecked = true;
                mostrarPaso.Invoke(identificarSubcarpetas, [Enum.Parse(tipoPaso, "Guardar")]);
                identificarSubcarpetas.UpdateLayout();
                var controlOrganizacion = (System.Windows.Controls.UserControl)
                    identificarSubcarpetas.FindName("ControlOrganizacion")!;
                ((OrganizacionCarpetaControl)controlOrganizacion).Iniciar(
                    FormatoCarpeta.AnioMes,
                    "yyyy\\yyyyMM",
                    false
                );
                identificarSubcarpetas.UpdateLayout();
                var rutaEsperada = Path.Combine(
                    raiz,
                    "2026",
                    "202610",
                    Path.GetFileName(rutaPdfSubcarpetas)
                );
                var vistaRuta = (System.Windows.Controls.TextBlock)
                    identificarSubcarpetas.FindName("TxtPreviewActual")!;
                if (
                    !vistaRuta.Text.StartsWith("Se guardará en: ", StringComparison.Ordinal)
                    || !vistaRuta.Text.Contains(rutaEsperada, StringComparison.OrdinalIgnoreCase)
                )
                    throw new InvalidOperationException("La ruta final no se mostró destacada.");
                identificarSubcarpetas.Show();
                identificarSubcarpetas.UpdateLayout();
                var siguienteSubcarpetas = (System.Windows.Controls.Button)
                    identificarSubcarpetas.FindName("BtnSiguiente")!;
                siguienteSubcarpetas.RaiseEvent(
                    new System.Windows.RoutedEventArgs(
                        System.Windows.Controls.Primitives.ButtonBase.ClickEvent
                    )
                );
                var formatoElegido = (FormatoCarpeta)
                    typeof(IdentificarDocumentoWindow)
                        .GetField(
                            "_formato",
                            System.Reflection.BindingFlags.Instance
                                | System.Reflection.BindingFlags.NonPublic
                        )!
                        .GetValue(identificarSubcarpetas)!;
                var patronElegido = (string?)
                    typeof(IdentificarDocumentoWindow)
                        .GetField(
                            "_patronCarpeta",
                            System.Reflection.BindingFlags.Instance
                                | System.Reflection.BindingFlags.NonPublic
                        )!
                        .GetValue(identificarSubcarpetas);
                if (formatoElegido != FormatoCarpeta.AnioMes || patronElegido != "yyyy\\yyyyMM")
                    throw new InvalidOperationException(
                        "El asistente no conservó el formato y patrón elegidos."
                    );
                int idSubcarpetas = new ConfiguracionDocumentoRepository().GuardarNueva(
                    emisorSubcarpetas,
                    "Factura propia",
                    raiz,
                    formatoElegido,
                    patronElegido,
                    false,
                    marcasSubcarpetas.Values.ToList()
                );
                var configuracionSubcarpetas =
                    new ConfiguracionDocumentoRepository().BuscarPorEmisorYTipo(
                        emisorSubcarpetas,
                        "Factura propia"
                    )!;
                if (configuracionSubcarpetas.Id != idSubcarpetas)
                    throw new InvalidOperationException(
                        "La configuración guardada no coincide con el diseño del asistente."
                    );
                string rutaGuardada = ClasificadorService.Clasificar(
                    rutaPdfSubcarpetas,
                    configuracionSubcarpetas,
                    new DateTime(2026, 10, 6),
                    null
                );
                if (
                    configuracionSubcarpetas.FormatoCarpeta != FormatoCarpeta.AnioMes
                    || configuracionSubcarpetas.PatronCarpeta != "yyyy\\yyyyMM"
                    || rutaGuardada != rutaEsperada
                    || !File.Exists(rutaGuardada)
                )
                    throw new InvalidOperationException(
                        "Guardar en subcarpetas no conservó el patrón por año y mes o no guardó el PDF en el período."
                    );
                identificarSubcarpetas.Close();

                var sinTexto = new IdentificarSinTextoWindow(rutaPdf);
                sinTexto.Width = 1366;
                sinTexto.Height = 768;
                sinTexto.Show();
                sinTexto.UpdateLayout();
                sinTexto.Close();

                var configuracion = new AdministrarClasificacionesWindow();
                configuracion.Show();
                configuracion.UpdateLayout();
                configuracion.Close();

                var duplicado = new ResolverDuplicadoWindow(rutaPdf, rutaPdf);
                duplicado.Show();
                duplicado.UpdateLayout();
                duplicado.Close();

                var administrarAtajos = new AdministrarAtajosWindow();
                administrarAtajos.Show();
                administrarAtajos.UpdateLayout();
                administrarAtajos.Close();

                var guardarAtajo = new GuardarAtajoWindow(
                    "Acceso de prueba",
                    new AtajoGuardadoRapidoRepository()
                );
                guardarAtajo.Show();
                guardarAtajo.UpdateLayout();
                guardarAtajo.Close();

                Tema.Aplicar(aplicacion, ModoTema.Oscuro);
                var identificarOscuro = new IdentificarDocumentoWindow(rutaPdfOscuro);
                identificarOscuro.Width = 1366;
                identificarOscuro.Height = 768;
                identificarOscuro.Show();
                identificarOscuro.UpdateLayout();
                identificarOscuro.Close();

                aplicacion.Shutdown();
            }
            catch (Exception excepcion)
            {
                error = excepcion;
            }
        });
        hilo.SetApartmentState(ApartmentState.STA);
        using var bloqueoVentanas = new Mutex(false, @"Local\Hormiguero.PruebasVentanas");
        var bloqueoTomado = false;

        try
        {
            bloqueoTomado = bloqueoVentanas.WaitOne(TimeSpan.FromMinutes(5));
            Assert.True(bloqueoTomado, "Otra prueba de ventanas no terminó a tiempo.");
            hilo.Start();
            Assert.True(hilo.Join(TimeSpan.FromMinutes(5)), "Las ventanas no terminaron de abrir.");
            Assert.Null(error);
        }
        finally
        {
            BaseDeDatos.RutaArchivo = rutaAnterior;
            SqliteConnection.ClearAllPools();
            for (var intento = 0; ; intento++)
            {
                try
                {
                    Directory.Delete(raiz, recursive: true);
                    break;
                }
                catch (IOException) when (intento < 19)
                {
                    SqliteConnection.ClearAllPools();
                    Thread.Sleep(100);
                }
                catch (UnauthorizedAccessException) when (intento < 19)
                {
                    SqliteConnection.ClearAllPools();
                    Thread.Sleep(100);
                }
            }

            if (bloqueoTomado)
            {
                bloqueoVentanas.ReleaseMutex();
            }
        }
    }
}
