using System.Windows.Media;

namespace ScreenApp.Actions;

/// <summary>
/// Диско (action_id = disco): живой захват экрана, который циклически переключает
/// трансформацию каждые ~0.5 c — нормально → зеркало → поворот 180° → поворот+зеркало →
/// и обратно. Создаёт эффект «дёргающегося» экрана.
///
/// Базовый <see cref="CaptureEffectAction"/> вызывает <see cref="OnFrame"/> каждый тик
/// (~33 мс); меняем трансформацию Image по фазе. Окно исключено из захвата, поэтому
/// переключения чёткие, без петли.
/// </summary>
public sealed class DiscoAction : CaptureEffectAction
{
    public override string ActionId => "disco";

    // Сколько кадров держится одна фаза (~15 * 33мс ≈ 0.5 c).
    private const int FramesPerPhase = 15;

    private int _lastPhase = -1;

    protected override void OnFrame(long frame)
    {
        if (Image is null)
        {
            return;
        }

        int phase = (int)((frame / FramesPerPhase) % 4);
        if (phase == _lastPhase)
        {
            return; // фаза не сменилась — лишний раз не трогаем трансформацию
        }
        _lastPhase = phase;

        Image.RenderTransform = phase switch
        {
            0 => Transform.Identity,                 // обычный
            1 => new ScaleTransform(-1, 1),          // зеркало
            2 => new RotateTransform(180),           // переворот
            _ => Combo(),                            // переворот + зеркало
        };
    }

    private static Transform Combo()
    {
        var group = new TransformGroup();
        group.Children.Add(new RotateTransform(180));
        group.Children.Add(new ScaleTransform(-1, 1));
        return group;
    }
}
