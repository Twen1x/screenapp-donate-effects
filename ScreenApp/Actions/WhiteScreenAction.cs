using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace ScreenApp.Actions;

/// <summary>Полноэкранный белый экран (action_id = white_screen).</summary>
public sealed class WhiteScreenAction : FullscreenColorAction
{
    public override string ActionId => "white_screen";
    protected override WpfBrush Fill => WpfBrushes.White;
}
