using System.Diagnostics;
using System.Runtime.InteropServices;
using ScreenApp.Interop;

namespace ScreenApp.Actions;

/// <summary>
/// Блокировка клавиатуры: ставит низкоуровневый хук WH_KEYBOARD_LL и «проглатывает»
/// все события клавиш (возврат (IntPtr)1 из процедуры хука прерывает их доставку
/// другим приложениям), пока действие активно. Revert снимает хук.
///
/// Важно (R4.3/R4.4): блокировка ВСЕГДА снимаема —
///  - автоснятие по таймеру обеспечивает <see cref="Effects.ActiveEffectManager"/>;
///  - аварийный стоп-кран (<see cref="Safety.PanicHotkey"/>) вызывает RevertAll;
///  - при выгрузке процесса хук уничтожается ОС автоматически.
///
/// Хук-процедуру держим в поле, чтобы делегат не был собран GC, пока хук установлен —
/// иначе нативный вызов придёт на уже освобождённый делегат и приложение упадёт.
///
/// Низкоуровневый хук требует насос сообщений в потоке, где он установлен. Execute/Revert
/// маршалятся в UI-поток WPF (через ActiveEffectManager), где насос сообщений есть.
/// </summary>
public sealed class DisableKeyboardAction : IScreenAction
{
    // Удерживаем делегат в поле, чтобы предотвратить сборку мусора (см. сводку класса).
    private readonly NativeMethods.LowLevelHookProc _proc;
    private IntPtr _hookHandle = IntPtr.Zero;

    public DisableKeyboardAction()
    {
        _proc = HookCallback;
    }

    public string ActionId => "disable_keyboard";

    public void Execute()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return; // уже установлен
        }

        // Для WH_KEYBOARD_LL hMod может быть NULL (или модуль текущего процесса) и
        // dwThreadId = 0 (глобальный хук). Передаём дескриптор модуля для надёжности.
        IntPtr hModule = NativeMethods.GetModuleHandle(null);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, hModule, 0);

        if (_hookHandle == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"Не удалось установить хук клавиатуры (WH_KEYBOARD_LL). Win32 error {err}. " +
                "Возможно, приложение запущено без прав администратора.");
        }
    }

    public void Revert()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return; // уже снят — идемпотентно
        }

        if (!NativeMethods.UnhookWindowsHookEx(_hookHandle))
        {
            int err = Marshal.GetLastWin32Error();
            Debug.WriteLine($"[DisableKeyboardAction] UnhookWindowsHookEx failed. Win32 error {err}.");
        }

        _hookHandle = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // nCode < 0 — обязаны передать дальше без обработки.
        if (nCode >= 0)
        {
            // Глотаем событие: ненулевой возврат прерывает цепочку и доставку события.
            return (IntPtr)1;
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}
