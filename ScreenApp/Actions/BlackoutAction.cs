using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace ScreenApp.Actions;

/// <summary>Полноэкранный чёрный экран (action_id = blackout).</summary>
public sealed class BlackoutAction : FullscreenColorAction
{
    public override string ActionId => "blackout";
    protected override WpfBrush Fill => WpfBrushes.Black;
}
