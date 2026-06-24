using System.Globalization;

namespace ScreenApp.Overlay;

/// <summary>
/// Чистое (без WPF) форматирование строки оверлея «ник — сумма — действие».
/// Вынесено отдельно, чтобы покрыть логику unit-тестами без живого UI-потока.
/// </summary>
public static class OverlayFormatter
{
    /// <summary>Разделитель частей строки оверлея (длинное тире с пробелами).</summary>
    public const string Separator = " — ";

    /// <summary>Форматирует сумму доната в рублях, например «100 ₽» или «99.5 ₽».</summary>
    public static string FormatAmount(decimal amount)
    {
        // Целые суммы без дробной части, дробные — с минимально нужными знаками.
        string num = amount == decimal.Truncate(amount)
            ? ((long)amount).ToString(CultureInfo.InvariantCulture)
            : amount.ToString("0.##", CultureInfo.InvariantCulture);
        return $"{num} ₽";
    }

    /// <summary>
    /// Собирает строку «ник — сумма — действие». Пустые части опускаются вместе со своим
    /// разделителем, чтобы не оставлять «висящих» тире.
    /// </summary>
    public static string FormatLine(string? viewer, string? amountText, string? shortLabel)
    {
        var parts = new List<string>(3);

        if (!string.IsNullOrWhiteSpace(viewer))
        {
            parts.Add(viewer.Trim());
        }

        if (!string.IsNullOrWhiteSpace(amountText))
        {
            parts.Add(amountText.Trim());
        }

        if (!string.IsNullOrWhiteSpace(shortLabel))
        {
            parts.Add(shortLabel.Trim());
        }

        return string.Join(Separator, parts);
    }
}
