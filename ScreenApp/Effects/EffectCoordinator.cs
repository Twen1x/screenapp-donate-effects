using System.Diagnostics;
using ScreenApp.Actions;
using ScreenApp.Donations;
using ScreenApp.Overlay;
using ScreenApp.Settings;

namespace ScreenApp.Effects;

/// <summary>
/// Связывает приём доната с запуском эффекта: подбирает действие по цене
/// (<see cref="DonationRouter"/>), запускает его через <see cref="ActiveEffectManager"/>
/// с автоснятием по длительности из настроек и показывает подпись оверлея.
///
/// Потокобезопасен в той мере, в какой потокобезопасны зависимости: Activate и оверлей
/// сами маршалят работу в UI-поток, поэтому методы можно звать из фонового слушателя DA.
/// </summary>
public sealed class EffectCoordinator
{
    private readonly ActionRegistry _registry;
    private readonly ActiveEffectManager _effects;
    private readonly DonationOverlayManager _overlay;
    private readonly DonationRouter _router;
    private readonly AppSettings _settings;
    private readonly Action<string>? _log;

    public EffectCoordinator(
        ActionRegistry registry,
        ActiveEffectManager effects,
        DonationOverlayManager overlay,
        DonationRouter router,
        AppSettings settings,
        Action<string>? log = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _effects = effects ?? throw new ArgumentNullException(nameof(effects));
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _log = log;
    }

    /// <summary>
    /// Обработать донат: подобрать действие по цене и запустить. Если подходящего
    /// действия нет — тихо игнорировать. Возвращает true, если эффект запущен.
    /// </summary>
    public bool HandleDonation(Donation donation)
    {
        ArgumentNullException.ThrowIfNull(donation);

        var route = _router.Route(donation);
        if (route is null)
        {
            _log?.Invoke($"⚠ Нет включённого действия с ценой {donation.Amount:0.##} ₽ — донат пропущен.");
            Debug.WriteLine($"[Effect] донат {donation.Amount}₽ от {donation.DisplayName} — нет действия с такой ценой.");
            return false;
        }

        bool ok = Run(route.ActionId, route.DurationSeconds, route.Text, donation, route.Info.DisplayName);
        _log?.Invoke(ok
            ? $"▶ Запущен эффект «{route.Info.DisplayName}» на {route.DurationSeconds} с."
            : $"✖ Не удалось запустить «{route.Info.DisplayName}».");
        return ok;
    }

    /// <summary>
    /// Запустить действие напрямую (кнопка «Тест» в настройках). Длительность и текст
    /// берутся из параметров; подпись оверлея показывается с тестовым донатом.
    /// </summary>
    public bool TestAction(string actionId, int durationSeconds, string? text)
    {
        var info = ActionCatalog.Find(actionId);
        string label = info?.DisplayName ?? actionId;

        var fakeDonation = new Donation
        {
            Amount = 0,
            Username = "Тест",
            Message = text,
            IsTest = true,
        };

        return Run(actionId, durationSeconds, text, fakeDonation, label, showAmount: false);
    }

    private bool Run(string actionId, int durationSeconds, string? text, Donation donation, string label, bool showAmount = true)
    {
        var action = _registry.Resolve(actionId);
        if (action is null)
        {
            Debug.WriteLine($"[Effect] действие '{actionId}' не зарегистрировано.");
            return false;
        }

        if (action is ITextAction textAction)
        {
            textAction.SetText(text);
        }

        try
        {
            _effects.Activate(action, durationSeconds);
            if (_settings.ShowOverlay)
            {
                string overlayLabel = showAmount ? label : $"{label} (тест)";
                _overlay.ShowDonation(donation, overlayLabel);
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Effect] ошибка запуска '{actionId}': {ex.Message}");
            return false;
        }
    }
}
