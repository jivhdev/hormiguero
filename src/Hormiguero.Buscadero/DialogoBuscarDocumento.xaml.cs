using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Buscadero.Core.Busqueda;

namespace Buscadero.App;

public sealed class ResultadoBusquedaLista
{
    public required string Ruta { get; init; }
    public required string Etiqueta { get; init; }
}

public partial class DialogoBuscarDocumento : Window
{
    private readonly ServicioBusqueda _servicioBusqueda;

    public DialogoBuscarDocumento(ServicioBusqueda servicioBusqueda)
    {
        InitializeComponent();
        _servicioBusqueda = servicioBusqueda;
        ComboFiltroCarpeta.ItemsSource = _servicioBusqueda.ObtenerSugerenciasCarpeta();
        Loaded += (_, _) => TextoNumero.Focus();
    }

    public string? RutaSeleccionada { get; private set; }

    private async void BotonBuscar_Click(object sender, RoutedEventArgs e) => await BuscarAsync();

    private async void TextoNumero_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await BuscarAsync();
        }
    }

    private ModoBusqueda ModoSeleccionado() =>
        ComboModo.SelectedIndex switch
        {
            1 => ModoBusqueda.Exacto,
            2 => ModoBusqueda.Alfanumerico,
            3 => ModoBusqueda.SoloNumero,
            4 => ModoBusqueda.SoloLetras,
            _ => ModoBusqueda.Todos,
        };

    private AlcanceBusqueda AlcanceSeleccionado() =>
        ComboAlcance.SelectedIndex switch
        {
            1 => AlcanceBusqueda.TodasLasCarpetas,
            2 => AlcanceBusqueda.CarpetaEspecifica,
            _ => AlcanceBusqueda.PrimeraCoincidencia,
        };

    private void ComboAlcance_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboFiltroCarpeta is null)
        {
            return;
        }

        var esCarpetaEspecifica = AlcanceSeleccionado() == AlcanceBusqueda.CarpetaEspecifica;
        ComboFiltroCarpeta.IsEnabled = esCarpetaEspecifica;

        if (esCarpetaEspecifica)
        {
            // Caso-15: solo carpetas madre, nada de subcarpetas ni "recientes".
            ComboFiltroCarpeta.Text = string.Empty;
            ComboFiltroCarpeta.ItemsSource = _servicioBusqueda.ObtenerCarpetasMadre();
        }
    }

    private void ComboFiltroCarpeta_TextChanged(object sender, TextChangedEventArgs e)
    {
        var texto = ComboFiltroCarpeta.Text;

        if (ComboFiltroCarpeta.SelectedItem is string seleccionada && seleccionada == texto)
        {
            return;
        }

        if (AlcanceSeleccionado() == AlcanceBusqueda.CarpetaEspecifica)
        {
            // Caso-15: filtrado instantáneo en memoria, solo entre las carpetas madre.
            var coincidenciasMadre = _servicioBusqueda
                .ObtenerCarpetasMadre()
                .Where(ruta =>
                    string.IsNullOrEmpty(texto)
                    || ruta.Contains(texto, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();

            ComboFiltroCarpeta.ItemsSource = coincidenciasMadre;
            ComboFiltroCarpeta.Text = texto;
            ComboFiltroCarpeta.IsDropDownOpen = coincidenciasMadre.Count > 0;
            return;
        }

        if (texto.Length < 3)
        {
            return;
        }

        var coincidencias = _servicioBusqueda.BuscarCarpetasPorNombre(texto);
        if (coincidencias.Count == 0)
        {
            return;
        }

        ComboFiltroCarpeta.ItemsSource = coincidencias;
        ComboFiltroCarpeta.Text = texto;
        ComboFiltroCarpeta.IsDropDownOpen = true;
    }

    private async Task BuscarAsync()
    {
        var numero = TextoNumero.Text.Trim();
        if (string.IsNullOrWhiteSpace(numero))
        {
            TextoEstado.Text = "Ingrese un número de documento.";
            return;
        }

        BotonBuscar.IsEnabled = false;
        TextoEstado.Text = "Buscando...";
        try
        {
            // Caso-14: el filtro de carpeta solo tiene efecto con "Carpeta específica".
            var filtro =
                AlcanceSeleccionado() == AlcanceBusqueda.CarpetaEspecifica
                    ? ComboFiltroCarpeta.Text.Trim()
                    : string.Empty;
            var modo = ModoSeleccionado();
            var alcance = AlcanceSeleccionado();
            var resultados = await Task.Run(() =>
                _servicioBusqueda.Buscar(numero, filtro, modo, alcance)
            );
            Lista.ItemsSource = resultados
                .Select(r => new ResultadoBusquedaLista
                {
                    Ruta = r.Ruta,
                    Etiqueta = $"{r.Nombre}  —  {r.Carpeta}",
                })
                .ToList();
            TextoEstado.Text =
                resultados.Count == 0
                    ? "Sin coincidencias."
                    : $"{resultados.Count} coincidencias. Seleccione una y presione Vincular.";
        }
        catch (Exception excepcion)
        {
            TextoEstado.Text = $"Error al buscar: {excepcion.Message}";
        }
        finally
        {
            BotonBuscar.IsEnabled = true;
        }
    }

    private void Aceptar_Click(object sender, RoutedEventArgs e)
    {
        if (Lista.SelectedItem is not ResultadoBusquedaLista seleccionado)
        {
            TextoEstado.Text = "Seleccione un resultado.";
            return;
        }

        RutaSeleccionada = seleccionado.Ruta;
        DialogResult = true;
    }
}
