using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ScreenApp.Effects;
using ScreenApp.Interop;

namespace ScreenApp.Actions;

/// <summary>
/// Базовое действие «цветной фильтр поверх экрана»: полноэкранное полупрозрачное
/// окно заданного цвета. Click-through, не активируется. Лёгкая альтернатива
/// шейдерным фильтрам (grayscale/invert требуют пиксельных шейдеров; здесь —
/// простой и надёжный цветной тинт).
/// </summary>
public abstract class TintOverlayAction : IScreenAction
{
    private Window? _window;

    public abstract string ActionId { get; }

    /// <summary>Цвет тинта с альфой.</summary>
    protected abstract System.Windows.Media.Color TintColor { get; }

    public void Execute()
    {
        if (_window is not null)
        {
            return;
        }

        var (left, top, width, height) = EffectTarget.GetDipBounds();
        _window = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = new SolidColorBrush(TintColor),
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
        };

        _window.SourceInitialized += OnSourceInitialized;
        _window.Show();
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
        if (_window is null)
        {
            return;
        }

        _window.SourceInitialized -= OnSourceInitialized;
        _window.Close();
        _window = null;
    }
}

/// <summary>Красный фильтр (action_id = red_tint).</summary>
public sealed class RedTintAction : TintOverlayAction
{
    public override string ActionId => "red_tint";
    protected override System.Windows.Media.Color TintColor => System.Windows.Media.Color.FromArgb(110, 200, 0, 0);
}

/// <summary>Ночной режим — тёмно-синий фильтр (action_id = night).</summary>
public sealed class NightAction : TintOverlayAction
{
    public override string ActionId => "night";
    protected override System.Windows.Media.Color TintColor => System.Windows.Media.Color.FromArgb(150, 0, 0, 40);
}

/// <summary>Радужная мягкая подсветка экрана (action_id = rainbow).</summary>
public sealed class RainbowAction : TintOverlayAction
{
    public override string ActionId => "rainbow";
    // Полупрозрачная фиолетово-розовая дымка (приятная, не мешает).
    protected override System.Windows.Media.Color TintColor => System.Windows.Media.Color.FromArgb(70, 180, 80, 220);
}
