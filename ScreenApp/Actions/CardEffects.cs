using System.Windows;

namespace ScreenApp.Actions;

/// <summary>
/// Базовая «карточка»: крупный фиксированный текст по центру экрана (поверх всего,
/// исключён из захвата). Использует <see cref="TextOverlayAction"/>, но текст задаётся
/// самим действием, а не зрителем.
/// </summary>
public abstract class FixedCardAction : TextOverlayAction
{
    protected FixedCardAction()
    {
        SetCardText(CardText);
    }

    /// <summary>Текст карточки.</summary>
    protected abstract string CardText { get; }

    protected override double FontSizePx => 64;
    protected override System.Windows.VerticalAlignment VAlign => System.Windows.VerticalAlignment.Center;

    // Карточка фиксированная: внешний текст задания игнорируем.
    public override void SetText(string? text) { /* no-op: используем CardText */ }
}

/// <summary>Карточка благодарности (action_id = thank_you_card).</summary>
public sealed class ThankYouCardAction : FixedCardAction
{
    public override string ActionId => "thank_you_card";
    protected override string CardText => "Спасибо за поддержку! 💜";
}

/// <summary>Игровая плашка «Level Up» (action_id = level_up).</summary>
public sealed class LevelUpAction : FixedCardAction
{
    public override string ActionId => "level_up";
    protected override string CardText => "⭐ LEVEL UP! ⭐";
}
