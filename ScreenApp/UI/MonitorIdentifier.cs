using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Screen = System.Windows.Forms.Screen;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ScreenApp.UI;

/// <summary>
/// «Определение мониторов»: на пару секунд показывает крупный номер по центру каждого
/// монитора, чтобы понять, какой из них №1, №2 и т.д. (соответствует списку выбора).
/// </summary>
public static class MonitorIdentifier
{
    public static void Flash(TimeSpan duration)
    {
        var windows = new List<Window>();
        var screens = Screen.AllScreens;

        double scale = PrimaryScale();

        for (int i = 0; i < screens.Length; i++)
        {
            var b = screens[i].Bounds;
            var win = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromArgb(170, 30, 24, 48)),
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = b.X / scale,
                Top = b.Y / scale,
                Width = b.Width / scale,
                Height = b.Height / scale,
                Content = new TextBlock
                {
                    Text = $"{i + 1}{(screens[i].Primary ? "\n(основной)" : "")}",
                    Foreground = Brushes.White,
                    FontSize = 200,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            win.Show();
            windows.Add(win);
        }

        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            foreach (var w in windows)
            {
                w.Close();
            }
        };
        timer.Start();
    }

    private static double PrimaryScale()
    {
        var primary = Screen.PrimaryScreen;
        if (primary is null || SystemParameters.PrimaryScreenWidth <= 0)
        {
            return 1.0;
        }
        return primary.Bounds.Width / SystemParameters.PrimaryScreenWidth;
    }
}
