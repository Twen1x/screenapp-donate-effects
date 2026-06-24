using ScreenApp.Interop;

namespace ScreenApp.Actions;

/// <summary>
/// Выключить звук на время эффекта (action_id = mute): шлёт VK_VOLUME_MUTE при
/// Execute и ещё раз при Revert (toggle), возвращая звук.
/// </summary>
public sealed class MuteAction : IScreenAction
{
    public string ActionId => "mute";

    public void Execute() => Toggle();
    public void Revert() => Toggle();

    private static void Toggle()
    {
        NativeMethods.keybd_event(NativeMethods.VK_VOLUME_MUTE, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_VOLUME_MUTE, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}

/// <summary>
/// Поменять местами кнопки мыши на время эффекта (action_id = swap_mouse_buttons).
/// Revert возвращает нормальную раскладку.
/// </summary>
public sealed class SwapMouseButtonsAction : IScreenAction
{
    public string ActionId => "swap_mouse_buttons";

    public void Execute() => NativeMethods.SwapMouseButton(true);
    public void Revert() => NativeMethods.SwapMouseButton(false);
}
