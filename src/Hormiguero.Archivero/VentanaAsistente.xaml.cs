using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Hormiguero.Archivero.Logica;
using Hormiguero.Nucleo.Datos;
using Hormiguero.Nucleo.Pdf;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace Hormiguero.Archivero;

public sealed partial class VentanaAsistente : Window
{
    private readonly Identificaciones identificaciones;
    private readonly CarpetasEntrada entradas;
    private readonly InfoPdf info;
    private readonly string nombreOriginal;
    private readonly long id;
    private readonly Zona?[] zonas = new Zona?[4];
    private int paso;
    private bool preparada;

    public VentanaAsistente(
        SqliteConnection conexion,
        string rutaPdf,
        Identificacion? existente = null
    )
    {
        InitializeComponent();
        identificaciones = new(conexion);
        entradas = new(conexion);
        nombreOriginal = Path.GetFileName(rutaPdf);
        id = existente?.Id ?? 0;
        using var memoria = new MemoryStream();
        using (
            var archivo = new FileStream(
                rutaPdf,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete
            )
        )
        {
            archivo.CopyTo(memoria);
        }
        memoria.Position = 0;
        info = LectorPdf.Leer(memoria);
        Loaded += (_, _) =>
        {
            PreviewKeyDown += Atajo;
            if (!info.TieneTexto)
            {
                MessageBox.Show(this, "Este PDF no tiene texto: guárdalo a mano.");
                DialogResult = false;
            }
        };
        Unloaded += (_, _) => PreviewKeyDown -= Atajo;
        if (!info.TieneTexto)
        {
            return;
        }
        Visor.Mostrar(memoria.ToArray());
        NombreEmisor.ItemsSource = identificaciones
            .Listar()
            .Select(i => i.Emisor)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (existente is not null)
        {
            var configuracion = ConfiguracionArchivo.DeJson(existente.Datos);
            zonas[0] = configuracion.ZonaTipo;
            zonas[1] = configuracion.ZonaEmisor;
            zonas[2] = configuracion.ZonaNumero;
            zonas[3] = configuracion.ZonaFecha;
            NombreTipo.Text = existente.Tipo;
            NombreEmisor.Text = existente.Emisor;
            Carpeta.Text = configuracion.Destino.CarpetaMadre;
            AnioMes.IsChecked = configuracion.Destino.Forma == FormaCarpeta.AnioYMes;
            SoloAnio.IsChecked = configuracion.Destino.Forma == FormaCarpeta.SoloAnio;
            Directo.IsChecked = configuracion.Destino.Forma == FormaCarpeta.Directo;
            FechaDocumento.IsChecked = zonas[3] is not null;
            FechaHoy.IsChecked = zonas[3] is null;
            foreach (var (casilla, parte) in Partes())
            {
                casilla.IsChecked = configuracion.Destino.Partes.Contains(parte);
            }
            Separador.Text = configuracion.Destino.Separador;
        }
        Visor.ZonaMarcada += ZonaMarcada;
        preparada = true;
        Actualizar();
    }

    private (CheckBox Casilla, ParteNombre Parte)[] Partes() =>
        [
            (ParteTipo, ParteNombre.Tipo),
            (ParteEmisor, ParteNombre.Emisor),
            (ParteNumero, ParteNombre.Numero),
            (ParteFecha, ParteNombre.Fecha),
            (ParteOriginal, ParteNombre.NombreOriginal),
        ];

    private string Texto(int indice) => zonas[indice] is Zona zona ? ZonaPdf.Texto(info, zona) : "";

    private string Numero() => new(Texto(2).Where(char.IsDigit).ToArray());

    private DateOnly? FechaLeida() => Reconocedor.LeerFecha(Texto(3));

    private DateOnly FechaBase() =>
        FechaDocumento.IsChecked == true
            ? FechaLeida() ?? DateOnly.FromDateTime(DateTime.Today)
            : DateOnly.FromDateTime(DateTime.Today);

    private ReglaDestino Regla() =>
        new(
            Carpeta.Text,
            AnioMes.IsChecked == true ? FormaCarpeta.AnioYMes
                : SoloAnio.IsChecked == true ? FormaCarpeta.SoloAnio
                : FormaCarpeta.Directo,
            Partes().Where(p => p.Casilla.IsChecked == true).Select(p => p.Parte).ToArray(),
            Separador.Text
        );

    private void ZonaMarcada(object? sender, Zona zona)
    {
        if (paso > 3 || (paso == 3 && FechaDocumento.IsChecked != true))
        {
            return;
        }
        zonas[paso] = zona;
        if (paso == 1 && string.IsNullOrWhiteSpace(NombreEmisor.Text))
        {
            NombreEmisor.Text = Texto(1);
        }
        Aviso.Text =
            paso == 2 && Numero().Length == 0 ? "En esa zona no hay ningún número"
            : paso == 3 && FechaLeida() is null ? "No pude leer una fecha en esa zona"
            : "";
        Actualizar();
    }

