using ScreenApp.Settings;

namespace ScreenApp.Donations;

/// <summary>Результат подбора действия под донат.</summary>
public sealed record RouteResult(
    ActionSetting Setting,
    ActionInfo Info,
    string? Text)
{
    /// <summary>Идентификатор действия.</summary>
    public string ActionId => Info.ActionId;

    /// <summary>Длительность эффекта (сек) из настроек.</summary>
    public int DurationSeconds => Setting.DurationSeconds;
}

/// <summary>
/// Подбор действия под донат по цене. Чистая логика (без WPF/сети) — покрыта тестами.
///
/// Правило: донат запускает ВКЛЮЧЁННОЕ действие, цена которого точно равна сумме доната.
/// Если несколько действий имеют одинаковую цену — берётся первое в порядке каталога.
/// Для текстовых действий сообщение зрителя передаётся как текст эффекта.
/// </summary>
public sealed class DonationRouter
{
    private readonly AppSettings _settings;

    public DonationRouter(AppSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Подобрать действие под донат или вернуть null, если подходящего нет
    /// (нет включённого действия с такой ценой).
    /// </summary>
    public RouteResult? Route(Donation donation)
    {
        ArgumentNullException.ThrowIfNull(donation);

        // Цены целочисленные: сопоставляем только целые положительные суммы.
        if (donation.Amount <= 0 || donation.Amount % 1 != 0)
        {
            return null;
        }

        int amount = (int)donation.Amount;

        // Идём в порядке каталога, чтобы при равных ценах выбор был детерминирован.
        foreach (var info in ActionCatalog.All)
        {
            var setting = _settings.Actions.FirstOrDefault(a =>
                string.Equals(a.ActionId, info.ActionId, StringComparison.OrdinalIgnoreCase));

            if (setting is { Enabled: true } && setting.Price == amount)
            {
                string? text = info.TakesText ? donation.Message : null;
                return new RouteResult(setting, info, text);
            }
        }

        return null;
    }
}
