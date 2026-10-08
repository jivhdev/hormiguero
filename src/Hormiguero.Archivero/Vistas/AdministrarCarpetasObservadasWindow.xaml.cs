using System.Windows;
using System.Windows.Controls;
using Archivero.Datos;

namespace Archivero.Vistas;

internal sealed record CarpetaObservadaFila(
    Guid Id,
    string Nombre,
    string Ruta,
    string Resumen,
    bool Activa
);

public partial class AdministrarCarpetasObservadasWindow : Window
{
    private readonly CarpetasObservadasRepository _repositorio = new();
    private List<CarpetaObservadaExterna> _carpetas;

    public AdministrarCarpetasObservadasWindow()
    {
        InitializeComponent();
        _carpetas = _repositorio.Leer().ToList();
        ActualizarLista();
    }

    private void ActualizarLista(Guid? seleccionar = null)
    {
        ListaCarpetas.ItemsSource = null;
        ListaCarpetas.ItemsSource = _carpetas
            .Select(c => new CarpetaObservadaFila(
                c.Id,
                c.Nombre,
                c.Ruta,
                CrearResumen(c),
                c.Activa
            ))
            .ToList();
        if (seleccionar is not null)
            ListaCarpetas.SelectedItem = ListaCarpetas
                .Items.Cast<CarpetaObservadaFila>()
                .FirstOrDefault(c => c.Id == seleccionar);
    }

    private static string CrearResumen(CarpetaObservadaExterna carpeta)
    {
        if (carpeta.ModoReconocimiento == "TipoPorCarpeta")
            return carpeta.Resumen;
        var cantidad = carpeta.ConfiguracionesDocumentoIds?.Count ?? 0;
        string identificacion = string.IsNullOrWhiteSpace(carpeta.IdentificacionEsperada)
            ? "identificación pendiente"
            : $"reconoce «{carpeta.IdentificacionEsperada}»";
        string accion = carpeta.AccionAlLlegar switch
        {
            "SoloRegistrar" => "solo registra",
            "ImprimirPrimeraPagina" => "imprime la primera página",
            "ImprimirTodo" => "imprime todo",
            "Avisar" => "avisa",
            "AvisarImprimirPrimeraPagina" => "avisa e imprime la primera página",
            _ => "acción sin definir",
        };
        return $"{identificacion} · {cantidad} diseño(s) · {accion}{(carpeta.SeguirPeriodo ? " · sigue el período" : "")}";
    }

    private void ListaCarpetas_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private void Agregar_Click(object sender, RoutedEventArgs e) => AbrirAsistente(null);

    private void Editar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaCarpetas.SelectedItem is CarpetaObservadaFila fila)
            AbrirAsistente(_carpetas.First(c => c.Id == fila.Id));
    }

    private void AbrirAsistente(CarpetaObservadaExterna? existente)
    {
        var asistente = new AsistenteCarpetaObservadaWindow(existente) { Owner = this };
        if (asistente.ShowDialog() != true || asistente.CarpetaGuardada is not { } guardada)
            return;
        int indice = _carpetas.FindIndex(c => c.Id == guardada.Id);
        if (indice < 0)
            _carpetas.Add(guardada);
        else
            _carpetas[indice] = guardada;
        _repositorio.Guardar(_carpetas);
        ActualizarLista(guardada.Id);
    }

    private void Activa_Changed(object sender, RoutedEventArgs e)
    {
        if (
            sender
            is not System.Windows.Controls.CheckBox
            {
                DataContext: CarpetaObservadaFila fila
            } control
        )
            return;
        int indice = _carpetas.FindIndex(c => c.Id == fila.Id);
        if (indice < 0)
            return;
        var actualizada = _carpetas[indice] with { Activa = control.IsChecked == true };
        _carpetas[indice] = actualizada;
        _repositorio.Guardar(_carpetas);
        ActualizarLista(actualizada.Id);
    }

    private void Quitar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaCarpetas.SelectedItem is not CarpetaObservadaFila fila)
            return;
        _carpetas.RemoveAll(c => c.Id == fila.Id);
        _repositorio.Guardar(_carpetas);
        ActualizarLista();
    }

    private void Listo_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
