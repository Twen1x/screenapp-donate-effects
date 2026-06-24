using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ScreenApp.Effects;
using ScreenApp.Interop;

namespace ScreenApp.Actions;

/// <summary>
/// Частичное затемнение экрана (~50%): полноэкранное полупрозрачное чёрное окно.
///
/// === Фикс «экран тух на 100%» ===
/// Ранее использовалось AllowsTransparency=false + SetLayeredWindowAttributes(LWA_ALPHA).
/// На WPF-окне этот layered-альфа-путь ненадёжен: альфа не применялась и оставался
/// сплошной чёрный экран. Теперь используем штатный WPF-путь — AllowsTransparency=true
/// и полупрозрачную кисть (#80000000 ≈ 50%), что даёт корректное затемнение.
///
/// Окно «сквозное» для кликов (WS_EX_TRANSPARENT), чтобы стример продолжал работать
/// под затемнением. Несколько экземпляров накладываются независимо (R4.5).
/// </summary>
public sealed class DimAction : IScreenAction
{
    /// <summary>Непрозрачность затемнения (0..1). 0.5 ≈ 50%.</summary>
    private const double DimOpacity = 0.5;

    private Window? _window;

    public string ActionId => "dim_50";

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
            // Полупрозрачная чёрная заливка: альфа = DimOpacity * 255 ≈ 0x80.
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)(DimOpacity * 255), 0, 0, 0)),
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

        // Делаем окно «сквозным» для мыши и не активируемым.
        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
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