    private void Actualizar()
    {
        if (!preparada)
        {
            return;
        }
        string[] nombres = ["Tipo", "Emisor", "Número", "Dónde se guarda", "Nombre"];
        TituloPaso.Text = $"Paso {paso + 1} de 5 · {nombres[paso]}";
        StackPanel[] paneles = [PasoTipo, PasoEmisor, PasoNumero, PasoDestino, PasoNombre];
        for (int indice = 0; indice < paneles.Length; indice++)
        {
            paneles[indice].Visibility = indice == paso ? Visibility.Visible : Visibility.Collapsed;
        }
        BtnAtras.IsEnabled = paso > 0;
        BtnSiguiente.Content = paso == 4 ? "Guardar" : "Siguiente";
        BtnSiguiente.IsEnabled = paso != 4 || Regla().Partes.Count > 0;
        TextoTipo.Text = $"Dice: {Texto(0)}";
        TextoEmisor.Text = $"Dice: {Texto(1)}";
        TextoNumero.Text = $"Número: {Numero()}";
        PanelFecha.Visibility =
            FechaDocumento.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        TextoFecha.Text = $"Fecha: {FechaLeida()?.ToString("dd-MM-yyyy")}";
        Visor.MostrarZonas(
            zonas
                .Select(
                    (zona, indice) =>
                        (Zona: zona, Etiqueta: indice == 3 ? "Fecha" : nombres[indice])
                )
                .Where(z => z.Zona is not null)
                .Select(z => (z.Zona!, z.Etiqueta))
        );
        var regla = Regla();
        DateOnly fecha = FechaBase();
        // El primer día permite mostrar meses vecinos incluso en los límites de DateOnly.
        DateOnly mes = new(fecha.Year, fecha.Month, 1);
        VistaCarpetas.Text = string.IsNullOrWhiteSpace(Carpeta.Text)
            ? ""
            : string.Join(
                Environment.NewLine,
                new[] { -1, 0, 1 }
                    .Where(desplazamiento =>
                        !(mes.Year == 1 && mes.Month == 1 && desplazamiento == -1)
                        && !(mes.Year == 9999 && mes.Month == 12 && desplazamiento == 1)
                    )
                    .Select(desplazamiento =>
                        DestinoArchivo.Carpeta(regla, mes.AddMonths(desplazamiento))
                    )
            );
        try
        {
            bool cedible =
                nombreOriginal.Contains("CEDIBLE", StringComparison.OrdinalIgnoreCase)
                || info.Palabras.Any(p =>
                    p.Texto.Equals("CEDIBLE", StringComparison.OrdinalIgnoreCase)
                );
            VistaNombre.Text =
                $"Nombre: {DestinoArchivo.Nombre(regla, new(NombreTipo.Text.Trim(), NombreEmisor.Text.Trim(), Numero(), nombreOriginal, fecha, cedible))}";
        }
        catch (InvalidOperationException)
        {
            VistaNombre.Text = "Nombre: ";
        }
    }

    private bool Validar(int indice)
    {
        Aviso.Text = indice switch
        {
            0 when string.IsNullOrWhiteSpace(Texto(0)) =>
                "Marca en el documento dónde dice qué tipo de documento es.",
            0 when string.IsNullOrWhiteSpace(NombreTipo.Text) =>
                "¿Cómo se llama este tipo de documento?",
            1 when string.IsNullOrWhiteSpace(Texto(1)) =>
                "Marca dónde aparece quién emite el documento (puede ser el RUT).",
            1 when string.IsNullOrWhiteSpace(NombreEmisor.Text) => "Nombre del emisor",
            2 when Numero().Length == 0 => "En esa zona no hay ningún número",
            3 when string.IsNullOrWhiteSpace(Carpeta.Text) => "Elegir carpeta",
            3 when entradas.ContieneA(Carpeta.Text) =>
                "Esa carpeta está dentro de una carpeta vigilada: elige otra",
            3 when FechaDocumento.IsChecked == true && FechaLeida() is null =>
                "No pude leer una fecha en esa zona",
            _ => "",
        };
        return Aviso.Text.Length == 0 && (indice != 4 || Regla().Partes.Count > 0);
    }

    private void Siguiente_Click(object sender, RoutedEventArgs e)
    {
        if (!preparada || !Validar(paso))
        {
            return;
        }
        if (paso < 4)
        {
            paso++;
            Actualizar();
            return;
        }
        for (int indice = 0; indice < 5; indice++)
        {
            if (!Validar(indice))
            {
                paso = indice;
                Actualizar();
                return;
            }
        }
        var configuracion = new ConfiguracionArchivo(
            zonas[0]!,
            Texto(0),
            zonas[1]!,
            Texto(1),
            zonas[2]!,
            FechaDocumento.IsChecked == true ? zonas[3] : null,
            Regla()
        );
        try
        {
            identificaciones.Guardar(
                new(id, NombreTipo.Text.Trim(), NombreEmisor.Text.Trim(), configuracion.AJson())
            );
            DialogResult = true;
        }
        catch (InvalidOperationException error)
        {
            Aviso.Text = error.Message;
        }
    }

    private void Atras_Click(object sender, RoutedEventArgs e)
    {
        if (paso > 0)
        {
            paso--;
            Aviso.Text = "";
            Actualizar();
        }
    }

    private void ElegirCarpeta_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFolderDialog();
        if (dialogo.ShowDialog(this) == true)
        {
            Carpeta.Text = dialogo.FolderName;
            Validar(3);
            Actualizar();
        }
    }

    private void Opciones_Click(object sender, RoutedEventArgs e)
    {
        Aviso.Text = "";
        Actualizar();
    }

    private void Separador_TextChanged(object sender, TextChangedEventArgs e) => Actualizar();

    private void Cancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Atajo(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.S)
        {
            Siguiente_Click(sender, e);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.A)
        {
            Atras_Click(sender, e);
            e.Handled = true;
        }
    }
}
