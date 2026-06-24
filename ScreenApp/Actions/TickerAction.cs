using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ScreenApp.Effects;
using ScreenApp.Interop;

namespace ScreenApp.Actions;

/// <summary>
/// Бегущая строка (action_id = ticker): текст зрителя едет справа налево по полосе
/// внизу экрана. Окно прозрачное, topmost, click-through, исключено из захвата.
/// </summary>
public sealed class TickerAction : IScreenAction, ITextAction
{
    private Window? _window;
    private string _text = "";

    public string ActionId => "ticker";

    public void SetText(string? text) => _text = (text ?? "").Trim();

    public void Execute()
    {
        if (_window is not null || string.IsNullOrEmpty(_text))
        {
            return;
        }

        var (left, top, width, height) = EffectTarget.GetDipBounds();
        double screenW = width;

        var textBlock = new TextBlock
        {
            Text = _text,
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 48,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 8, ShadowDepth = 0, Color = Colors.Black, Opacity = 0.9,
            },
        };

        var canvas = new Canvas { Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 0, 0, 0)) };
        canvas.Children.Add(textBlock);
        Canvas.SetTop(textBlock, 10);
        Canvas.SetLeft(textBlock, screenW);

        var band = new Grid { Height = 80, VerticalAlignment = VerticalAlignment.Bottom, Children = { canvas } };

        _window = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            Content = band,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = left,
            Top = top,
            Width = screenW,
            Height = height,
        };

        _window.SourceInitialized += OnSourceInitialized;
        _window.Loaded += (_, _) =>
        {
            // Анимация прокрутки: справа за экран влево.
            var anim = new DoubleAnimation
            {
                From = screenW,
                To = -2000,
                Duration = new Duration(TimeSpan.FromSeconds(10)),
                RepeatBehavior = RepeatBehavior.Forever,
            };
            textBlock.BeginAnimation(Canvas.LeftProperty, anim);
        };
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
