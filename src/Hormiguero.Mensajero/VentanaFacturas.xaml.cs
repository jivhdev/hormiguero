using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Hormiguero.Mensajero.Core;
using Hormiguero.Mensajero.Core.ClickFactura;
using Microsoft.Win32;

namespace Hormiguero.Mensajero.App;

public partial class VentanaFacturas : Window
{
    private static readonly string[] Meses =
    [
        "Enero",
        "Febrero",
        "Marzo",
        "Abril",
        "Mayo",
        "Junio",
        "Julio",
        "Agosto",
        "Septiembre",
        "Octubre",
        "Noviembre",
        "Diciembre",
    ];

    private readonly AlmacenMensajero almacen;
    private readonly string baseTemporal;
    private readonly string baseDocumentos;
    private IReadOnlyList<DocumentoFactura> documentos = [];
    private IReadOnlyList<ClienteAnalizado> clientes = [];
    private readonly HashSet<string> clientesEnviados = new(StringComparer.Ordinal);
    private int indiceCliente;
    private string? rutaXls;
    private string descripcionSemana = "";
    private string? carpetaTemporalActual;

    public VentanaFacturas()
    {
        InitializeComponent();
        almacen = AlmacenMensajero.AbrirComun();
        string temporalGuardada = almacen.LeerValor("factura.carpeta_temporal");
        baseTemporal = string.IsNullOrWhiteSpace(temporalGuardada)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClickFactura",
                "temp_envios"
            )
            : temporalGuardada;
        almacen.GuardarValor("factura.carpeta_temporal", baseTemporal);
        string documentosGuardados = almacen.LeerValor("factura.carpeta_documentos");
        baseDocumentos = string.IsNullOrWhiteSpace(documentosGuardados)
            ? BuscadorPdfFactura.RutaBasePredeterminada
            : documentosGuardados;
        almacen.GuardarValor("factura.carpeta_documentos", baseDocumentos);

