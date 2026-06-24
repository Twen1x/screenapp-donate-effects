namespace ScreenApp.Actions;

/// <summary>
/// Реестр действий: маппинг action_id → фабрика конкретной реализации <see cref="IScreenAction"/>.
///
/// Фабрика (а не singleton-экземпляр) используется специально: каждый запуск создаёт
/// СВЕЖИЙ экземпляр действия, чтобы независимые одновременные эффекты (R4.5) не делили
/// состояние (например, два затемнения/окна живут и снимаются независимо).
///
/// Зарегистрированы базовые визуальные действия (задача 12):
/// rotate_180, white_screen, blackout, dim_50 — и действия блокировки ввода (задача 13):
/// disable_mouse, disable_keyboard. Зеркало (задача 15, mirror) зарегистрировано здесь же.
/// Неизвестный action_id → <see cref="Resolve"/> возвращает null (вызывающий помечает задание ошибкой).
/// </summary>
public sealed class ActionRegistry
{
    private readonly Dictionary<string, Func<IScreenAction>> _factories;

    public ActionRegistry()
    {
        _factories = new Dictionary<string, Func<IScreenAction>>(StringComparer.OrdinalIgnoreCase)
        {
            ["rotate_180"] = () => new RotateAction(),
            ["white_screen"] = () => new WhiteScreenAction(),
            ["blackout"] = () => new BlackoutAction(),
            ["dim_50"] = () => new DimAction(),
            ["disco"] = () => new DiscoAction(),
            ["caption"] = () => new CaptionAction(),
            ["subtitle"] = () => new SubtitleAction(),
            ["ticker"] = () => new TickerAction(),
            ["tts"] = () => new TtsAction(),
            ["confetti"] = () => new ConfettiAction(),
            ["hearts"] = () => new HeartsAction(),
            ["snowfall"] = () => new SnowfallAction(),
            ["balloons"] = () => new BalloonsAction(),
            ["stars"] = () => new StarsAction(),
            ["coin_rain"] = () => new CoinRainAction(),
            ["fireworks"] = () => new FireworksAction(),
            ["thank_you_card"] = () => new ThankYouCardAction(),
            ["level_up"] = () => new LevelUpAction(),
            ["upside_down"] = () => new UpsideDownAction(),
            ["flip_vertical"] = () => new FlipVerticalAction(),
            ["zoom_in"] = () => new ZoomInAction(),
            ["tiny_screen"] = () => new TinyScreenAction(),
            ["shake"] = () => new ShakeAction(),
            ["red_tint"] = () => new RedTintAction(),
            ["night"] = () => new NightAction(),
            ["rainbow"] = () => new RainbowAction(),
            ["mute"] = () => new MuteAction(),
            ["swap_mouse_buttons"] = () => new SwapMouseButtonsAction(),
            ["giant_cursor"] = () => new GiantCursorAction(),
            ["disable_mouse"] = () => new DisableMouseAction(),
            ["disable_keyboard"] = () => new DisableKeyboardAction(),
            ["mirror"] = () => new MirrorAction(),
        };
    }

    /// <summary>
    /// Зарегистрировать (или переопределить) фабрику действия. Позволяет задачам 13/15
    /// добавлять свои действия без изменения этого класса.
    /// </summary>
    public void Register(string actionId, Func<IScreenAction> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(factory);
        _factories[actionId] = factory;
    }

    /// <summary>True, если для action_id есть зарегистрированная реализация.</summary>
    public bool IsRegistered(string actionId) =>
        !string.IsNullOrWhiteSpace(actionId) && _factories.ContainsKey(actionId);

    /// <summary>
    /// Создать новый экземпляр действия по action_id или null, если действие не зарегистрировано.
    /// </summary>
    public IScreenAction? Resolve(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return null;
        }

        return _factories.TryGetValue(actionId, out var factory) ? factory() : null;
    }
}
