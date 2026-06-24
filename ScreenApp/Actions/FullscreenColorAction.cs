using System.Windows;
using System.Windows.Media;
using ScreenApp.Effects;
using WpfBrush = System.Windows.Media.Brush;

namespace ScreenApp.Actions;

/// <summary>
/// Базовое действие «сплошной полноэкранный цвет»: borderless topmost-окно во весь
/// виртуальный экран, закрашенное заданным цветом. Используется белым и чёрным экраном.
///
/// Окно создаётся в Execute и закрывается в Revert. Оба метода должны вызываться
/// в UI-потоке (это обеспечивает ActiveEffectManager).
/// </summary>
public abstract class FullscreenColorAction : IScreenAction
{
    private Window? _window;

    public abstract string ActionId { get; }

    /// <summary>Цвет заливки окна.</summary>
    protected abstract WpfBrush Fill { get; }

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
            AllowsTransparency = false,
            Background = Fill,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            // Покрываем только выбранный монитор.
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
        };

        _window.Show();
    }

    public void Revert()
    {
        if (_window is null)
        {
            return;
        }

        _window.Close();
        _window = null;
    }
}
