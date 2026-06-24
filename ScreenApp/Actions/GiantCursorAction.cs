using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ScreenApp.Effects;
using ScreenApp.Interop;

namespace ScreenApp.Actions;

/// <summary>
/// Огромный курсор (action_id = giant_cursor): прозрачный полноэкранный оверлей,
/// поверх которого нарисована большая стрелка-курсор, следящая за реальной мышью.
///
/// Окно click-through (WS_EX_TRANSPARENT) — клики проходят сквозь него к реальным
/// окнам, поэтому стример продолжает пользоваться мышью. Позиция реального курсора
/// читается через GetCursorPos на быстром таймере (~60 fps) и стрелка двигается за ним.
/// Исключено из захвата, чтобы capture-эффекты его не дублировали.
/// </summary>
public sealed class GiantCursorAction : IScreenAction
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(16);

    // Масштаб стрелки относительно «обычного» курсора (~20px) → ~10x.
    private const double Scale = 10.0;

    private Window? _window;
    private Canvas? _canvas;
    private Polygon? _arrow;
    private DispatcherTimer? _timer;
    private double _originX;
    private double _originY;
    private double _dpiX = 1.0;
    private double _dpiY = 1.0;

    public string ActionId => "giant_cursor";

    public void Execute()
    {
        if (_window is not null)
        {
            return;
        }

        var (left, top, width, height) = EffectTarget.GetDipBounds();
        _originX = left;
        _originY = top;

        // Классическая форма стрелки-курсора (точки в «единичных» координатах * Scale).
        _arrow = new Polygon
        {
            Fill = System.Windows.Media.Brushes.White,
            Stroke = System.Windows.Media.Brushes.Black,
            StrokeThickness = 2,
            Points = new PointCollection(new[]
            {
                new System.Windows.Point(0 * Scale, 0 * Scale),
                new System.Windows.Point(0 * Scale, 16 * Scale),
                new System.Windows.Point(4 * Scale, 12 * Scale),
                new System.Windows.Point(7 * Scale, 19 * Scale),
                new System.Windows.Point(9 * Scale, 18 * Scale),
                new System.Windows.Point(6 * Scale, 11 * Scale),
                new System.Windows.Point(11 * Scale, 11 * Scale),
            }),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 12, ShadowDepth = 4, Color = Colors.Black, Opacity = 0.6,
            },
        };

        _canvas = new Canvas();
        _canvas.Children.Add(_arrow);

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
            Left = _originX,
            Top = _originY,
            Width = width,
            Height = height,
        };

        _window.SourceInitialized += OnSourceInitialized;
        _window.Show();

        UpdatePosition();
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = Tick };
        _timer.Tick += (_, _) => UpdatePosition();
        _timer.Start();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
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

        // DPI окна — GetCursorPos отдаёт физические пиксели, WPF работает в DIP.
        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget is not null)
        {
            _dpiX = source.CompositionTarget.TransformToDevice.M11;
            _dpiY = source.CompositionTarget.TransformToDevice.M22;
        }

        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
        NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
    }

    private void UpdatePosition()
    {
        if (_arrow is null)
        {
            return;
        }

        if (!NativeMethods.GetCursorPos(out var p))
        {
            return;
        }

        // Физические пиксели → DIP, затем в координаты окна (с учётом начала вирт. экрана).
        double dipX = p.X / _dpiX;
        double dipY = p.Y / _dpiY;
        Canvas.SetLeft(_arrow, dipX - _originX);
        Canvas.SetTop(_arrow, dipY - _originY);
    }

    public void Revert()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer = null;
        }

        if (_window is not null)
        {
            _window.SourceInitialized -= OnSourceInitialized;
            _window.Close();
            _window = null;
        }

        _arrow = null;
        _canvas = null;
    }
}
