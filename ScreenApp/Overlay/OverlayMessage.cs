using ScreenApp.Donations;

namespace ScreenApp.Overlay;

/// <summary>
/// Одна готовая к показу подпись оверлея: собранная строка «ник — сумма — действие».
/// </summary>
public sealed class OverlayMessage
{
    public OverlayMessage(string text)
    {
        Text = text ?? "";
    }

    /// <summary>Готовая строка для показа.</summary>
    public string Text { get; }

    /// <summary>Создаёт подпись из доната и названия запущенного действия.</summary>
    public static OverlayMessage FromDonation(Donation donation, string actionLabel)
    {
        ArgumentNullException.ThrowIfNull(donation);

        string amount = OverlayFormatter.FormatAmount(donation.Amount);
        string line = OverlayFormatter.FormatLine(donation.DisplayName, amount, actionLabel);
        return new OverlayMessage(line);
    }
}
