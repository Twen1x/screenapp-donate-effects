namespace ScreenApp.Actions;

/// <summary>
/// Действие над экраном/вводом. Каждое действие умеет применить эффект (Execute)
/// и снять его (Revert). Длительность (duration_seconds) не хранится в самом действии —
/// её задаёт задание, а автоснятие по таймеру обеспечивает <see cref="Effects.ActiveEffectManager"/>.
///
/// Все методы (Execute/Revert) для оконных действий ДОЛЖНЫ выполняться в UI-потоке.
/// Маршалинг в Dispatcher выполняет ActiveEffectManager, поэтому реализации действий
/// могут создавать/закрывать WPF-окна напрямую.
/// </summary>
public interface IScreenAction
{
    /// <summary>snake_case идентификатор действия (совпадает с action_id на сайте).</summary>
    string ActionId { get; }

    /// <summary>Применить эффект.</summary>
    void Execute();

    /// <summary>
    /// Снять эффект. Должен быть идемпотентным — повторный вызов после снятия
    /// не должен бросать исключение или повторно выполнять работу.
    /// </summary>
    void Revert();
}
