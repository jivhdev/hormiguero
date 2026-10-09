using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Hormiguero.Mensajero.Core;

namespace Hormiguero.Mensajero.App;

public partial class VentanaCorreosProveedores : Window
{
    private readonly AlmacenMensajero almacen;
    private readonly ObservableCollection<CorreoProveedor> proveedores = [];

    public VentanaCorreosProveedores(AlmacenMensajero almacen)
    {
        InitializeComponent();
        this.almacen = almacen;
        ListaProveedores.ItemsSource = proveedores;
        ActualizarLista();
    }

    private void ListaProveedores_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListaProveedores.SelectedItem is CorreoProveedor proveedor)
        {
            CampoProveedor.Text = proveedor.Proveedor;
            CampoCorreos.Text = proveedor.Correos;
        }
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string correos =
                Hormiguero.Mensajero.Core.ClickFactura.CorreoFactura.NormalizarParaGuardar(
                    CampoCorreos.Text
                );
            if (string.IsNullOrWhiteSpace(CampoProveedor.Text))
                throw new FormatException("Ingresa el nombre del proveedor.");
            string? proveedorAnterior = (
                ListaProveedores.SelectedItem as CorreoProveedor
            )?.Proveedor;
            almacen.GuardarCorreosProveedor(CampoProveedor.Text, CampoCorreos.Text);
            if (
                proveedorAnterior is not null
                && AlmacenMensajero.NormalizarProveedor(proveedorAnterior)
                    != AlmacenMensajero.NormalizarProveedor(CampoProveedor.Text)
            )
                almacen.EliminarCorreosProveedor(proveedorAnterior);
            CampoCorreos.Text = correos;
            ActualizarLista();
            TextoAviso.Text = "Correos guardados.";
        }
        catch (Exception excepcion)
        {
            TextoAviso.Text = excepcion.Message;
        }
    }

    private void Quitar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaProveedores.SelectedItem is not CorreoProveedor proveedor)
        {
            TextoAviso.Text = "Selecciona un proveedor para quitar.";
            return;
        }

        almacen.EliminarCorreosProveedor(proveedor.Proveedor);
        ActualizarLista();
        CampoProveedor.Clear();
        CampoCorreos.Clear();
        TextoAviso.Text = "Proveedor quitado.";
    }

    private void Nuevo_Click(object sender, RoutedEventArgs e)
    {
        ListaProveedores.SelectedItem = null;
        CampoProveedor.Clear();
        CampoCorreos.Clear();
        CampoProveedor.Focus();
    }

    private void ActualizarLista()
    {
        proveedores.Clear();
        foreach (CorreoProveedor proveedor in almacen.LeerCorreosProveedores())
            proveedores.Add(proveedor);
    }
}
