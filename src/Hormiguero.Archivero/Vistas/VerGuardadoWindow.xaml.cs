using System.Diagnostics;
using System.IO;
using System.Windows;
using Archivero.Datos;
using Archivero.Servicios;

namespace Archivero.Vistas;

/// <summary>
/// Vista simple de un documento ya guardado (Caso-1, punto 4): solo dónde quedó, con dos
/// botones — abrir su ubicación, o editar la configuración usando ESE MISMO archivo.
/// </summary>
public partial class VerGuardadoWindow : Window
{
    private readonly string _rutaArchivo;
    private readonly ConfiguracionDocumentoRepository _configuraciones = new();

    /// <summary>Caso-11, punto 3: si se editó la configuración, la ventana principal reprocesa los pendientes al cerrar.</summary>
    public bool ConfiguracionEditada { get; private set; }

    public VerGuardadoWindow(string rutaArchivo)
    {
        InitializeComponent();
        _rutaArchivo = rutaArchivo;
        TxtRuta.Text = rutaArchivo;
    }

    private void BtnAbrirUbicacion_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(_rutaArchivo))
        {
            System.Windows.MessageBox.Show(this, "El archivo ya no está en esa ubicación (se movió o se borró por fuera de Archivero).",
                "Archivero", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Process.Start("explorer.exe", $"/select,\"{_rutaArchivo}\"");
    }

    private void BtnEditarConfiguracion_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(_rutaArchivo))
        {
            System.Windows.MessageBox.Show(this, "El archivo ya no está en esa ubicación (se movió o se borró por fuera de Archivero).",
                "Archivero", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var configuraciones = _configuraciones.ObtenerTodasConPatrones();
        var coincidencia = CoincidenciaAutomaticaService.BuscarConfiguracionQueCoincide(_rutaArchivo, configuraciones);

        if (coincidencia is null)
        {
            System.Windows.MessageBox.Show(this,
                "No se pudo volver a reconocer este documento contra ninguna configuración guardada. " +
                "Probá editarlo desde \"Administrar clasificaciones\".",
                "Archivero", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var owner = Owner;
        var asistente = new IdentificarDocumentoWindow(_rutaArchivo, coincidencia, coincidencia.Patrones.Single(), comenzarEnPasoCarpeta: true) { Owner = owner };
        Close();
        ConfiguracionEditada = asistente.ShowDialog() == true;
    }
}
