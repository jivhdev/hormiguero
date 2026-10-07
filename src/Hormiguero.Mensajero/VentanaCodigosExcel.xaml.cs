using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Hormiguero.Mensajero.Core;
using Microsoft.Win32;

namespace Hormiguero.Mensajero.App;

public partial class VentanaCodigosExcel : Window
{
    private readonly AlmacenMensajero almacen;
    private readonly Action<string, string> avisar;
    private VentanaCodigosFlotante? flotante;
    private string? rutaPdf;
    private int largo = CodigosProducto.LargoPredeterminado;
    public ObservableCollection<FilaCodigo> Codigos { get; } = [];

    public VentanaCodigosExcel(AlmacenMensajero almacen, Action<string, string> avisar)
    {
        InitializeComponent();
        this.almacen = almacen;
        this.avisar = avisar;
        DataContext = this;
        if (
            int.TryParse(almacen.LeerValor("codigos.largo"), out int guardado)
            && guardado is >= 1 and <= 20
        )
            largo = guardado;
        CampoLargo.Text = largo.ToString();
        ActualizarContador();
    }

    private void ElegirPdf_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog
        {
            Filter = "Archivos PDF (*.pdf)|*.pdf",
            Title = "Elegir PDF",
        };
        if (dialogo.ShowDialog(this) != true)
            return;
        try
        {
            using var archivo = File.OpenRead(dialogo.FileName);
            IReadOnlyList<CodigoProducto> encontrados = CodigosProducto.LeerPdf(archivo, largo);
            rutaPdf = dialogo.FileName;
            TextoPdf.Text = Path.GetFileName(rutaPdf);
            Codigos.Clear();
            foreach (CodigoProducto codigo in encontrados)
                AgregarCodigo(codigo);
            ActualizarContador();
            avisar(
                encontrados.Count == 0
                    ? "No se encontraron códigos en el PDF."
                    : $"Se encontraron {encontrados.Count} códigos.",
                encontrados.Count == 0 ? "warning" : "success"
            );
        }
        catch (Exception excepcion)
        {
            avisar($"No se pudo leer el PDF: {excepcion.Message}", "error");
        }
    }

    private void AplicarLargo_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(CampoLargo.Text, out int valor) || valor is < 1 or > 20)
        {
            avisar("El largo debe ser un número entre 1 y 20.", "error");
            return;
        }
        largo = valor;
        almacen.GuardarValor("codigos.largo", largo.ToString());
        if (rutaPdf is not null)
        {
            try
            {
                using var archivo = File.OpenRead(rutaPdf);
                IReadOnlyList<CodigoProducto> encontrados = CodigosProducto.LeerPdf(archivo, largo);
                Codigos.Clear();
                foreach (CodigoProducto codigo in encontrados)
                    AgregarCodigo(codigo);
                ActualizarContador();
            }
            catch (Exception excepcion)
            {
                avisar($"No se pudo volver a leer el PDF: {excepcion.Message}", "error");
                return;
            }
        }
        avisar("Largo del código guardado.", "success");
    }

    private void CopiarTodos_Click(object sender, RoutedEventArgs e)
    {
        Copiar(
            string.Join(
                Environment.NewLine,
                Codigos.Where(codigo => codigo.Incluido).Select(codigo => codigo.Codigo)
            ),
            "No hay códigos seleccionados."
        );
    }

    private void VentanaFlotante_Click(object sender, RoutedEventArgs e)
    {
        var seleccionados = Codigos
            .Where(codigo => codigo.Incluido)
            .Select(codigo => codigo.Codigo)
            .ToArray();
        if (seleccionados.Length == 0)
        {
            avisar("No hay códigos seleccionados.", "warning");
            return;
        }
        if (flotante is null)
        {
            flotante = new VentanaCodigosFlotante(seleccionados, CopiarUno) { Owner = this };
            flotante.Closed += (_, _) => flotante = null;
            flotante.Show();
        }
        else
        {
            flotante.Actualizar(seleccionados);
            flotante.Activate();
        }
    }

    private void CopiarUno(string codigo) => Copiar(codigo, "No se pudo copiar el código.");

    private void Copiar(string texto, string vacio)
    {
        if (string.IsNullOrEmpty(texto))
        {
            avisar(vacio, "warning");
            return;
        }
        try
        {
            Clipboard.SetText(texto);
            avisar("Código copiado al portapapeles.", "success");
        }
        catch (Exception excepcion)
        {
            avisar($"No se pudo acceder al portapapeles: {excepcion.Message}", "error");
        }
    }

    private void ActualizarContador() =>
        TextoContador.Text =
            $"{Codigos.Count(codigo => codigo.Incluido)} de {Codigos.Count} códigos";

    private void AgregarCodigo(CodigoProducto codigo)
    {
        var fila = new FilaCodigo(codigo.Codigo, codigo.Repetido);
        fila.PropertyChanged += (_, _) => ActualizarContador();
        Codigos.Add(fila);
    }

    public sealed class FilaCodigo : INotifyPropertyChanged
    {
        private bool incluido = true;
        public string Codigo { get; }
        public string EtiquetaRepetido =>
            Repetido ? Visibility.Visible.ToString() : Visibility.Collapsed.ToString();
        public bool Repetido { get; }
        public bool Incluido
        {
            get => incluido;
            set
            {
                incluido = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Incluido)));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;

        public FilaCodigo(string codigo, bool repetido) => (Codigo, Repetido) = (codigo, repetido);
    }
}
