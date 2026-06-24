using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ScreenApp.Effects;
using ScreenApp.Interop;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ScreenApp.Actions;

/// <summary>
/// Базовое действие «текст поверх экрана»: прозрачное, topmost, click-through,
/// исключённое из захвата окно (чтобы capture-эффекты его не «съедали») с одним
/// TextBlock. Подклассы задают расположение/стиль (caption — по центру крупно,
/// subtitle — снизу). Текст приходит от зрителя через <see cref="ITextAction.SetText"/>.
/// </summary>
public abstract class TextOverlayAction : IScreenAction, ITextAction
{
    private Window? _window;
    private string _text = "";

    public abstract string ActionId { get; }

    /// <summary>Размер шрифта.</summary>
    protected abstract double FontSizePx { get; }

    /// <summary>Вертикальное выравнивание текста в окне.</summary>
    protected abstract VerticalAlignment VAlign { get; }

    public virtual void SetText(string? text) => _text = (text ?? "").Trim();

    /// <summary>Установить текст из самого действия (для фиксированных карточек).</summary>
    protected void SetCardText(string text) => _text = (text ?? "").Trim();

    public void Execute()
    {
        if (_window is not null || string.IsNullOrEmpty(_text))
        {
            // Без текста показывать нечего — тихо выходим (Revert будет no-op).
            return;
        }

        var textBlock = new TextBlock
        {
            Text = _text,
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = FontSizePx,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VAlign,
            Margin = new Thickness(60, 40, 60, 80),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 8, ShadowDepth = 0, Color = Colors.Black, Opacity = 0.9,
            },
        };

        var (left, top, width, height) = EffectTarget.GetDipBounds();
        _window = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            Content = textBlock,
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

        // Click-through + не активируется + исключено из захвата эффектов.
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
