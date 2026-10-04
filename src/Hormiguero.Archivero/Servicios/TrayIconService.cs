using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Archivero.Servicios;

/// <summary>
/// Icono de la bandeja del sistema (REQ-005): parpadea mientras haya algo pendiente por
/// reconocer, sin ventanas emergentes ni sonidos. Clic o "Abrir Archivero" vuelve a la
/// ventana principal; "Salir" es la unica forma de apagar Archivero de verdad.
/// </summary>
public class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Timer _timerParpadeo;
    private readonly Icon _iconoNormal;
    private readonly Icon _iconoAlerta;
    private bool _mostrandoAlerta;

    public event Action? MostrarVentanaSolicitado;
    public event Action? SalirSolicitado;

    public TrayIconService()
    {
        _iconoNormal = CrearIcono(Color.SteelBlue);
        _iconoAlerta = CrearIcono(Color.OrangeRed);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir Archivero", null, (_, _) => MostrarVentanaSolicitado?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => SalirSolicitado?.Invoke());

        _notifyIcon = new NotifyIcon
        {
            Icon = _iconoNormal,
            Text = "Archivero",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.Click += (_, _) => MostrarVentanaSolicitado?.Invoke();

        _timerParpadeo = new Timer { Interval = 800 };
        _timerParpadeo.Tick += (_, _) => AlternarIcono();
    }

    public void ActualizarPendientes(bool hayPendientes)
    {
        if (hayPendientes)
        {
            if (!_timerParpadeo.Enabled)
            {
                _timerParpadeo.Start();
            }
        }
        else
        {
            _timerParpadeo.Stop();
            _mostrandoAlerta = false;
            _notifyIcon.Icon = _iconoNormal;
        }
    }

    private void AlternarIcono()
    {
        _mostrandoAlerta = !_mostrandoAlerta;
        _notifyIcon.Icon = _mostrandoAlerta ? _iconoAlerta : _iconoNormal;
    }

    private static Icon CrearIcono(Color color)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pincel = new SolidBrush(color);
            g.FillEllipse(pincel, 2, 2, 28, 28);
            using var fuente = new Font("Segoe UI", 14, System.Drawing.FontStyle.Bold);
            using var pincelTexto = new SolidBrush(Color.White);
            g.DrawString("A", fuente, pincelTexto, new PointF(8, 5));
        }

        // Icon.FromHandle no destruye el handle nativo automaticamente, pero son solo dos
        // icons creados una vez y usados durante toda la vida del proceso: el sistema
        // operativo libera el handle al terminar el proceso.
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _timerParpadeo.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _iconoNormal.Dispose();
        _iconoAlerta.Dispose();
    }
}
