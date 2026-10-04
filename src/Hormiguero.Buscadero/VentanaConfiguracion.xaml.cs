using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Hormiguero.Buscadero.Logica;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Buscadero;

public sealed partial class VentanaConfiguracion : Window
{
    private readonly CarpetasConfiguradas carpetasConfiguradas;
    private readonly PreferenciaImpresion preferenciaImpresion;
    private readonly IReadOnlyList<string> carpetasIniciales;
    private List<string> impresorasInstaladas = [];

    public bool CarpetasCambiaron { get; private set; }

    public VentanaConfiguracion(SqliteConnection conexion)
    {
        InitializeComponent();

        carpetasConfiguradas = new CarpetasConfiguradas(conexion);
        preferenciaImpresion = new PreferenciaImpresion(conexion);
        carpetasIniciales = carpetasConfiguradas.Listar();

        var pantallaCarpetas = new PantallaCarpetas(carpetasConfiguradas);
        pantallaCarpetas.Listo += (_, _) => Close();
        Carpetas.Content = pantallaCarpetas;

        CargarImpresoras();
        CargarPreferenciaGuardada();
    }

    private void CargarImpresoras()
    {
        impresorasInstaladas = System
            .Drawing.Printing.PrinterSettings.InstalledPrinters.Cast<string>()
            .ToList();

        if (impresorasInstaladas.Count == 0)
        {
            Impresoras.ItemsSource = null;
            RbImpresoraFija.IsEnabled = false;
            return;
        }

        Impresoras.ItemsSource = impresorasInstaladas;
        RbImpresoraFija.IsEnabled = true;
    }

    private void CargarPreferenciaGuardada()
    {
        var modo = preferenciaImpresion.Modo;
        var impresoraGuardada = preferenciaImpresion.Impresora;

        switch (modo)
        {
            case ModoImpresion.Directa:
                RbDirecta.IsChecked = true;
                break;

            case ModoImpresion.ImpresoraFija:
                if (
                    !string.IsNullOrWhiteSpace(impresoraGuardada)
                    && impresorasInstaladas.Contains(impresoraGuardada)
                )
                {
                    RbImpresoraFija.IsChecked = true;
                    Impresoras.SelectedItem = impresoraGuardada;
                }
                else
                {
                    // La impresora guardada ya no existe
                    RbDirecta.IsChecked = true;
                }
                break;

            case ModoImpresion.CuadroDeWindows:
                RbCuadroWindows.IsChecked = true;
                break;
        }

        ActualizarEstadoComboBox();
    }

    private void ModoImpresion_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ActualizarEstadoComboBox();
        GuardarPreferencia();
    }

    private void ActualizarEstadoComboBox()
    {
        Impresoras.IsEnabled = RbImpresoraFija.IsChecked == true;
    }

    private void Impresoras_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        GuardarPreferencia();
    }

    private void GuardarPreferencia()
    {
        if (RbDirecta.IsChecked == true)
        {
            preferenciaImpresion.Guardar(ModoImpresion.Directa);
        }
        else if (RbImpresoraFija.IsChecked == true)
        {
            if (Impresoras.SelectedItem is string impresora)
            {
                preferenciaImpresion.Guardar(ModoImpresion.ImpresoraFija, impresora);
            }
            // Si no hay impresora seleccionada, no se guarda nada
        }
        else if (RbCuadroWindows.IsChecked == true)
        {
            preferenciaImpresion.Guardar(ModoImpresion.CuadroDeWindows);
        }
    }

    private void Cerrar_Click(object sender, RoutedEventArgs e) => Close();

    private void Ventana_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        var carpetasFinales = carpetasConfiguradas.Listar();
        CarpetasCambiaron = !carpetasIniciales.SequenceEqual(carpetasFinales);
    }
}
