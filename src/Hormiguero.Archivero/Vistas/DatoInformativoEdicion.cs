using System.ComponentModel;
using System.Runtime.CompilerServices;
using Hormiguero.Nucleo.Datos;

namespace Archivero.Vistas;

public sealed class DatoInformativoEdicion : INotifyPropertyChanged
{
    private bool _marcado;
    private string _estado = "No aparece en este diseño.";

    public DatoInformativoEdicion(string id, string nombre, ZonaInformativa? zona = null)
    {
        Id = id;
        Nombre = nombre;
        if (zona is not null)
        {
            Marcado = true;
            Pagina = zona.Pagina - 1;
            X = zona.X;
            Y = zona.Y;
            Ancho = zona.Ancho;
            Alto = zona.Alto;
            Estado = "Zona guardada.";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Id { get; }
    public string Nombre { get; }
    public bool Marcado
    {
        get => _marcado;
        set
        {
            _marcado = value;
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

    public ZonaInformativa ComoZona() => new(Id, Pagina + 1, X, Y, Ancho, Alto);

    private void AvisarCambio([CallerMemberName] string? nombre = null) =>
        PropertyChanged?.Invoke(this, new(nombre));
}
