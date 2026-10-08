using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Hormiguero.Mensajero.Core;
using Hormiguero.Mensajero.Core.ClickFactura;
using Microsoft.Win32;

namespace Hormiguero.Mensajero.App;

public partial class VentanaClientesFactura : Window
{
    private readonly AlmacenMensajero almacen;
    private readonly List<ClienteEdicion> clientes = [];
    private readonly ObservableCollection<string> correos = [];
    private ClienteEdicion? actual;
    private bool cargando;

    public VentanaClientesFactura(AlmacenMensajero almacen)
    {
        InitializeComponent();
        this.almacen = almacen;
        ListaCorreos.ItemsSource = correos;
        Width = Math.Min(Width, SystemParameters.WorkArea.Width);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height);
        Recargar();
    }

    private void Recargar()
    {
        clientes.Clear();
        clientes.AddRange(
            almacen.LeerTodosClientesFactura().Select(cliente => new ClienteEdicion(cliente))
        );
        ActualizarLista();
    }

    private void ActualizarLista()
    {
        ClienteEdicion? seleccionado = actual;
        cargando = true;
        ListaClientes.ItemsSource = clientes
            .Where(cliente =>
                cliente.Rut.Contains(CampoBuscar.Text.Trim(), StringComparison.OrdinalIgnoreCase)
                || cliente.RazonSocial.Contains(
                    CampoBuscar.Text.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .ToArray();
        if (seleccionado is not null && ListaClientes.Items.Contains(seleccionado))
            ListaClientes.SelectedItem = seleccionado;
        cargando = false;
    }

    private void Buscar_TextChanged(object sender, TextChangedEventArgs e) => ActualizarLista();

    private void Cliente_Seleccionado(object sender, SelectionChangedEventArgs e)
    {
        if (cargando)
            return;
        CapturarActual();
        actual = ListaClientes.SelectedItem as ClienteEdicion;
        MostrarActual();
    }

    private void MostrarActual()
    {
        cargando = true;
        CampoRut.Text = actual?.Rut ?? "";
        CampoRazon.Text = actual?.RazonSocial ?? "";
        CampoActivo.IsChecked = actual?.Activo ?? true;
        correos.Clear();
        if (actual is not null)
            foreach (string correo in CorreoFactura.Separar(actual.Correo))
                correos.Add(correo);
        CampoCorreo.Clear();
        cargando = false;
    }

    private void CapturarActual()
    {
        if (actual is null || cargando)
            return;
        actual.Rut = RutFactura.NormalizarSeguro(CampoRut.Text);
        actual.RazonSocial = CampoRazon.Text.Trim();
        actual.Correo = string.Join("; ", correos);
        actual.Activo = CampoActivo.IsChecked == true;
        actual.Modificado = true;
    }

    private void AgregarCorreo_Click(object sender, RoutedEventArgs e)
    {
        string correo = CampoCorreo.Text.Trim();
        if (!CorreoFactura.EsValido(correo))
        {
            MostrarAviso("Correo no válido", "Ingresa un correo electrónico válido.", false);
            return;
        }
        if (correos.Contains(correo, StringComparer.OrdinalIgnoreCase))
        {
            MostrarAviso("Correo repetido", "Ese correo ya está en la lista.", false);
            return;
        }
        correos.Add(correo);
        CampoCorreo.Clear();
        CapturarActual();
    }

    private void QuitarCorreo_Click(object sender, RoutedEventArgs e)
    {
        if (ListaCorreos.SelectedItem is not string correo)
            return;
        correos.Remove(correo);
        CapturarActual();
    }

    private void NuevoCliente_Click(object sender, RoutedEventArgs e)
    {
        CapturarActual();
        actual = new ClienteEdicion
        {
            Activo = true,
            EsNuevo = true,
            Modificado = true,
        };
        clientes.Add(actual);
        CampoBuscar.Clear();
        ActualizarLista();
        ListaClientes.SelectedItem = actual;
        MostrarActual();
        CampoRut.Focus();
    }

    private void EliminarCliente_Click(object sender, RoutedEventArgs e)
    {
        CapturarActual();
        if (actual is null)
            return;
        if (
            MessageBox.Show(
                this,
                $"¿Eliminar al cliente {actual.RazonSocial} ({actual.Rut})?",
                "Eliminar cliente",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            ) != MessageBoxResult.Yes
        )
            return;
        try
        {
            if (!actual.EsNuevo)
                almacen.EliminarClienteFactura(actual.Rut, DateTime.Now);
            clientes.Remove(actual);
            actual = null;
            MostrarActual();
            ActualizarLista();
            TextoAviso.Text = "Cliente eliminado.";
        }
        catch (Exception excepcion)
        {
            MostrarAviso("Error", $"No se pudo eliminar: {excepcion.Message}", false);
        }
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        CapturarActual();
        try
        {
            foreach (ClienteEdicion cliente in clientes.Where(cliente => cliente.Modificado))
            {
                almacen.GuardarClienteFacturaGestion(
                    cliente.Rut,
                    cliente.RazonSocial,
                    CorreoFactura.Separar(cliente.Correo),
                    cliente.Activo,
                    DateTime.Now
                );
                cliente.Modificado = false;
                cliente.EsNuevo = false;
            }
            TextoAviso.Text = "Cambios guardados.";
            Recargar();
        }
        catch (Exception excepcion)
        {
            MensajeroLog.RegistrarError("Guardar clientes de Facturas", excepcion);
            MostrarAviso(
                "Error",
                $"No se pudieron guardar los cambios: {excepcion.Message}",
                false
            );
        }
    }

    private void ImportarClientesFactura_Click(object sender, RoutedEventArgs e)
    {
        if (almacen.LeerTodosClientesFactura().Count > 0)
        {
            MostrarAviso("Importar de ClickFactura", "Solo se importa en una lista vacía.", false);
            return;
        }
        string carpetaInicial = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClickFactura",
            "data"
        );
        var dialogo = new OpenFileDialog
        {
            Title = "Seleccionar base de datos de ClickFactura",
            Filter = "Base de datos de ClickFactura (clickfactura.db)|clickfactura.db",
            FileName = "clickfactura.db",
        };
        if (Directory.Exists(carpetaInicial))
            dialogo.InitialDirectory = carpetaInicial;
        if (dialogo.ShowDialog(this) != true)
            return;
        try
        {
            int cantidad = almacen.ImportarClientesFactura(dialogo.FileName);
            Recargar();
            MostrarAviso("Importar clientes", $"Se importaron {cantidad} clientes.", true);
        }
        catch (Exception excepcion)
        {
            MensajeroLog.RegistrarError("Importar clientes de ClickFactura", excepcion);
            MostrarAviso(
                "Error",
                $"No se pudieron importar los clientes: {excepcion.Message}",
                false
            );
        }
    }

    private void Cerrar_Click(object sender, RoutedEventArgs e) => Close();

    private void MostrarAviso(string titulo, string mensaje, bool exito)
    {
        if (!exito)
            MensajeroLog.Registrar("AVISO_ERROR", titulo);
        MessageBox.Show(
            this,
            mensaje,
            titulo,
            MessageBoxButton.OK,
            exito ? MessageBoxImage.Information : MessageBoxImage.Error
        );
    }

    private sealed class ClienteEdicion
    {
        public ClienteEdicion() { }

        public ClienteEdicion(ClienteFacturaGestion cliente)
        {
            Rut = cliente.Rut;
            RazonSocial = cliente.RazonSocial;
            Correo = cliente.Correo;
            Activo = cliente.Activo;
        }

        public string Rut { get; set; } = "";
        public string RazonSocial { get; set; } = "";
        public string Correo { get; set; } = "";
        public bool Activo { get; set; }
        public bool Modificado { get; set; }
        public bool EsNuevo { get; set; }

        public override string ToString() => $"{RazonSocial} ({Rut})";
    }
}
