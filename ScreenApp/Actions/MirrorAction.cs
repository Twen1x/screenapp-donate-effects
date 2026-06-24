using System.Windows.Media;

namespace ScreenApp.Actions;

/// <summary>
/// Зеркальное отображение экрана (action_id = mirror): живой захват экрана с
/// горизонтальным отражением (ScaleX = -1). Окно исключено из захвата
/// (см. <see cref="CaptureEffectAction"/>), поэтому отражение стабильное, без мерцания.
/// </summary>
public sealed class MirrorAction : CaptureEffectAction
{
    public override string ActionId => "mirror";

    // Горизонтальный flip относительно центра.
    protected override Transform BuildTransform() => new ScaleTransform(-1, 1);
}
