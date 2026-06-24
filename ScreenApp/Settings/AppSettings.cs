using System.Text.Json.Serialization;

namespace ScreenApp.Settings;

/// <summary>
/// Настройки приложения, хранящиеся локально (см. <see cref="SettingsStore"/>).
///
/// Здесь нет связи с сайтом по токену — приложение слушает DonationAlerts напрямую.
/// Содержит реквизиты доступа к DA OAuth, аварийную клавишу и список действий с ценами.
/// </summary>
public sealed class AppSettings
{
    // ─── DonationAlerts OAuth ────────────────────────────────────────────────

    /// <summary>OAuth access token DA (обязателен для подключения).</summary>
    [JsonPropertyName("da_access_token")]
    public string DaAccessToken { get; set; } = "";

    /// <summary>Refresh token DA (необязателен; нужен для авто-рефреша).</summary>
    [JsonPropertyName("da_refresh_token")]
    public string DaRefreshToken { get; set; } = "";

    /// <summary>OAuth client_id приложения DA (для авто-рефреша и авторизации).</summary>
    [JsonPropertyName("da_client_id")]
    public string DaClientId { get; set; } = "";

    /// <summary>OAuth client_secret приложения DA.</summary>
    [JsonPropertyName("da_client_secret")]
    public string DaClientSecret { get; set; } = "";

    /// <summary>Unix-время истечения access token (0 — неизвестно).</summary>
    [JsonPropertyName("da_token_expires_at")]
    public long DaTokenExpiresAt { get; set; }

    // ─── DonateX ─────────────────────────────────────────────────────────────

    /// <summary>External token DonateX (бессрочный, из кабинета DonateX → Настройки → Api).</summary>
    [JsonPropertyName("donatex_token")]
    public string DonateXToken { get; set; } = "";

    // ─── Какие сервисы слушать ───────────────────────────────────────────────

    /// <summary>Слушать ли DonationAlerts.</summary>
    [JsonPropertyName("da_enabled")]
    public bool DaEnabled { get; set; } = true;

    /// <summary>Слушать ли DonateX.</summary>
    [JsonPropertyName("donatex_enabled")]
    public bool DonateXEnabled { get; set; } = true;

    /// <summary>Показывать ли на экране подпись доната «ник — сумма — действие».</summary>
    [JsonPropertyName("show_overlay")]
    public bool ShowOverlay { get; set; } = true;

    // ─── Поведение приложения ────────────────────────────────────────────────

    /// <summary>Глобальная аварийная клавиша снятия всех эффектов.</summary>
    [JsonPropertyName("panic_hotkey")]
    public string PanicHotkey { get; set; } = "Ctrl+Alt+End";

    /// <summary>Автозапуск слушателя донатов при старте приложения.</summary>
    [JsonPropertyName("auto_start")]
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// DeviceName монитора, на котором показывать эффекты (пусто — основной монитор).
    /// Совпадает с System.Windows.Forms.Screen.DeviceName, например "\\.\DISPLAY1".
    /// </summary>
    [JsonPropertyName("target_monitor")]
    public string TargetMonitorDevice { get; set; } = "";

    /// <summary>Настройки действий (цена/длительность/включено).</summary>
    [JsonPropertyName("actions")]
    public List<ActionSetting> Actions { get; set; } = new();

    /// <summary>
    /// Привести список действий в соответствие с каталогом: добавить недостающие
    /// (с дефолтами), убрать неизвестные, отсортировать в порядке каталога.
    /// Вызывается после загрузки и при первом запуске.
    /// </summary>
    public void SyncWithCatalog()
    {
        var byId = Actions
            .Where(a => ActionCatalog.Find(a.ActionId) is not null)
            .GroupBy(a => a.ActionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var synced = new List<ActionSetting>(ActionCatalog.All.Count);
        foreach (var info in ActionCatalog.All)
        {
            synced.Add(byId.TryGetValue(info.ActionId, out var existing)
                ? existing
                : ActionSetting.FromCatalog(info));
        }

        Actions = synced;
    }

    /// <summary>Настройки по умолчанию (все действия из каталога с дефолтными ценами).</summary>
    public static AppSettings CreateDefault()
    {
        var s = new AppSettings();
        s.SyncWithCatalog();
        return s;
    }
}