        SemanaUno.ItemsSource = SemanaDos.ItemsSource = Enumerable.Range(1, 5).ToArray();
        SemanaUno.SelectedIndex = SemanaDos.SelectedIndex = 0;
        MesUno.ItemsSource = MesDos.ItemsSource = Meses;
        MesUno.SelectedIndex = 6;
        MesDos.SelectedIndex = 7;
        int[] anios = Enumerable.Range(2020, 11).ToArray();
        AnioUno.ItemsSource = AnioDos.ItemsSource = anios;
        AnioUno.SelectedItem = AnioDos.SelectedItem = 2026;
        Height = Math.Min(Height, SystemParameters.WorkArea.Height);
        Width = Math.Min(Width, SystemParameters.WorkArea.Width);
        Closed += (_, _) => almacen.Dispose();
    }

    private void SegundaSemana_Changed(object sender, RoutedEventArgs e)
    {
        if (GrupoSegunda is not null && IncluyeSegunda is not null)
            GrupoSegunda.Visibility =
                IncluyeSegunda.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private string ConstruirDescripcion()
    {
        int semanaUno = (int)SemanaUno.SelectedItem!;
        int mesUno = MesUno.SelectedIndex + 1;
        int anioUno = (int)AnioUno.SelectedItem!;
        return PeriodoFactura.ConstruirDescripcion(
            semanaUno,
            mesUno,
            anioUno,
            IncluyeSegunda.IsChecked == true,
            (int)SemanaDos.SelectedItem!,
            MesDos.SelectedIndex + 1,
            (int)AnioDos.SelectedItem!
        );
    }

    private void VistaPrevia_Click(object sender, RoutedEventArgs e)
    {
        descripcionSemana = ConstruirDescripcion();
        TextoVistaPrevia.Text = $"Asunto: {descripcionSemana.ToUpperInvariant()}";
    }

    private void SiguientePeriodo_Click(object sender, RoutedEventArgs e)
    {
        descripcionSemana = ConstruirDescripcion();
        MostrarPagina(PaginaCarga);
    }

    private void AtrasPeriodo_Click(object sender, RoutedEventArgs e) =>
        MostrarPagina(PaginaPeriodo);

    private void SeleccionarXls_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Seleccionar archivo XLS",
            Filter = "Excel (*.xlsx *.xls)|*.xlsx;*.xls",
        };
        if (dialogo.ShowDialog(this) != true)
            return;
        rutaXls = dialogo.FileName;
        TextoArchivoXls.Text = rutaXls;
        BotonAnalizar.IsEnabled = true;
    }

    private void Analizar_Click(object sender, RoutedEventArgs e)
    {
        if (rutaXls is null)
            return;
        BotonAnalizar.IsEnabled = false;
        MostrarPagina(PaginaAnalisis);
        EstadoAnalisis.Text = "⏳ Analizando...";
        try
        {
            documentos = LectorExcelFactura.Leer(rutaXls);
            descripcionSemana = ConstruirDescripcion();
            var clientesPorRut = almacen
                .LeerClientesFactura()
                .ToDictionary(cliente => cliente.Rut, StringComparer.Ordinal);
            foreach (
                string rut in documentos
                    .Select(documento => RutFactura.Limpiar(documento.Entidad))
                    .Distinct(StringComparer.Ordinal)
            )
            {
                if (
                    !clientesPorRut.ContainsKey(rut)
                    && almacen.BuscarClienteFactura(rut) is { } cliente
                )
                    clientesPorRut[rut] = cliente;
            }
            ResultadoAnalisis resultado = AnalizadorFactura.Analizar(
                documentos,
                clientesPorRut,
                descripcionSemana,
                BuscarPdf
            );
            if (resultado.RutsNoRegistrados.Count > 0)
            {
                for (int indice = 0; indice < resultado.RutsNoRegistrados.Count; indice++)
                {
                    if (
                        !RegistrarCliente(
                            resultado.RutsNoRegistrados[indice],
                            indice + 1,
                            resultado.RutsNoRegistrados.Count
                        )
                    )
                    {
                        EstadoAnalisis.Text = "";
                        BotonAnalizar.IsEnabled = true;
                        return;
                    }
                }
                Analizar_Click(sender, e);
                return;
            }
            if (resultado.Error is not null)
            {
                EstadoAnalisis.Text = $"❌ Error: {resultado.Error}";
                MostrarAviso("Atención", resultado.Error, "warning");
                BotonAnalizar.IsEnabled = true;
                return;
            }
            clientes = resultado.ClientesProcesados;
            clientesEnviados.Clear();
            indiceCliente = 0;
            RefrescarTablaAnalisis();
            EstadoAnalisis.Text = "✅ Análisis completado";
            BotonIrEnvios.IsEnabled = true;
        }
        catch (Exception excepcion)
        {
            EstadoAnalisis.Text = $"❌ Error: {excepcion.Message}";
            MostrarAviso("Error", $"Error en el análisis:\n{excepcion.Message}", "error");
            BotonAnalizar.IsEnabled = true;
        }
    }

    private string? BuscarPdf(DocumentoFactura documento)
    {
        IReadOnlyList<(int Anio, int Mes)>? periodos = PeriodoFactura.ObtenerPeriodos(
            (int)AnioUno.SelectedItem!,
            MesUno.SelectedIndex + 1,
            BusquedaAmpliada.IsChecked == true,
            IncluyeSegunda.IsChecked == true,
            (int)AnioDos.SelectedItem!,
            MesDos.SelectedIndex + 1
        );
        if (periodos is null)
            return BuscadorPdfFactura.BuscarEnTodasLasCarpetas(
                baseDocumentos,
                documento.Tipo,
                documento.Numero
            );
        return BuscadorPdfFactura.BuscarEnPeriodos(
            baseDocumentos,
            documento.Tipo,
            documento.Numero,
            periodos
        );
    }

    private bool RegistrarCliente(string rut, int indice, int total)
    {
        var ventana = new Window
        {
            Title = $"Registrar Cliente {indice} de {total}",
            Width = 550,
            Height = 300,
            MinWidth = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
        };
        ventana.SetResourceReference(BackgroundProperty, "Hormiguero.Superficie");
        var contenido = new StackPanel { Margin = new Thickness(16) };
        contenido.Children.Add(
            new TextBlock
            {
                Text = $"📋 Cliente {indice} de {total}",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
            }
        );
        ((TextBlock)contenido.Children[^1]).SetResourceReference(
            TextBlock.ForegroundProperty,
            "Hormiguero.Texto"
        );
        var filaRut = new DockPanel { Margin = new Thickness(0, 12, 0, 8) };
        filaRut.Children.Add(
            new TextBlock
            {
                Text = "RUT:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
            }
        );
        string rutCorto = rut.Replace(".", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal);
        rutCorto = rutCorto.Length > 1 ? rutCorto[..^1] : rutCorto;
        filaRut.Children.Add(
            new TextBox
            {
                Text = rutCorto,
                IsReadOnly = true,
                FontSize = 18,
            }
        );
        var botonCopiarRut = new Button
        {
            Content = "📋 Copiar RUT",
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(8, 4, 8, 4),
        };
        botonCopiarRut.Click += (_, _) => CopiarTexto(rutCorto);
        DockPanel.SetDock(botonCopiarRut, Dock.Right);
        filaRut.Children.Add(botonCopiarRut);
        contenido.Children.Add(filaRut);
        contenido.Children.Add(new TextBlock { Text = "Razón Social:" });
        var panelRazon = CrearCampoConMarca("Pega aquí la razón social...", out TextBox razon);
        panelRazon.Margin = new Thickness(0, 2, 0, 8);
        contenido.Children.Add(panelRazon);
        contenido.Children.Add(new TextBlock { Text = "Correo electrónico:" });
        var panelCorreo = CrearCampoConMarca("facturas@empresa.cl", out TextBox correo);
        panelCorreo.Margin = new Thickness(0, 2, 0, 12);
        contenido.Children.Add(panelCorreo);
        var guardar = new Button
        {
            Content = "💾 Guardar y continuar",
            Padding = new Thickness(12, 7, 12, 7),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        guardar.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(razon.Text) || string.IsNullOrWhiteSpace(correo.Text))
            {
                MostrarAviso(
                    "Error",
                    "Razón Social y Correo son obligatorios.",
                    "warning",
                    ventana
                );
                return;
            }
            almacen.GuardarClienteFactura(rut, razon.Text.Trim(), correo.Text.Trim(), DateTime.Now);
            ventana.DialogResult = true;
        };
        contenido.Children.Add(guardar);
        ventana.Content = contenido;
        return ventana.ShowDialog() == true;
    }

    private static Grid CrearCampoConMarca(string marca, out TextBox campo)
    {
        var panel = new Grid();
        campo = new TextBox();
        TextBox entrada = campo;
        var textoMarca = new TextBlock
        {
            Text = marca,
            Margin = new Thickness(6, 2, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        textoMarca.SetResourceReference(TextBlock.ForegroundProperty, "Hormiguero.TextoSecundario");
        entrada.TextChanged += (_, _) =>
        {
            textoMarca.Visibility = string.IsNullOrEmpty(entrada.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        };
        panel.Children.Add(campo);
        panel.Children.Add(textoMarca);
        return panel;
    }

    private void RefrescarTablaAnalisis()
    {
        TablaAnalisis.ItemsSource = clientes
            .Select(cliente => new FilaAnalisis(
                cliente.Rut,
                cliente.RazonSocial,
                cliente.Correo,
                cliente.Documentos.Count,
                cliente.PdfsEncontrados.Count,
                clientesEnviados.Contains(cliente.Rut) ? "✅ Enviado" : "⏳ Pendiente"
            ))
            .ToArray();
    }

    private void IrEnvios_Click(object sender, RoutedEventArgs e)
    {
        indiceCliente = 0;
        CargarClienteActual();
        MostrarPagina(PaginaEnvio);
    }

    private void CargarClienteActual()
    {
        if (indiceCliente >= clientes.Count)
        {
            BotonPreparar.IsEnabled = false;
            BotonPendiente.IsEnabled = false;
            BotonEnviado.IsEnabled = false;
            MostrarAviso("Completado", "🎉 ¡Todos los clientes han sido procesados!", "success");
            return;
        }
        ClienteAnalizado cliente = clientes[indiceCliente];
        TextoClienteActual.Text =
            $"Cliente {indiceCliente + 1} de {clientes.Count}: {cliente.RazonSocial} ({cliente.Rut})\n✅ {clientesEnviados.Count} enviados | ⏳ {clientes.Count - clientesEnviados.Count} pendientes";
        TextoCorreo.Text = cliente.Correo;
        TablaDocumentos.ItemsSource = cliente
            .Documentos.Select(documento => new FilaDocumento(
                documento.Tipo,
                documento.Numero,
                documento.RutaPdf is null ? "❌ No encontrado" : Path.GetFileName(documento.RutaPdf)
            ))
            .ToArray();
        CampoAsunto.Text = cliente.Mensaje.Asunto;
        CampoCuerpo.Text = cliente.Mensaje.Cuerpo;
        BotonPreparar.IsEnabled = true;
        BotonPendiente.IsEnabled = true;
        BotonEnviado.IsEnabled = false;
    }

    private void PrepararEnvio_Click(object sender, RoutedEventArgs e)
    {
        if (indiceCliente >= clientes.Count)
            return;
        ClienteAnalizado cliente = clientes[indiceCliente];
        if (cliente.PdfsEncontrados.Count == 0)
        {
            if (
                !Preguntar(
                    "Sin PDFs",
                    "No hay PDFs para este cliente. ¿Desea marcarlo como enviado de todas formas?"
                )
            )
                return;
            BotonEnviado.IsEnabled = true;
            return;
        }
        try
        {
            carpetaTemporalActual = PreparadorEnvioFactura.CrearCarpetaTemporal(
                baseTemporal,
                cliente.Rut,
                DateTime.Now
            );
            IReadOnlyList<string> copiados = PreparadorEnvioFactura.CopiarPdfs(
                cliente
                    .PdfsEncontrados.Where(documento => documento.RutaPdf is not null)
                    .Select(documento => documento.RutaPdf!)
                    .ToArray(),
                carpetaTemporalActual
            );
            CopiarArchivos(copiados);
            MostrarAviso(
                "Listo",
                $"✅ {copiados.Count} archivos listos en:\n{carpetaTemporalActual}\n\nArrastre los archivos desde esa carpeta a su correo.",
                "success"
            );
            BotonEnviado.IsEnabled = true;
        }
        catch (Exception excepcion)
        {
            MostrarAviso("Error", $"Error al preparar envío:\n{excepcion.Message}", "error");
        }
    }

    private void Pendiente_Click(object sender, RoutedEventArgs e)
    {
        if (
            indiceCliente >= clientes.Count
            || !Preguntar(
                "Confirmar",
                "¿Desea dejar este cliente como pendiente y pasar al siguiente?"
            )
        )
            return;
        ClienteAnalizado cliente = clientes[indiceCliente];
        LimpiarTemporalActual();
        MostrarAviso(
            "Pendiente",
            $"⏳ {cliente.RazonSocial} quedó pendiente.\nPodrá volver a él desde la pantalla de análisis.",
            "success"
        );
        indiceCliente++;
        CargarClienteActual();
    }

    private void Enviado_Click(object sender, RoutedEventArgs e)
    {
        if (
            indiceCliente >= clientes.Count
            || !Preguntar("Confirmar", "¿Confirma que el correo fue enviado a este cliente?")
        )
            return;
        ClienteAnalizado cliente = clientes[indiceCliente];
        clientesEnviados.Add(cliente.Rut);
        RefrescarTablaAnalisis();
        LimpiarTemporalActual();
        MostrarAviso(
            "Enviado",
            $"✅ {cliente.RazonSocial} marcado como enviado.\n({clientesEnviados.Count} de {clientes.Count} clientes completados)",
            "success"
        );
        indiceCliente++;
        CargarClienteActual();
    }

    private void LimpiarTemporalActual()
    {
        if (carpetaTemporalActual is not null)
            PreparadorEnvioFactura.LimpiarCarpetaTemporal(carpetaTemporalActual);
        carpetaTemporalActual = null;
    }

    private void VolverAnalisis_Click(object sender, RoutedEventArgs e) =>
        MostrarPagina(PaginaAnalisis);

    private void AtrasCarga_Click(object sender, RoutedEventArgs e) => MostrarPagina(PaginaCarga);

    private void GestionarClientes_Click(object sender, RoutedEventArgs e)
    {
        var ventana = new VentanaClientesFactura(almacen) { Owner = this };
        ventana.ShowDialog();
    }

    private void CopiarCorreo_Click(object sender, RoutedEventArgs e) =>
        CopiarTexto(TextoCorreo.Text);

    private void CopiarAsunto_Click(object sender, RoutedEventArgs e) =>
        CopiarTexto(CampoAsunto.Text);

    private void CopiarCuerpo_Click(object sender, RoutedEventArgs e) =>
        CopiarTexto(CampoCuerpo.Text);

    private void Ventana_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;
        Key tecla = e.Key == Key.System ? e.SystemKey : e.Key;
        switch (tecla)
        {
            case Key.D1:
            case Key.NumPad1:
                CopiarTexto(TextoCorreo.Text);
                e.Handled = true;
                break;
            case Key.D2:
            case Key.NumPad2:
                CopiarTexto(CampoAsunto.Text);
                e.Handled = true;
                break;
            case Key.D3:
            case Key.NumPad3:
                CopiarTexto(CampoCuerpo.Text);
                e.Handled = true;
                break;
        }
    }

    private void CopiarTexto(string texto)
    {
        if (string.IsNullOrEmpty(texto))
            return;
        for (int intento = 0; intento < 3; intento++)
        {
            try
            {
                Clipboard.SetText(texto);
                return;
            }
            catch (ExternalException) when (intento < 2)
            {
                Thread.Sleep(50);
            }
            catch (Exception excepcion)
            {
                MostrarAviso("Error", $"❌ Error al copiar: {excepcion.Message}", "error");
                return;
            }
        }
        MostrarAviso("Error", "❌ No se pudo acceder al portapapeles", "error");
    }

    private void CopiarArchivos(IReadOnlyList<string> rutas)
    {
        var lista = new StringCollection();
        lista.AddRange(rutas.ToArray());
        for (int intento = 0; intento < 3; intento++)
        {
            try
            {
                Clipboard.SetFileDropList(lista);
                return;
            }
            catch (ExternalException) when (intento < 2)
            {
                Thread.Sleep(50);
            }
            catch (Exception excepcion)
            {
                throw new IOException($"❌ Error al copiar: {excepcion.Message}", excepcion);
            }
        }
        throw new IOException("❌ No se pudo acceder al portapapeles");
    }

    private void MostrarPagina(UIElement pagina)
    {
        foreach (
            var elemento in new[]
            {
                "PaginaPeriodo",
                "PaginaCarga",
                "PaginaAnalisis",
                "PaginaEnvio",
            }
        )
        {
            if (FindName(elemento) is UIElement actual)
                actual.Visibility = ReferenceEquals(actual, pagina)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
    }

    private bool Preguntar(string titulo, string mensaje) =>
        MostrarDialogo(titulo, mensaje, "warning", true, this);

    private static void MostrarAviso(
        string titulo,
        string mensaje,
        string tipo,
        Window? owner = null
    ) => MostrarDialogo(titulo, mensaje, tipo, false, owner);

    private static bool MostrarDialogo(
        string titulo,
        string mensaje,
        string tipo,
        bool pregunta,
        Window? owner
    )
    {
        var ventana = new Window
        {
            Title = titulo,
            Width = 430,
            SizeToContent = SizeToContent.Height,
            MinHeight = 170,
            WindowStartupLocation = owner is null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner,
            Owner = owner,
            Background = owner?.TryFindResource("Hormiguero.Superficie") as Brush,
            Foreground =
                owner?.TryFindResource(
                    tipo switch
                    {
                        "success" => "Hormiguero.Exito",
                        "warning" => "Hormiguero.Aviso",
                        "error" => "Hormiguero.Error",
                        _ => "Hormiguero.Texto",
                    }
                ) as Brush,
        };
        if (owner is not null)
        {
            ventana.SetResourceReference(BackgroundProperty, "Hormiguero.Superficie");
            ventana.SetResourceReference(
                ForegroundProperty,
                tipo switch
                {
                    "success" => "Hormiguero.Exito",
                    "warning" => "Hormiguero.Aviso",
                    "error" => "Hormiguero.Error",
                    _ => "Hormiguero.Texto",
                }
            );
        }
        var panel = new DockPanel { Margin = new Thickness(18) };
        var botones = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        bool respuesta = false;
        var aceptar = new Button
        {
            Content = pregunta ? "Sí" : "Aceptar",
            IsDefault = true,
            MinWidth = 78,
            Margin = new Thickness(4, 12, 0, 0),
            Padding = new Thickness(10, 5, 10, 5),
        };
        aceptar.Click += (_, _) =>
        {
            respuesta = true;
            ventana.DialogResult = true;
        };
        botones.Children.Add(aceptar);
        if (pregunta)
        {
            var cancelar = new Button
            {
                Content = "No",
                IsCancel = true,
                MinWidth = 78,
                Margin = new Thickness(8, 12, 0, 0),
                Padding = new Thickness(10, 5, 10, 5),
            };
            botones.Children.Add(cancelar);
        }
        DockPanel.SetDock(botones, Dock.Bottom);
        panel.Children.Add(botones);
        var texto = new TextBlock
        {
            Text = mensaje,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (owner is not null)
            texto.SetResourceReference(
                TextBlock.ForegroundProperty,
                tipo switch
                {
                    "success" => "Hormiguero.Exito",
                    "warning" => "Hormiguero.Aviso",
                    "error" => "Hormiguero.Error",
                    _ => "Hormiguero.Texto",
                }
            );
        panel.Children.Add(texto);
        ventana.Content = panel;
        ventana.ShowDialog();
        return respuesta;
    }

    private sealed record FilaAnalisis(
        string Rut,
        string RazonSocial,
        string Correo,
        int CantidadDocumentos,
        int CantidadEncontrados,
        string Estado
    );

    private sealed record FilaDocumento(string Tipo, string Numero, string Archivo);
}
