namespace ScreenApp.Settings;

/// <summary>Категория действия (для группировки в окне настроек).</summary>
public enum ActionCategory
{
    Transform,
    Screen,
    Tint,
    Particles,
    Cards,
    Text,
    System,
    Input,
}

/// <summary>
/// Статическое описание одного действия: его идентификатор (совпадает с action_id
/// в <see cref="Actions.ActionRegistry"/>), русское название, категория, значения
/// по умолчанию (цена/длительность) и нужен ли ему текст зрителя.
/// </summary>
public sealed record ActionInfo(
    string ActionId,
    string DisplayName,
    ActionCategory Category,
    int DefaultPrice,
    int DefaultDurationSeconds,
    bool TakesText = false);

/// <summary>
/// Каталог всех поддерживаемых действий. Единый источник правды о наборе эффектов:
/// окно настроек строит из него список «действие — цена — длительность», а
/// <see cref="AppSettings"/> досоздаёт недостающие записи по этому каталогу.
/// </summary>
public static class ActionCatalog
{
    /// <summary>Все действия в порядке показа в настройках.</summary>
    public static readonly IReadOnlyList<ActionInfo> All = new[]
    {
        // — Трансформации экрана —
        new ActionInfo("rotate_180",     "Перевернуть экран",        ActionCategory.Transform, 100, 30),
        new ActionInfo("mirror",         "Зеркало",                  ActionCategory.Transform, 75,  30),
        new ActionInfo("upside_down",    "Вверх ногами",             ActionCategory.Transform, 75,  30),
        new ActionInfo("flip_vertical",  "Отражение по вертикали",   ActionCategory.Transform, 75,  30),
        new ActionInfo("zoom_in",        "Приближение",              ActionCategory.Transform, 60,  20),
        new ActionInfo("tiny_screen",    "Маленький экран",          ActionCategory.Transform, 60,  20),
        new ActionInfo("shake",          "Тряска экрана",            ActionCategory.Transform, 80,  15),
        new ActionInfo("disco",          "Диско",                    ActionCategory.Transform, 120, 20),

        // — Сплошной экран —
        new ActionInfo("white_screen",   "Белый экран",              ActionCategory.Screen, 50, 10),
        new ActionInfo("blackout",       "Чёрный экран",             ActionCategory.Screen, 50, 10),
        new ActionInfo("dim_50",         "Затемнение",               ActionCategory.Screen, 40, 20),

        // — Цветные фильтры —
        new ActionInfo("red_tint",       "Красный фильтр",           ActionCategory.Tint, 40, 20),
        new ActionInfo("night",          "Ночной режим",             ActionCategory.Tint, 40, 30),
        new ActionInfo("rainbow",        "Радуга",                   ActionCategory.Tint, 40, 30),

        // — Частицы —
        new ActionInfo("confetti",       "Конфетти",                 ActionCategory.Particles, 50, 20),
        new ActionInfo("hearts",         "Сердечки",                 ActionCategory.Particles, 50, 20),
        new ActionInfo("snowfall",       "Снегопад",                 ActionCategory.Particles, 50, 30),
        new ActionInfo("balloons",       "Шарики",                   ActionCategory.Particles, 50, 20),
        new ActionInfo("stars",          "Звёзды",                   ActionCategory.Particles, 50, 20),
        new ActionInfo("coin_rain",      "Дождь из монет",           ActionCategory.Particles, 60, 20),
        new ActionInfo("fireworks",      "Фейерверк",                ActionCategory.Particles, 70, 15),

        // — Карточки —
        new ActionInfo("thank_you_card", "Карточка «Спасибо»",       ActionCategory.Cards, 30, 8),
        new ActionInfo("level_up",       "Плашка «Level Up»",        ActionCategory.Cards, 30, 8),

        // — Текст зрителя —
        new ActionInfo("caption",        "Подпись на экране",        ActionCategory.Text, 50, 15, TakesText: true),
        new ActionInfo("subtitle",       "Субтитры снизу",           ActionCategory.Text, 50, 15, TakesText: true),
        new ActionInfo("ticker",         "Бегущая строка",           ActionCategory.Text, 60, 20, TakesText: true),
        new ActionInfo("tts",            "Озвучка текста",           ActionCategory.Text, 70, 10, TakesText: true),

        // — Системные —
        new ActionInfo("mute",           "Выключить звук",           ActionCategory.System, 80, 15),
        new ActionInfo("swap_mouse_buttons", "Поменять кнопки мыши", ActionCategory.System, 90, 20),
        new ActionInfo("giant_cursor",   "Огромный курсор",          ActionCategory.System, 70, 20),

        // — Блокировки ввода (требуют прав администратора) —
        new ActionInfo("disable_mouse",    "Блокировка мыши",        ActionCategory.Input, 150, 10),
        new ActionInfo("disable_keyboard", "Блокировка клавиатуры",  ActionCategory.Input, 150, 10),
    };

    private static readonly Dictionary<string, ActionInfo> ById =
        All.ToDictionary(a => a.ActionId, StringComparer.OrdinalIgnoreCase);

    /// <summary>Найти описание действия по идентификатору (или null).</summary>
    public static ActionInfo? Find(string actionId) =>
        actionId is not null && ById.TryGetValue(actionId, out var info) ? info : null;

    /// <summary>Русское название категории для заголовков групп в UI.</summary>
    public static string CategoryName(ActionCategory category) => category switch
    {
        ActionCategory.Transform => "Трансформации экрана",
        ActionCategory.Screen    => "Сплошной экран",
        ActionCategory.Tint      => "Цветные фильтры",
        ActionCategory.Particles => "Частицы",
        ActionCategory.Cards     => "Карточки",
        ActionCategory.Text      => "Текст зрителя",
        ActionCategory.System    => "Системные",
        ActionCategory.Input     => "Блокировки ввода",
        _ => "Прочее",
    };
}
