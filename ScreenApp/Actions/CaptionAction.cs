using System.Windows;

namespace ScreenApp.Actions;

/// <summary>Крупный текст зрителя по центру экрана (action_id = caption).</summary>
public sealed class CaptionAction : TextOverlayAction
{
    public override string ActionId => "caption";
    protected override double FontSizePx => 72;
    protected override VerticalAlignment VAlign => VerticalAlignment.Center;
}
