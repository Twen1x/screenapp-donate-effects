using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using Clipboard = System.Windows.Clipboard;

namespace ScreenApp.UI;

/// <summary>
/// Окно «Поддержать»: ссылка на донат-площадку и криптокошелёк (USDT TRC20).
/// QR-код берётся из встроенного ресурса Resources/support-qr.png, либо из файла
/// support-qr.png рядом с .exe; если его нет — показывается подсказка.
/// </summary>
public partial class SupportWindow : Window
{
    private const string DonateUrl = "https://donatex.gg/donate/Twenix";
    private const string UsdtTrc20Address = "TBAFE73JqAUMVCeDKm2mXdC84PHYKn71Lx";

    public SupportWindow()
    {
        InitializeComponent();
        Icon = AppIcon.WindowIcon;

        DonateLinkBox.Text = DonateUrl;
        CryptoBox.Text = UsdtTrc20Address;

        LoadQr();
    }

    private void LoadQr()
    {
        var qr = TryLoadQr();
        if (qr is not null)
        {
            QrImage.Source = qr;
        }
        else
        {
            QrImage.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Встроенный ресурс Resources/support-qr.png.</summary>
    private static BitmapImage? TryLoadQr()
    {
        try
        {
            var res = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Resources/support-qr.png", UriKind.Absolute));
            if (res is null)
            {
                return null;
            }

            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = res.Stream;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void OnDonateClick(object sender, RoutedEventArgs e) => OpenUrl(DonateUrl);

    private void OnCopyLinkClick(object sender, RoutedEventArgs e) => CopyToClipboard(DonateUrl);

    private void OnCopyCryptoClick(object sender, RoutedEventArgs e) => CopyToClipboard(UsdtTrc20Address);

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Support] не удалось открыть ссылку: {ex.Message}");
        }
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Support] буфер обмена недоступен: {ex.Message}");
        }
    }
}
