using System.Windows.Media.Imaging;
using DrawingIcon = System.Drawing.Icon;

namespace ScreenApp.UI;

/// <summary>
/// Доступ к иконке приложения (Resources/app.ico) в двух видах: <see cref="DrawingIcon"/>
/// для трей-иконки (WinForms NotifyIcon) и <see cref="BitmapImage"/> для окон WPF.
/// Иконка одна и та же везде — окно, трей, настройки, .exe.
/// </summary>
public static class AppIcon
{
    private const string ResourceUri = "pack://application:,,,/Resources/app.ico";

    /// <summary>Иконка для окон WPF (Window.Icon).</summary>
    public static BitmapImage WindowIcon { get; } = LoadBitmap();

    private static BitmapImage LoadBitmap()
    {
        var img = new BitmapImage();
        img.BeginInit();
        img.UriSource = new Uri(ResourceUri, UriKind.Absolute);
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.EndInit();
        img.Freeze();
        return img;
    }

    /// <summary>Иконка для трея (NotifyIcon.Icon). Загружается из ресурса.</summary>
    public static DrawingIcon LoadTrayIcon()
    {
        var stream = System.Windows.Application.GetResourceStream(
            new Uri(ResourceUri, UriKind.Absolute))?.Stream;

        return stream is not null
            ? new DrawingIcon(stream)
            : System.Drawing.SystemIcons.Application;
    }
}
