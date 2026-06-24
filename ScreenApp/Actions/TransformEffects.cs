using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ScreenApp.Actions;

/// <summary>Переворот по вертикали — «вверх ногами» (action_id = upside_down).</summary>
public sealed class UpsideDownAction : CaptureEffectAction
{
    public override string ActionId => "upside_down";
    protected override Transform BuildTransform() => new RotateTransform(180);
}

/// <summary>Отражение по вертикали (action_id = flip_vertical).</summary>
public sealed class FlipVerticalAction : CaptureEffectAction
{
    public override string ActionId => "flip_vertical";
    protected override Transform BuildTransform() => new ScaleTransform(1, -1);
}

/// <summary>Приближение экрана ~1.5x (action_id = zoom_in).</summary>
public sealed class ZoomInAction : CaptureEffectAction
{
    public override string ActionId => "zoom_in";
    protected override Transform BuildTransform() => new ScaleTransform(1.6, 1.6);
}

/// <summary>Маленький экран — картинка ужимается к центру (action_id = tiny_screen).</summary>
public sealed class TinyScreenAction : CaptureEffectAction
{
    public override string ActionId => "tiny_screen";
    protected override Transform BuildTransform() => new ScaleTransform(0.4, 0.4);
}

/// <summary>
/// Тряска экрана (action_id = shake): захват экрана + анимация дрожащего смещения.
/// </summary>
public sealed class ShakeAction : CaptureEffectAction
{
    public override string ActionId => "shake";

    protected override Transform BuildTransform()
    {
        var translate = new TranslateTransform();

        var animX = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        animX.KeyFrames.Add(new LinearDoubleKeyFrame(0,   System.Windows.Media.Animation.KeyTime.FromPercent(0.0)));
        animX.KeyFrames.Add(new LinearDoubleKeyFrame(-18, System.Windows.Media.Animation.KeyTime.FromPercent(0.25)));
        animX.KeyFrames.Add(new LinearDoubleKeyFrame(16,  System.Windows.Media.Animation.KeyTime.FromPercent(0.5)));
        animX.KeyFrames.Add(new LinearDoubleKeyFrame(-12, System.Windows.Media.Animation.KeyTime.FromPercent(0.75)));
        animX.KeyFrames.Add(new LinearDoubleKeyFrame(0,   System.Windows.Media.Animation.KeyTime.FromPercent(1.0)));
        animX.Duration = new System.Windows.Duration(TimeSpan.FromMilliseconds(120));

        var animY = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        animY.KeyFrames.Add(new LinearDoubleKeyFrame(0,   System.Windows.Media.Animation.KeyTime.FromPercent(0.0)));
        animY.KeyFrames.Add(new LinearDoubleKeyFrame(14,  System.Windows.Media.Animation.KeyTime.FromPercent(0.25)));
        animY.KeyFrames.Add(new LinearDoubleKeyFrame(-16, System.Windows.Media.Animation.KeyTime.FromPercent(0.5)));
        animY.KeyFrames.Add(new LinearDoubleKeyFrame(10,  System.Windows.Media.Animation.KeyTime.FromPercent(0.75)));
        animY.KeyFrames.Add(new LinearDoubleKeyFrame(0,   System.Windows.Media.Animation.KeyTime.FromPercent(1.0)));
        animY.Duration = new System.Windows.Duration(TimeSpan.FromMilliseconds(110));

        translate.BeginAnimation(TranslateTransform.XProperty, animX);
        translate.BeginAnimation(TranslateTransform.YProperty, animY);
        return translate;
    }
}
