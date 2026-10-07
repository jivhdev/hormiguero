using System.ComponentModel;
using System.Runtime.CompilerServices;
using Archivero.Datos;
using Archivero.Servicios.Pdf;

namespace Archivero.Vistas;

public sealed class DatoEnlazanteEdicion : INotifyPropertyChanged
{
    private bool _incluido;
    private bool _marcado;
    private bool _enlazable = true;
    private bool _defineTipo;
    private string _estado = "No aparece en este diseño.";
    public string ValorLeido { get; set; } = string.Empty;

    public DatoEnlazanteEdicion(DatoEnlazanteConfigurado dato)
    {
        Id = dato.Id;
        Nombre = dato.Nombre;
        Grupo = dato.Grupo;
        Incluido = dato.Incluido;
        Marcado = dato.Marcado;
        Enlazable = dato.Enlazable;
        DefineTipo = dato.DefineTipo;
        Pagina = dato.Pagina;
        X = dato.X;
        Y = dato.Y;
        Ancho = dato.Ancho;
        Alto = dato.Alto;
        if (dato.Marcado)
            Estado = "Zona guardada.";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Id { get; }
    public string Nombre { get; }
    public string Grupo { get; }
    public string Etiqueta => $"{Grupo}: {Nombre}";
    public bool Incluido
    {
        get => _incluido;
        set
        {
            _incluido = value;
            AvisarCambio();
        }
    }
    public bool Marcado
    {
        get => _marcado;
        set
        {
            _marcado = value;
            AvisarCambio();
        }
    }
    public bool Enlazable
    {
        get => _enlazable;
        set
        {
            _enlazable = value;
            AvisarCambio();
        }
    }
    public bool DefineTipo
    {
        get => _defineTipo;
        set
        {
            _defineTipo = value;
            AvisarCambio();
        }
    }
    public string Estado
    {
        get => _estado;
        set
        {
            _estado = value;
            AvisarCambio();
        }
    }
    public int Pagina { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Ancho { get; set; }
    public double Alto { get; set; }

    public DatoEnlazanteConfigurado AConfigurado() =>
        new(
            Id,
            Nombre,
            Grupo,
            Incluido,
            Marcado,
            Pagina,
            X,
            Y,
            Ancho,
            Alto,
            Estado,
            Enlazable,
            DefineTipo
        );

    private void AvisarCambio([CallerMemberName] string? propiedad = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
}
