using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ScreenApp.Effects;
using ScreenApp.Interop;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ScreenApp.Actions;

/// <summary>
/// Базовое позитивное действие «частицы поверх экрана»: прозрачное, topmost,
/// click-through, исключённое из захвата окно с Canvas, по которому летают эмодзи-частицы
/// (конфетти, сердечки, снег, шары, звёзды, монетки и т.п.).
///
/// Подкласс задаёт набор символов (<see cref="Glyphs"/>), направление и параметры.
/// Реализация лёгкая (эмодзи + DispatcherTimer ~60 fps), без внешних зависимостей.
/// </summary>
public abstract class ParticleOverlayAction : IScreenAction
{
    private static readonly Random Rng = new();
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(16);

    private Window? _window;
    private Canvas? _canvas;
    private DispatcherTimer? _timer;
    private readonly List<Particle> _particles = new();
    private double _w;
    private double _h;

    public abstract string ActionId { get; }

    /// <summary>Символы частиц (эмодзи).</summary>
    protected abstract string[] Glyphs { get; }

    /// <summary>Сколько частиц одновременно на экране.</summary>
    protected virtual int Count => 60;

    /// <summary>Размер символа (px), базовый.</summary>
    protected virtual double Size => 36;

    /// <summary>true — частицы падают сверху вниз; false — всплывают снизу вверх.</summary>
    protected virtual bool FallDown => true;

    public void Execute()
    {
        if (_window is not null)
        {
            return;
        }

        var (left, top, width, height) = EffectTarget.GetDipBounds();
        _w = width;
        _h = height;

        _canvas = new Canvas();
        _window = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            Content = _canvas,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = left,
            Top = top,
            Width = _w,
            Height = _h,
        };

        _window.SourceInitialized += OnSourceInitialized;
        _window.Show();

        for (int i = 0; i < Count; i++)
        {
            _particles.Add(SpawnParticle(initial: true));
        }

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = Tick };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private Particle SpawnParticle(bool initial)
    {
        var tb = new TextBlock
        {
            Text = Glyphs[Rng.Next(Glyphs.Length)],
            FontSize = Size * (0.7 + Rng.NextDouble() * 0.8),
        };
        _canvas!.Children.Add(tb);

        double x = Rng.NextDouble() * _w;
        double y = FallDown
            ? (initial ? Rng.NextDouble() * _h : -Size)
            : (initial ? Rng.NextDouble() * _h : _h + Size);

        var p = new Particle
        {
            Visual = tb,
            X = x,
            Y = y,
            SpeedY = (1.5 + Rng.NextDouble() * 3.0) * (FallDown ? 1 : -1),
            DriftX = (Rng.NextDouble() - 0.5) * 1.5,
            Rotation = Rng.NextDouble() * 360,
            RotSpeed = (Rng.NextDouble() - 0.5) * 8,
        };
        tb.RenderTransform = new RotateTransform(p.Rotation);
        Canvas.SetLeft(tb, p.X);
        Canvas.SetTop(tb, p.Y);
        return p;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        foreach (var p in _particles)
        {
            p.Y += p.SpeedY;
            p.X += p.DriftX;
            p.Rotation += p.RotSpeed;

            bool offScreen = FallDown ? p.Y > _h + Size : p.Y < -Size;
            if (offScreen)
            {
                // Респаун с противоположного края.
                p.X = Rng.NextDouble() * _w;
                p.Y = FallDown ? -Size : _h + Size;
            }

            Canvas.SetLeft(p.Visual, p.X);
            Canvas.SetTop(p.Visual, p.Y);
            ((RotateTransform)p.Visual.RenderTransform).Angle = p.Rotation;
        }
    }

    private static void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
        NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
    }

    public void Revert()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
            _timer = null;
        }

        _particles.Clear();

        if (_window is not null)
        {
            _window.SourceInitialized -= OnSourceInitialized;
            _window.Close();
            _window = null;
        }

        _canvas = null;
    }

    private sealed class Particle
    {
        public required TextBlock Visual { get; init; }
        public double X;
        public double Y;
        public double SpeedY;
        public double DriftX;
        public double Rotation;
        public double RotSpeed;
    }
}
