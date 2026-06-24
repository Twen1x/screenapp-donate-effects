using System.Windows;

namespace ScreenApp.Actions;

/// <summary>Текст зрителя снизу экрана, как субтитры (action_id = subtitle).</summary>
public sealed class SubtitleAction : TextOverlayAction
{
    public override string ActionId => "subtitle";
    protected override double FontSizePx => 40;
    protected override VerticalAlignment VAlign => VerticalAlignment.Bottom;
}
