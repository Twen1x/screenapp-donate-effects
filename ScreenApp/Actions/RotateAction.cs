using System.Windows.Media;

namespace ScreenApp.Actions;

/// <summary>
/// Переворот экрана на 180° (action_id = rotate_180).
///
/// Реализован как ПРОГРАММНЫЙ поворот через живой захват экрана (а не аппаратный
/// ChangeDisplaySettingsEx): так оверлей доната, который рисуется поверх и исключён
/// из захвата, остаётся ровным, а не переворачивается вместе с картинкой.
///
/// По просьбе: переворот совмещён с зеркалом — кадр поворачивается на 180°
/// и дополнительно отражается по горизонтали (ScaleX = -1).
/// </summary>
public sealed class RotateAction : CaptureEffectAction
{
    public override string ActionId => "rotate_180";

    protected override Transform BuildTransform()
    {
        var group = new TransformGroup();
        group.Children.Add(new RotateTransform(180));   // переворот на 180°
        group.Children.Add(new ScaleTransform(-1, 1));  // + зеркало по горизонтали
        return group;
    }
}
