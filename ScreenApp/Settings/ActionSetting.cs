using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ScreenApp.Settings;

/// <summary>
/// Настройка одного действия: включено ли оно, цена (₽) и длительность (сек).
/// Реализует <see cref="INotifyPropertyChanged"/>, чтобы окно настроек правило
/// значения, а маршрутизатор донатов читал их «вживую» (изменения применяются сразу).
/// </summary>
public sealed class ActionSetting : INotifyPropertyChanged
{
    private bool _enabled = true;
    private int _price;
    private int _durationSeconds;

    /// <summary>Идентификатор действия (см. <see cref="ActionCatalog"/>).</summary>
    [JsonPropertyName("action_id")]
    public string ActionId { get; set; } = "";

    /// <summary>Включено ли действие (доступно зрителям).</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    /// <summary>Цена в рублях. Донат на эту сумму запускает действие.</summary>
    [JsonPropertyName("price")]
    public int Price
    {
        get => _price;
        set => Set(ref _price, value < 0 ? 0 : value);
    }

    /// <summary>Длительность эффекта в секундах (0 — без автоснятия по таймеру).</summary>
    [JsonPropertyName("duration_seconds")]
    public int DurationSeconds
    {
        get => _durationSeconds;
        set => Set(ref _durationSeconds, value < 0 ? 0 : value);
    }

    /// <summary>Русское название действия (для UI). Не сериализуется.</summary>
    [JsonIgnore]
    public string DisplayName => ActionCatalog.Find(ActionId)?.DisplayName ?? ActionId;

    /// <summary>Категория действия (для UI). Не сериализуется.</summary>
    [JsonIgnore]
    public string CategoryName =>
        ActionCatalog.Find(ActionId) is { } info ? ActionCatalog.CategoryName(info.Category) : "Прочее";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Создать настройку с дефолтами из каталога.</summary>
    public static ActionSetting FromCatalog(ActionInfo info) => new()
    {
        ActionId = info.ActionId,
        Enabled = true,
        Price = info.DefaultPrice,
        DurationSeconds = info.DefaultDurationSeconds,
    };
}
